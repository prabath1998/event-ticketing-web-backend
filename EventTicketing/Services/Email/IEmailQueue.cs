using System.Threading.Channels;

namespace EventTicketing.Services.Email
{
    public interface IEmailQueue
    {
        void Enqueue(EmailJob job);
        IAsyncEnumerable<EmailJob> DequeueAllAsync(CancellationToken ct);
    }

    public class EmailQueue : IEmailQueue
    {
        private readonly Channel<EmailJob> _channel =
            Channel.CreateUnbounded<EmailJob>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        public void Enqueue(EmailJob job) => _channel.Writer.TryWrite(job);

        public IAsyncEnumerable<EmailJob> DequeueAllAsync(CancellationToken ct)
            => _channel.Reader.ReadAllAsync(ct);
    }
}