namespace IISMonitor
{
    partial class IISMonitorService
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.ServiceName = "IISMonitorService";
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = true;
        }
    }
}
