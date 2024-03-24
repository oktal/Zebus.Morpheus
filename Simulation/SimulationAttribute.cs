namespace Zebus.Morpheus.Simulation;

[AttributeUsage(AttributeTargets.Class)]
public class SimulationAttribute : Attribute
{
	/// <summary>
	/// Optional name of the simulation
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// When a simulation can be configured with parameters, represents the <see cref="Type" /> of the parameters
	/// class
	/// </summary>
	public Type? Parameters { get; set; }
}
