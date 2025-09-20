using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventTicketing.Services.Email
{
    public class EmailWorker : BackgroundService
    {
        private readonly IEmailQueue _queue;
        private readonly IEmailSender _sender;
        private readonly ILogger<EmailWorker> _log;

        public EmailWorker(IEmailQueue queue, IEmailSender sender, ILogger<EmailWorker> log)
        {
            _queue = queue; _sender = sender; _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
            {
                try
                {
                    await _sender.SendAsync(job.To, job.Subject, job.HtmlBody, stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed sending email to {To}", job.To);
                }
                
                await Task.Delay(10, stoppingToken);
            }
        }
    }
}