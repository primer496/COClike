using AStarPathfinding;
using System;
using System.Collections.Generic;
using Unity.Collections;

namespace DevelopersHub.ClashOfWhatecer
{
    public partial class Battle
    {
        // ── 共享缓冲区（用于 GetPathToWall / FillSortedTargets） ──
        private readonly List<KeyValuePair<int, float>> sortedTargetBuffer = new List<KeyValuePair<int, float>>(64);
        private readonly List<int> sharedWallUnitIndexBuffer = new List<int>(16);
        private readonly List<BattleVector2Int> sharedWallUnitPositionBuffer = new List<BattleVector2Int>(16);
        private readonly List<Vector2Int> sharedWallPathStartBuffer = new List<Vector2Int>(16);
        private readonly List<int> sharedWallSlotBuffer = new List<int>(16);

        // ── 单位移动/攻击 ──

        /// <summary>
        /// HandleUnit_MoveAttack 的薄包装：先执行移动+攻击，若目标丢失则立即触发单单位寻路。
        /// 批量路径模式下不直接调用此方法；由 ExecuteFrame 的两趟循环控制调用 HandleUnit_MoveAttack。
        /// </summary>
        private void HandleUnit(int index, double deltaTime)
        {
            HandleUnit_MoveAttack(index, deltaTime);

            if (_units[index].health <= 0 || _units[index].target >= 0)
                return;
            if (_units[index].unit.id == Data.UnitID.healer)
                return;

            _units[index].moving = false;
            using (s_UnitFindTargets.Auto())
                FindTargets(index, _units[index].unit.priority);
            if (deltaTime > 0 && _units[index].target >= 0)
            {
                HandleUnit_MoveAttack(index, deltaTime);
            }
        }

        /// <summary>
        /// 仅执行移动+攻击，不触发寻路。供批量路径模式的第一趟和第二趟循环使用。
        /// 若本帧移动/攻击后目标丢失，仅将 target 置为 -1，不做任何寻路。
        /// </summary>
        private void HandleUnit_MoveAttack(int index, double deltaTime)
        {
            if (_units[index].unit.id == Data.UnitID.healer)
            {
                if (_units[index].target >= 0 && (_units[_units[index].target].health <= 0 || _units[_units[index].target].health >= _units[_units[index].target].unit.health))
                {
                    _units[index].moving = false;
                    _units[index].target = -1;
                }
                if (_units[index].target < 0)
                {
                    _units[index].moving = false;
                    FindHealerTargets(index);
                }
                if (_units[index].target >= 0)
                {
                    _units[index].moving = false;
                    float distance = BattleVector2.Distance(_units[index].position, _units[_units[index].target].position);
                    if (distance + Data.gridCellSize <= _units[index].unit.attackRange)
                    {
                        _units[index].attackTimer += deltaTime;
                        if (_units[index].attackTimer >= _units[index].unit.attackSpeed)
                        {
                            _units[index].attackTimer -= _units[index].unit.attackSpeed;
                            if (_units[index].unit.attackRange > 0 && _units[index].unit.rangedSpeed > 0)
                            {
                                Projectile projectile = new Projectile();
                                projectile.type = TargetType.unit;
                                projectile.target = _units[index].target;
                                projectile.timer = distance / (_units[index].unit.rangedSpeed * Data.gridCellSize);
                                projectile.damage = GetUnitDamage(index);
                                projectile.follow = true;
                                projectile.position = _units[index].position;
                                projectile.heal = true;
                                projectileCount++;
                                projectile.id = projectileCount;
                                projectiles.Add(projectile);
                                if (projectileCallback != null)
                                {
                                    projectileCallback.Invoke(projectile.id, _units[index].position, _units[_units[index].target].position);
                                }
                            }
                            else
                            {
                                float baseHeal = GetUnitDamage(index);
                                _units[_units[index].target].Heal(baseHeal);
                                for (int i = 0; i < _units.Count; i++)
                                {
                                    if (_units[i].health <= 0 || i == index || i == _units[index].target)
                                    {
                                        continue;
                                    }
                                    float d = BattleVector2.Distance(_units[i].position, _units[_units[index].target].position);
                                    if (d < _units[i].unit.splashRange * Data.gridCellSize)
                                    {
                                        float amount = baseHeal * (1f - (d / _units[i].unit.splashRange * Data.gridCellSize));
                                        _units[i].Heal(amount);
                                    }
                                }
                            }
                            if (_units[index].attackCallback != null)
                            {
                                _units[index].attackCallback.Invoke(_units[index].unit.databaseID, 0);
                            }
                        }  
                    }
                    else
                    {
                        _units[index].moving = true;
                        float d = (float)deltaTime * GetUnitMoveSpeed(index) * Data.gridCellSize;
                        _units[index].position = BattleVector2.LerpUnclamped(_units[index].position, _units[_units[index].target].position, d / distance);
                        return;
                    }
                }
            }
            else
            {
                if (_units[index].path != null)
                {
                    if (_units[index].target < 0 || (_units[index].target >= 0 && _buildings[_units[index].target].health <= 0))
                    {
                        _units[index].moving = false;
                        _units[index].ClearPath();
                        _units[index].target = -1;
                    }
                    else
                    {
                        _units[index].moving = true;

                        double remainedTime = _units[index].pathTime - _units[index].pathTraveledTime;
                        if (remainedTime >= deltaTime)
                        {
                            double moveExtra = 1;
                            double s = GetUnitMoveSpeed(index);
                            if (s != _units[index].unit.moveSpeed)
                            {
                                moveExtra = s / _units[index].unit.moveSpeed;
                            }
                            _units[index].pathTraveledTime += (deltaTime * moveExtra);
                            if (_units[index].pathTraveledTime > _units[index].pathTime)
                            {
                                _units[index].pathTraveledTime = _units[index].pathTime;
                            }
                            if (_units[index].pathTraveledTime < 0)
                            {
                                _units[index].pathTraveledTime = 0;
                            }
                            deltaTime = 0;
                        }
                        else
                        {
                            _units[index].pathTraveledTime = _units[index].pathTime;
                            deltaTime -= remainedTime;
                        }

                        using (s_UnitPathFollow.Auto())
                            _units[index].position = GetPathPosition(_units[index].path.points, _units[index].path.pointCount, (float)(_units[index].pathTraveledTime / _units[index].pathTime));

                        if (_units[index].unit.attackRange > 0 && IsBuildingInRange(index, _units[index].target))
                        {
                            _units[index].ClearPath();
                        }
                        else
                        {
                            Vector2Int lastPoint = _units[index].path.LastPoint;
                            BattleVector2 targetPosition = GridToWorldPosition(new BattleVector2Int(lastPoint.X, lastPoint.Y));
                            float distance = BattleVector2.Distance(_units[index].position, targetPosition);
                            if (distance <= Data.gridCellSize * 0.05f)
                            {
                                _units[index].position = targetPosition;
                                _units[index].ClearPath();
                                _units[index].moving = false;
                            }
                        }
                    }
                }

                if (_units[index].target >= 0)
                {
                    if (_buildings[_units[index].target].health > 0)
                    {
                        if (_buildings[_units[index].target].building.id == Data.BuildingID.wall && _units[index].mainTarget >= 0 && _buildings[_units[index].mainTarget].health <= 0)
                        {
                            _units[index].moving = false;
                            _units[index].target = -1;
                        }
                        else
                        {
                            if (_units[index].path == null)
                            {
                                _units[index].moving = false;
                                _units[index].attackTimer += deltaTime;
                                if (_units[index].attackTimer >= _units[index].unit.attackSpeed)
                                {
                                    float multiplier = 1;
                                    if (_units[index].unit.priority != Data.TargetPriority.all || _units[index].unit.priority != Data.TargetPriority.none)
                                    {
                                        switch (_buildings[_units[index].target].building.id)
                                        {
                                            case Data.BuildingID.townhall:
                                            case Data.BuildingID.goldmine:
                                            case Data.BuildingID.goldstorage:
                                            case Data.BuildingID.elixirmine:
                                            case Data.BuildingID.elixirstorage:
                                            case Data.BuildingID.darkelixirmine:
                                            case Data.BuildingID.darkelixirstorage:
                                                if (_units[index].unit.priority == Data.TargetPriority.resources)
                                                {
                                                    multiplier = _units[index].unit.priorityMultiplier;
                                                }
                                                break;
                                            case Data.BuildingID.wall:
                                                if (_units[index].unit.priority == Data.TargetPriority.walls)
                                                {
                                                    multiplier = _units[index].unit.priorityMultiplier;
                                                }
                                                break;
                                            case Data.BuildingID.cannon:
                                            case Data.BuildingID.archertower:
                                            case Data.BuildingID.mortor:
                                            case Data.BuildingID.airdefense:
                                            case Data.BuildingID.wizardtower:
                                            case Data.BuildingID.hiddentesla:
                                            case Data.BuildingID.bombtower:
                                            case Data.BuildingID.xbow:
                                            case Data.BuildingID.infernotower:
                                                if (_units[index].unit.priority == Data.TargetPriority.defenses)
                                                {
                                                    multiplier = _units[index].unit.priorityMultiplier;
                                                }
                                                break;
                                        }
                                    }

                                    float distance = BattleVector2.Distance(_units[index].position, _buildings[_units[index].target].worldCenterPosition);
                                    if (_units[index].unit.attackRange > 0 && _units[index].unit.rangedSpeed > 0)
                                    {
                                        Projectile projectile = new Projectile();
                                        projectile.type = TargetType.building;
                                        projectile.target = _units[index].target;
                                        projectile.timer = distance / (_units[index].unit.rangedSpeed * Data.gridCellSize);
                                        projectile.damage = GetUnitDamage(index) * multiplier;
                                        projectile.follow = true;
                                        projectile.position = _units[index].position;
                                        projectileCount++;
                                        projectile.id = projectileCount;
                                        projectiles.Add(projectile);
                                        if (projectileCallback != null)
                                        {
                                            projectileCallback.Invoke(projectile.id, _units[index].position, _buildings[_units[index].target].worldCenterPosition);
                                        }
                                    }
                                    else
                                    {
                                        DamageBuilding(_units[index].target, GetUnitDamage(index) * multiplier);
                                    }
                                    _units[index].attackTimer -= _units[index].unit.attackSpeed;
                                    if (_units[index].attackCallback != null)
                                    {
                                        _units[index].attackCallback.Invoke(_units[index].unit.databaseID, _buildings[_units[index].target].building.databaseID);
                                    }
                                    if (_units[index].unit.id == Data.UnitID.wallbreaker)
                                    {
                                        _units[index].TakeDamage(_units[index].health);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        _units[index].moving = false;
                        _units[index].target = -1;
                    }
                }
            }
        }

        /// <summary>
        /// 预算型跨单位微批处理：从跨帧持久队列中取出最多 MAX_PATH_UNITS_PER_FRAME 个单位，
        /// 在 MAX_PATH_SLOTS_PER_FRAME 槽位预算内统一 Schedule → 一次 Flush → 读取结果。
        /// 成功拿到路径的单位从队列移除；未处理或失败的单位留在队列中等待后续帧。
        /// </summary>
        private void ProcessRepathQueueWithBudget(double deltaTime)
        {
            int unitsProcessed = 0;
            int slotsUsed = 0;
            _batchUnitCount = 0;
            int totalCornerSlots = 0;
            int queueIdx = 0;

            // ── 阶段 1：按预算收集候选建筑 + 调度 A* ──
            while (queueIdx < _batchPathUnitQueue.Count && unitsProcessed < MAX_PATH_UNITS_PER_FRAME)
            {
                int unitIndex = _batchPathUnitQueue[queueIdx];

                // 跳过死亡单位或治疗者（清理出队）
                if (_units[unitIndex].health <= 0 || _units[unitIndex].unit.id == Data.UnitID.healer)
                {
                    _pendingRepathSet.Remove(unitIndex);
                    _batchPathUnitQueue.RemoveAt(queueIdx);
                    continue;
                }

                var priority = _units[unitIndex].unit.priority;
                ListUnitTargets(unitIndex, priority);

                int buildingIndex = -1;
                if (priority == Data.TargetPriority.defenses && _units[unitIndex].defenceTargets.Count > 0)
                    TryGetClosestTarget(_units[unitIndex].defenceTargets, out buildingIndex);
                else if (priority == Data.TargetPriority.resources && _units[unitIndex].resourceTargets.Count > 0)
                    TryGetClosestTarget(_units[unitIndex].resourceTargets, out buildingIndex);
                else
                    TryGetClosestAllTarget(unitIndex, out buildingIndex);

                // 无有效候选：跳过（留在队列中，下帧重试）
                if (buildingIndex < 0)
                {
                    queueIdx++;
                    continue;
                }
                if (_buildings[buildingIndex].building.id == Data.BuildingID.wall ||
                    _buildings[buildingIndex].building.id == Data.BuildingID.decoration ||
                    _buildings[buildingIndex].building.id == Data.BuildingID.obstacle)
                {
                    queueIdx++;
                    continue;
                }

                BattleVector2Int unitGridPos = WorldToGridPosition(_units[unitIndex].position);
                int startX = _buildings[buildingIndex].building.x;
                int endX = _buildings[buildingIndex].building.x + _buildings[buildingIndex].building.columns - 1;
                int startY = _buildings[buildingIndex].building.y;
                int endY = _buildings[buildingIndex].building.y + _buildings[buildingIndex].building.rows - 1;

                int colCount = startX == endX ? 1 : 2;
                int rowCount = startY == endY ? 1 : 2;
                int cornerCount = colCount * rowCount;

                // ── 槽位预算检查：若本帧不够放这个单位的所有角，停止处理 ──
                if (slotsUsed + cornerCount > MAX_PATH_SLOTS_PER_FRAME)
                    break;

                int slotBase = totalCornerSlots * 2;

                bool isGround = _units[unitIndex].unit.movement == Data.UnitMoveType.ground;
                int ci = 0;
                for (int cx = 0; cx < colCount; cx++)
                {
                    int col = cx == 0 ? startX : endX;
                    for (int cy = 0; cy < rowCount; cy++)
                    {
                        int row = cy == 0 ? startY : endY;
                        if (!IsGridPositionValid(col, row))
                        {
                            ci++;
                            continue;
                        }

                        int flatIdx = slotBase + ci * 2;
                        Vector2Int start = new Vector2Int(col, row);
                        Vector2Int goal = new Vector2Int(unitGridPos.x, unitGridPos.y);

                        _batchFlatSlots[flatIdx] = ScheduleBurstFindLocations(false, start, goal);
                        _batchFlatSlots[flatIdx + 1] = isGround
                            ? ScheduleBurstFindLocations(true, start, goal)
                            : -1;

                        _batchFlatCandidates[slotBase / 2 + ci] = new BattleVector2Int(col, row);
                        _batchFlatStarts[slotBase / 2 + ci] = start;
                        _batchFlatGoals[slotBase / 2 + ci] = goal;
                        ci++;
                    }
                }

                _batchCornerCounts[_batchUnitCount] = cornerCount;
                _batchSlotOffsets[_batchUnitCount] = slotBase;
                _batchPathBuildingQueue.Add(buildingIndex);
                _batchUnitIndices[_batchUnitCount] = unitIndex;
                _batchUnitCount++;
                totalCornerSlots += cornerCount;
                slotsUsed += cornerCount;
                unitsProcessed++;
                queueIdx++;
            }

            if (_batchUnitCount == 0) return;

            // ── 阶段 2：统一 Flush ──
            FlushBurstRequests();

            // ── 阶段 3：逐一读取结果，选出最优路径 ──
            for (int bi = 0; bi < _batchUnitCount; bi++)
            {
                int unitIndex = _batchUnitIndices[bi];
                int buildingIndex = _batchPathBuildingQueue[bi];
                int cornerCount = _batchCornerCounts[bi];
                int slotBase = _batchSlotOffsets[bi];

                BattleVector2Int unitGridPos = WorldToGridPosition(_units[unitIndex].position);
                Path bestPath = null;
                float bestDistance = 99999f;
                int bestBlocks = 999;
                int bestTargetIndex = buildingIndex;

                for (int ci = 0; ci < cornerCount; ci++)
                {
                    int flatIdx = slotBase + ci * 2;
                    int searchSlot = _batchFlatSlots[flatIdx];
                    int unlimitedSlot = _batchFlatSlots[flatIdx + 1];

                    if (searchSlot < 0) continue;

                    Vector2Int start = _batchFlatStarts[slotBase / 2 + ci];
                    Vector2Int goal = _batchFlatGoals[slotBase / 2 + ci];
                    BattleVector2Int candidate = _batchFlatCandidates[slotBase / 2 + ci];

                    GetBurstResult(searchSlot, out int sc, out NativeArray<Vector2Int> sp);
                    if (Path.IsValid(sp, sc, start, goal))
                    {
                        float len = GetPathLength(sp, sc);
                        int blk = (int)Math.Floor(len / (Data.battleTilesWorthOfOneWall * Data.gridCellSize));
                        if (len < bestDistance && blk <= bestBlocks)
                        {
                            Path p = new Path();
                            if (p.Create(sp, sc, candidate, unitGridPos))
                            {
                                p.length = len;
                                bestPath = p;
                                bestDistance = len;
                                bestBlocks = blk;
                            }
                        }
                    }

                    if (unlimitedSlot >= 0)
                    {
                        GetBurstResult(unlimitedSlot, out int uc, out NativeArray<Vector2Int> up);
                        if (Path.IsValid(up, uc, start, goal))
                        {
                            float len = GetPathLength(up, uc);
                            int blk = CountBlockingWalls(up, uc);
                            if (len < bestDistance && blk <= bestBlocks)
                            {
                                Path p = new Path();
                                if (p.Create(up, uc, candidate, unitGridPos))
                                {
                                    PopulateBlockingWalls(p);
                                    p.length = len;
                                    bestPath = p;
                                    bestDistance = len;
                                    bestBlocks = blk;
                                }
                            }
                        }
                    }
                }

                if (bestPath != null)
                {
                    bestPath.ReversePoints();

                    if (bestPath.blocks.Count > 0)
                    {
                        Tile last = bestPath.blocks[bestPath.blocks.Count - 1];
                        for (int pi = bestPath.pointCount - 1; pi >= 0; pi--)
                        {
                            if (bestPath.points[pi].X == last.position.x &&
                                bestPath.points[pi].Y == last.position.y)
                            {
                                bestPath.pointCount = pi;
                                break;
                            }
                        }
                        _units[unitIndex].mainTarget = buildingIndex;
                        _units[unitIndex].AssignTarget(last.index, bestPath);
                    }
                    else
                    {
                        _units[unitIndex].AssignTarget(bestTargetIndex, bestPath);
                    }

                    // ── 成功拿到路径，从跨帧队列移除 ──
                    _pendingRepathSet.Remove(unitIndex);
                }
                // 失败的单位留在队列中，下帧自动重试
            }

            // ── 从队列中移除本帧已成功处理的单位 ──
            for (int qi = _batchPathUnitQueue.Count - 1; qi >= 0; qi--)
            {
                int i = _batchPathUnitQueue[qi];
                if (!_pendingRepathSet.Contains(i))
                {
                    _batchPathUnitQueue.RemoveAt(qi);
                }
            }

            _batchPathBuildingQueue.Clear();
        }

        // ── 治疗单位索敌 ──

        /// <summary>
        /// 为治疗单位寻找最近的受伤友军。
        /// </summary>
        private void FindHealerTargets(int index)
        {
            int target = -1;
            float distance = 99999;
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].health <= 0 || i == index || _units[i].health >= _units[i].unit.health || _units[i].unit.movement == Data.UnitMoveType.fly)
                {
                    continue;
                }
                float d = BattleVector2.Distance(_units[i].position, _units[index].position);
                if (d < distance)
                {
                    target = i;
                    distance = d;
                }
            }
            if (target >= 0)
            {
                _units[index].AssignHealerTarget(target, distance + Data.gridCellSize);
            }
        }

        // ── 目标选择与排序 ──

        /// <summary>
        /// 按资源、防御、其他三类整理当前单位可攻击的建筑候选。
        /// </summary>
        private void ListUnitTargets(int index, Data.TargetPriority priority)
        {
            _units[index].resourceTargets.Clear();
            _units[index].defenceTargets.Clear();
            _units[index].otherTargets.Clear();
            if (priority == Data.TargetPriority.walls)
            {
                priority = Data.TargetPriority.all;
            }
            for (int i = 0; i < _buildings.Count; i++)
            {
                if (_buildings[i].health <= 0 || _buildings[i].building.id == Data.BuildingID.wall || !IsBuildingCanBeAttacked(_buildings[i].building.id))
                {
                    continue;
                }
                float distance = BattleVector2.Distance(_buildings[i].worldCenterPosition, _units[index].position);
                switch (_buildings[i].building.id)
                {
                    case Data.BuildingID.townhall:
                    case Data.BuildingID.elixirmine:
                    case Data.BuildingID.elixirstorage:
                    case Data.BuildingID.darkelixirmine:
                    case Data.BuildingID.darkelixirstorage:
                    case Data.BuildingID.goldmine:
                    case Data.BuildingID.goldstorage:
                        _units[index].resourceTargets.Add(i, distance);
                        break;
                    case Data.BuildingID.cannon:
                    case Data.BuildingID.archertower:
                    case Data.BuildingID.mortor:
                    case Data.BuildingID.airdefense:
                    case Data.BuildingID.wizardtower:
                    case Data.BuildingID.hiddentesla:
                    case Data.BuildingID.bombtower:
                    case Data.BuildingID.xbow:
                    case Data.BuildingID.infernotower:
                        _units[index].defenceTargets.Add(i, distance);
                        break;
                    default:
                        _units[index].otherTargets.Add(i, distance);
                        break;
                }
            }
        }

        /// <summary>
        /// 根据单位优先级从候选建筑中选出最终目标，并尝试规划路径。
        /// </summary>
        private void FindTargets(int index, Data.TargetPriority priority)
        {
            ListUnitTargets(index, priority);
            if (priority == Data.TargetPriority.defenses)
            {
                if (_units[index].defenceTargets.Count > 0)
                {
                    AssignTarget(index, ref _units[index].defenceTargets);
                }
                else
                {
                    FindTargets(index, Data.TargetPriority.all);
                    return;
                }
            }
            else if (priority == Data.TargetPriority.resources)
            {
                if (_units[index].resourceTargets.Count > 0)
                {
                    AssignTarget(index, ref _units[index].resourceTargets);
                }
                else
                {
                    FindTargets(index, Data.TargetPriority.all);
                    return;
                }
            }
            else if (priority == Data.TargetPriority.all || priority == Data.TargetPriority.walls)
            {
                AssignTarget(index, priority == Data.TargetPriority.walls);
            }
        }

        private static bool TryGetClosestTarget(Dictionary<int, float> targets, out int targetIndex)
        {
            targetIndex = -1;
            float minDistance = float.MaxValue;
            foreach (var target in targets)
            {
                if (target.Value < minDistance)
                {
                    minDistance = target.Value;
                    targetIndex = target.Key;
                }
            }
            return targetIndex >= 0;
        }

        private static void UpdateClosestTarget(Dictionary<int, float> targets, ref float minDistance, ref int targetIndex)
        {
            foreach (var target in targets)
            {
                if (target.Value < minDistance)
                {
                    minDistance = target.Value;
                    targetIndex = target.Key;
                }
            }
        }

        private bool TryGetClosestAllTarget(int unitIndex, out int targetIndex)
        {
            targetIndex = -1;
            float minDistance = float.MaxValue;
            UpdateClosestTarget(_units[unitIndex].otherTargets, ref minDistance, ref targetIndex);
            UpdateClosestTarget(_units[unitIndex].resourceTargets, ref minDistance, ref targetIndex);
            UpdateClosestTarget(_units[unitIndex].defenceTargets, ref minDistance, ref targetIndex);
            return targetIndex >= 0;
        }

        private void FillSortedTargets(int unitIndex)
        {
            sortedTargetBuffer.Clear();
            foreach (var target in _units[unitIndex].otherTargets)
            {
                sortedTargetBuffer.Add(target);
            }
            foreach (var target in _units[unitIndex].resourceTargets)
            {
                sortedTargetBuffer.Add(target);
            }
            foreach (var target in _units[unitIndex].defenceTargets)
            {
                sortedTargetBuffer.Add(target);
            }
            sortedTargetBuffer.Sort((left, right) => left.Value.CompareTo(right.Value));
        }

        /// <summary>
        /// 为单位指定最终目标，并在需要时优先求解打墙路径。
        /// </summary>
        private void AssignTarget(int index, ref Dictionary<int, float> targets, bool wallsPriority = false)
        {
            if (wallsPriority)
            {
                var wallPath = GetPathToWall(index);
                if (wallPath.Item1 >= 0)
                {
                    _units[index].AssignTarget(wallPath.Item1, wallPath.Item2);
                    return;
                }
            }

            if (TryGetClosestTarget(targets, out int min))
            {
                var path = GetPathToBuilding(min, index);
                if (path.Item1 >= 0)
                {
                    _units[index].AssignTarget(path.Item1, path.Item2);
                }
            }
        }

        private void AssignTarget(int index, bool wallsPriority)
        {
            if (wallsPriority)
            {
                var wallPath = GetPathToWall(index);
                if (wallPath.Item1 >= 0)
                {
                    _units[index].AssignTarget(wallPath.Item1, wallPath.Item2);
                    return;
                }
            }

            if (TryGetClosestAllTarget(index, out int min))
            {
                var path = GetPathToBuilding(min, index);
                if (path.Item1 >= 0)
                {
                    _units[index].AssignTarget(path.Item1, path.Item2);
                }
            }
        }

        // ── 单位属性计算 ──

        private bool IsBuildingInRange(int unitIndex, int buildingIndex)
        {
            for (int x = _buildings[buildingIndex].building.x; x < _buildings[buildingIndex].building.x + _buildings[buildingIndex].building.columns; x++)
            {
                for (int y = _buildings[buildingIndex].building.y; y < _buildings[buildingIndex].building.y + _buildings[buildingIndex].building.columns; y++)
                {
                    float distance = BattleVector2.Distance(GridToWorldPosition(new BattleVector2Int(x, y)), _units[unitIndex].position);
                    if (distance <= _units[unitIndex].unit.attackRange)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 计算单位在当前法术增益下的实际伤害值。
        /// </summary>
        private float GetUnitDamage(int index)
        {
            float damage = _units[index].unit.damage;
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done) { continue; }
                if (_spells[i].spell.id == Data.SpellID.rage)
                {
                    damage += (_units[index].unit.damage * _spells[i].spell.server.pulsesValue);
                }
            }
            return damage;
        }

        /// <summary>
        /// 计算单位在当前法术增益下的实际移动速度。
        /// </summary>
        private float GetUnitMoveSpeed(int index)
        {
            float speed = _units[index].unit.moveSpeed;
            for (int i = 0; i < _spells.Count; i++)
            {
                if (_spells[i].done) { continue; }
                if (_spells[i].spell.id == Data.SpellID.rage)
                {
                    speed += _spells[i].spell.server.pulsesValue2;
                }
                else if (_spells[i].spell.id == Data.SpellID.haste)
                {
                    speed += _spells[i].spell.server.pulsesValue;
                }
            }
            return speed;
        }

        // ── 法术处理 ──

        /// <summary>
        /// 处理单个法术在当前帧的脉冲计时与效果结算。
        /// </summary>
        private bool HandleSpell(int index, double deltaTime)
        {
            bool end = false;
            _spells[index].palsesTimer += deltaTime;
            if (_spells[index].palsesTimer >= _spells[index].spell.server.pulsesDuration)
            {
                _spells[index].palsesTimer -= _spells[index].spell.server.pulsesDuration;
                _spells[index].palsesDone += 1;
                switch (_spells[index].spell.id)
                {
                    case Data.SpellID.lightning:
                        for (int i = 0; i < _buildings.Count; i++)
                        {
                            if (_buildings[i].health <= 0 || !IsBuildingCanBeAttacked(_buildings[i].building.id)) { continue; }
                            float damage = (float)Math.Ceiling(GetBuildingInSpellRangePercentage(index, i) * _spells[index].spell.server.pulsesValue);
                            if (damage <= 0) { continue; }
                            DamageBuilding(i, damage);
                        }
                        break;
                    case Data.SpellID.healing:
                        for (int i = 0; i < _units.Count; i++)
                        {
                            if (_units[i].health <= 0) { continue; }
                            float distance = BattleVector2.Distance(_units[i].position, _spells[index].position);
                            if (distance > _spells[index].spell.server.radius * Data.gridCellSize) { continue; }
                            _units[i].Heal(_spells[index].spell.server.pulsesValue);
                        }
                        break;
                    case Data.SpellID.rage:
                    case Data.SpellID.jump:
                    case Data.SpellID.freeze:
                    case Data.SpellID.invisibility:
                    case Data.SpellID.earthquake:
                    case Data.SpellID.haste:
                    case Data.SpellID.skeleton:
                    case Data.SpellID.bat:
                        break;
                }
                if (_spells[index].pulseCallback != null)
                {
                    _spells[index].pulseCallback.Invoke(_spells[index].spell.databaseID);
                }
            }
            if (_spells[index].palsesDone >= _spells[index].spell.server.pulsesCount)
            {
                _spells[index].done = true;
                if (_spells[index].doneCallback != null)
                {
                    _spells[index].doneCallback.Invoke(_spells[index].spell.databaseID);
                }
                end = true;
            }
            return end;
        }

        /// <summary>
        /// 统计当前战斗中已经掠夺的资源，以及可掠夺的总资源。
        /// </summary>
        public (int, int, int, int, int, int) GetlootedResources()
        {
            int totalGold = 0;
            int totalElixir = 0;
            int totalDark = 0;
            int lootedGold = 0;
            int lootedElixir = 0;
            int lootedDark = 0;
            for (int i = 0; i < _buildings.Count; i++)
            {
                switch (_buildings[i].building.id)
                {
                    case Data.BuildingID.townhall:
                        totalGold += _buildings[i].lootGoldStorage;
                        lootedGold += _buildings[i].lootedGold;
                        totalElixir += _buildings[i].lootElixirStorage;
                        lootedElixir += _buildings[i].lootedElixir;
                        totalDark += _buildings[i].lootDarkStorage;
                        lootedDark += _buildings[i].lootedDark;
                        break;
                    case Data.BuildingID.goldmine:
                    case Data.BuildingID.goldstorage:
                        totalGold += _buildings[i].lootGoldStorage;
                        lootedGold += _buildings[i].lootedGold;
                        break;
                    case Data.BuildingID.elixirmine:
                    case Data.BuildingID.elixirstorage:
                        totalElixir += _buildings[i].lootElixirStorage;
                        lootedElixir += _buildings[i].lootedElixir;
                        break;
                    case Data.BuildingID.darkelixirmine:
                    case Data.BuildingID.darkelixirstorage:
                        totalDark += _buildings[i].lootDarkStorage;
                        lootedDark += _buildings[i].lootedDark;
                        break;
                    case Data.BuildingID.clancastle:
                        break;
                }
            }
            return (lootedGold, lootedElixir, lootedDark, totalGold, totalElixir, totalDark);
        }
    }
}
