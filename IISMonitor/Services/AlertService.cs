using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace IISMonitor
{
    /// <summary>
    /// 告警级别
    /// </summary>
    public enum AlertLevel
    {
        Info,    // 信息
        Warning, // 警告
        Error    // 错误
    }

    /// <summary>
    /// 告警通知服务，支持 SMTP 邮件和 Webhook
    /// </summary>
    public class AlertService
    {
        private AlertConfig _config;
        private Dictionary<string, DateTime> _lastAlertPerTarget = new Dictionary<string, DateTime>();
        private readonly object _alertLock = new object();

        public AlertService(AlertConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 发送告警通知（异步，不阻塞调用方）
        /// </summary>
        public void SendAlert(string target, string message, AlertLevel level)
        {
            // 冷却检查（同步，避免风暴）
            lock (_alertLock)
            {
                if (_lastAlertPerTarget.ContainsKey(target))
                {
                    TimeSpan elapsed = DateTime.Now - _lastAlertPerTarget[target];
                    if (elapsed.TotalSeconds < _config.AlertCooldownSeconds)
                    {
                        return;
                    }
                }
                _lastAlertPerTarget[target] = DateTime.Now;
            }

            string subject = $"[IISMonitor] {level} - {target}";

            // 异步发送，避免阻塞监控线程
            if (_config.EnableSmtp && !string.IsNullOrEmpty(_config.SmtpHost))
            {
                Task.Factory.StartNew(() =>
                {
                    try { SendSmtp(subject, message); }
                    catch (Exception ex) { Logger.LogError("SMTP 告警发送线程异常", ex); }
                });
            }

            if (_config.EnableWebhook && !string.IsNullOrEmpty(_config.WebhookUrl))
            {
                Task.Factory.StartNew(() =>
                {
                    try { SendWebhook(subject, message, level); }
                    catch (Exception ex) { Logger.LogError("Webhook 告警发送线程异常", ex); }
                });
            }

            Logger.Log($"告警已派发 [{level}]: {target} - {message}");
        }

        /// <summary>
        /// 通过 SMTP 发送邮件告警
        /// </summary>
        private void SendSmtp(string subject, string body)
        {
            try
            {
                using (var client = new System.Net.Mail.SmtpClient(_config.SmtpHost, _config.SmtpPort))
                {
                    client.EnableSsl = _config.SmtpUseSsl;
                    if (!string.IsNullOrEmpty(_config.SmtpUsername))
                    {
                        client.Credentials = new System.Net.NetworkCredential(_config.SmtpUsername, _config.SmtpPassword);
                    }

                    var mail = new System.Net.Mail.MailMessage(_config.FromAddress, _config.ToAddress, subject, body);
                    client.Send(mail);
                    Logger.Log($"SMTP 告警邮件已发送至 {_config.ToAddress}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"发送 SMTP 告警失败", ex);
            }
        }

        /// <summary>
        /// 通过 Webhook 发送告警
        /// </summary>
        private void SendWebhook(string subject, string message, AlertLevel level)
        {
            try
            {
                var payload = new
                {
                    subject = subject,
                    message = message,
                    level = level.ToString(),
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    source = "IISMonitor"
                };
                string json = JsonConvert.SerializeObject(payload);
                byte[] data = System.Text.Encoding.UTF8.GetBytes(json);

                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(_config.WebhookUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 10000;
                request.ContentLength = data.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                {
                    Logger.Log($"Webhook 告警已发送，响应码: {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"发送 Webhook 告警失败", ex);
            }
        }
    }
}
