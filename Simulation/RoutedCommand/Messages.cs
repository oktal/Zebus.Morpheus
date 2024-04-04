using Abc.Zebus;
using Abc.Zebus.Routing;
using ProtoBuf;

namespace Zebus.Morpheus.Simulation.RoutedCommand;

[ProtoContract]
[Routable]
public class RoutedCommand : ICommand
{
    [ProtoMember(1)]
    [RoutingPosition(1)]
    public required string RoutingString { get; set; }

    [ProtoMember(2)]
    public int Seq { get; set; }
}
