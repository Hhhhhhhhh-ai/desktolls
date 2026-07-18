# desktolls 下载日志

记录时间：2026-07-18（Asia/Hong_Kong）

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
