using Microsoft.Extensions.Logging;

namespace Zebus.Morpheus.Simulation.Echo;

public class EchoParameters
{
    /// <summary>
    /// Total number of <see cref="EchoCommand" /> to send
    /// </summary>
    public int Count { get; set; }

    public override string ToString()
        => $"Count={Count}";
}

/// <summary>
/// A <see cref="ISimulation" /> that sends a command with a text to echo back as a response to the command
///
/// This simulation will send a configured number of <see cref="EchoCommand" /> to the service and wait for
/// all the responses to come back.
/// The simulation will validate that the text that has been echoe'd back in the <see cref="EchoResponse" />
/// matches the original text that was sent
/// </summary>
[Simulation(Name = "echo", Parameters = typeof(EchoParameters))]
public class EchoSimulation : ParameteredSimulation<EchoParameters>, ISimulation
{
    public Task BeforeRun(SimulationContext context)
        => Task.CompletedTask;

    public async Task<SimulationResult> Run(SimulationContext context)
    {
        using var bus = context.CreateBus("Zebus.Morpheus.Echo.Simulation");

        bus.Start();
        context.Start();

        for (var i = 0; i < Parameters.Count; ++i)
        {
            var echoText = $"Echo {i + 1} from Morpheus";

            context.Logger.LogDebug($"Echoing {echoText}");

            var response = await bus.Send(new EchoCommand(echoText));

            if (!response.IsSuccess)
                return SimulationResult.CreateError(response.ErrorCode);

            if (response.Response is EchoResponse { } echoResponse)
            {
                if (echoResponse.Text != echoText)
                    return new SimulationResult.Error(new SimulationException($"Expected {echoText} response, got {echoResponse.Text}"));
            }
            else
            {
                return new SimulationResult.Error(
                    new SimulationException($"Invalid response, expected {nameof(EchoResponse)} message"));
            }
        }

        return new SimulationResult.Success();
    }

    public Task AfterRun(SimulationContext context)
        => Task.CompletedTask;
}
