using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DevelopersHub.ClashOfWhatecer
{
    // ═══════════════════════════════════════════════════════════════
    // 单例组件
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 战斗帧状态单例组件。
    /// 整个 BattleSimulationWorld 中只存在一个实体携带此组件，
    /// 由 Battle.ExecuteFrame() 每帧通过 BattleEcsRuntime.Update() 写入最新值。
    /// </summary>
    public struct BattleEcsFrameState : IComponentData
    {
        /// <summary>当前战斗帧序号，由 Battle.frameCount 同步。</summary>
        public int FrameCount;

        /// <summary>本帧模拟步长（秒），固定为 Data.battleFrameRate = 0.05。</summary>
        public float DeltaTime;

        /// <summary>当前场上投射物数量，供 System 预判容量。</summary>
        public int ProjectileCount;
    }

    // ═══════════════════════════════════════════════════════════════
    // 投射物计时组件（Phase 1 学习迁移）
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 投射物计时器组件。
    /// 每个飞行中的投射物对应一个 ECS Entity，携带此组件。
    /// ProjectileTimerSystem 每帧递减 Timer；当 Timer &lt;= 0 时标记 Expired，
    /// 同时添加 ProjectileExpiredTag 供 Battle.cs 查询哪些投射物已到期。
    ///
    /// ProjectileListIndex 是回到 Battle.projectiles 托管列表的"反向指针"，
    /// 使 Battle.cs 能通过 ECS Entity 找到对应的 Projectile 对象执行冲击逻辑。
    /// </summary>
    public struct ProjectileTimerData : IComponentData
    {
        /// <summary>距命中剩余时间（秒）。由 ECS System 逐帧递减。</summary>
        public float Timer;

        /// <summary>在 Battle.projectiles 列表中的索引，用于反向查找。</summary>
        public int ProjectileListIndex;

        /// <summary>是否已过期（Timer &lt;= 0）。Battle.cs 据此执行冲击。</summary>
        public bool Expired;
    }

    /// <summary>
    /// 投射物过期标签组件（零字段 IComponentData）。
    /// ProjectileTimerSystem 在 Timer 归零时添加此标签，
    /// BattleEcsRuntime.GetExpiredProjectileIndices() 通过查询此标签收集过期投射物。
    /// 标签实体会在当帧被销毁，因此生命周期极短（&lt; 1 帧）。
    /// </summary>
    public struct ProjectileExpiredTag : IComponentData
    {
    }

    // ═══════════════════════════════════════════════════════════════
    // System 定义
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 占位 System —— 当前不做任何实际工作。
    ///
    /// 作用：
    ///   1. 验证 ECS World 初始化/更新/释放流程是否通畅
    ///   2. 通过 RequireForUpdate 确保 BattleEcsFrameState 实体存在后 World 才会真正 Update
    ///   3. 为后续 System 提供注册模板
    /// </summary>
    public partial class BattleFrameStateSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<BattleEcsFrameState>();
        }

        protected override void OnUpdate()
        {
        }
    }

    /// <summary>
    /// 投射物计时器 System（Phase 1 学习迁移核心）。
    ///
    /// 职责：每帧对所有飞行中的投射物递减 Timer，在 Timer 归零时打上过期标签。
    /// 不负责冲击逻辑（伤害/治疗/溅射/回调）—— 这些由 Battle.cs 在查询过期标签后执行。
    ///
    /// ECS 学习要点：
    ///   - SystemAPI.GetSingleton&lt;T&gt;()  读取全局单例数据
    ///   - SystemAPI.Query + foreach     遍历所有匹配实体（现代用法，替代弃用的 Entities.ForEach）
    ///   - EntityCommandBuffer           批量提交结构性变更（添加/销毁实体）
    ///   - RequireForUpdate&lt;T&gt;()        声明前置依赖，无数据时自动跳过
    ///
    /// 注意：当前使用主线程 foreach 执行，未启用 Burst 编译。
    ///       改为 IJobEntity 即可启用 Burst + Job 并行。
    /// </summary>
    public partial class ProjectileTimerSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<BattleEcsFrameState>();
        }

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.GetSingleton<BattleEcsFrameState>().DeltaTime;

            // ECB 用于在本帧 OnUpdate 结束后统一提交实体变更。
            // Allocator.Temp 表示此 ECB 的生命周期不超过当前帧，性能最优。
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // SystemAPI.Query 遍历所有携带 ProjectileTimerData 的实体（现代用法，替代弃用的 Entities.ForEach）。
            foreach (var (timer, entity) in SystemAPI.Query<RefRW<ProjectileTimerData>>().WithEntityAccess())
            {
                timer.ValueRW.Timer -= deltaTime;
                if (timer.ValueRW.Timer <= 0f)
                {
                    timer.ValueRW.Expired = true;
                    ecb.AddComponent<ProjectileExpiredTag>(entity);
                }
            }

            ecb.Playback(EntityManager);
        }
    }
}
