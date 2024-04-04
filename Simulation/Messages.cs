using Abc.Zebus;
using ProtoBuf;

namespace Zebus.Morpheus.Simulation;

/// <summary>
/// Event raised when a simulation started and is ready to run
/// </summary>
[ProtoContract]
public class SimulationStarted : IEvent
{
    /// <summary>
    /// Name of the simulation
    /// </summary>
    [ProtoMember(1)]
    public required string Name { get; set; }

    /// <summary>
    /// JSON-encoded representation of the parameters of the simulation
    /// </summary>
    [ProtoMember(2)]
    public required string ParametersJson { get; set; }

    internal static SimulationStarted Create(ISimulation simulation, SimulationInfo simulationInfo)
    {
        return new SimulationStarted
        {
            Name = simulationInfo.Name,
            ParametersJson = SerializeParameters(),
        };

        string SerializeParameters()
        {
            return simulation switch
            {
                IParameteredSimulation parameteredSimulation => parameteredSimulation.ToJson(),
                _ => string.Empty,
            };
        }

    }

}
