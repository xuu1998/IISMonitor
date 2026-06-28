# IIS 监控看板

兼容 Windows Server 2008+ / .NET Framework 4.0 的 IIS 健康监控、自动重启与性能监控工具。

## 功能

### 核心监控
- 定时检查 IIS 应用程序池状态（运行/停止/回收状态）
- 定时检查网站 HTTP/HTTPS 可用性
- 9 种重启策略，灵活适配不同场景：
  - `AppPoolOnly` - 仅回收应用程序池
  - `AppPoolThenIIS` - 先回收应用池，失败则重启整个 IIS
  - `IISOnly` - 直接重启 IIS
  - `AppPoolRecycleOnly` - 仅回收应用池（不重启）
  - `SiteRestartOnly` - 仅重启站点
  - `AppPoolThenSite` - 先回收应用池，失败则重启站点
  - `SiteThenAppPool` - 先重启站点，失败则回收应用池
  - `SiteThenIIS` - 先重启站点，失败则重启 IIS
  - `All` - 全部尝试（应用池 → 站点 → IIS）
- 可配置连续失败次数阈值后再重启
- 故障恢复退避机制（5 分钟内最多恢复 3 次，防抖）

### 应用池性能监控
- **工作进程信息**：PID、进程数量
- **内存使用量**：按进程采集工作集内存（MB）
- **当前连接数**：通过 `W3SVC_W3WP` 计数器采集活动请求数
- **请求速率**：每秒请求数（Requests/Sec）
- **请求队列**：ASP.NET 全局请求队列长度
- 每 10 秒自动刷新，支持手动刷新
- 空闲应用池显示"空闲(无请求)"，已停止显示"已停止"

### 实时健康图表
- 折线图显示各目标可用率变化趋势
- CPU/内存/磁盘使用率实时曲线

### 告警通知
- 支持 SMTP 邮件告警
- 支持 Webhook 告警
- 冷却机制避免重复告警

### 服务器资源监控
- CPU 使用率（全局）
- 内存使用率（可用/总量）
- 磁盘使用率

### 历史报表导出
- CSV 格式导出
- HTML 可视化报告

### UI 功能
- Tab 页切换：监控日志 / 应用池性能
- 手动测试：双击 DataGridView 单元格即时测试站点或应用池
- 暗色主题切换，配置持久化
- 系统托盘图标：绿/黄/红三色指示健康状态
- 启动时自动最小化到托盘
- 从本机 IIS 快速选择监控站点和应用池

## 编译要求

- Visual Studio 2012 或更高版本
- .NET Framework 4.0（目标框架）
- `Microsoft.NETFramework.ReferenceAssemblies.net40` NuGet 包（自动还原，无需手动安装 .NET 4.0 SDK）
- `Newtonsoft.Json`（已包含在 `libs/` 目录）
- `Microsoft.Web.Administration`（已包含在 `libs/` 目录）

## 运行环境

- Windows Server 2008 R2 SP1 及以上
- .NET Framework 4.0 运行时
- 需要**管理员权限**运行（操作 IIS 需要）

## 部署

1. 克隆仓库并编译，得到 `IISMonitor.exe`
2. 以**管理员权限**运行
3. 在界面中配置：
   - 监控的站点 URL（每行一个，支持从本机 IIS 选择）
   - 监控的应用程序池名称（每行一个，支持从本机 IIS 选择）
   - 检查间隔（秒）
   - 连续失败几次后重启
   - 重启策略（9 种可选）
   - 告警通知（SMTP / Webhook）
   - 资源监控开关
4. 点击"启动监控"

### Tab 页说明

- **监控日志**：实时显示检查结果、错误信息、恢复操作记录
- **应用池性能**：显示所有应用池的工作进程 PID、内存、活动请求数、请求速率、队列长度，支持手动刷新

## 配置文件

程序自动生成 `MonitorConfig.xml` 保存配置。

## 日志

- 日志文件：`logs\IISMonitor_YYYYMMDD.log`（自动轮转，超过 10MB 分卷）
- 健康检查数据：`logs\health_results.jsonl`（可用于导出报表）
- 日志自动清理：超过 30 天的日志自动删除

## 注意事项

- **必须以管理员权限运行**，否则无法读取 IIS 配置和重启应用池
- 建议配合 Windows 任务计划程序设置开机自启
- 重启整个 IIS 会影响服务器上所有站点，请谨慎使用 `IISOnly` 策略
- 应用池性能监控依赖 `W3SVC_W3WP` 和 `ASP.NET` 性能计数器，需要 IIS 已安装并运行
- .NET 4.0 目标框架确保在 Windows Server 2008 R2 SP1 上无需安装更高版本 .NET 运行时
