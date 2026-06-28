# IIS 监控看板

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-4.0-blue.svg)]()
[![Platform](https://img.shields.io/badge/Platform-Windows%20Server%202008%2B-lightgrey.svg)]()

> 一款轻量级 IIS 健康监控与自动恢复工具，支持应用池性能实时监控，兼容 Windows Server 2008 R2 SP1 及以上系统。

## 功能特性

### 健康监控与自动恢复

| 能力 | 说明 |
|------|------|
| 应用池状态检查 | 定时检测运行/停止/回收状态 |
| HTTP 可用性检查 | 支持 HTTP/HTTPS，关键字匹配验证 |
| 9 种重启策略 | 应用池回收、站点重启、IIS 重启及其组合 |
| 失败阈值 | 连续失败 N 次后才触发恢复操作 |
| 恢复退避 | 5 分钟内最多恢复 3 次，防止抖动 |

**重启策略一览：**

| 策略 | 说明 |
|------|------|
| `AppPoolOnly` | 仅回收应用程序池 |
| `AppPoolRecycleOnly` | 仅回收（不重启） |
| `SiteRestartOnly` | 仅重启站点 |
| `AppPoolThenIIS` | 先回收应用池，失败则重启 IIS |
| `AppPoolThenSite` | 先回收应用池，失败则重启站点 |
| `SiteThenAppPool` | 先重启站点，失败则回收应用池 |
| `SiteThenIIS` | 先重启站点，失败则重启 IIS |
| `IISOnly` | 直接重启整个 IIS |
| `All` | 应用池 -> 站点 -> IIS 依次尝试 |

### 应用池性能监控

实时采集每个应用池的运行指标，以表格形式展示：

| 指标 | 数据源 |
|------|--------|
| 工作进程 PID | `ServerManager.WorkerProcesses` |
| 内存使用量 | `Process.WorkingSet64` |
| 活动请求数 | `W3SVC_W3WP` / `Active Requests` 计数器 |
| 请求速率 | `W3SVC_W3WP` / `Total Requests/Sec` 计数器 |
| 队列长度 | `ASP.NET` / `Requests Queued` 计数器 |

- 每 10 秒自动刷新，支持手动刷新
- 空闲应用池显示"空闲(无请求)"，已停止显示"已停止"

### 可视化与告警

- **实时健康图表**：折线图展示 CPU/内存/磁盘使用率趋势
- **服务器资源监控**：全局 CPU、内存（可用/总量）、磁盘使用率
- **告警通知**：SMTP 邮件 + Webhook，带冷却机制防重复
- **历史报表导出**：CSV / HTML 格式

### UI 功能

- Tab 页切换：监控日志 / 应用池性能
- 双击表格行：即时测试单个站点或应用池
- 暗色主题切换，配置持久化
- 系统托盘图标：绿/黄/红三色指示整体健康状态
- 启动时自动最小化到托盘
- 从本机 IIS 快速选择监控站点和应用池

## 快速开始

### 环境要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows Server 2008 R2 SP1 及以上 |
| 运行时 | .NET Framework 4.0 |
| 权限 | 管理员（操作 IIS 需要） |

### 编译

```bash
git clone https://github.com/xuu1998/IISMonitor.git
cd IISMonitor
dotnet build IISMonitor\IISMonitor.csproj -c Release
```

依赖项：
- `Microsoft.NETFramework.ReferenceAssemblies.net40` — NuGet 自动还原
- `Newtonsoft.Json` — 已包含在 `libs/` 目录
- `Microsoft.Web.Administration` — 已包含在 `libs/` 目录

### 运行

1. 以**管理员权限**运行 `IISMonitor.exe`
2. 在界面中配置监控目标（支持从本机 IIS 一键选择）
3. 选择检查间隔、失败阈值、重启策略
4. 点击 **启动监控**

## 项目结构

```
IISMonitor/
├── IISMonitor/
│   ├── Models/                 # 数据模型
│   │   ├── HealthRecord.cs     # 健康检查记录
│   │   └── AppPoolMetrics.cs   # 应用池性能指标
│   ├── Services/               # 服务层
│   │   ├── ResourceMonitor.cs  # 服务器资源监控
│   │   └── AlertService.cs     # 告警通知服务
│   ├── Export/                 # 报表导出
│   │   └── ReportExporter.cs   # CSV / HTML 导出
│   ├── UI/                     # UI 组件
│   │   └── HealthChart.cs      # 实时健康图表
│   ├── Infrastructure/         # 基础设施
│   │   ├── ThemeManager.cs     # 暗色主题管理
│   │   └── EnumExtensions.cs   # 枚举扩展
│   ├── libs/                   # 依赖 DLL
│   ├── MainForm.cs             # 主窗体逻辑
│   ├── MainForm.Designer.cs    # 主窗体布局
│   ├── MonitorService.cs       # 核心监控服务
│   ├── MonitorConfig.cs        # 配置模型
│   ├── IISHelper.cs            # IIS 操作封装
│   └── Logger.cs               # 异步日志
├── IISMonitor.Tests/           # 单元测试
├── LICENSE                     # MIT 协议
└── README.md
```

## 配置与日志

| 文件 | 说明 |
|------|------|
| `MonitorConfig.xml` | 运行配置（自动生成） |
| `logs\IISMonitor_YYYYMMDD.log` | 运行日志（10MB 自动轮转，30 天自动清理） |
| `logs\health_results.jsonl` | 健康检查数据（可用于导出报表） |

## 注意事项

- **必须以管理员权限运行**，否则无法读取 IIS 配置和重启应用池
- 重启整个 IIS 会影响服务器上**所有站点**，请谨慎使用 `IISOnly` 策略
- 应用池性能监控依赖 `W3SVC_W3WP` 和 `ASP.NET` 性能计数器，需要 IIS 已安装并运行
- 空闲应用池（无请求）不会创建工作进程，此时 PID/内存/连接数均显示 "-"
- 建议配合 Windows 任务计划程序设置开机自启

## 开源协议

本项目基于 [MIT 协议](LICENSE) 开源。
