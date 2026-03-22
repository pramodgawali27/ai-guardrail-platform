using System.Threading.Channels;

namespace Guardrail.Infrastructure.Evaluation;

public sealed class EvaluationBackgroundQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();

    public ValueTask QueueAsync(Guid runId, CancellationToken cancellationToken = default)
        => _queue.Writer.WriteAsync(runId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken = default)
        => _queue.Reader.ReadAllAsync(cancellationToken);
}
