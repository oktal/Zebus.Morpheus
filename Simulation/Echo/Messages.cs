using Abc.Zebus;
using ProtoBuf;

namespace Zebus.Morpheus.Simulation.Echo;

[ProtoContract]
public class EchoCommand : ICommand
{
    [ProtoMember(1)]
    public string Text { get; set; }

    public EchoCommand(string text)
    {
        Text = text;
    }
}

[ProtoContract]
public class EchoResponse : IMessage
{
    [ProtoMember(1)]
    public required string Text { get; set; }
}
