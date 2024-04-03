using Abc.Zebus;

namespace Zebus.Morpheus.Simulation.BasicEvent;

public record MessageEntry(IMessage Message, PeerId ReceiverId);

public class BasicEventHandler(IAsyncChannel<MessageEntry> channel, IBus bus) : IAsyncMessageHandler<BasicEvent>
{
    public async Task Handle(BasicEvent message)
    {
		await channel.Send(new MessageEntry(message, bus.PeerId));
    }
}

public class BasicEventParameters
{
	/// <summary>
	/// Total number of peers to start
	/// </summary>
	public int PeerCount { get; set; }

	/// <summary>
	/// Start sequence number of the event
	/// </summary>
	public int Seq { get; set; }

	/// <summary>
	/// Total number of events to receive per peer
	/// </summary>
	public int Count { get; set; }
}

/// <summary>
/// A <see cref="ISimulation" /> that will start a configured number of peers and wait for a number of events
/// to be received by each receiving peer.
/// Each received event should be a <see cref="BasicEvent" /> properly sequence with an increasing sequence number
/// </summary>
[Simulation(Name = "basic-event", Parameters = typeof(BasicEventParameters))]
public class BasicEventSimulation : ParameteredSimulation<BasicEventParameters>, ISimulation
{
	private readonly IAsyncChannel<MessageEntry> _channel = AsyncChannel<MessageEntry>.Create();
	private List<IBus> _buses = [];

    public Task BeforeRun(SimulationContext context)
    {
		var buses = Enumerable.Range(0, Parameters.PeerCount).Select(idx => {
			var bus = context
				.CreateBusFactory()
				.WithPeerId($"Zebus.Morpheus.BasicEvent.Simulation.{idx}")
				.WithHandlers(typeof(BasicEventHandler))
				.ConfigureContainer(cfg => cfg.ForSingletonOf<IAsyncChannel<MessageEntry>>().Use(_channel))
				.CreateBus();

			return bus;
		}).ToList();

		foreach (var bus in buses)
			bus.Start();

		_buses = buses;
		return Task.CompletedTask;
    }

    public async Task<SimulationResult> Run(SimulationContext context)
    {
		context.Start();

		var expectedMessageCount = Parameters.PeerCount * Parameters.Count;
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
				if (msgEntry.Message is not BasicEvent basicEvent)
					throw new InvalidOperationException($"Expected message of type {nameof(BasicEvent)} got {msgEntry.Message.GetType()} for peer {peerId}");

				if (basicEvent.Seq != expectedSequence)
					return new SimulationResult.Error(new SimulationException($"Expected sequence {expectedSequence} got {basicEvent.Seq} for peer {peerId}"));
			}
		}


		return new SimulationResult.Success();
    }

    public Task AfterRun(SimulationContext context)
    {
		foreach (var bus in _buses)
			bus.Stop();

		return Task.CompletedTask;
    }

}
