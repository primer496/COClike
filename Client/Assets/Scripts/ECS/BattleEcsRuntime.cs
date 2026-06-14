using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace DevelopersHub.ClashOfWhatecer
{
    /// <summary>
    /// Battle 专属 ECS World 的生命周期管理器。
    ///
    /// 一句话作用：
    ///   让 Battle.cs 可以用 3 行代码（Initialize / Update / Dispose）
    ///   驱动一个独立的 ECS 小世界，而不需要 Battle.cs 自己理解 World / EntityManager / SystemGroup。
    ///
    /// 调用链：
    ///   Battle.Initialize()          → battleEcsRuntime.Initialize()   创建 World + System + 帧单例实体
    ///   Battle.ExecuteFrame()        → battleEcsRuntime.Update(...)    写入帧数据 → world.Update() 驱动所有 System
    ///   Battle.DisposeRuntimeData()  → battleEcsRuntime.Dispose()      释放 World 及所有内部资源
    ///
    /// 当前状态（2026-06-08）：
    ///   - World 中只有 1 个 System：BattleFrameStateSystem（空占位）
    ///   - World 中只有 1 个 Entity：携带 BattleEcsFrameState 组件
    ///   - 不影响任何玩法逻辑，仅是骨架
    ///
    /// 设计原则：
    ///   - 不做异步：world.Update() 在 ExecuteFrame() 同步路径内调用，保证 20 tick/s 确定性不变
    ///   - 不做侵入：Battle.cs 原有 List&lt;Unit&gt; / List&lt;Projectile&gt; 完全不受影响
    ///   - 先骨架后填肉：World + System + Entity 生命周期正确后，再逐步往里面加 ProjectileSystem / PathSystem
    /// </summary>
    public sealed class BattleEcsRuntime : IDisposable
    {
        /// <summary>
        /// ECS 总开关。默认 false（关闭），验证骨架稳定后再置 true。
        /// 关闭时 Initialize / Update 均为空操作，零开销。
        /// </summary>
        public static bool EnableECS = true;

        /// <summary>ECS 世界实例。每场战斗一个，战斗结束即销毁。</summary>
        private World world;

        /// <summary>携带 BattleEcsFrameState 单例组件的实体。</summary>
        private Entity frameStateEntity;

        /// <summary>
        /// 投射物列表索引 → ECS Entity 的映射表。
        /// Key = Battle.projectiles 列表中的索引，Value = 对应的 ECS Entity。
        /// 用于双向同步：创建投射物时建立映射，过期/销毁时清理映射。
        /// </summary>
        private readonly Dictionary<int, Entity> _projectileEntityMap = new Dictionary<int, Entity>();

        /// <summary>安全判活：World 已创建且未被 Dispose。</summary>
        public bool IsCreated => world != null && world.IsCreated;

        /// <summary>
        /// 初始化 ECS World。
        ///
        /// 做四件事：
        ///   1. 先 Dispose 旧 World（如果存在），防止重复创建泄漏
        ///   2. new World("BattleSimulationWorld", WorldFlags.Game)
        ///      - WorldFlags.Game 表示这是游戏运行时 World（非 Editor 预览）
        ///   3. 获取 SimulationSystemGroup 并向其中注册 BattleFrameStateSystem
        ///      - SimulationSystemGroup 是 ECS 框架的默认主循环组，world.Update() 会按序执行组内所有 System
        ///      - SortSystems() 按依赖关系排序（当前只有一个 System，但为后续扩展预留）
        ///   4. 创建一个携带 BattleEcsFrameState 组件的实体
        ///      - 这就是 ECS 里的"全局单例"模式：存一个实体，所有 System 都能查到它的组件数据
        ///      - SetName 仅用于调试时在 Entity Debugger 中识别
        /// </summary>
        public void Initialize()
        {
            if (!EnableECS)
            {
                return;
            }

            // 防止重复初始化导致多个 World 并存泄漏
            Dispose();

            // 创建 World："BattleSimulationWorld" 是调试名称，WorldFlags.Game 标记为运行时世界
            world = new World("BattleSimulationWorld", WorldFlags.Game);

            // 获取主循环组并注册 System
            // SimulationSystemGroup 相当于 MonoBehaviour 的 Update() 循环，
            // world.Update() 时会依次调用组内每个 System 的 OnUpdate()
            var simulationGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simulationGroup.AddSystemToUpdateList(world.CreateSystemManaged<BattleFrameStateSystem>());
            // Phase 1：注册投射物计时器 System
            simulationGroup.AddSystemToUpdateList(world.CreateSystemManaged<ProjectileTimerSystem>());
            simulationGroup.SortSystems();

            // 创建帧状态单例实体
            // 整个 World 中只此一个实体带 BattleEcsFrameState，
            // 所有 System 通过 RequireForUpdate / SystemAPI.Query 访问它
            frameStateEntity = world.EntityManager.CreateEntity(typeof(BattleEcsFrameState));
            world.EntityManager.SetName(frameStateEntity, "BattleFrameState");
        }

        /// <summary>
        /// 每帧由 Battle.ExecuteFrame() 调用，驱动 ECS World 推进一帧。
        ///
        /// 做三件事：
        ///   1. 安全守卫：World 未创建则跳过；实体意外丢失则重建（防御性编程）
        ///   2. 将 Battle 侧的帧数据写入 ECS 单例组件
        ///      - 这是"托管侧 → ECS 侧"的唯一数据入口
        ///      - 后续 ProjectileSystem 通过 SystemAPI.GetSingleton&lt;BattleEcsFrameState&gt;() 读取这些值
        ///   3. world.Update() 驱动所有已注册 System 的 OnUpdate()
        ///      - 当前只有 BattleFrameStateSystem（空），后续加入 ProjectileTimerSystem 等
        ///
        /// 注意：world.Update() 是同步调用，会阻塞直到所有 System 执行完毕。
        ///       这与 JobHandle.Complete() 不同 —— 它不涉及 Job 调度，只是纯 ECS System 循环。
        /// </summary>
        /// <param name="frameCount">当前帧序号，来自 Battle.frameCount</param>
        /// <param name="deltaTime">本帧模拟步长，固定为 Data.battleFrameRate = 0.05</param>
        /// <param name="projectileCount">当前场上投射物数量，来自 Battle.projectiles.Count</param>
        public void Update(int frameCount, float deltaTime, int projectileCount)
        {
            if (!EnableECS || !IsCreated)
            {
                return;
            }

            // 防御性重建：如果实体因未知原因丢失（理论上不会），在此补建
            if (!world.EntityManager.Exists(frameStateEntity))
            {
                frameStateEntity = world.EntityManager.CreateEntity(typeof(BattleEcsFrameState));
                world.EntityManager.SetName(frameStateEntity, "BattleFrameState");
            }

            // 将本帧数据从 Battle 托管侧写入 ECS 侧
            // SetComponentData 会触发 ECS 的变更追踪，
            // 依赖此组件的 System 下次 OnUpdate 时可读到最新值
            world.EntityManager.SetComponentData(frameStateEntity, new BattleEcsFrameState
            {
                FrameCount = frameCount,
                DeltaTime = deltaTime,
                ProjectileCount = projectileCount,
            });

            // 驱动 ECS World 内所有 System 执行一帧
            world.Update();
        }

        // ═══════════════════════════════════════════════════════════════
        // 投射物实体生命周期管理（Phase 1）
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 为托管侧 projectile 创建对应的 ECS Entity。
        /// 仅在 EnableECS=true 且 World 存活时执行。
        /// </summary>
        /// <param name="listIndex">投射物在 Battle.projectiles 列表中的索引</param>
        /// <param name="timer">距命中的剩余时间（秒）</param>
        public void CreateProjectileEntity(int listIndex, float timer)
        {
            if (!EnableECS || !IsCreated)
            {
                return;
            }

            // 防御：重复创建同一个索引的实体会导致映射混乱
            if (_projectileEntityMap.ContainsKey(listIndex))
            {
                return;
            }

            var entity = world.EntityManager.CreateEntity(typeof(ProjectileTimerData));
            world.EntityManager.SetComponentData(entity, new ProjectileTimerData
            {
                Timer = timer,
                ProjectileListIndex = listIndex,
                Expired = false,
            });
            world.EntityManager.SetName(entity, $"Projectile_{listIndex}");
            _projectileEntityMap[listIndex] = entity;
        }

        /// <summary>
        /// 销毁指定投射物索引对应的 ECS Entity，并从映射表移除。
        /// 调用时机：投射物命中后从 projectiles 列表移除时。
        /// </summary>
        public void DestroyProjectileEntity(int listIndex)
        {
            if (!EnableECS || !IsCreated)
            {
                return;
            }

            if (_projectileEntityMap.TryGetValue(listIndex, out var entity))
            {
                if (world.EntityManager.Exists(entity))
                {
                    world.EntityManager.DestroyEntity(entity);
                }
                _projectileEntityMap.Remove(listIndex);
            }
        }

        /// <summary>
        /// 查询所有过期投射物（携带 ProjectileExpiredTag 的实体），
        /// 收集其 ProjectileListIndex 并立即销毁这些 ECS Entity。
        ///
        /// 调用时机：Battle.ExecuteFrame() 在 HandleUnits 之后调用，
        /// 获取本帧到期的投射物列表以执行冲击逻辑。
        ///
        /// 返回的 NativeList 由调用方负责 Dispose（Allocator.Temp）。
        /// </summary>
        /// <returns>本帧到期的投射物在 Battle.projectiles 中的索引列表</returns>
        public NativeList<int> GetExpiredProjectileIndices()
        {
            var result = new NativeList<int>(Allocator.Temp);

            if (!EnableECS || !IsCreated)
            {
                return result;
            }

            // 查询所有携带过期标签的实体
            var query = world.EntityManager.CreateEntityQuery(
                typeof(ProjectileExpiredTag),
                typeof(ProjectileTimerData));

            var entities = query.ToEntityArray(Allocator.Temp);
            var timers = query.ToComponentDataArray<ProjectileTimerData>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                int listIndex = timers[i].ProjectileListIndex;
                result.Add(listIndex);

                // 立即销毁 ECS Entity 并清理映射
                world.EntityManager.DestroyEntity(entities[i]);
                _projectileEntityMap.Remove(listIndex);
            }

            entities.Dispose();
            timers.Dispose();
            query.Dispose();

            return result;
        }

        /// <summary>
        /// 将托管侧 projectiles 列表中尚未同步的投射物创建为 ECS Entity。
        /// 幂等操作：已在 _projectileEntityMap 中的索引会被跳过。
        ///
        /// 调用时机：Battle.ExecuteFrame() 开头，在 world.Update() 之前，
        /// 确保 ProjectileTimerSystem 本帧能处理到这些投射物。
        ///
        /// 注意：HandleBuildings / HandleUnits 中新增的投射物会在下一帧同步，
        /// 存在 1 tick (0.05s) 的同步延迟，对玩法无实质影响。
        /// </summary>
        public void SyncNewProjectiles(System.Collections.Generic.List<Battle.Projectile> projectiles)
        {
            if (!EnableECS || !IsCreated)
            {
                return;
            }

            for (int i = 0; i < projectiles.Count; i++)
            {
                if (!_projectileEntityMap.ContainsKey(i))
                {
                    CreateProjectileEntity(i, projectiles[i].timer);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 释放 ECS World 及所有内部资源。
        ///
        /// 调用时机：
        ///   - 正常：UI_Battle.Close() → battle.DisposeRuntimeData() → 此处
        ///   - 重建：同一 UI_Battle 实例中重新 new Battle() 前，旧实例的 DisposeRuntimeData 会先调用
        ///
        /// World.Dispose() 会递归释放：
        ///   - 所有 Entity 及其组件数据
        ///   - 所有 System 及其内部 NativeArray
        ///   - World 自身的内部结构
        /// </summary>
        public void Dispose()
        {
            // 先置空 Entity 引用，避免悬垂指针
            frameStateEntity = Entity.Null;

            // 清理投射物实体映射表（实体在 World.Dispose 时一并销毁）
            _projectileEntityMap.Clear();

            if (world != null && world.IsCreated)
            {
                world.Dispose();
            }
            world = null;
        }
    }
}
