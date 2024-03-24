using Microsoft.Extensions.Logging;
using System.Text.Json;
using Abc.Zebus;
using Zebus.Morpheus.Simulation;
using Pastel;
using System.Drawing;

namespace Zebus.Morpheus;

public class Morpheus(Options options)
{
	public async Task Run()
	{
		switch (options)
		{
			case Options.RunOptions runOptions:
				await Run(runOptions);
				break;
			default:
				throw new InvalidOperationException($"Unhandled options {options.GetType().Name}");
		}
	}

	public static async Task Run(string[] args)
		=> await Options.InvokeAsync(args, opts => new Morpheus(opts).Run());

	private async Task Run(Options.RunOptions runOptions)
	{
		var simulations = LoadSimulations(runOptions.Simulations);

		using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
		ZebusLogManager.LoggerFactory = loggerFactory;

		foreach (var (simulation, simulationInfo) in simulations)
		{
			var context = new SimulationContext
			{
				Logger = loggerFactory.CreateLogger(simulationInfo.Type.Name),
				Configuration = new BusConfiguration { DirectoryServiceEndPoints = runOptions.DirectoryEndpoints },
				Environment = runOptions.Environment
			};
			await RunSimulation(context, simulationInfo, simulation);
		}
	}

	private async Task RunSimulation(SimulationContext context, SimulationInfo simulationInfo, ISimulation simulation)
	{
		try
		{
			Console.WriteLine($"running {simulationInfo.Name} ...");
			var result = await simulation.Run(context);
			HandleSimulationResult(simulationInfo, result);
		}
		catch (Exception ex)
		{
			HandleSimulationResult(simulationInfo, new SimulationResult.Error(ex));
		}
	}

    private void HandleSimulationResult(SimulationInfo simulationInfo, SimulationResult result)
    {
		if (result is SimulationResult.Success)
		{
			Console.WriteLine($"[{simulationInfo.Name}]         ... {"OK".Pastel(Color.Green)}");
		}
		else if (result is SimulationResult.Error error)
		{
		    var errorColor = simulationInfo.Name.Pastel(Color.Red);
			Console.WriteLine($"[{simulationInfo.Name}]         ... {"FAILED".Pastel(Color.Red)}");
			Console.WriteLine($"[{errorColor}] {error.Exception.Message}");
		}
    }

    private static IEnumerable<(ISimulation, SimulationInfo)> LoadSimulations(FileInfo simulationsFile)
	{
		using var fileStream = simulationsFile.OpenRead();

		var descriptors = SimulationInfo.LoadAll().Select(SimulationDescriptor.Create).ToList();

		var document = JsonDocument.Parse(fileStream);
		foreach (var property in document.RootElement.EnumerateObject())
		{
			var simulationName = property.Name;
		
			var descriptor = descriptors.FirstOrDefault(d => d.Is(simulationName));

			if (descriptor is null)
				throw new InvalidOperationException($"Unknown simulation {property.Name}");

			var simulation = descriptor.Deserialize(property.Value);
			yield return (simulation, descriptor.SimulationInfo);
			
		}
	}

	private class SimulationDescriptor
	{
		public required SimulationInfo SimulationInfo { get; init; }

		public required ISimulationParametersDeserializer? ParametersDeserializer { get; init; }

		public bool Is(string name)
			=> string.Equals(SimulationInfo.Name, name, StringComparison.OrdinalIgnoreCase);

		public static SimulationDescriptor Create(SimulationInfo simulationInfo)
		{
			return new()
			{
				SimulationInfo = simulationInfo,
				ParametersDeserializer = GenerateParametersDeserializer(simulationInfo.Type),
			 };
		}

		public ISimulation Deserialize(JsonElement jsonElement)
		{
			var simulation = (ISimulation?) Activator.CreateInstance(SimulationInfo.Type)
				 ?? throw new InvalidOperationException($"Failed to create an instance of {SimulationInfo.Type}");

			if (ParametersDeserializer is { } parametersDeserializer)
				parametersDeserializer.DeserializeInto(simulation, jsonElement);

			return simulation;
		}

		private static ISimulationParametersDeserializer? GenerateParametersDeserializer(Type simulationType)
		{
			Type? currentType = simulationType;
			while (currentType != null && currentType != typeof(object))
			{
				if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(ParameteredSimulation<>)) {
					var parametersType = currentType.GenericTypeArguments[0];
					var deserializer = (ISimulationParametersDeserializer?)
						Activator.CreateInstance(typeof(SimulationParametersDeserializer<>).MakeGenericType(parametersType))
							?? throw new InvalidOperationException($"Failed to create deserialize for parameters of {parametersType}");
					return deserializer;
					
				}

				currentType = currentType.BaseType;
			}

			return null;
		}
	}

	private interface ISimulationParametersDeserializer
	{
		void DeserializeInto(ISimulation simulation, JsonElement element);
	}

	private class SimulationParametersDeserializer<TParameters> : ISimulationParametersDeserializer
		where TParameters: new()
	{

        public void DeserializeInto(ISimulation simulation, JsonElement element)
        {
			var parameteredSimulation = (ParameteredSimulation<TParameters>) simulation;
		    parameteredSimulation.Parameters = element.Deserialize<TParameters>()
				 ?? throw new InvalidOperationException($"Failed to deserialize parameters of type {typeof(TParameters)} from {element}");
        }
    }
}
