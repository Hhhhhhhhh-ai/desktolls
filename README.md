# desktolls

desktolls 是一个面向 Windows 10/11 x64 的轻量桌面工具，使用 C#、WPF 和 .NET 8 编写。

设置界面按“系统”“输入与性能”“下载”分为三个顶部页签，切换功能类别时无需在一条长页面中反复滚动。

## 功能

- 鼠标位于桌面时，中键隐藏或显示桌面图标。
- 随 Windows 启动，并可在启动后默认隐藏桌面图标。
- 在 Windows 11 中切换 Win10 样式的完整右键菜单。
- 一键切换 Windows 任务栏自动隐藏，无需重启 Explorer 或管理员权限。
- 退出时可自动显示桌面图标和任务栏，并恢复右键菜单及 desktolls 管理的更新策略。
- 使用系统策略禁止 Windows 自动下载和安装更新，仍保留手动更新能力。
- 全局热键控制鼠标左键连点，热键和频率可调。
- 为物理键盘的 `Ctrl+C` 和 `Ctrl+V` 提供不同的轻量提示音，可分别关闭。
- 仅裁剪 desktolls 自身工作集的定时内存优化。
- 支持 Range 分段、失败重试、自动回退和文件名识别的 HTTP/HTTPS 下载器。

## 下载

预编译的 Windows x64 单文件请从 [GitHub Releases](../../releases/latest) 下载。

当前版本：`1.10.0`

官方构建的 `desktolls.exe` 是 .NET 8 自包含程序，不要求用户另行安装 .NET。未签名的可执行文件可能触发 Windows SmartScreen 提示；请在运行前核对 Release 中公布的 SHA-256。

## 使用提示

- 程序关闭窗口后仍会在系统托盘运行，需从托盘菜单选择“退出”才会结束。
- 切换 Windows 更新策略时会请求 UAC 管理员授权，主程序不会以管理员权限常驻。
- 禁止自动更新会延迟安全补丁安装，请定期手动检查更新。
- 连点器可能违反部分游戏或软件的使用规则，使用者应自行确认目标软件条款。
- 下载器不绕过登录、Cookie、验证码、防盗链或网站权限。
- 复制/粘贴提示音只监听物理组合键，不读取或记录剪贴板内容。

完整说明见 [desktolls-使用说明.md](desktolls-使用说明.md)。

## 从源码构建

要求：Windows x64 和 .NET 8 SDK。

```powershell
dotnet restore .\src\desktolls\desktolls.csproj
dotnet build .\src\desktolls\desktolls.csproj -c Release
dotnet publish .\src\desktolls\desktolls.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

程序设置保存在 `%LocalAppData%\desktolls\settings.json`。只有发生错误时才会生成 `%LocalAppData%\desktolls\desktolls-error.log`。

## 安全边界

- Windows 更新功能只设置微软支持的策略值，不禁用服务、计划任务、系统 DLL 或 hosts。
- 内存优化只操作当前 desktolls 进程，不枚举或裁剪其他进程。
- 下载任务先写入 `.desktolls.part`，完成并校验大小后才替换正式文件。
- 项目不收集遥测，不上传用户设置，也不读取浏览器 Cookie。

相关实现说明：

- [Windows 更新说明](desktolls-Windows更新说明.md)
- [内存优化说明](desktolls-内存优化说明.md)
- [自定义下载说明](desktolls-自定义下载说明.md)

## 许可证与署名

desktolls 自有源码使用 [MIT License](LICENSE)。第三方说明及 PCL 研究署名见 [NOTICE.md](NOTICE.md)。
