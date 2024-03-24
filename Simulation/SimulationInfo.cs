using System.Reflection;

namespace Zebus.Morpheus.Simulation;

/// <summary>
/// Provides information about a <see cref="ISimulation" />
/// </summary>
/// <param name="type">The <see cref="Type" /> of the simulation</param>
/// <param name="Name">The name of the simulation</param>
/// <param name="ParametersType">
/// When the simulation can be configured with parameters, represents the <see cref="Type" /> of the parameters to
/// configure the simulation
/// </param>
public record SimulationInfo(Type Type, string Name, Type? ParametersType)
{
	public static IEnumerable<SimulationInfo> LoadAll()
		=> LoadAll(typeof(ISimulation).Assembly);

	/// <summary>
	/// Load all simulations from a given <see cref="Assembly" /> <paramref name="assembly" />
	/// </summary>
	public static IEnumerable<SimulationInfo> LoadAll(Assembly assembly)
	{
		return assembly
			.GetTypes()
			.Select(type => new {
				Attribute = (SimulationAttribute?) Attribute.GetCustomAttribute(type, typeof(SimulationAttribute)),
				Type = type
			}).Where(s => {
				return s.Attribute != null || IsSimulation(s.Type);
			}).Select(s => {
			    var name = s.Attribute?.Name ?? GetSimulationName(s.Type);
				return new SimulationInfo(s.Type, name, s.Attribute?.Parameters);
			});
		
		static bool IsSimulation(Type type)
			=> typeof(ISimulation).IsAssignableFrom(type);
	}

	private static string GetSimulationName(Type simulationType)
	{
		const string simulationSuffix = "Simulation";
		
		if (simulationType.Name.EndsWith(simulationSuffix))
			return simulationType.Name.Substring(0, simulationType.Name.Length - simulationSuffix.Length);

		return simulationType.Name;
	}
}
