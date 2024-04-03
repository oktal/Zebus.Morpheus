using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Zebus.Morpheus.Simulation;

/// <summary>
/// A basic asynchronous channel to send and receive entries
/// </summary>
public interface IAsyncChannel<TEntry>
{
	Task Send(TEntry entry, CancellationToken cancellationToken = default(CancellationToken));

	IAsyncEnumerable<TEntry> Receive(int count, CancellationToken cancellationToken = default(CancellationToken));
}

public class AsyncChannel<TEntry> : IAsyncChannel<TEntry>
{
	private readonly ChannelWriter<TEntry> _writer;
	private readonly ChannelReader<TEntry> _reader;

	private AsyncChannel(Channel<TEntry> channel)
	{
		_reader = channel.Reader;
		_writer = channel.Writer;
	}

	public static AsyncChannel<TEntry> Create()
	{
		var channel = Channel.CreateUnbounded<TEntry>();
		return new AsyncChannel<TEntry>(channel);
	}

    public async IAsyncEnumerable<TEntry> Receive(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
    {
		int received = 0;

		while (received < count)
		{
			var message = await _reader.ReadAsync(cancellationToken);
			received++;
			yield return message;
		}
    }

    public async Task Send(TEntry entry, CancellationToken cancellationToken = default)
		=> await _writer.WriteAsync(entry, cancellationToken);
}
