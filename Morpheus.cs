using Microsoft.Extensions.Logging;
using System.Text.Json;
using Abc.Zebus;
using Zebus.Morpheus.Simulation;
using Pastel;
using System.Drawing;
using Abc.Zebus.Core;

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
		var results = await RunAll(runOptions).ToListAsync();
		ReportSimulationResults(results);
	}

	private async IAsyncEnumerable<SimulationInfoResult> RunAll(Options.RunOptions runOptions)
	{
		var simulations = LoadSimulations(runOptions.Simulations);

		using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole(opts => {
			opts.IncludeScopes = true;
			opts.SingleLine = true;
			opts.TimestampFormat = "[HH:mm:ss] ";
		}));
		ZebusLogManager.LoggerFactory = loggerFactory;

		var logger = loggerFactory.CreateLogger(nameof(Morpheus));

		var busConfiguration = new BusConfiguration { DirectoryServiceEndPoints = runOptions.DirectoryEndpoints };

		using var controlBus = new BusFactory()
			.WithConfiguration(busConfiguration, runOptions.Environment)
			.WithPeerId("Zebus.Oracle")
			.CreateBus();

		controlBus.Start();

		foreach (var (simulation, simulationInfo) in simulations)
		{
			var context = new SimulationContext
			{
				Logger = loggerFactory.CreateLogger(simulationInfo.Type.Name),
				Configuration = busConfiguration,
				Environment = runOptions.Environment,
				Simulation = simulationInfo,
			};

			yield return await RunSimulation(controlBus, logger, context, simulationInfo, simulation);
		}
	}

    private async Task<SimulationInfoResult> RunSimulation(IBus controlBus, ILogger logger, SimulationContext context, SimulationInfo simulationInfo, ISimulation simulation)
	{
		try
		{
			context.OnStarted += () => controlBus.Publish(SimulationStarted.Create(simulation, simulationInfo));
			logger.LogInformation($"Preparing {simulationInfo.Name} ...");

			await simulation.BeforeRun(context);

			logger.LogInformation($"Running {simulationInfo.Name} ...");
			var result = await simulation.Run(context).TimeoutAfter(TimeSpan.FromSeconds(60));

			await simulation.AfterRun(context);
			return new SimulationInfoResult(simulationInfo, result);

		}
		catch (Exception ex)
		{
			return new SimulationInfoResult(simulationInfo, new SimulationResult.Error(ex));
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

    private void ReportSimulationResults(List<SimulationInfoResult> results)
    {
		const int SeparatorPadding = 10;

		var longestSimulationName = results.Max(r => r.Simulation.Name.Length);

		foreach (var result in results)
		{
			var simulation = result.Simulation;
			var (prefix, resultText, errorText) = result.Result switch {
				SimulationResult.Success _ => ("🚀", "OK".Pastel(Color.Green), string.Empty),
				SimulationResult.Error err   => ("✗", "FAILED".Pastel(Color.Red), err.Exception.Message),
				_ => throw new ArgumentOutOfRangeException(nameof(SimulationResult)),
			};

			var padding = (longestSimulationName - simulation.Name.Length) + SeparatorPadding + 2 + resultText.Length;

			if (!string.IsNullOrEmpty(errorText))
				Console.WriteLine($"{prefix} [{simulation.Name}] {resultText.PadLeft(padding)} | {errorText}");
			else
				Console.WriteLine($"{prefix} [{simulation.Name}] {resultText.PadLeft(padding)}");
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

	private record SimulationInfoResult(SimulationInfo Simulation, SimulationResult Result);
}
