using System.Threading.Channels;

namespace SmartTorque.Web.Infrastructure.BackgroundServices;

public record TelemetryReadingItem(
    int DeviceId,
    string TransmitterId,
    long? SessionId,
    decimal Value,
    string Unit,
    long? DeviceSeq,
    DateTime CapturedAt,
    bool IsStable,
    bool InTolerance
);

public interface ITelemetryIngestionChannel
{
    ValueTask QueueReadingAsync(TelemetryReadingItem item, CancellationToken ct = default);
    bool TryQueueReading(TelemetryReadingItem item);
    IAsyncEnumerable<TelemetryReadingItem> ReadAllAsync(CancellationToken ct = default);
}

public class TelemetryIngestionChannel : ITelemetryIngestionChannel
{
    private readonly Channel<TelemetryReadingItem> _channel;

    public TelemetryIngestionChannel(int capacity = 10000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true
        };
        _channel = Channel.CreateBounded<TelemetryReadingItem>(options);
    }

    public ValueTask QueueReadingAsync(TelemetryReadingItem item, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(item, ct);
    }

    public bool TryQueueReading(TelemetryReadingItem item)
    {
        return _channel.Writer.TryWrite(item);
    }

    public IAsyncEnumerable<TelemetryReadingItem> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
