# IIS 监控看板

兼容 Windows Server 2008+ / .NET Framework 4.8 的 IIS 健康监控与自动重启工具。

## 功能

### 核心监控
- 定时检查 IIS 应用程序池状态
- 定时检查网站 HTTP/HTTPS 可用性
- 支持三种恢复策略：
  - `AppPoolOnly` - 仅回收应用程序池
  - `AppPoolThenIIS` - 先回收应用池，失败则重启整个 IIS
  - `IISOnly` - 直接重启 IIS
- 可配置连续失败次数阈值后再重启
- 故障恢复退避机制（5 分钟内最多恢复 3 次，防抖）

### 新增功能
- **实时健康图表**：折线图显示各目标可用率变化趋势
- **告警通知**：支持 SMTP 邮件和 Webhook 告警，带冷却机制
- **服务器资源监控**：CPU/内存/磁盘使用率监控
- **历史报表导出**：支持 CSV 和 HTML 格式的可视化报告
- **手动测试**：双击 DataGridView 单元格可即时测试单个站点或应用池
- **暗色主题**：支持亮/暗主题切换，配置持久化
- **托盘图标健康指示**：绿/黄/红三色图标直观显示整体健康状态
- **启动自动最小化**：启动监控后可自动最小化到系统托盘

### 架构优化
- 使用 `Microsoft.Web.Administration` API 替代 PowerShell 进程调用，性能大幅提升
- 线程安全计数器（`ConcurrentDictionary`），消除多线程竞态条件
- 异步日志写入（`BlockingCollection` + 后台线程），不阻塞监控线程
- 配置损坏检测与弹窗告警

## 编译要求

- Visual Studio 2012 或更高版本
- .NET Framework 4.8
- 需要引用 `Microsoft.Web.Administration`（已包含在 `libs/` 目录）

## 部署

1. 编译项目，得到 `IISMonitor.exe`
2. 以**管理员权限**运行（因为需要操作 IIS）
3. 在界面中配置：
   - 监控的站点 URL（每行一个）
   - 监控的应用程序池名称（每行一个）
   - 检查间隔（秒）
   - 连续失败几次后重启
   - 重启策略
   - 告警通知（SMTP / Webhook）
   - 资源监控开关
4. 点击"启动监控"

### 安装为 Windows 服务（后台运行）

以管理员身份运行 `install_service.bat`，然后启动服务：
```
sc start IISMonitorService
```

## 配置文件

程序会自动生成 `MonitorConfig.xml` 保存配置。

## 日志

- 日志文件：`logs\IISMonitor_YYYYMMDD.log`（自动轮转，超过 10MB 分卷）
- 健康检查数据：`logs\health_results.jsonl`（可用于导出报表）
- 日志自动清理：超过 30 天的日志自动删除

## 注意事项

- **必须以管理员权限运行**，否则无法读取 IIS 配置和重启应用池
- 建议配合 Windows 任务计划程序设置开机自启
- 重启整个 IIS 会影响服务器上所有站点，请谨慎使用 `IISOnly` 策略
