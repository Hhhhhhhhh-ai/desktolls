# desktolls 自定义文件下载说明

## PCL2 查证结果

本次参考的是 PCL2 当前官方公开仓库 [`Meloong-Git/PCL`](https://github.com/Meloong-Git/PCL)，研究时 `main` 分支提交为 [`bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1`](https://github.com/Meloong-Git/PCL/commit/bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1)。

需要区分两部分：

1. 百宝箱页面的具体实现没有开源。[`PageOtherTest.xaml.vb`](https://github.com/Meloong-Git/PCL/blob/bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1/Plain%20Craft%20Launcher%202/Pages/PageOther/PageOtherTest.xaml.vb#L1) 明确提示“为便于维护，开源内容中不包含百宝箱功能”。因此不能负责任地声称拿到了截图中“下载自定义文件”按钮的私有事件代码。
2. PCL2 共用的多线程下载引擎是公开的，位于 [`ModNet.vb`](https://github.com/Meloong-Git/PCL/blob/bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1/Plain%20Craft%20Launcher%202/Modules/Base/ModNet.vb)。可以直接确认以下机制：

- [`NetTaskThreadLimit`](https://github.com/Meloong-Git/PCL/blob/bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1/Plain%20Craft%20Launcher%202/Modules/Base/ModNet.vb#L381) 从设置读取最大下载线程数。
- [`TryBeginThread`](https://github.com/Meloong-Git/PCL/blob/bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1/Plain%20Craft%20Launcher%202/Modules/Base/ModNet.vb#L881) 会寻找最大的未完成片段并拆出新线程。
- 下载线程通过 HTTP [`Range`](https://github.com/Meloong-Git/PCL/blob/bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1/Plain%20Craft%20Launcher%202/Modules/Base/ModNet.vb#L987) 请求指定起点，并检查服务器返回的长度。
- 遇到不支持 Range、`416` 或连续失败时，[`SourceFail`](https://github.com/Meloong-Git/PCL/blob/bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1/Plain%20Craft%20Launcher%202/Modules/Base/ModNet.vb#L1146) 会禁用分段源并以单线程重新尝试。
- 所有片段完成后，[`Merge`](https://github.com/Meloong-Git/PCL/blob/bbb4fc31a8fe09c9c1b446eaf428c2687c2f03c1/Plain%20Craft%20Launcher%202/Modules/Base/ModNet.vb#L1239) 按顺序合并临时片段并校验文件。

desktolls 没有复制 PCL2 的私有百宝箱代码；实现依据是上述公开、可核实的通用下载机制，以及标准 HTTP Range 语义。

## desktolls 实现

- 仅接受完整的 `HTTP` / `HTTPS` 地址。
- 默认自动识别文件名；可取消“自动识别文件名”后手动命名。
- 文件名依次参考 `Content-Disposition`、重定向后的 URL、原始 URL、`Content-Type` 和前 512 字节文件头。可靠的服务器名称优先级最高。
- 可识别常见的 EXE、DLL、SYS、MSI、ZIP、7Z、RAR、PDF、Office 文档、图片、音频和视频扩展名；无法可靠判断时不强制伪造后缀。
- 下载线程可选 `1`、`2`、`4`、`8`，默认 `4`。
- 开始时发送 `bytes=0-511` 探测请求；只有服务器正确返回 `206 Partial Content` 和总大小时才启用并行分段。
- 文件不小于 `1 MB` 时才尝试并行，每个分段至少约 `512 KB`，小文件自动使用单线程。
- 每个分段最多尝试 `3` 次，网络错误后短暂退避再重试。
- 服务器忽略或拒绝 Range 时自动切换为单线程，不把错误的分段响应写成完整文件。
- 下载期间写入目标文件旁的 `文件名.desktolls.part`；所有字节完成并通过大小校验后才移动为正式文件。
- 取消或失败会清理临时文件。覆盖同名文件前由界面要求确认。
- 显示百分比、已下载大小、总大小、实时速度和当前分段数。
- 退出 desktolls 时会取消仍在进行的下载。

## 限制

该功能是直链下载器，不负责绕过网站权限。必须登录、依赖浏览器 Cookie、验证码、防盗链或网盘客户端的网站可能返回 `401`、`403`、`429` 等错误。desktolls 不获取浏览器 Cookie，也不尝试绕过这些限制。

多线程是否更快取决于服务器、网络和磁盘。部分服务器会限制并发请求，此时可在界面选择单线程或 `2 线程`。

## 本机验证

内置本地 HTTP 回归测试使用同一份确定性二进制数据验证：

- 支持 Range 的服务器：并行分段下载完成，合并后逐字节一致。
- 第一个实际分段故意返回一次 `503`：自动重试后完成。
- 忽略 Range 的服务器：自动使用单线程，结果逐字节一致。
- 输入错误的 `.wrong` 后缀、服务器返回 VS Code 安装包名称：预览和正式保存均自动纠正为 `VSCodeUserSetup-x64-1.129.0.exe`。
- 慢速服务器中途取消：没有留下正式文件或 `.desktolls.part`。
- WPF 下载界面已在本机 `175%` DPI、`1190 × 1260` 实际窗口像素下截图检查；长文件名、自动识别开关和其他控件均无截断或重叠。
