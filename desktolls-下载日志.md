# desktolls 下载日志

记录时间：2026-07-20（Asia/Hong_Kong）

## 初始环境

检查时本机没有可用的 `dotnet`、`csc`、`msbuild`、`node`。最终实现只使用 .NET / WPF，没有下载 Node.js、Electron 或第三方应用框架。

## 下载记录

### 1. .NET 官方安装脚本

- 成功来源：`https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.ps1`
- 保存位置：`D:\DESK\.tools\dotnet-install.ps1`
- 文件大小：76,680 字节
- SHA-256：`6585899AED55FF6AE13DBE1E8C3B878F2D00433520E7EFBE250B75DB948B7DA9`

第一次尝试访问短地址 `https://dot.net/v1/dotnet-install.ps1` 时，当前网络无法解析 `dot.net`，因此没有下载到文件。随后改用上面的微软官方直链并成功下载。

系统 PowerShell 执行策略不允许直接运行脚本。没有修改永久执行策略，仅对安装脚本的单次子进程使用 `-ExecutionPolicy Bypass`。

### 2. .NET 8 SDK

- 版本：`.NET SDK 8.0.423`
- 运行时：`Microsoft.NETCore.App 8.0.29`、`Microsoft.WindowsDesktop.App 8.0.29`、`Microsoft.AspNetCore.App 8.0.29`
- 官方包：`https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.423/dotnet-sdk-8.0.423-win-x64.zip`
- 下载大小：285,072,593 字节
- 安装位置：`D:\DESK\.tools\dotnet`
- 安装后目录大小：748,426,619 字节（约 713.76 MB）
- 安装器完整输出：`D:\DESK\.tools\dotnet-install-output.log`

安装器核对远端和本地文件大小一致后完成解压，并删除了临时压缩包 `%TEMP%\rlyu5el5.f3f`。SDK 没有加入系统 `PATH`，也没有进行系统级安装。

### 3. 自包含发布所需 NuGet 包

来源：`https://api.nuget.org/v3/index.json`

以下目录是还原后的包缓存大小，包含解压文件和 NuGet 元数据：

| 包 | 版本 | 缓存大小 | 保存位置 |
| --- | --- | ---: | --- |
| `microsoft.aspnetcore.app.runtime.win-x64` | 8.0.29 | 38.88 MB | `D:\DESK\.tools\nuget-packages\microsoft.aspnetcore.app.runtime.win-x64` |
| `microsoft.net.illink.tasks` | 8.0.29 | 4.77 MB | `D:\DESK\.tools\nuget-packages\microsoft.net.illink.tasks` |
| `microsoft.netcore.app.runtime.win-x64` | 8.0.29 | 122.77 MB | `D:\DESK\.tools\nuget-packages\microsoft.netcore.app.runtime.win-x64` |
| `microsoft.windows.sdk.net.ref` | 10.0.19041.56 | 51.42 MB | `D:\DESK\.tools\nuget-packages\microsoft.windows.sdk.net.ref` |
| `microsoft.windowsdesktop.app.runtime.win-x64` | 8.0.29 | 124.52 MB | `D:\DESK\.tools\nuget-packages\microsoft.windowsdesktop.app.runtime.win-x64` |

发布命令输出保存在 `D:\DESK\.tools\dotnet-publish-output.log`。

## 构建结果

- 最终程序：`D:\DESK\desktolls.exe`
- 目标：Windows x64，自包含单文件
- 版本：`1.3.1`
- 文件大小与 SHA-256：见本文末尾“v1.3.1 文件名自动识别更新”
- 源码：`D:\DESK\src\desktolls`
- v1.0 发布暂存副本：`D:\DESK\.tools\publish-staging`
- v1.1 发布暂存副本：`D:\DESK\.tools\publish-v1.1`
- v1.2 发布暂存副本：`D:\DESK\.tools\publish-v1.2`
- v1.3 发布暂存副本：`D:\DESK\.tools\publish-v1.3`
- v1.3.1 发布暂存副本：`D:\DESK\.tools\publish-v1.3.1`
- 自检结果：`D:\DESK\desktolls-self-test.log`

构建工具和 NuGet 缓存只用于重新编译；`desktolls.exe` 运行时不读取这些目录。

## v1.1 内存优化更新

本次更新没有下载新的 SDK、NuGet 包、应用框架或可执行工具，继续使用上面已经保存在 `D:\DESK\.tools` 中的 .NET 8 构建环境。

为核对 PCL2 行为，在线读取了 `Meloong-Git/PCL`（原 `Hex-Dragon/PCL2`）官方仓库的 GitHub API、Raw 源码和公开 Issue。查询结果只在内存中分析，没有把 PCL 仓库、压缩包或二进制文件保存到本机。具体依据记录在 `D:\DESK\desktolls-内存优化说明.md`。

## v1.2 Windows Update 更新

本次更新没有下载新的 SDK、NuGet 包、第三方 EXE、服务或驱动，继续使用现有 .NET 8 构建环境。

在线查阅了微软 Windows Update 策略文档，以及以下 GitHub 仓库中的公开源码：

- `ChrisTitusTech/winutil`
- `WereDev/Wu10Man`
- `farag2/Sophia-Script-for-Windows`
- `Aetherinox/pause-windows-updates`
- `tsgrgo/windows-update-disabler`

这些查询只在内存中分析，没有把仓库、脚本或二进制文件保存到本机。对比结论与 desktolls 的安全边界记录在 `D:\DESK\desktolls-Windows更新说明.md`。

## v1.3 自定义文件下载更新

本次更新没有下载新的 SDK、NuGet 包、应用框架、浏览器组件、第三方 EXE、服务或驱动，继续使用 `D:\DESK\.tools` 中已有的 .NET 8 构建环境。

为核对 PCL2 的下载机制，下载了 PCL2 当前官方公开源码快照：

- 仓库：`https://github.com/Meloong-Git/PCL`
- 下载地址：`https://codeload.github.com/Meloong-Git/PCL/zip/refs/heads/main`
- 研究时 `main` 提交：`bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1`
- 压缩包：`D:\DESK\.tools\research\PCL-main.zip`
- 压缩包大小：11,350,410 字节
- 压缩包 SHA-256：`5B1905FBED22FDBC2182A53AFE94CE4D231AE6C4A15C96B50FE5C32A78512B21`
- 解压位置：`D:\DESK\.tools\research\PCL-main\PCL-main`
- 解压内容：280 个文件，15,156,072 字节（约 14.45 MB）
- 重点研究文件：`Plain Craft Launcher 2\Modules\Base\ModNet.vb`
- 该文件 SHA-256：`6085330946650F2888B43E254BA73B404AA616EB1A7F7879B309AEBF2156F5A7`

第一次查询旧仓库 `Hex-Dragon/PCL2` 时，GitHub 匿名 API 返回共享出口限流；随后两次 Git 远程查询遇到连接重置/超时。这些失败请求没有在本机生成仓库或压缩包。最终通过上面的 PCL2 当前官方仓库下载成功。

PCL2 百宝箱页面本身未包含在开源内容中；本次实现参考的是官方公开 `ModNet.vb` 中可验证的 HTTP Range 分段、失败重试、Range 不支持时回退单线程、临时片段合并与校验机制。详细证据和 desktolls 的实现边界记录在 `D:\DESK\desktolls-自定义下载说明.md`。

最终发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.3.0.0`
- 文件大小：187,168,171 字节（约 178.50 MB）
- SHA-256：`2B228BE3F30088206A9A3D9FA22F00AF67888C1D8A20E513FDDA2982AB916E4B`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.3`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.3-output.log`
- 自检：18 项全部通过，结果保存于 `D:\DESK\desktolls-self-test.log`

## v1.3.1 文件名自动识别更新

本次更新没有下载新的 SDK、NuGet 包、源码仓库、应用框架、第三方 EXE、服务或驱动，继续使用现有 .NET 8 构建环境和 NuGet 缓存。

新增内容：

- 默认根据服务器 `Content-Disposition` 自动填写完整文件名。
- 支持从重定向后的真实 URL 获取文件名。
- 使用 `Content-Type` 和前 512 字节文件头补充或纠正扩展名。
- 支持关闭“自动识别文件名”后手动命名。
- 正式下载会再次识别，不依赖界面预览是否已经完成。
- 只允许覆盖用户确认过的同一个目标路径，防止两次服务器响应名称变化时误覆盖其他文件。

最终发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.3.1.0`
- 文件大小：187,192,747 字节（约 178.52 MB）
- SHA-256：`7D914E6241E3779F3F7350BDAEA3E170AC8B69115801912A1AA315477C96CEE1`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.3.1`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.3.1-output.log`
- 自检：20 项全部通过，结果保存于 `D:\DESK\desktolls-self-test.log`

## GitHub 发布工具

为创建 GitHub 仓库和 Release，下载了 GitHub 官方便携版 CLI，没有进行系统级安装，也没有加入永久 `PATH`：

- 工具：GitHub CLI `2.96.0`（2026-07-02）
- 官方发布页：`https://github.com/cli/cli/releases/tag/v2.96.0`
- 下载地址：`https://github.com/cli/cli/releases/download/v2.96.0/gh_2.96.0_windows_amd64.zip`
- 压缩包：`D:\DESK\.tools\gh\gh_2.96.0_windows_amd64.zip`
- 压缩包大小：14,821,821 字节
- SHA-256：`C2D6ACC935CD2F00E2144D7E036D5CD82E6B6BD5594E8C75AA75EF2A4ED6AAC3`
- 解压位置：`D:\DESK\.tools\gh\2.96.0`
- 可执行文件：`D:\DESK\.tools\gh\2.96.0\bin\gh.exe`

## GitHub 公开发布结果

- GitHub 账号：`Hhhhhhhhh-ai`
- 公开仓库：`https://github.com/Hhhhhhhhh-ai/desktolls`
- 首次源码提交：`802e5edca43993fe13a30f1e9fae6d58863c462f`
- Release：`https://github.com/Hhhhhhhhh-ai/desktolls/releases/tag/v1.3.1`
- Release 附件：`desktolls-v1.3.1-win-x64.zip`
- 附件大小：71,966,303 字节（约 68.63 MB）
- 附件 SHA-256：`751DFBB38C0398473ACCB9EC4FAE8EE0C01348530230489E04B7516BF0590E5F`
- ZIP 内 `desktolls.exe` SHA-256：`7D914E6241E3779F3F7350BDAEA3E170AC8B69115801912A1AA315477C96CEE1`
- GitHub Actions：`https://github.com/Hhhhhhhhh-ai/desktolls/actions/runs/29649078665`，Windows 云端构建成功。

GitHub 登录使用官方 OAuth 设备授权，访问令牌保存在 Windows 凭据存储中，没有写入项目文件或下载日志。公开仓库不包含 `.tools`、PCL 源码快照、.NET SDK、NuGet 缓存、`bin`、`obj`、本地日志或根目录 EXE。

## v1.4.0 复制/粘贴提示音更新

本次更新没有下载新的 SDK、NuGet 包、声音素材、应用框架、第三方 EXE、服务或驱动，继续使用 `D:\DESK\.tools` 中已有的 .NET 8 构建环境和 NuGet 缓存。

两段提示音由 desktolls 在内存中自行生成，没有引用或分发第三方音频：

- 复制音：880 Hz 上升至 1175 Hz，总时长约 100 毫秒。
- 粘贴音：659 Hz 下降至 494 Hz，总时长约 120 毫秒。
- 格式：44.1 kHz、16 位、单声道 WAV，带短淡入淡出和约 15% 峰值振幅。

新增内容：

- 全局监听物理键盘的标准 `Ctrl+C` 与 `Ctrl+V`，不拦截按键，不读取剪贴板。
- 过滤模拟键盘输入、按键自动重复以及带 Shift、Alt 或 Windows 键的组合。
- 复制和粘贴提示音具有独立开关与试听按钮，设置可持久保存。
- 定时内存优化完成后会恢复键盘钩子；程序退出时完整释放钩子与音频资源。

最终本地发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.4.0.0`
- 文件大小：187,200,939 字节（约 178.53 MB）
- SHA-256：`B241FC07D6F652463597B0D342B8DE8A88E9ADF22E2AE23FF172FF39864C4061`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.4`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.4-output.log`
- 自检：25 项全部通过，结果保存于 `D:\DESK\desktolls-self-test.log`
- 交互验证：两个开关可独立保存并恢复，两个试听按钮均成功调用。

按本次要求，v1.4.0 只在本机生成和运行，没有提交、推送或创建新的 GitHub Release；公开仓库与 Release 仍保持 v1.3.1。

## v1.5.0 Steam 网页访问优化更新

本次更新没有下载新的 SDK、NuGet 包、代理组件、证书、驱动、服务、网络加速器或第三方 EXE，继续使用 `D:\DESK\.tools` 中已有的 .NET 8 构建环境和 NuGet 缓存。

为区分免费网页访问优化和游戏线路加速，在线只读研究了 Watt Toolkit 官方仓库，没有把仓库、源码、配置或二进制文件保存到本机：

- 官方仓库：`https://github.com/BeyondDimension/SteamTools`
- 研究分支：`develop`
- 研究提交：`c16ffa08e03b192d23ada290c4969e77f9201f3d`
- 项目许可证：GPL-3.0
- 核对内容：YARP 本地反向代理、hosts 回环映射、根证书、DoH、WinDivert 和迅游 SDK 的职责边界。

desktolls 没有复制 Watt Toolkit GPL 源码、域名配置或资源，也没有使用 YARP、WinDivert、根证书或迅游 SDK。本次功能独立实现为无证书、无本地代理的直接 hosts 方案，详细边界记录在 `D:\DESK\desktolls-Steam网页优化说明.md`。

新增内容：

- Steam 商店、社区和创意工坊三个独立选择项及总开关。
- 并行查询 DNSPod DoH、AliDNS DoH 和 Cloudflare DoH，失败时回退 Windows DNS。
- 只接受固定白名单域名和公开 IPv4。
- 候选 IP 必须连续两次完成 TCP、目标域名 TLS 证书校验并收到真实 HTTP 响应头。
- 商店或社区关键域名失败时，整个对应分组不写入零散静态资源规则。
- 一次性 UAC 管理员子进程只增删 desktolls hosts 标记块，主程序不以管理员权限常驻。
- 第一次修改前备份完整 hosts；重复应用替换旧块，关闭后保留其他 hosts 内容。
- 标记不完整、重复或顺序异常时拒绝自动写入。

真实交互验证：

- 第一轮只做 TLS 握手的候选出现应用后超时，未作为最终实现；测试规则已完整恢复。
- 第二轮加强为连续两次完整 HTTPS 检测后曾找到商店候选，但该公网 IP 数分钟后失效；测试规则再次完整恢复，未作为最终逻辑。
- 最终逻辑先验证 Windows 正常 DNS，只有正常直连失败时才考虑公共 DoH 候选。
- 当前网络下 9 个域名中 7 个正常直连；Steam 商店正常 DNS 返回 HTTPS `200`，因此没有覆盖商店地址。
- 社区关键域名没有连续稳定的公共候选，程序正确显示社区和创意工坊无稳定线路，并保持总开关关闭。
- 最终验证没有弹 UAC、没有写入 hosts，避免用不稳定候选替换正常线路。
- hosts 备份、UAC 应用、UAC 恢复、DNS 刷新和状态回读均通过。
- 没有新增匹配 desktolls/Watt Toolkit 的受信任根证书，也没有本地 443 监听。

最终本地发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.5.0.0`
- 文件大小：187,258,283 字节（约 178.58 MB）
- SHA-256：`B0640841BB4349ADF8368A717FBE9C7CAD8C506E9F60A468C64D6651823196F2`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.5`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.5-output.log`
- 自检：34 项全部通过，结果保存于 `D:\DESK\desktolls-self-test.log`

按本次要求，v1.5.0 只在本机生成和运行，没有提交、推送或创建新的 GitHub Release；公开仓库与 Release 仍保持 v1.3.1。

## v1.6.0 Steam 本地 HTTPS 反向代理更新

本次继续使用已有的 .NET SDK 8.0.423 和 .NET 8.0.29 运行时，没有下载新的 SDK、Node.js、Electron、证书文件、音频素材、第三方 EXE、驱动、服务、VPN 或商业网络加速器。

从 NuGet 官方源新增了两个 MIT 许可包：

| 包 | 版本 | NUPKG 大小 | NUPKG SHA-256 | 解压缓存位置与大小 |
| --- | --- | ---: | --- | --- |
| `Yarp.ReverseProxy` | 2.3.0 | 578,775 字节 | `FF12EF5F9C46C3F3E815575A45B7598C68043B9EB101A9AAB18ED3BD8C025A24` | `D:\DESK\.tools\nuget-packages\yarp.reverseproxy\2.3.0`，2,474,871 字节 |
| `System.IO.Hashing` | 8.0.0 | 203,635 字节 | `B33386B744CD068E9D11D0B781FE87F9EF585B7370D30A1AE949C218618AE5C1` | `D:\DESK\.tools\nuget-packages\system.io.hashing\8.0.0`，743,627 字节 |

- NuGet 索引：`https://api.nuget.org/v3/index.json`
- YARP 包：`D:\DESK\.tools\nuget-packages\yarp.reverseproxy\2.3.0\yarp.reverseproxy.2.3.0.nupkg`
- System.IO.Hashing 包：`D:\DESK\.tools\nuget-packages\system.io.hashing\8.0.0\system.io.hashing.8.0.0.nupkg`
- YARP 负责经过审查的流式 HTTP 转发；`System.IO.Hashing` 是 YARP 在 .NET 8 下声明的传递依赖。
- 第三方许可和来源已补充到 `D:\DESK\NOTICE.md`。

desktolls 的根证书和域名证书由程序使用 Windows 加密 API 在本机自行生成，不是下载素材。自检生成的本地根材料位于 `%LocalAppData%\desktolls\steam-proxy-root.cer`、`steam-proxy-root.pfx` 和 `steam-proxy-root-password.bin`；PFX 随机密码由 Windows DPAPI 绑定当前用户保护。自检没有把根证书安装到系统受信任根。

实现与验证结果：

- YARP/Kestrel 只绑定 IPv4 回环 `127.0.0.1:443`，真实 TLS 握手自检通过。
- 内置 Steam 域名白名单、公共 IPv4 过滤、DoH 解析、上游证书校验、回环 hosts 生成、重复应用和恢复测试通过。
- Debug、Release、自包含单文件和正式路径四轮构建/自检均通过；最终正式 EXE 为 37 项全部通过。
- 系统级真实启用会安装专用受信任根并修改 hosts，该测试被安全审批拦截，因此没有强行执行或绕过；只读复核确认 hosts 无 desktolls 标记、系统根证书库无 desktolls 根、443 无残留监听。
- 当前图形工具跨隔离桌面无法捕获 WPF 合成内容；v1.6 与未修改的 v1.5 基准均表现为内容区空白，因此未把该截图作为 UI 渲染结论。UI Automation 可读取完整控件树、文本、边界和启用状态。

最终本地发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.6.0.0`
- 文件大小：214,050,290 字节（约 204.13 MB）
- SHA-256：`78C78EFBE2C185FE4C59CCA5E90BB7CE6D94AB2A95F0E5BCA38D07E540220E17`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.6`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.6-output.log`
- 自检结果：37 项全部通过，保存于 `D:\DESK\desktolls-self-test.log`

按本次要求，v1.6.0 只在本机生成和运行，没有提交、推送或创建新的 GitHub Release；公开仓库与 Release 仍保持 v1.3.1。

## v1.7.0 Steam 加速功能移除

本次没有下载新的 SDK、NuGet 包、素材、第三方 EXE、驱动或服务。构建继续使用已有的 .NET SDK 8.0.423，并通过项目内离线 NuGet 配置重新生成依赖资产清单。

移除内容：

- 删除 Steam 加速界面、设置字段、启动和退出恢复逻辑。
- 删除本地 HTTPS 代理、DoH 线路选择、hosts 管理和证书管理服务源码。
- 删除 `Yarp.ReverseProxy`、`System.IO.Hashing` 传递依赖及 `Microsoft.AspNetCore.App` 框架引用；旧 NuGet 缓存仍保留在 `.tools`，但不再参与构建或进入程序。
- 删除 Steam 专项使用说明，并从当前 README、使用说明和 NOTICE 中移除对应功能及依赖描述。
- 发布前确认系统 hosts 无 desktolls Steam 标记、系统受信任根无 desktolls 证书、本地 443 端口无该程序监听。
- 清理 `%LocalAppData%\desktolls` 中旧功能生成的 hosts 备份和本地证书材料，保留 `settings.json` 等其他功能数据。

最终本地发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.7.0.0`
- 文件大小：187,200,498 字节（约 178.53 MB）
- SHA-256：`DF6E3AAB6D319FE358A4CAEC061EB3C21B80A349A2513604EF7E547F07F43747`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.7`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.7-output.log`
- 自检：25 项全部通过，Steam 专项测试和功能入口均已消失。
- 二进制检查：未发现 `Yarp.ReverseProxy`、`SteamOptimizationService` 或“Steam 本地网页加速”文本。

按本次要求，v1.7.0 只在本机生成和运行，没有提交、推送或创建新的 GitHub Release；公开仓库与 Release 仍保持 v1.3.1。

## v1.8.0 任务栏自动隐藏更新

本次没有下载新的 SDK、NuGet 包、图标素材、第三方 EXE、驱动或服务，继续使用已有的 .NET SDK 8.0.423 和 Windows Shell API。

新增内容：

- 增加“自动隐藏任务栏”独立开关，设置会持久保存。
- 使用 Windows `SHAppBarMessage` 读取和切换任务栏状态，不修改注册表、不重启 Explorer、不需要管理员权限。
- 启动时以 Windows 当前真实状态同步开关，避免软件设置与系统状态不一致。
- 增加任务栏结构尺寸、状态位组合和真实状态读取自检。
- 实际交互验证完成 `关闭 → 开启 → 关闭` 可逆切换，测试结束后恢复原来的常驻显示状态。

最终本地发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.8.0.0`
- 文件大小：187,204,594 字节（约 178.53 MB）
- SHA-256：`B473F8A4268EE53794A5D3FA465602AECE9F50E584FE311E42FF83265D742E6F`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.8`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.8-output.log`
- 自检：28 项全部通过，结果保存于 `D:\DESK\desktolls-self-test.log`

按本次要求，v1.8.0 只在本机生成和运行，没有提交、推送或创建新的 GitHub Release；公开仓库与 Release 仍保持 v1.3.1。

## v1.9.0 设置界面分页更新

本次没有下载新的 SDK、NuGet 包、UI 框架、图标素材、第三方 EXE 或服务。在线只读参考了微软官方的 NavigationView 和 WPF ScrollViewer 设计文档，没有保存或引入外部代码与素材：

- `https://learn.microsoft.com/en-us/windows/apps/design/controls/navigationview`
- `https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/scrollviewer-overview`

界面调整：

- 将原来的单条长设置页拆为“系统”“输入与性能”“下载”三个顶部页签。
- 系统页显示桌面图标、任务栏、经典右键菜单和 Windows 更新设置。
- 输入与性能页显示内存优化、鼠标连点和复制/粘贴提示音。
- 下载页单独显示自定义文件下载器，常用下载控件可在一屏内查看。
- 切换页签时滚动区域回到顶部；未选类别的卡片不参与布局和滚动。
- 页签具有固定尺寸、图标、悬停、选中和键盘焦点状态。

验证结果：

- Debug、Release 和 Windows x64 自包含单文件构建均成功，零警告、零错误。
- 28 项正式自检全部通过。
- UI Automation 验证三个页签均显示正确标题和所属卡片，非当前类别卡片全部折叠。
- 自动化选择最初暴露了键盘选择不触发 `Click` 的问题，已改用 `Checked` 事件，鼠标、键盘和辅助技术现使用同一路径。
- 三页截图：`D:\DESK\.tools\ui-v1.9-system.png`、`ui-v1.9-input.png`、`ui-v1.9-download.png`。

最终本地发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.9.0.0`
- 文件大小：187,208,690 字节（约 178.54 MB）
- SHA-256：`8F68B1CA21A00C7C5381CF2C0D978D9553659FDC8B8F69AD732A0123AD361530`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.9`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.9-output.log`

按本次要求，v1.9.0 只在本机生成和运行，没有提交、推送或创建新的 GitHub Release；公开仓库与 Release 仍保持 v1.3.1。

## v1.10.0 退出时恢复系统设置

本次没有下载新的 SDK、NuGet 包、素材、第三方 EXE、驱动或服务，继续使用已有的 .NET SDK 8.0.423 和 Windows 原生接口。

新增内容：

- 底部增加默认勾选的“退出时恢复设置”复选框。
- 明确点击“退出”或托盘“退出”时，先恢复 desktolls 改动的系统状态，再释放进程资源。
- 恢复范围包括：显示桌面图标、关闭任务栏自动隐藏、恢复 Windows 11 默认右键菜单，以及恢复 desktolls 自己管理的 Windows 更新策略。
- 只恢复带 desktolls 备份标记的更新策略，不覆盖其他管理工具或系统管理员原有的策略。
- 普通关闭窗口和“隐藏到托盘”不触发恢复，后台功能继续运行。
- UAC 被取消、管理员子进程失败或恢复后状态校验不通过时，程序拒绝退出并显示原因。

验证结果：

- Debug、Release 和 Windows x64 自包含单文件构建均成功，零警告、零错误。
- 29 项无副作用自检全部通过。
- 完整退出测试从受控的隐藏桌面图标、经典右键菜单、任务栏自动隐藏和更新禁用状态开始，程序最终以退出码 0 结束。
- 退出后逐项确认：`HideIcons=0`、经典菜单 CLSID 不存在、任务栏自动隐藏为 `False`、desktolls 更新策略备份和 `NoAutoUpdate` 均已恢复。
- 设置文件同步保存 `ClassicContextMenuEnabled=false`、`TaskbarAutoHideEnabled=false`、`RestoreSystemSettingsOnExit=true`。
- UI Automation 确认底部复选框和按钮均位于窗口范围内，间距正常、无重叠。

最终本地发布信息：

- 正式程序：`D:\DESK\desktolls.exe`
- 版本：`1.10.0.0`
- 文件大小：187,212,788 字节（约 178.54 MB）
- SHA-256：`25A99DCC6D7100D71E3C7BDA50D9E6DC1D2F881F318F63B634FBAD2F2DA17AC3`
- 发布暂存目录：`D:\DESK\.tools\publish-v1.10`
- 发布输出日志：`D:\DESK\.tools\dotnet-publish-v1.10-output.log`

GitHub 发布记录（2026-07-24）：

- 复用此前保存在 `D:\DESK\.tools\gh\2.96.0\bin\gh.exe` 的便携 GitHub CLI，本次没有下载新工具或素材。
- 将 v1.4.0 至 v1.10.0 的累计源码与文档更新提交并推送到公开仓库。
- 创建 `v1.10.0` Git 标签和 GitHub Release，附件为上述正式版 `desktolls.exe`。
- Release 附件应以本节记录的文件大小和 SHA-256 为准。
