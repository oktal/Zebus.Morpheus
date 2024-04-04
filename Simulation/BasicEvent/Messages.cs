using Abc.Zebus;
using ProtoBuf;

namespace Zebus.Morpheus.Simulation.BasicEvent;

[ProtoContract]
public class BasicEvent : IEvent
{
    [ProtoMember(1)]
    public int Seq { get; set; }
}
