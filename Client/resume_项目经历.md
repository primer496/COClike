# 部落冲突克隆 — DOTS 性能优化
**Unity C# | Burst/Jobs/ECS | 独立完成**

基于开源 Unity 项目进行 DOTS 性能改造，战斗帧耗时从 ~22ms 降至 ~4.5ms（5倍提升）。

- 将 A* 寻路算法从托管堆改造为 BurstCompile IJob，NativeArray 替代 GC 对象，消除热路径 GC.Alloc
- 设计 Grid Epoch O(1) 延迟重置方案，纪元戳替代每帧 2401 格全量清零，A* 每次调用从 O(n²) 降为 O(1)
- 实施预算型微批处理寻路调度：每帧 8 单位 / 64 A* 槽上限 + 跨帧削峰填谷，消除峰值帧卡顿
- 单体 Battle.cs（40KB+）拆解为 5 个 partial class 文件，按核心/嵌套类型/单位/建筑/寻路职责分离
- 搭建 ECS 最小骨架（BattleEcsRuntime + IComponentData），ProjectileTimerSystem 已迁移，World 与确定性帧同步对齐
- 修复团结引擎 Cinemachine + Input System API 兼容，以及 UnityMCP 中断导致 Burst 包版本撕裂
- 建立 Profiler 驱动的优化闭环：ProfilerMarker 热点定位 → 方案设计 → 实现 → Profiler 验证，Scratch Pool / 去 LINQ / buffer 复用均由此流程产出
- AI 辅助 Burst/Jobs 编译错误诊断与热路径重构方案快速对比，缩短试错与迭代周期
