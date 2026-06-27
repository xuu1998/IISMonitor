using System.ServiceProcess;

namespace IISMonitor
{
    public partial class IISMonitorService : ServiceBase
    {
        private MonitorService _monitorService;

        public IISMonitorService()
        {
            InitializeComponent();
            ServiceName = "IISMonitorService";
        }

        protected override void OnStart(string[] args)
        {
            var config = MonitorConfig.Load(
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "MonitorConfig.xml"));
            Logger.Initialize(config.LogPath);

            if (config.IsCorrupted)
            {
                Logger.Log("配置文件已损坏，使用默认配置启动服务，请尽快重新配置", true);
            }

            // 服务模式下同样校验监控项，避免与 UI 模式行为不一致
            if (!MonitorConfig.ValidateItems(config.MonitoredSites, config.MonitoredAppPools, out string errMsg))
            {
                Logger.LogError($"配置校验失败，服务无法启动: {errMsg}");
                this.Stop();
                return;
            }

            if ((config.MonitoredSites == null || config.MonitoredSites.Length == 0) &&
                (config.MonitoredAppPools == null || config.MonitoredAppPools.Length == 0))
            {
                Logger.LogError("未配置任何监控目标（站点和应用池均为空），服务无法启动");
                this.Stop();
                return;
            }

            _monitorService = new MonitorService();
            _monitorService.Start(config);
        }

        protected override void OnStop()
        {
            _monitorService?.Stop();
            Logger.Shutdown();
        }
    }
}
