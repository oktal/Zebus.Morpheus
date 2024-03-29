using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Abc.Zebus;

namespace Zebus.Morpheus.Simulation;

/// <summary>
/// A basic asynchronous channel to send and receive <see cref="IMessage" /> messages
/// </summary>
public interface IMessageChannel
{
	Task Send(IMessage message, CancellationToken cancellationToken = default(CancellationToken));

	IAsyncEnumerable<IMessage> Receive(int count, CancellationToken cancellationToken = default(CancellationToken));
}

public class MessageChannel : IMessageChannel
{
	private readonly ChannelWriter<IMessage> _writer;
	private readonly ChannelReader<IMessage> _reader;

	private MessageChannel(Channel<IMessage> channel)
	{
		_reader = channel.Reader;
		_writer = channel.Writer;
	}

	public static MessageChannel Create()
	{
		var channel = Channel.CreateUnbounded<IMessage>();
		return new MessageChannel(channel);
	}

    public async IAsyncEnumerable<IMessage> Receive(int count, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
    {
		int received = 0;

		while (received < count)
		{
			var message = await _reader.ReadAsync(cancellationToken);
			received++;
			yield return message;
		}
    }

    public async Task Send(IMessage message, CancellationToken cancellationToken = default)
		=> await _writer.WriteAsync(message, cancellationToken);
}
