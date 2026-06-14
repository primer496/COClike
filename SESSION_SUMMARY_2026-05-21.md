# Clash of Clans Clone 本轮联调与性能分析完整经验总结

## 1. 本轮目标与最终结果

本轮会话的目标主要有两条：

1. 让本地服务端正常启动，并让 Android 真机可以连接到服务端
2. 让 Unity Profiler 成功连接 OPPO K12x 5G，并建立后续性能分析方法

最终状态：

- 服务端已经可以正常启动
- MySQL 连接池导致的重启异常已经修复
- Android APK 与服务器通信链路已经打通
- OPPO K12x 5G 已通过无线 ADB 成功连接电脑
- Unity Profiler 已成功连接真机
- 已明确当前项目的性能分析重点、采样方法和 DOTS 切入方向

## 2. 本轮关键问题与结论

### 2.1 服务端最初不能稳定跑起来，核心是运行时和数据库初始化问题

本轮首先遇到的是服务端运行与重启问题，最终确认有两个核心点：

- 服务端目标框架需要切到 net8.0，避免因缺少 .NET 7 运行时导致启动失败
- MySQL 连接池在重启过程中触发连接池键冲突，导致数据库初始化异常

最终采用的稳定方案：

- 服务端使用 net8.0 运行
- MySQL 连接字符串中的 POOLING 改为 FALSE
- 初始化前主动调用 MySqlConnection.ClearAllPools()

相关位置：

- Server/Scripts/Database.cs
- Server/bin/Release/net8.0/Clash Dark Server.dll

### 2.2 Android 客户端连接不到服务器时，必须分清本机、WiFi、USB 共享三种地址

本轮实际验证中出现过多种 IP：

- 本机回环：127.0.0.1
- 电脑 WiFi IP：10.145.150.236
- USB 网络共享电脑 IP：192.168.155.168
- USB 网络共享手机 IP：192.168.155.32

经验结论：

- 编辑器本机连服务端时可以使用 127.0.0.1
- Android 真机不能用 127.0.0.1，它会指向手机自己
- 真机应连接电脑当前实际可达的局域网地址
- 如果使用 USB 网络共享，客户端应连 192.168.155.168:5555
- 如果切换网络环境，客户端内保存的服务器地址也要同步修改并重新打包验证

相关配置位置：

- Client/Assets/DevelopersHub/RealtimeNetworking/Resources/Settings.asset

### 2.3 Android 9+ 的明文流量限制会直接导致客户端连不上非 HTTPS 服务

本轮联调过程中，APK 与本地 TCP 服务端通信时还遇到 Android 明文流量限制。

实际处理方式：

- 增加 AndroidManifest 配置，允许 cleartext traffic
- 增加 network_security_config，允许明文网络访问

这一步的意义不是为了 Unity 编辑器，而是为了 Android 真机上的非 HTTPS 通信链路。

相关文件：

- Client/Assets/Plugins/Android/AndroidManifest.xml
- Client/Assets/Plugins/Android/res/xml/network_security_config.xml

### 2.4 Windows 能识别手机，不代表 ADB 一定能用 USB 连上

这是本轮最容易误判的问题。

现场现象：

- 设备管理器中可以看到 OPPO K12x 5G
- 设备管理器中也能看到 ADB Interface
- 但是 adb devices 一直是空列表

这说明：

- Windows 驱动层面已经识别 USB 设备
- 但 ADB USB 通道没有真正建立起可用链路
- 不能因为看到 ADB Interface 就认定授权已经完成

期间确认过的事实：

- 手机开发者选项中的 USB 调试已经开启
- USB 线具备数据传输能力
- 手机也能切换到文件传输模式
- 问题并不在普通用户最常见的“线坏了”这一层

### 2.5 ADB 默认端口 5037 被 VS Code 占用时，会直接导致 ADB 协议异常

本轮发现一个非常关键但不常见的问题：

- VS Code 的 Code.exe 占用了 5037
- ADB 默认也使用 5037 作为 server 端口
- 导致 adb 无法正常工作，并出现 protocol fault HTTP 一类异常

最终处理方式：

- 持久化设置环境变量 ANDROID_ADB_SERVER_PORT=5039
- 后续所有 adb server 都运行在 5039

经验结论：

- 如果 adb 行为异常，不要只盯手机和驱动
- 先确认 5037 是否被其它进程占用
- 更换端口后要保证当前终端和后续工具都使用同一端口

### 2.6 本机 USB ADB 长时间无结果时，无线 ADB 是更高效的替代方案

在 USB ADB 长时间无法建立有效设备列表之后，最终切换到了无线 ADB。

最终验证成功的链路：

- 手机开启无线调试
- 使用校园网环境完成配对与连接
- 成功执行 adb pair 与 adb connect
- adb devices 最终出现 10.141.43.83:41899 device

这说明：

- 当前校园网环境至少在这次连接中允许电脑访问手机无线调试端口
- 无线 ADB 在本项目下可作为 USB ADB 的正式替代方案

本次成功命令模式：

```powershell
adb pair 10.141.43.83:41047
adb connect 10.141.43.83:4189910
adb devices
```

成功标志：

```text
10.141.43.83:41899    device
```

### 2.7 无线 ADB 的连接建立后，Unity Profiler 可以正常工作，但稳定性弱于 USB

无线 ADB 打通后，Unity Profiler 已成功连接真机。

成功标志：

- Profiler 底部显示已连接 AndroidPlayer
- 目标设备显示为 OPPO_PJT110@ADB:10.141.43.83:41899

但后续又出现过一次典型错误：

```text
Unrecognized block header in profiler data file, stopping deserialization
```

这通常不是业务逻辑问题，而是采样流中断或无线传输不稳定导致的 Profiler 数据包损坏。

结论：

- 无线 ADB 可以用来做开发阶段的性能定位
- 但做长时间连续录制时，稳定性不如 USB
- 采样时要尽量只开必要模块，并且优先做短时间、场景化采样

## 3. 本轮实际修改与配置记录

### 3.1 服务端代码与配置

已确认的关键处理：

- 服务端目标框架改为 net8.0
- 数据库初始化前调用 MySqlConnection.ClearAllPools()
- 连接字符串中设置 POOLING=FALSE
- 服务端日志增加客户端连接与断开的 IP 输出

涉及文件：

- Server/Scripts/Database.cs
- Server/Terminal.cs

### 3.2 客户端 Android 网络通信配置

已确认的关键处理：

- 客户端服务器地址保存在 Settings.asset 中
- Android 侧已补充明文通信所需配置

涉及文件：

- Client/Assets/DevelopersHub/RealtimeNetworking/Resources/Settings.asset
- Client/Assets/Plugins/Android/AndroidManifest.xml
- Client/Assets/Plugins/Android/res/xml/network_security_config.xml

### 3.3 启动方式与常用入口

为简化启动流程，本轮建立并验证了服务端直接启动方式：

- 项目根目录中的 启动服务器.bat

其用途是直接启动：

```text
Server/bin/Release/net8.0/Clash Dark Server.dll
```

## 4. ADB 与真机联调经验

### 4.1 不要把“设备管理器看得到手机”误当成“ADB 已经正常”

设备管理器中的以下信息只能说明 USB 枚举成功：

- OPPO K12x 5G
- ADB Interface

它不能保证以下事情已经成立：

- ADB server 能成功扫描到设备
- 手机已完成 RSA 授权
- adb devices 会列出可用设备

正确判断标准只有一个：

```text
adb devices
```

结果中必须出现：

- unauthorized：说明设备被发现但未授权
- device：说明设备已可用

如果列表完全为空，就不能认为 ADB 已打通。

### 4.2 在 Windows CMD 与 PowerShell 中，不同语法不要混用

本轮中有一个小坑：

- 在 CMD 里执行了 PowerShell 环境变量语法 `$env:ANDROID_ADB_SERVER_PORT`

这当然会报路径或语法错误。

经验结论：

- PowerShell 用 `$env:变量名`
- CMD 用 `%变量名%`

排障时先明确自己当前用的是哪种终端。

### 4.3 当 USB ADB 难以打通时，优先切到无线 ADB，不要在驱动上无限消耗时间

本轮曾尝试检查 Google USB Driver，但由于本地 Android SDK 环境来自 DevEco Studio 路径，驱动内容和当前设备匹配并不稳定。

最终证明，在当前环境下继续纠结 USB 驱动的收益不高。

更高效的判断方式是：

- 如果你已经确认手机支持无线调试
- 手机和电脑之间存在可达网络
- 又急需走通 Profiler 或 ADB 命令链路

那么直接切换无线 ADB 往往更快。

## 5. Unity Profiler 的正确使用经验

### 5.1 已连上 Profiler 不等于已经开始有效采样

本轮一开始出现过“Profiler 已连接但没有数据”的情况。

根因通常有以下几类：

- 还没有真正开始录制
- 游戏不在前台
- 安装包不是 Development Build
- Profiler 已连接，但当前显示目标切回了 Main Editor Process

正确检查顺序：

1. 确认 Build Settings 中已勾选开发构建和自动连接探查器
2. 确认手机上的游戏在前台运行
3. 确认 Profiler 当前目标是 AndroidPlayer 而不是 Main Editor Process
4. 确认录制按钮处于开启状态

### 5.2 Profiler 图上的高峰值和稳定平坦段都重要，但关注点不同

在本项目中，分析方法应该区分两类帧：

#### 峰值帧

作用：

- 定位卡顿来源
- 定位 GC、批量实例化、资源加载、异常 UI 重建

适合回答的问题：

- 为什么玩家会突然感觉卡一下
- 哪一个行为触发了突发性重负载

#### 平坦稳态帧

作用：

- 评估正常运行时的固定帧开销
- 判断系统长期性能基线

适合回答的问题：

- 当前系统日常运行到底贵不贵
- 优化后整体 CPU 占用是否真的下降

经验结论：

- 先看峰值，解决明显卡顿
- 再看平坦段，建立稳态性能基线

### 5.3 对当前截图的实际结论：CPU 不是第一瓶颈，更多是在等帧同步

当前已观察到的典型信息：

- PlayerLoop 约 31.77ms
- WaitForLastPresentationAndUpdateTime 约 26.04ms
- WaitForVsync 约 26.00ms

这意味着：

- 当前主线程大部分时间在等待显示或帧同步
- 游戏更像是被 30FPS 限制住，而不是 CPU 算不过来
- 当前 CPU 侧并没有暴露出持续性重负载

因此不能看到一条 30FPS 线就直接断言“脚本太慢”。

### 5.4 做原始数据记录时，要按场景记录，而不是随便截一张图

对这个项目，建议建立如下基准场景：

1. 主村庄静置 5 秒
2. 点击建筑并弹出 UI
3. 拖动或放置建筑
4. 收集资源
5. 进入战斗并大量放兵

每一种场景都应记录：

- 一帧代表性的平坦帧
- 一帧代表性的峰值帧
- 当时的模块勾选状态
- 是否为无线 ADB
- 手机是否清后台、插电、关闭省电模式

这样后续做优化前后对比才有可重复性。

### 5.5 手机上的最佳调试状态

为了减少非项目因素干扰，建议真机进入如下调试状态：

- 清掉最近任务中的后台应用
- 关闭省电模式
- 保持插电，避免温控或低电量策略影响频率
- 尽量避免通知弹窗干扰
- 保持游戏在前台，屏幕常亮
- 只打开当前需要的 Profiler 模块

无线 ADB 场景下还应注意：

- 避免长时间连续录制
- 优先抓短片段或单帧
- 如果出现反序列化错误，先减少模块数量再重试

## 6. 针对这个项目的性能分析重点

### 6.1 先盯 CPU Usage，不要一上来就泛泛而谈“优化”

本项目当前更适合从 CPU Usage 入手，因为代码结构明显还是传统 MonoBehaviour 架构。

已直接观察到的潜在重点点位：

- Building.Update()
- Building.AdjustUI()
- Building.LookAt()
- 与 UI_Bar、UI_Button 相关的动态 UI 更新链路

其中一个明确信号是：

- Building 的 Update 中每帧都会调用 AdjustUI()

这类模式在建筑数量变多时很容易成为稳态 CPU 开销来源。

### 6.2 UI 优化的重点不是“让界面少一点”，而是减少动态重建

如果你的目标之一是优化 UI，那么 Profiler 中重点关注：

- UI 模块
- Canvas.SendWillRenderCanvases
- 动态血条、建造条、按钮状态更新

这类项目最常见问题不是单个 UI 元素复杂，而是：

- 过多元素在同一 Canvas 下
- 少量动态变化导致整块 Canvas 重建
- 每帧更新文本、进度条、激活状态

经验方向：

- 静态 UI 与动态 UI 分 Canvas
- 把“每帧刷”改成“状态变化时刷”
- 先减少 UI 脏标记，再谈视觉层细节优化

### 6.3 DOTS 的最佳切入点不是 UI，而是大量实体并行行为

如果你的主要目标之一是使用 DOTS 做优化，那么本项目最合适的切入点不是 UI。

更适合 DOTS 的部分是：

- 士兵单位移动
- 炮弹飞行
- 目标查找
- 大量单位的距离判断和攻击判定
- 大规模战斗中的并行运算

当前代码里有一个明显信号：

- Client/Assets/Scripts/Unit.cs 当前几乎是空壳

这意味着如果要引入 DOTS，Unit 相关系统反而是最干净的切入口。

建议的实施顺序：

1. 先保留现有 UI 和建筑系统，不要一开始就大拆
2. 先把大量单位逻辑抽成可独立验证的战斗子系统
3. 从移动、目标筛选、投射物模拟中挑一个最热点场景做 ECS 原型
4. 用 Profiler 对比 MonoBehaviour 版本与 DOTS 原型的 CPU 时间和 GC Alloc

## 7. 推荐的后续工作顺序

### 7.1 建立性能基线

先在当前版本下，按固定场景采样并留档：

- 主村庄静置
- 打开建筑 UI
- 建筑拖拽
- 战斗放兵

每个场景至少保留：

- 一张稳态帧截图
- 一张峰值帧截图
- 一段关键函数展开视图

### 7.2 先做最便宜的 UI 优化

优先检查：

- 是否存在每帧刷新 UI 的逻辑
- 是否能改为事件驱动刷新
- 是否需要拆分 Canvas

因为这类优化改动范围小、收益稳定、风险低。

### 7.3 再做 DOTS 原型验证

不要一开始就全项目 ECS 化。

更合理的路径是：

- 选战斗中单位数量多的局部逻辑
- 用最小范围做 DOTS 试点
- 用同一台手机、同一套场景、同一采样方法做前后对比

只有这样，DOTS 优化结果才是可量化、可复现、可决策的。

## 8. 本轮会话中最值得保留的经验

1. ADB 出问题时，先确认端口冲突，再确认驱动，再决定是否切无线 ADB
2. 设备管理器识别手机，不等于 adb devices 一定能看到设备
3. 无线 ADB 对 Profiler 足够可用，但不适合长时间重负载连续采样
4. Profiler 分析不要只看一张图，要区分峰值帧与稳态帧
5. 当前项目初看不是 CPU 已打满，更像是先被 30FPS 和渲染同步限制住
6. UI 优化优先关注动态重建和每帧刷新
7. DOTS 优化应优先切入大量单位并行逻辑，而不是 UI
