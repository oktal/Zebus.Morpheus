using Abc.Zebus;
using Abc.Zebus.Core;
using Microsoft.Extensions.Logging;

namespace Zebus.Morpheus.Simulation;

public class SimulationContext
{
	public required ILogger Logger { get;  init; }
	public required IBusConfiguration Configuration { get; init; }
	public required string Environment { get; init; }

	public IBus CreateBus(string? peerId = null, string? endpoint = null)
	{
		var busFactory = new BusFactory()
			.WithConfiguration(Configuration, Environment)
			.WithScan()
			.WithEndpoint(endpoint ?? "tcp://*:*")
			.WithPeerId(peerId ?? "Abc.Morpheus.*");


		return busFactory.CreateBus();
	}
}

/// <summary>
/// Result of a simulation
/// </summary>
public abstract record SimulationResult
{
	public record Success() : SimulationResult;
	public record Error(Exception Exception) : SimulationResult;

	public static SimulationResult CreateError(int errorCode)
		=> new Error(new SimulationException($"Command returned error {errorCode}"));
}

public class SimulationException : Exception
{
	public SimulationException(string message)
		: base(message)
	{
	}
}

/// <summary>
/// Base interface for a simulation
///
/// A simulation will replicate a scenario in a controlled environment
/// to validate that the behavior of a Zebus implementation corresponds to the
/// behavior expected by the simulation
/// </summary>
public interface ISimulation
{
	/// <summary>
	/// Run the simulation
	/// </summary>
	Task<SimulationResult> Run(SimulationContext context);
}

public abstract class ParameteredSimulation<TParameters>
	where TParameters: new()
{
	public TParameters Parameters { get; set; } = new();
}
