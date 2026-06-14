# 🏰 COClike — Clash of Clans Clone

> 使用 Unity + .NET + MySQL 从零构建的《部落冲突》风格实时策略游戏。包含完整的客户端-服务端 TCP 网络架构、帧同步战斗系统、以及经过 Burst/Jobs/ECS 深度优化的高性能核心。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-2022.3-black?logo=unity)](https://unity.com/)
[![.NET](https://img.shields.io/badge/.NET-6.0-blue?logo=dotnet)](https://dotnet.microsoft.com/)
[![MySQL](https://img.shields.io/badge/MySQL-8.0-orange?logo=mysql)](https://www.mysql.com/)

---

## 📸 项目概述

| 模块 | 技术栈 | 说明 |
|------|--------|------|
| **客户端 (Client/)** | Unity 2022.3, C#, DOTS/ECS | 3D 渲染、UI、战斗模拟 |
| **服务端 (Server/)** | .NET 6, TCP Socket | 账号管理、匹配、数据持久化 |
| **数据库 (Database/)** | MySQL / MariaDB | 玩家数据、部落、战斗记录 |

### 核心特性

- ⚔️ **帧同步战斗** — 20 ticks/s 确定性战斗模拟，45×45 网格战场
- 🏗️ **37 种建筑** / **27 种兵种** / **11 种法术** — 完整游戏内容
- 🚀 **深度性能优化** — Burst 编译器 + Jobs 多线程 + ECS 架构，战斗帧耗时从 22ms 降至 4.5ms
- 🌐 **实时 TCP 网络** — 自研 `RealtimeNetworking` 网络库
- 🔍 **A\* 寻路** — 纯 C# 实现，Epoch 标记 O(1) 网格重置
- 📱 **Android 适配** — 团结引擎 (TuanJie) 移动端支持

---

## 🗂️ 项目结构

```
├── Client/                          # Unity 客户端
│   ├── Assets/Scripts/
│   │   ├── Battle/                  # 战斗核心（partial class）
│   │   │   ├── Battle.cs            # 主模拟循环
│   │   │   ├── Battle.Units.cs      # 单位 + 战斗逻辑
│   │   │   ├── Battle.Buildings.cs  # 建筑逻辑
│   │   │   ├── Battle.Pathfinding.cs # A* 寻路 + Burst Jobs
│   │   │   └── Battle.NestedTypes.cs # 嵌套类型定义
│   │   ├── AStarPathfinding/        # 纯 C# A* 算法
│   │   ├── UI/                      # UI 脚本（UI_ 前缀）
│   │   ├── ECS/                     # DOTS/ECS 组件
│   │   ├── Config/                  # ScriptableObject 配置
│   │   └── Editor/                  # Editor 扩展工具
│   └── AGENTS.md                    # 架构文档 & 开发约定
├── Server/                          # .NET 服务端
│   ├── Program.cs                   # 入口 + 主循环
│   ├── Scripts/                     # 业务逻辑
│   └── Terminal.cs                  # 服务端 Tick 循环
├── Database/
│   └── database.sql                 # MySQL 数据库初始化脚本
├── docs/                            # 详细设计文档
├── LICENSE                          # MIT License
└── README.md
```

---

## 🚀 快速开始

### 环境要求

| 工具 | 版本 | 用途 |
|------|------|------|
| **Unity** | 2022.3 (TuanJie) | 客户端编辑器 |
| **.NET SDK** | 6.0+ | 服务端编译运行 |
| **MySQL / MariaDB** | 8.0+ (推荐 XAMPP) | 数据存储 |
| **Visual Studio / Rider** | 任意 | IDE |

### 1. 数据库配置

```bash
# 使用 XAMPP 启动 MySQL，然后导入数据库
mysql -u root -p < Database/database.sql
```

数据库名称：`clash_of_whatever`

### 2. 启动服务端

```bash
cd Server
dotnet run
```

### 3. 启动客户端

1. 使用 **Unity 2022.3 (TuanJie)** 打开 `Client/` 目录
2. 打开 `Assets/Scenes/MainScene.unity`
3. 点击 Play 运行

> 📹 [安装视频教程](https://www.youtube.com/watch?v=3ZGwn49kLgY)

---

## ⚡ 性能优化亮点

### 战斗帧优化历程

```
优化前:  ExecuteFrame ~22ms  ❌ 超出 50ms 预算
Phase 1: Burst A* + Grid Epoch O(1) → ~12ms
Phase 2: Jobs 多线程 → ~8ms  
Phase 3: ECS 架构迁移 → ~4.5ms  ✅
```

### 关键技术

| 优化项 | 技术 | 效果 |
|--------|------|------|
| 寻路网格重置 | Epoch 标记法 O(1) | 消除每帧全量清零 |
| A* 寻路 | Burst 编译器 + IJob | 计算移至工作线程 |
| 内存分配 | NativeArray + 预分配 Buffer | 零 GC Alloc |
| 主村 UI | 移除 ForceMeshUpdate 每帧调用 | 消除周期性卡顿 |
| 网络同步 | GZip → 异步解压 | 消除主线程阻塞 |

详见 [`OPTIMIZATION_SUMMARY.md`](OPTIMIZATION_SUMMARY.md) 和 [`docs/`](docs/)

---

## 🏛️ 架构设计

```
┌─────────────────────────────────────────┐
│  UI 层 (70+ UI_*.cs MonoBehaviour)      │
├─────────────────────────────────────────┤
│  表现层 (BattleUnit.cs, Building, VFX)  │
├─────────────────────────────────────────┤
│  逻辑层 (Battle.cs 纯C#, AStarPath)     │ ← 不依赖 UnityEngine
├─────────────────────────────────────────┤
│  数据层 (Data.cs, DataModels, Enums)    │
├─────────────────────────────────────────┤
│  网络层 (Player.cs, RealtimeNetworking) │
└─────────────────────────────────────────┘
```

### 核心约定

- **Battle.cs 禁止引用 UnityEngine** — 使用自定义 `BattleVector2` 等纯 C# 结构
- **热点路径零分配** — 预分配 buffer，禁止 LINQ
- **委托回调解耦** — 逻辑层通过 `delegate` 通知 UI，不直接依赖
- **partial class 拆分** — 按职责拆分大文件

详见 [`Client/AGENTS.md`](Client/AGENTS.md)

---

## 📄 许可证

本项目基于 [MIT License](LICENSE) 开源。

---

## 🙏 致谢

- [Unity](https://unity.com/) — 游戏引擎
- [Developers Hub](https://github.com/DevelopersHub) — 原始网络框架
- 本项目仅用于学习和研究目的
