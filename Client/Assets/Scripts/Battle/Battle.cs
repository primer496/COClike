using AStarPathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace DevelopersHub.ClashOfWhatecer
{
    public partial class Battle
    {
        // ── Profiler 采样节点 ────────────────────────────────────────────
        // 用于在真机 Profiler 中定位战斗模拟的 CPU 热路径。
        // 不影响任何游戏逻辑，Release 构建中 ProfilerMarker 会被自动剔除。
        //   ├── HandleBuildings / HandleUnits / HandleProjectiles 为顶层阶段
        //   └── Unit.FindTargets / Unit.PathFollow 为 HandleUnits 内部子阶段
        private static readonly ProfilerMarker s_ExecuteFrame      = new ProfilerMarker(ProfilerCategory.Scripts, "Battle.ExecuteFrame");
        private static readonly ProfilerMarker s_HandleBuildings   = new ProfilerMarker(ProfilerCategory.Scripts, "Battle.HandleBuildings");
        private static readonly ProfilerMarker s_HandleUnits       = new ProfilerMarker(ProfilerCategory.Scripts, "Battle.HandleUnits");
        private static readonly ProfilerMarker s_HandleProjectiles = new ProfilerMarker(ProfilerCategory.Scripts, "Battle.HandleProjectiles");
        private static readonly ProfilerMarker s_UnitFindTargets    = new ProfilerMarker(ProfilerCategory.Scripts, "Battle.Unit.FindTargets");
        private static readonly ProfilerMarker s_UnitPathFollow      = new ProfilerMarker(ProfilerCategory.Scripts, "Battle.Unit.PathFollow");
        // ────────────────────────────────────────────────────────────────

        // Battle 管理一场战斗的完整运行态：包含静态建筑布局、动态单位与法术，以及逐帧结算逻辑。

        public long id = 0;
        public DateTime baseTime = DateTime.Now;
        public int frameCount = 0;
        public long defender = 0;
        public long attacker = 0;
        public List<Building> _buildings = new List<Building>();
        public List<Unit> _units = new List<Unit>();
        public List<Spell> _spells = new List<Spell>();
        public List<UnitToAdd> _unitsToAdd = new List<UnitToAdd>();
        public List<SpellToAdd> _spellsToAdd = new List<SpellToAdd>();
        // grid 仅维护 blocked 状态，不再参与搜索；实际寻路由 Burst A* 的 burstSearchBlocked 承担。
        private Grid grid = null;
        // blockedTiles 与 grid.Blocked 保持同步，主要供 CountBlockingWalls / PopulateBlockingWalls 遍历。
        private List<Tile> blockedTiles = new List<Tile>();
        public List<Projectile> projectiles = new List<Projectile>();
        public double percentage = 0;
        public bool end = false;
        public bool surrender = false;
        public int surrenderFrame = 0;
        public float duration = 0;

        public int unitsDeployed = 0;
        public bool townhallDestroyed = false;
        public bool fiftyPercentDestroyed = false;
        public bool completelyDestroyed = false;

        public int winTrophies = 0;
        public int loseTrophies = 0;
        private int projectileCount = 0;
        private readonly BattleEcsRuntime battleEcsRuntime = new BattleEcsRuntime();

        // ── 预算型微批处理（跨帧持久队列） ──
        private const int MAX_PATH_UNITS_PER_FRAME = 8;
        private readonly List<int> _batchPathUnitQueue = new List<int>(128);        // 跨帧持久：等待重寻路的 unit index
        private readonly HashSet<int> _pendingRepathSet = new HashSet<int>();       // O(1) 去重，与 _batchPathUnitQueue 同步
        private bool _batchModeActive = false;                                        // 当前是否处于两趟制模式
        // ────────────────────────────────────────────────────────────

        public void DisposeRuntimeData()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                _units[i].ClearPath();
            }
            battleEcsRuntime.Dispose();
            DisposeBurstAStarBuffers();
        }

        public int stars { get { int s = 0; if (townhallDestroyed) { s++; } if (fiftyPercentDestroyed) { s++; } if (completelyDestroyed) { s++; } return s; } }

        public ProjectileCallback projectileCallback = null;

        /// <summary>
        /// 按当前星级计算这场战斗的奖惩杯数。
        /// </summary>
        /// <returns>胜利返回正数奖杯，失败返回负数奖杯。</returns>
        public int GetTrophies()
        {
            int s = stars;
            if (s > 0)
            {
                if (s >= 3)
                {
                    // 满星直接奖励。
                    return winTrophies;
                }
                else
                {
                    // 胜利但未满星按比例奖励，向下取整。Floor
                    int t = (int)Math.Floor((double)winTrophies / (double)s);
                    return t * s;
                }
            }
            else
            {
                return loseTrophies * -1;
            }
        }

        /// <summary>
        /// 初始化战斗状态、建筑占用网格以及外部回调。
        /// </summary>
        /// <param name="buildings">初始建筑列表。</param>
        /// <param name="time">战斗开始时间。</param>
        /// <param name="attackCallback">攻击触发时的回调。</param>
        /// <param name="destroyCallback">建筑被摧毁时的回调。</param>
        /// <param name="damageCallback">受到伤害时的回调。</param>
        /// <param name="starGained">获得星级时的回调。</param>
        /// <param name="projectileCallback">投射物创建时的回调。</param>
        public void Initialize(List<Building> buildings, DateTime time, AttackCallback attackCallback = null, DoubleCallback destroyCallback = null, FloatCallback damageCallback = null, BlankCallback starGained = null, ProjectileCallback projectileCallback = null)
        {
            baseTime = time;
            duration = Data.battleDuration;
            frameCount = 0;
            percentage = 0;
            unitsDeployed = 0;
            fiftyPercentDestroyed = false;
            townhallDestroyed = false;
            completelyDestroyed = false;
            end = false;
            projectileCount = 0;
            surrender = false;
            this.projectileCallback = projectileCallback;
            _buildings = buildings;
            grid = new Grid(Data.gridSize + (Data.battleGridOffset * 2), Data.gridSize + (Data.battleGridOffset * 2));
            battleEcsRuntime.Initialize();
            EnsureBurstAStarBuffers();
            for (int i = 0; i < _buildings.Count; i++)
            {
                _buildings[i].attackCallback = attackCallback;
                _buildings[i].destroyCallback = destroyCallback;
                _buildings[i].damageCallback = damageCallback;
                _buildings[i].starCallback = starGained;

                _buildings[i].Initialize();
                _buildings[i].worldCenterPosition = new BattleVector2((_buildings[i].building.x + (_buildings[i].building.columns / 2f)) * Data.gridCellSize, (_buildings[i].building.y + (_buildings[i].building.rows / 2f)) * Data.gridCellSize);

                int startX = _buildings[i].building.x;
                int endX = _buildings[i].building.x + _buildings[i].building.columns;

                int startY = _buildings[i].building.y;
                int endY = _buildings[i].building.y + _buildings[i].building.rows;

                // 大型建筑只阻挡内部格子，保留外围可行走区域，这样近战单位可以贴边后再攻击。
                if (_buildings[i].building.id != Data.BuildingID.wall && _buildings[i].building.columns > 1 && _buildings[i].building.rows > 1)
                {
                    startX++;
                    startY++;
                    endX--;
                    endY--;
                    if (endX <= startX || endY <= startY)
                    {
                        continue;
                    }
                }

                for (int x = startX; x < endX; x++)
                {
                    for (int y = startY; y < endY; y++)
                    {
                        grid[x, y].Blocked = true;
                        burstSearchBlocked[(y * grid.Width) + x] = 1;
                        blockedTiles.Add(new Tile(_buildings[i].building.id, new BattleVector2Int(x, y), i));
                    }
                }
            }
        }

        /// <summary>
        /// 判断战场上是否仍存在存活单位。
        /// </summary>
        /// <returns>只要有任意单位血量大于 0，就返回 true。</returns>
        public bool IsAliveUnitsOnGrid()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].health > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 根据战斗进度、剩余单位和剩余时长判断战斗是否还能继续。
        /// </summary>
        /// <returns>尚未满摧毁、仍有单位存活且未超时则返回 true。</returns>
        public bool CanBattleGoOn()
        {
            if (Math.Abs(percentage - 1d) > 0.0001d && IsAliveUnitsOnGrid())
            {
                double time = (float)frameCount * Data.battleFrameRate;
                if (time < duration)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断指定位置是否允许投放单位。
        /// </summary>
        /// <param name="x">未偏移前的网格 X 坐标。</param>
        /// <param name="y">未偏移前的网格 Y 坐标。</param>
        /// <returns>若未与任意存活建筑占用区域重叠，则返回 true。</returns>
        public bool CanAddUnit(int x, int y)
        {
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;

            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].health <= 0)
                {
                    continue;
                }

                int startX = _buildings[i].building.x;
                int endX = _buildings[i].building.x + _buildings[i].building.columns;

                int startY = _buildings[i].building.y;
                int endY = _buildings[i].building.y + _buildings[i].building.rows;

                for (int x2 = startX; x2 < endX; x2++)
                {
                    for (int y2 = startY; y2 < endY; y2++)
                    {
                        if (x == x2 && y == y2)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 判断指定位置是否允许释放法术。
        /// </summary>
        /// <param name="x">未偏移前的网格 X 坐标。</param>
        /// <param name="y">未偏移前的网格 Y 坐标。</param>
        /// <returns>当前实现始终允许释放法术。</returns>
        public bool CanAddSpell(int x, int y)
        {
            return true;
        }

        /// <summary>
        /// 将一个单位加入待投放队列，真正进入战场会在下一帧开始时完成。
        /// </summary>
        /// <param name="unit">单位配置。</param>
        /// <param name="x">未偏移前的网格 X 坐标。</param>
        /// <param name="y">未偏移前的网格 Y 坐标。</param>
        /// <param name="callback">单位生成时的回调。</param>
        /// <param name="attackCallback">单位攻击时的回调。</param>
        /// <param name="dieCallback">单位死亡时的回调。</param>
        /// <param name="damageCallback">单位受伤时的回调。</param>
        /// <param name="healCallback">单位被治疗时的回调。</param>
        /// <param name="targetCallback">单位切换目标时的回调。</param>
        public void AddUnit(Data.Unit unit, int x, int y, Spawned callback = null, AttackCallback attackCallback = null, IndexCallback dieCallback = null, FloatCallback damageCallback = null, FloatCallback healCallback = null, IndexCallback targetCallback = null)
        {
            if (end)
            {
                return;
            }
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;
            UnitToAdd unitToAdd = new UnitToAdd();
            unitToAdd.callback = callback;
            Unit battleUnit = new Unit();
            battleUnit.attackCallback = attackCallback;
            battleUnit.dieCallback = dieCallback;
            battleUnit.damageCallback = damageCallback;
            battleUnit.healCallback = healCallback;
            battleUnit.targetCallback = targetCallback;
            battleUnit.unit = unit;
            battleUnit.Initialize(x, y);
            battleUnit.health = unit.health;
            unitToAdd.unit = battleUnit;
            unitToAdd.x = x;
            unitToAdd.y = y;
            _unitsToAdd.Add(unitToAdd);
        }

        /// <summary>
        /// 将一个法术加入待释放队列，真正生效会在下一帧开始时完成。
        /// </summary>
        /// <param name="spell">法术配置。</param>
        /// <param name="x">未偏移前的网格 X 坐标。</param>
        /// <param name="y">未偏移前的网格 Y 坐标。</param>
        /// <param name="callback">法术创建时的回调。</param>
        /// <param name="pulseCallback">法术每次脉冲结算时的回调。</param>
        /// <param name="doneCallback">法术结束时的回调。</param>
        public void AddSpell(Data.Spell spell, int x, int y, SpellSpawned callback = null, IndexCallback pulseCallback = null, IndexCallback doneCallback = null)
        {
            if (end)
            {
                return;
            }
            x += Data.battleGridOffset;
            y += Data.battleGridOffset;
            SpellToAdd spellToAdd = new SpellToAdd();
            spellToAdd.callback = callback;
            Spell battleSpell = new Spell();
            battleSpell.doneCallback = doneCallback;
            battleSpell.pulseCallback = pulseCallback;
            battleSpell.spell = spell;
            battleSpell.Initialize(x, y);
            spellToAdd.spell = battleSpell;
            spellToAdd.x = x;
            spellToAdd.y = y;
            _spellsToAdd.Add(spellToAdd);
        }

        /// <summary>
        /// 推进一帧战斗模拟，按固定顺序处理生成、攻击、移动、法术和投射物命中。
        /// </summary>
        public void ExecuteFrame()
        {
            using var _framescope = s_ExecuteFrame.Auto();
            // Phase 1：将本帧之前已创建的投射物同步到 ECS World，
            // 使 ProjectileTimerSystem 能在本帧递减其计时器。
            battleEcsRuntime.SyncNewProjectiles(projectiles);
            battleEcsRuntime.Update(frameCount, (float)Data.battleFrameRate, projectiles.Count);
            // 每一帧都会先结算待生成对象，再处理防御建筑、单位、法术，最后处理投射物命中。
            // 这样可以保证新生成的单位和法术在同一帧内就能参与战斗，而不是至少要等一帧。
            // addIndex 记录“本次插入到现有列表的哪个位置后面”，避免一边遍历待加入列表一边改变原列表时顺序混乱。
            int addIndex = _units.Count;
            for (int i = _unitsToAdd.Count - 1; i >= 0; i--)
            {
                /*
                if (CanAddUnit(_unitsToAdd[i].x, _unitsToAdd[i].y))
                {
                    
                }*/
                // hosing 看起来表示该单位占用的人口/住房数，用来累计本场已投放兵力。
                unitsDeployed += _unitsToAdd[i].unit.unit.hosing;
                // 这里再次叠加 battleGridOffset，说明待加入队列中的坐标与实际战斗网格坐标不是同一套坐标系。
                _unitsToAdd[i].x += Data.battleGridOffset;
                _unitsToAdd[i].y += Data.battleGridOffset;
                _units.Insert(addIndex, _unitsToAdd[i].unit);

                addIndex++;
                if (_unitsToAdd[i].callback != null)
                {
                    _unitsToAdd[i].callback.Invoke(_unitsToAdd[i].unit.unit.databaseID);
                }
                _unitsToAdd.RemoveAt(i);
            }

            addIndex = _spells.Count;
            for (int i = _spellsToAdd.Count - 1; i >= 0; i--)
            {
                // 法术与单位一样，先进入正式列表，再通过回调把生成信息抛给外部表现层。
                _spellsToAdd[i].x += Data.battleGridOffset;
                _spellsToAdd[i].y += Data.battleGridOffset;
                _spells.Insert(addIndex, _spellsToAdd[i].spell);
                if (_spellsToAdd[i].callback != null)
                {
                    // 回调参数依次是：数据库 ID、法术类型、网格坐标中的落点、服务器配置的作用半径。
                    _spellsToAdd[i].callback.Invoke(_spellsToAdd[i].spell.spell.databaseID, _spellsToAdd[i].spell.spell.id, _spells[addIndex].positionOnGrid, _spellsToAdd[i].spell.spell.server.radius);
                }
                addIndex++;
                _spellsToAdd.RemoveAt(i);
            }

            // Data.battleFrameRate 在这里不是“每秒帧数”，而是“单帧经过的时间长度”。
            // 因此后面的 HandleXxx 都是在用同一个 deltaTime 推进内部计时器。
            using (s_HandleBuildings.Auto())
            {
                for (int i = 0; i < _buildings.Count; i++)
                {
                    if (_buildings[i].building.targetType != Data.BuildingTargetType.none && _buildings[i].health > 0)
                    {
                        HandleBuilding(i, Data.battleFrameRate);
                    }
                }
            }

            using (s_HandleUnits.Auto())
            {
                double dt = Data.battleFrameRate;

                // ── 第一趟：移动 + 攻击（不触发寻路），维护跨帧待处理队列 ──
                _batchModeActive = true;
                for (int i = 0; i < _units.Count; i++)
                {
                    if (_units[i].health <= 0) continue;

                    HandleUnit_MoveAttack(i, dt);

                    // 目标已恢复有效的单位，从待处理集合中移除
                    if (_units[i].target >= 0 && _pendingRepathSet.Contains(i))
                    {
                        _pendingRepathSet.Remove(i);
                    }
                    // 目标丢失的单位，加入跨帧待处理队列（去重）
                    else if (_units[i].target < 0 && !_pendingRepathSet.Contains(i))
                    {
                        _pendingRepathSet.Add(i);
                        _batchPathUnitQueue.Add(i);
                    }
                }

                // ── 清理队列中的死亡/已恢复单位 ──
                for (int qi = _batchPathUnitQueue.Count - 1; qi >= 0; qi--)
                {
                    int i = _batchPathUnitQueue[qi];
                    if (_units[i].health <= 0 || !_pendingRepathSet.Contains(i))
                    {
                        _batchPathUnitQueue.RemoveAt(qi);
                    }
                }

                // ── 预算型跨单位批量寻路（每帧最多 N 个单位、M 个槽位） ──
                if (_batchPathUnitQueue.Count > 0)
                {
                    ProcessRepathQueueWithBudget(dt);
                }
                _batchModeActive = false;

                // ── 第二趟：本帧刚拿到路径的单位执行移动 ──
                for (int i = 0; i < _units.Count; i++)
                {
                    if (_units[i].health > 0 && _units[i].target >= 0 && _units[i].path != null)
                    {
                        HandleUnit_MoveAttack(i, dt);
                    }
                }

                // ── 法术处理（不变） ──
                for (int i = 0; i < _spells.Count; i++)
                {
                    if (_spells[i].done == false)
                    {
                        HandleSpell(i, Data.battleFrameRate);
                    }
                }
            }


            // --- 投射物命中处理 ---
            // Phase 1 (ECS 学习迁移)：计时器倒计时已移至 ECS ProjectileTimerSystem。
            if (projectiles.Count > 0)
            {
                using var _projscope = s_HandleProjectiles.Auto();
                var expiredIndices = battleEcsRuntime.GetExpiredProjectileIndices();
                if (expiredIndices.Length > 0)
                {
                    for (int ei = expiredIndices.Length - 1; ei >= 0; ei--)
                    {
                        int i = expiredIndices[ei];
                        if (i >= projectiles.Count) { continue; }
                        if (projectiles[i].type == TargetType.unit)
                        {
                            if (projectiles[i].heal)
                            {
                                _units[projectiles[i].target].Heal(projectiles[i].damage);
                                for (int j = 0; j < _units.Count; j++)
                                {
                                    if (_units[j].health <= 0 || j == projectiles[i].target || _units[j].unit.movement == Data.UnitMoveType.fly) { continue; }
                                    float distance = BattleVector2.Distance(_units[j].position, _units[projectiles[i].target].position);
                                    if (distance < projectiles[i].splash * Data.gridCellSize)
                                    {
                                        _units[j].Heal(projectiles[i].damage * (1f - (distance / projectiles[i].splash * Data.gridCellSize)));
                                    }
                                }
                            }
                            else
                            {
                                _units[projectiles[i].target].TakeDamage(projectiles[i].damage);
                                if (projectiles[i].splash > 0)
                                {
                                    for (int j = 0; j < _units.Count; j++)
                                    {
                                        if (j != projectiles[i].target)
                                        {
                                            float distance = BattleVector2.Distance(_units[j].position, _units[projectiles[i].target].position);
                                            if (distance < projectiles[i].splash * Data.gridCellSize)
                                            {
                                                _units[j].TakeDamage(projectiles[i].damage * (1f - (distance / projectiles[i].splash * Data.gridCellSize)));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            DamageBuilding(projectiles[i].target, projectiles[i].damage);
                        }
                        projectiles.RemoveAt(i);
                    }
                }
                expiredIndices.Dispose();
            }
            FlushBurstRequests();
            frameCount++;
        }

        /// <summary>
        /// 判断某类建筑是否允许被单位或法术作为有效攻击目标。
        /// </summary>
        /// <param name="id">建筑类型。</param>
        /// <returns>陷阱、装饰物、障碍物等不可攻击对象返回 false。</returns>
        public static bool IsBuildingCanBeAttacked(Data.BuildingID id)
        {
            switch (id)
            {
                case Data.BuildingID.obstacle:
                case Data.BuildingID.decoration:
                case Data.BuildingID.boomb:
                case Data.BuildingID.springtrap:
                case Data.BuildingID.airbomb:
                case Data.BuildingID.giantbomb:
                case Data.BuildingID.seekingairmine:
                case Data.BuildingID.skeletontrap:
                    return false;
            }
            return true;
        }

    }
}
