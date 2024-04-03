using Abc.Zebus;
using Microsoft.Extensions.Logging;

namespace Zebus.Morpheus.Simulation.RoutedCommand;

public record MessageEntry(IMessage Message, PeerId ReceiverId);

public class RoutedCommandHandler(IAsyncChannel<MessageEntry> channel, IBus bus) : IAsyncMessageHandler<RoutedCommand>
{
    public async Task Handle(RoutedCommand message)
    {
		await channel.Send(new MessageEntry(message, bus.PeerId));
    }
}

public class RoutedCommandParameters
{
	/// <summary>
	/// A list of routing keys to subscribe to.
	/// Each routing key of this list will be subscribed from a different peer
	/// </summary>
	public List<string> RoutingKeys { get; set; } = new();

	/// <summary>
	/// Number of commands to send per routing
	/// </summary>
	public int Count { get; set; }

	/// <summary>
	/// Start sequence number
	/// </summary>
	public int Seq { get; set; } = 1;
}

/// <summary>
/// A <see cref="ISimulation" /> that will start multiple peers and will subscribe to a routed
/// command <see cref="RoutedCommand" />
/// Once the simulation starts, it will wait for every bus to receive a configured number of
/// commands properly routed and will check that every peer received the right number of commands
/// </summary>
[Simulation(Name = "routed-command", Parameters = typeof(RoutedCommandParameters))]
public class RoutedCommandSimulation : ParameteredSimulation<RoutedCommandParameters>, ISimulation
{
	private readonly IAsyncChannel<MessageEntry> _channel = AsyncChannel<MessageEntry>.Create();
	private readonly List<IBus> _buses = new();

    public async Task BeforeRun(SimulationContext context)
    {
		foreach (var routingKey in Parameters.RoutingKeys)
		{
			var bus = context
				.CreateBusFactory()
				.WithPeerId($"Zebus.Morpheus.RoutedCommand.Simulation.{routingKey}")
				.WithHandlers(typeof(RoutedCommandHandler))
				.ConfigureContainer(cfg => cfg.ForSingletonOf<IAsyncChannel<MessageEntry>>().Use(_channel))
				.CreateBus();

			bus.Start();

			context.Logger.LogInformation($"Subscribing to {routingKey}");
			await bus.SubscribeAsync(Subscription.Matching<RoutedCommand>(cmd => cmd.RoutingString == routingKey));

			_buses.Add(bus);
		}
    }

    public async Task<SimulationResult> Run(SimulationContext context)
    {
		context.Start();
	
		var expectedMessageCount = Parameters.RoutingKeys.Count * Parameters.Count;
		var messages = await _channel.Receive(expectedMessageCount).ToListAsync();

		var messagesByReceiverIds = messages.GroupBy(m => m.ReceiverId);

		foreach (var messagesByReceiverId in messagesByReceiverIds)
		{
			var peerId = messagesByReceiverId.Key;
			var receivedMessages = messagesByReceiverId.ToList();

			if (receivedMessages.Count != Parameters.Count)
				return new SimulationResult.Error(new SimulationException($"Peer {peerId} received {receivedMessages.Count} messages, expected {Parameters.Count}"));

			foreach (var (msgEntry, expectedSequence) in receivedMessages.Zip(Enumerable.Range(Parameters.Seq, Parameters.Seq + Parameters.Count)))
			{
				if (msgEntry.Message is not RoutedCommand command)
					throw new InvalidOperationException($"Expected message of type {nameof(RoutedCommand)} got {msgEntry.Message.GetType()} for peer {peerId}");

				if (command.Seq != expectedSequence)
					return new SimulationResult.Error(new SimulationException($"Expected sequence {expectedSequence} got {command.Seq} for peer {peerId}"));
			}
		}


		return new SimulationResult.Success();
    }

    public Task AfterRun(SimulationContext context)
    {
		foreach (var bus in _buses)
			bus.Stop();

		_buses.Clear();
		return Task.CompletedTask;
    }
}
