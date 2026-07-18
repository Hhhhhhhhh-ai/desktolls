# desktolls 内存优化说明

## PCL2 查证结果

PCL2 官方仓库已从 `Hex-Dragon/PCL2` 转移到 [`Meloong-Git/PCL`](https://github.com/Meloong-Git/PCL)。官方代码可以确认：

1. [`ModEvent.vb`](https://github.com/Meloong-Git/PCL/blob/main/Plain%20Craft%20Launcher%202/Modules/ModEvent.vb) 中的“内存优化”事件会调用 `PageOtherTest.MemoryOptimize(True)`。
2. [`PageOtherTest.xaml.vb`](https://github.com/Meloong-Git/PCL/blob/main/Plain%20Craft%20Launcher%202/Pages/PageOther/PageOtherTest.xaml.vb) 明确写着“为便于维护，开源内容中不包含百宝箱功能”，所以无法从官方源码核实百宝箱使用的具体函数和参数。
3. 官方 Issue [#7903](https://github.com/Meloong-Git/PCL/issues/7903) 中，协作者说明 PCL 内存优化会把内存换出到硬盘，可能产生大量磁盘写入和卡顿。
4. 官方 Issue [#5645](https://github.com/Meloong-Git/PCL/issues/5645) 中，贡献者说明 PCL 是调用 Win32 API 让 Windows 执行该过程。
5. 官方 Issue [#8442](https://github.com/Meloong-Git/PCL/issues/8442) 中，协作者说明换出过程不能主动“还原”，只能等程序再次访问页面时由 Windows 载回。

因此，可以确认 PCL2 的按钮是全局、偏激进的 Windows 内存换出操作，但不能负责任地声称知道其闭源实现具体调用了哪个 API。

## desktolls 的实现边界

desktolls 不复制 PCL2 的全局行为，只实现用户要求的当前进程版本：

1. 定时器只获取 `Process.GetCurrentProcess()`。
2. 只把当前进程句柄传给微软 [`EmptyWorkingSet`](https://learn.microsoft.com/windows/win32/api/psapi/nf-psapi-emptyworkingset)。
3. 不调用 `Process.GetProcesses`、`OpenProcess` 或 `NtSetSystemInformation`。
4. 不清理系统待机列表，不修改其他进程工作集，不申请管理员权限。
5. 不强制并发执行 .NET 全量 GC；托管对象继续由 CLR 正常回收，避免 GC 扫描与页面裁剪互相造成抖动。
6. 每次成功裁剪后只重新注册 desktolls 自己的桌面中键钩子，避免低级钩子回调因页面换入超时而被 Windows 静默移除。

`EmptyWorkingSet` 降低的是当前进程驻留在物理内存中的页面数量。它不会伪装任务管理器数据，也不保证降低虚拟内存提交量；被裁剪页面在再次使用时会由 Windows 按需载回。

## 本机验证

- v1.0 后台基线：工作集约 `164.31 MB`。
- v1.1 启动后、首次定时优化前：工作集约 `150.75 MB`。
- 默认 10 秒间隔触发后：工作集约 `27.88 MB`，随后稳定在约 `28–29 MB`。
- 隐藏到托盘并立即优化后的界面记录：工作集约 `4.2 MB`。
- 测试期间私有提交约 `113 MB`，进程持续响应；这符合“减少后台物理内存占用”而不是释放全部提交内存的设计目标。
