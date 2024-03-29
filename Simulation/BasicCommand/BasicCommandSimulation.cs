using Abc.Zebus;

namespace Zebus.Morpheus.Simulation.BasicCommand;

public class BasicCommandHandler(IMessageChannel channel) : IAsyncMessageHandler<BasicCommand>
{
    public async Task Handle(BasicCommand message)
    {
		await channel.Send(message);
    }
}

/// <summary>
/// Parameters for <see cref="BasicCommandSimulation" /> simulation
/// </summary>
public class BasicCommandParameters
{
	/// <summary>
	/// Number of commands to send
	/// </summary>
	public int Count { get; set; }

	/// <summary>
	/// Start sequence number
	/// </summary>
	public int Seq { get; set; } = 1;
}

/// <summary>
/// A <see cref="ISimulation" /> that will start a bus and wait for a peer to send a configured
/// number of <see cref="BasicCommand" /> commands.
/// Every <see cref="BasicCommand" /> must include a sequence number, starting from the
/// <see cref="BasicCommandParameters.Seq" /> sequence.
/// Sequence numbers should increment
/// This simulation will ensure that the other peer sent the right <see cref="BasicCommand" /> commands
/// and will check that the sequence number specified in the received command matches the sequence
/// number that was expected
/// </summary>
[Simulation(Name = "basic-command", Parameters = typeof(BasicCommandParameters))]
public class BasicCommandSimulation : ParameteredSimulation<BasicCommandParameters>, ISimulation
{
	private readonly IMessageChannel _channel;
	private IBus? _bus;

	public BasicCommandSimulation()
	{
		_channel = MessageChannel.Create();
	}

    public Task BeforeRun(SimulationContext context)
    {
		 _bus = context
			.CreateBusFactory()
			.WithPeerId("Zebus.Morpheus.Simulation.BasicCommand")
			.WithHandlers(typeof(BasicCommandHandler))
			.ConfigureContainer(cfg => cfg.ForSingletonOf<IMessageChannel>().Use(_channel))
			.CreateBus();

		return Task.CompletedTask;
    }

    public async Task<SimulationResult> Run(SimulationContext context)
    {
		if (_bus is null)
			throw new InvalidOperationException("Bus is null");

		_bus.Start();
		context.Start();

		var messages = await _channel.Receive(Parameters.Count).ToListAsync();

		foreach (var (message, expectedSequence) in messages.Zip(Enumerable.Range(Parameters.Seq, Parameters.Seq + Parameters.Count)))
		{
			if (message is not BasicCommand command)
				return new SimulationResult.Error(new SimulationException($"Invalid command type. Expected {nameof(BasicCommand)} got {message.GetType().Name}"));

			if (command.Sequence != expectedSequence)
				return new SimulationResult.Error(new SimulationException($"Expected sequence {expectedSequence} got {command.Sequence}"));
		}

		return new SimulationResult.Success();
    }

    public Task AfterRun(SimulationContext context)
    {
		_bus?.Stop();
		_bus = null;
		return Task.CompletedTask;
    }

}
