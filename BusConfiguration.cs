using Abc.Zebus;

namespace Zebus.Morpheus;

public class BusConfiguration : IBusConfiguration
{
    public string[] DirectoryServiceEndPoints { get; set; } = [];

    public TimeSpan RegistrationTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan FaultedDirectoryRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan StartReplayTimeout { get; set; } = TimeSpan.FromMinutes(1);

    public bool IsPersistent { get; set; } = false;

    public bool IsDirectoryPickedRandomly { get; set; } = true;

    public bool IsErrorPublicationEnabled { get; set; } = false;

    public int MessagesBatchSize { get; set; } = 100;
}
