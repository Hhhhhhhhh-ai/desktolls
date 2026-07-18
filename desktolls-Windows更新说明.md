# desktolls Windows 更新策略说明

## 查证结果

微软官方文档 [`Configure Automatic Updates`](https://learn.microsoft.com/en-us/windows/deployment/update/waas-wu-settings) 明确列出策略注册表位置：

`HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU`

- `NoAutoUpdate=1`：禁用自动更新。
- `AUOptions=1`：关闭自动保持计算机更新。
- 删除这些策略值后，Windows 会重新使用本地管理员原有的更新偏好。

GitHub 工具采用的方案差异较大：

1. [`ChrisTitusTech/winutil`](https://github.com/ChrisTitusTech/winutil/blob/main/functions/public/Invoke-WPFUpdatesdisable.ps1) 除了写入上述策略，还停止 `BITS/wuauserv/UsoSvc` 并禁用多组更新计划任务。
2. [`WereDev/Wu10Man`](https://github.com/WereDev/Wu10Man) 提供组策略、服务、任务和 hosts 等多种独立开关。
3. [`Aetherinox/pause-windows-updates`](https://github.com/Aetherinox/pause-windows-updates) 主要通过写入未来日期延长“暂停更新”，仍属于暂停机制。
4. [`tsgrgo/windows-update-disabler`](https://github.com/tsgrgo/windows-update-disabler) 会修改服务配置、计划任务，甚至重命名系统 DLL，副作用和恢复风险较高。

## desktolls 的选择

desktolls 只采用微软正式且可逆的策略层：

1. 打开开关时备份现有 `NoAutoUpdate` 和 `AUOptions`。
2. 写入 `NoAutoUpdate=1`、`AUOptions=1`。
3. 备份保存在 `HKLM\SOFTWARE\desktolls\Backups\WindowsUpdate`。
4. 关闭开关时恢复备份；如果原来没有对应值，就删除 desktolls 写入的值。
5. 不停止更新、传输或修复服务，不操作计划任务、hosts、服务 ACL 或系统文件。

该策略禁止自动下载和安装，但保留手动检查更新。策略没有几周后自动失效的暂停期限，重启后仍然有效。

## 权限与当前状态

主程序始终以普通用户权限运行。每次切换开关时，只启动一个带固定参数的一次性管理员子进程，完成后立即退出；非管理员直接调用策略命令会被拒绝。

v1.2 发布完成时没有替用户直接写入系统策略：`NoAutoUpdate`、`AUOptions` 和 desktolls 备份键均不存在。需要用户在设置窗口中亲自打开“禁止 Windows 自动更新”并确认 UAC，策略才会生效。

禁止自动更新会延迟安全补丁。建议定期手动打开 Windows Update 检查并安装安全更新。
