# Clash of Clans Clone 排障总结

## 1. 这次排障解决了什么

这次主要走通了以下几类问题：

- Android 打包后 APK 能安装但打不开
- Android 安装时提示无图标风险，桌面没有正常入口
- 手机无法连接本机服务器
- 多个 Unity 编辑器实例只能有一个连上服务器
- 重新进入游戏后账号像是新账号，数据不持久
- 训兵进度条一直重置，兵数量不增加
- 新账号缺少兵营和营地，导致训练链路不完整
- 多端联机和匹配逻辑没有按预期工作

最终状态：

- 主工程和 ParrelSync 克隆工程都能独立登录
- Android APK 能正常安装和打开
- 手机能连接本机服务器
- 建筑升级和训练时间已压缩到 5 秒便于测试
- 训兵和法术酿造逻辑恢复正常

## 2. 关键结论

### 2.1 手机连本机服务器时，127.0.0.1 一定是错的

客户端原本默认连接 127.0.0.1。这个地址在手机上表示手机自己，不表示电脑。

结论：

- 编辑器本机测试可以使用 127.0.0.1
- 手机必须填写电脑当前局域网 IP
- 本次排障中实际验证过的电脑局域网 IP 包括 192.168.233.38 和 192.168.143.46
- 端口统一为 5555

对应代码位置：

- Client/Assets/Scripts/StartLoading.cs
- Client/Assets/DevelopersHub/RealtimeNetworking/Scripts/Client.cs
- Client/Assets/DevelopersHub/RealtimeNetworking/Scripts/Settings.cs

### 2.2 多编辑器联机失败，不是网络问题，而是身份冲突

多个 Unity 编辑器实例使用了相同的 deviceUniqueIdentifier，服务端把它们当成同一个客户端。

解决方式：

- 在编辑器环境下，为设备 ID 增加基于 Application.dataPath 的后缀
- 同时把用户名和密码对应的 PlayerPrefs key 也做编辑器隔离

对应代码位置：

- Client/Assets/Scripts/Player.cs

### 2.3 账号反复变新号，本质是服务端认证逻辑有问题

问题根因不是客户端没存档，而是服务端认证时按 password + device_id 做匹配，并且在不匹配时错误清空了 device_id，导致账号无法重新绑定到原设备，进而不断创建新账号。

解决方式：

- 认证改为仅使用 device_id 查找账号
- 移除会清空 device_id 的错误更新逻辑

对应代码位置：

- Server/Scripts/Database.cs

### 2.4 训兵一直读条但不产兵，根因是 MySQL 8 的 ONLY_FULL_GROUP_BY

这次最核心的服务端问题是训练 SQL 在 MySQL 8 上被拒绝执行，但错误被静默吞掉了。

具体表现：

- 客户端本地 UI 看到训练条走满
- 下一次同步后进度条又回到 0
- 兵不会进入营地
- 同样的问题也影响法术酿造

根因：

- 训练更新语句里使用了 GROUP BY account_id
- 但 SELECT 中同时读取了非聚合字段，例如 units.id 和 server_units.housing
- MySQL 8 默认开启 ONLY_FULL_GROUP_BY，会拒绝这种写法
- 服务端的重试包装没有把异常暴露出来，所以表面上像是逻辑正常执行了，实际上 SQL 根本没生效

解决方式：

- 对这些 GROUP BY 查询中的非聚合列使用 ANY_VALUE()
- 同样修复了法术酿造相关查询

对应代码位置：

- Server/Scripts/Database.cs

### 2.5 Android 打包能装但打不开，根因是自定义 Manifest 干扰了 Unity 默认入口

这次 Android 问题最容易误判。中途看到过图标异常、Launcher 信息异常、自定义 Activity 和 theme 问题，但最终根因不是图标本身，而是自定义 AndroidManifest 干扰了 Unity 默认生成的启动配置。

最终稳定方案：

- 删除自定义 AndroidManifest
- 删除其对应的 meta 文件
- 让 Unity 使用默认生成的 Android Manifest

结果：

- APK 恢复了可启动入口
- App 可以正常打开

额外说明：

- 旧式 Assets/Plugins/Android/res 目录在当前 Unity 版本中已经废弃
- 原来用于 network_security_config 的 res 目录会导致构建失败
- 对当前项目这种自定义 TCP 通信来说，usesCleartextTraffic 和 networkSecurityConfig 不是必须项

## 3. 本次实际做过的代码和配置修复

### 3.1 客户端

1. 给启动场景增加服务器 IP 和端口输入面板
2. 把用户输入的服务器地址保存到 PlayerPrefs
3. 增加 Settings 的 SetIP 和 SetPort 接口
4. 给编辑器实例生成独立 device id
5. 给编辑器实例隔离 PlayerPrefs 中的用户名和密码 key

主要文件：

- Client/Assets/Scripts/StartLoading.cs
- Client/Assets/DevelopersHub/RealtimeNetworking/Scripts/Settings.cs
- Client/Assets/Scripts/Player.cs

### 3.2 服务端

1. 修复认证逻辑，避免设备绑定被错误清空
2. 打开在线目标匹配开关
3. 修复单位训练 SQL
4. 修复法术酿造 SQL
5. 为新账号初始化兵营和营地

主要文件：

- Server/Scripts/Database.cs
- Server/Terminal.cs

### 3.3 数据库和测试配置

为了加快联调速度，本次还做了测试用配置调整：

- 建筑建造时间和升级时间改为 5 秒
- 训练时间改为 5 秒
- 为已有测试账号补齐兵营和营地
- 清理无效孤儿账号

注意：这些属于测试环境配置，不一定适合正式环境。

## 4. Android 问题的排查经验

### 4.1 先验 APK，不要先猜 Unity 设置

本次一个很重要的经验是：

- 不要只看 Unity 面板里显示了什么
- 要直接检查最终 APK 里有什么

实用命令：

```powershell
$apk="D:\path\to\app.apk"
$aapt=Get-ChildItem "D:\TuanJie\2022.3.62t7\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools" -Recurse -Filter aapt.exe | Sort-Object FullName -Descending | Select-Object -First 1
& $aapt.FullName dump badging $apk
```

重点看：

- package
- application-label
- application-icon
- launchable-activity

如果没有 launchable-activity，安装后打不开是必然的。

### 4.2 入口 Activity 恢复后，再判断是不是图标问题

这次中间一度表现为：

- 安装有风险提示
- 图标不正常
- 桌面没有正确入口

但最终事实是：

- 图标问题会影响体验
- 没有 launchable-activity 才会直接导致无法打开

所以排查顺序应该是：

1. 先看有没有 launchable-activity
2. 再看 application-icon
3. 最后再讨论 Adaptive Icon 是否完美

### 4.3 Unity Android 上能不用自定义 Manifest 就尽量不用

如果项目只是做基础联网，通常不需要自己写 AndroidManifest。

经验结论：

- 优先让 Unity 生成默认 Manifest
- 只有真的需要自定义权限、组件、intent-filter 时再加
- 一旦加了自定义 Manifest，优先检查是不是覆盖了 Unity 默认入口 Activity 或 application 配置

## 5. 手机无法连接服务器时的标准排查顺序

### 第一步：确认客户端连接的是不是电脑局域网 IP

手机不能使用 127.0.0.1。

### 第二步：确认服务端是否真的监听了对外地址

本次确认结果：

- TCP 监听在 0.0.0.0:5555
- UDP 监听在 0.0.0.0:5555

可以用这类命令检查：

```powershell
Get-NetTCPConnection -State Listen -LocalPort 5555
Get-NetUDPEndpoint -LocalPort 5555
```

### 第三步：确认 Windows 防火墙是否放行 5555

本次确认结果：

- TCP 5555 入站规则已开启
- UDP 5555 入站规则已开启
- Profile 为 Any

### 第四步：确认手机和电脑是否真在同一个局域网

不要只看“都能上网”。需要保证它们在同一个局域网段里，或者手机热点和电脑之间确实能互通。

### 第五步：必要时抓 ADB 日志

如果 App 打开了但连接失败，下一步应抓手机日志，而不是继续猜。

## 6. 服务端问题的排查经验

### 6.1 表面像客户端问题时，也要怀疑数据库语句是否根本没执行

训兵一直转圈这类问题，很容易先盯着 UI 看。

但这次真正的根因在服务端 SQL：

- 客户端只是按照本地时间更新了进度条
- 真正决定训练是否完成的是服务端数据库状态
- 只要服务端 SQL 没成功，客户端 UI 再正常也没用

### 6.2 MySQL 8 默认行为和旧 SQL 兼容性要单独验证

这次 ONLY_FULL_GROUP_BY 就是典型案例。

经验结论：

- 只要项目是老 SQL 逻辑迁到 MySQL 8，优先检查 sql_mode
- 只要有 GROUP BY + 非聚合字段，就要怀疑兼容问题
- 如果异常被吞掉，要先想办法验证 SQL 是否真的执行成功

## 7. 对客户端开发者最有用的几条经验

### 7.1 先验证最终产物，不要只看编辑器状态

比如 Android 问题，最终应以 APK 实际内容为准，而不是以 Unity Inspector 是否看起来正常为准。

### 7.2 联机问题要拆成三层看

建议按下面三层排查：

1. 客户端是不是连到了正确地址
2. 服务器是不是在正确地址和端口监听
3. 数据库和服务端逻辑是不是正确处理了连接后的状态

### 7.3 看到“像 UI bug”的问题时，别忘了看服务端状态

多人联机项目里，很多看似 UI 或客户端同步的问题，本质是：

- 服务端没发对
- 服务端根本没更新
- 数据库存储状态错了

### 7.4 排查时尽量保存可复用结论

这次最终得到的高价值结论包括：

- Android 手机连接本机服务必须使用局域网 IP
- Unity 默认 Android Manifest 比自定义 Manifest 稳定
- 多编辑器实例必须使用独立设备标识
- MySQL 8 下要特别注意 ONLY_FULL_GROUP_BY

## 8. 当前项目里最值得记住的文件

- Client/Assets/Scripts/StartLoading.cs
- Client/Assets/Scripts/Player.cs
- Client/Assets/DevelopersHub/RealtimeNetworking/Scripts/Settings.cs
- Client/Assets/DevelopersHub/RealtimeNetworking/Scripts/Client.cs
- Server/Scripts/Database.cs
- Server/Terminal.cs

## 9. 以后再遇到类似问题，建议按这个顺序处理

1. 先确认现象属于打包问题、联网问题、认证问题还是服务端逻辑问题
2. 如果是 Android，先验 APK 的 launchable-activity 和图标信息
3. 如果是手机连不上，先确认局域网 IP、监听端口、防火墙
4. 如果是多实例互斥，先查设备标识和本地缓存 key
5. 如果是数据不对，优先检查服务端数据库语句是否真的成功执行
6. 如果是训练、酿造、收集这类定时系统异常，优先检查服务端定时更新逻辑和 SQL 兼容性

## 10. 一句话总结

这次并不是单一 bug，而是一组客户端、服务端、数据库、Unity Android 构建配置共同叠加的问题。真正有效的排查方法不是一直盯着某一层，而是把最终产物、网络链路、服务端状态和数据库执行结果一起验证。