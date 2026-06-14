# AGENTS.md — Clash of Clans Clone 全局上下文

> 自动加载的架构约定与禁止事项。写任何代码前先读这里。

---

## 项目概况

| 项目 | 详情 |
|------|------|
| **引擎** | Unity 2022.3 (TuanJie Android) |
| **语言** | C# (.NET Standard 2.1) |
| **命名空间** | `DevelopersHub.ClashOfWhatecer` |
| **架构** | 客户端-服务器 TCP 长连接 + 帧同步战斗 |
| **网络层** | `DevelopersHub.RealtimeNetworking` (TCP 自实现) |
| **战斗帧率** | 20 ticks/s（每帧 50ms），与渲染帧（60fps）解耦 |
| **格子尺寸** | 45×45 战斗网格（内部 53×53 含偏移） |

---

## 核心架构分层

```
┌─────────────────────────────────────────────┐
│  UI 层 (70+ UI_*.cs MonoBehaviour)          │
├─────────────────────────────────────────────┤
│  表现层 (BattleUnit.cs, Building.cs, VFX)    │
├─────────────────────────────────────────────┤
│  逻辑层 (Battle.cs 纯C#, AStarPathfinding/)  │  ← 不依赖 UnityEngine
├─────────────────────────────────────────────┤
│  数据层 (Data.cs, DataModels, DataEnums)     │
├─────────────────────────────────────────────┤
│  网络层 (Player.cs 单例, RealtimeNetworking) │
└─────────────────────────────────────────────┘
```

---

## 关键文件速查

| 文件 | 角色 | 注意事项 |
|------|------|----------|
| `Battle/Battle.cs` | 战斗模拟核心（partial class，已拆5文件） | **纯C#**，禁止引用 UnityEngine |
| `Battle/Battle.NestedTypes.cs` | 所有嵌套类型（Unit, Building, Spell, Path 等） | ~23KB |
| `Battle/Battle.Units.cs` | 单位+法术逻辑 | ~40KB |
| `Battle/Battle.Buildings.cs` | 建筑逻辑 | ~11KB |
| `Battle/Battle.Pathfinding.cs` | 寻路+A*池+Burst调度 | ~33KB |
| `Player.cs` | 网络枢纽单例（53种请求类型） | 所有联机逻辑入口 |
| `Data.cs` | 全局常量+静态数据+序列化工具 | 修改需谨慎，影响全局 |
| `DataEnums.cs` | 所有枚举（BuildingID 37/UnitID 27/SpellID 11） | |
| `DataModels.cs` | 数据传输对象（Player, Building, Clan, BattleFrame） | 序列化友好 |
| `GameRules.cs` | 游戏规则计算（奖杯/战利品/经验） | |
| `BuildGrid.cs` | 等距格子管理 | |
| `CameraController.cs` | 正交摄像机（捏合缩放+拖拽） | 使用 Input System |
| `AssetsBank.cs` | 图标/预制体静态引用库 | Inspector 拖拽赋值 |

---

## 架构约定

### ✅ DO — 必须遵守

1. **Battle.cs 保持纯C#** — 不引用 `UnityEngine`（除自定义结构如 `BattleVector2`），确保可脱离 Unity 测试
2. **热路径零分配** — `ExecuteFrame()` / `HandleUnits` 内禁止 `new` 集合、LINQ、装箱
3. **Burst 兼容数据** — 热路径数据结构用 `NativeArray<T>` / `NativeList<T>`，不用托管对象
4. **UI 命名前缀** — UI 脚本一律 `UI_` 前缀，放在 `Scripts/UI/` 下
5. **partial class 拆分** — 大文件用 `partial` 按职能拆分（参考 Battle 的 5 文件模式）
6. **网络请求通过 Player.cs** — 所有 TCP 请求经过 `Player.cs` 单例，不直接调 `Sender`
7. **数据与逻辑分离** — DTO 放 `DataModels.cs`，业务规则放 `GameRules.cs`，常量放 `Data.cs`
8. **回调委托驱动** — 逻辑层→表现层用 `delegate` 回调（如 `AttackCallback`, `FloatCallback`），不直接调 UI

### ❌ DON'T — 绝对禁止

1. **禁止新增 MonoBehaviour Update() 循环** — 现有 3 个已是性能瓶颈，用事件驱动替代
2. **禁止在热路径使用 LINQ** — `GetAllTargets()` 等每帧调用方法中 LINQ 会造成 GC 压力
3. **禁止在战斗帧内分配临时集合** — 用 `sortedTargetBuffer` 等复用 buffer 模式
4. **禁止在 Battle.cs 引用 Unity API** — 保持逻辑层与引擎解耦
5. **禁止绕过 Player.cs 直接发包** — 破坏网络层单一入口
6. **禁止修改 Data.cs 的枚举值顺序** — 与服务器协议对应，顺序即 ID

---

## 性能红线

| 指标 | 红线值 | 当前状态 |
|------|--------|----------|
| `Battle.ExecuteFrame` 单帧耗时 | < 10ms | ✅ ~4.5ms（预算型微批处理后） |
| GC.Alloc per frame | < 1KB | ✅ Burst 化后显著改善 |
| Battle 帧耗时 | < 50ms（20tick 保障） | ✅ 充裕 |
| A* 寻路 | O(1) grid reset | ✅ Epoch 方案已落地 |
| Job 等待 | 每帧 ≤ 3 次 Complete | ✅ 预算型微批处理削峰 |
| 重寻路峰值帧 | 无突兀尖峰 | ✅ 跨帧消峰生效 |

---

## 改造路线图（当前进度）

```
Phase 0  ✅ ProfilerMarker 细粒度采样
Phase A  ✅ Grid Epoch O(1) 重置
Phase B  ✅ Burst A* IJob + 托管A*移除
Phase C  ✅ Scratch Pool + 批量并行 Schedule
Phase 2  ✅ Jobs 多线程优化
Phase 3  ✅ ECS 最小骨架接入（BattleEcsComponents / BattleEcsRuntime 已创建）
Phase 3.5 ✅ **预算型微批处理** — ExecuteFrame 22ms→4.5ms，削平峰值卡顿
Phase 4  ⬜ 数据驱动配置系统（ScriptableObject 双源）
```

---

## 编码模式参考

### 复用 Buffer 模式（避免 GC）
```csharp
// ❌ 错误：每帧 new List
var targets = new List<int>();
foreach (var t in candidates) { ... }

// ✅ 正确：复用 Battle 级 buffer
_sortedTargetBuffer.Clear();
foreach (var t in candidates) { _sortedTargetBuffer.Add(...); }
```

### Burst Job 调度模式
```csharp
// Schedule 收集 → 统一 Flush → 读取结果
ScheduleJob(A); ScheduleJob(B); ScheduleJob(C);
FlushBurstRequests();  // 一次 Complete
ReadResult(A); ReadResult(B); ReadResult(C);
```

### 逻辑层→表现层回调模式
```csharp
// Battle.cs（逻辑层）定义委托
public delegate void FloatCallback(int id, float value);
public FloatCallback onUnitHealthChange;

// UI_Battle.cs（表现层）注册
battle.onUnitHealthChange += UpdateHealthBar;
```

---

## 目录结构约定

```
Assets/Scripts/
├── Battle/           ← 战斗核心（partial class 拆分）
├── AStarPathfinding/ ← 纯C# A*（无 Unity 依赖）
├── UI/               ← 所有 UI 面板（UI_ 前缀）
├── ECS/              ← ECS 组件与运行时（Phase 3+）
├── Config/           ← ScriptableObject 配置
├── Tools/            ← 通用工具类
├── Input/            ← Input System 映射
├── Language/         ← 多语言 RTL 支持
├── Editor/           ← Editor 扩展
└── *.cs              ← 根级核心脚本
```

---

## 相关文档索引

| 文档 | 内容 |
|------|------|
| `docs/architecture/项目分析报告.md` | 完整项目分析报告（推荐新人先读） |
| `docs/architecture/寻路系统设计.md` | 寻路系统深度分析 |
| `docs/changelog/2026-06-07_Jobs多线程优化.md` | Jobs 多线程优化记录 |
| `docs/changelog/2026-06-08_ECS引入.md` | ECS 引入记录 |
| `docs/changelog/2026-06-09_Burst改造阶段总结.md` | Burst 改造阶段总结 |
| `docs/changelog/2026-06-12_预算型微批处理实施.md` | **预算型微批处理**（22ms→4.5ms） |
