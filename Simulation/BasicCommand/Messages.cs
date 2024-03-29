using Abc.Zebus;
using ProtoBuf;

namespace Zebus.Morpheus.Simulation.BasicCommand;

[ProtoContract]
public class BasicCommand : ICommand
{
	[ProtoMember(1)]
	public required int Sequence { get; set; }
}
