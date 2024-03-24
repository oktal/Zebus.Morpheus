namespace Zebus.Morpheus;

using System.CommandLine;

/// <summary>
/// Command line parameters for Morpheus
/// </summary>
public class Options
{
	internal static async Task InvokeAsync(string[] args, Func<Options, Task> handler)
	{
		var rootCommand = new RootCommand("Morpheus command line interface");
		rootCommand.Add(RunOptions.CreateCommand(handler));
		await rootCommand.InvokeAsync(args);
	}

	public class RunOptions : Options
	{
	    /// <summary>
		/// Configuration file of the simulations to run
		/// </summary>
	    public required FileInfo Simulations { get; init; }

		/// <summary>
		/// List of  of the directory peer endpoints
		/// </summary>
		public required string[] DirectoryEndpoints { get; init; }

		/// <summary>
		/// Environment of the bus
		/// </summary>
		public required string Environment { get; init; }

		/// <summary>
		/// The inbound endpoint
		/// </summary>
		public required string Endpoint { get; init; }

		/// <summary>
		/// PeerId to register with
		/// </summary>
		public required string PeerId { get; init;}

		internal static Command CreateCommand(Func<Options, Task> handler)
		{
			var command = new Command("run", "Run simulations");

			var simulationsOption = new Option<FileInfo>(
				name: "--simulations",
				description: "A file containing configuration of simulations to run"
			);

			var directoryEndpointsOption = new Option<List<string>>(
				name: "--directory-endpoints",
				description: "A list of endpoint for the directory peers"
			) { IsRequired = true };

			var environmentOption = new Option<string>(
				name: "--environment",
				description: "The environment on which to start the bus"
			) { IsRequired = true };

			var endpointOption = new Option<string>(
				name: "--endpoint",
				description: "The inbound endpoint",
				getDefaultValue: () => "tcp://*:*"
			);

			var peerIdOption = new Option<string>(
				name: "--peer-id",
				description: "The environment on which to start the bus",
				getDefaultValue: () => "Morpheus.*"
			);

			command.Add(simulationsOption);
			command.Add(directoryEndpointsOption);
			command.Add(environmentOption);
			command.Add(endpointOption);
			command.Add(peerIdOption);

			command.SetHandler(async (simulations, directoryEndpoints, environment, endpoint, peerId) => {
				var options = new RunOptions
				{ 
					Simulations = simulations,
					DirectoryEndpoints = directoryEndpoints.ToArray(),
					Environment = environment,
					Endpoint = endpoint,
					PeerId = peerId,
				};

				await handler.Invoke(options);
			}, simulationsOption, directoryEndpointsOption, environmentOption, endpointOption, peerIdOption);


			return command;
		}
	}

}
