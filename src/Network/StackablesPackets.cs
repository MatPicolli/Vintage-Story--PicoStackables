using System.Collections.Generic;
using ProtoBuf;

namespace PicoStackables.Network;

public static class Channel
{
    public const string Name = "picostackables";
}

/// <summary>
/// Server → Client: sent on player join.
/// Contains current config and the vanilla (pre-mod) stack sizes needed
/// to render the "original → new" preview without the client needing to
/// know what the server already multiplied.
/// </summary>
[ProtoContract]
public class StackablesInitPacket
{
    [ProtoMember(1)] public float GlobalMultiplier { get; set; }
    [ProtoMember(2)] public Dictionary<string, int> ItemOverrides  { get; set; } = new();
    [ProtoMember(3)] public Dictionary<string, int> BlockOverrides { get; set; } = new();
    // Original (vanilla) values before any mod multiplication
    [ProtoMember(4)] public Dictionary<string, int> OriginalItemStacks  { get; set; } = new();
    [ProtoMember(5)] public Dictionary<string, int> OriginalBlockStacks { get; set; } = new();
}

/// <summary>
/// Client → Server: save request with the new desired config.
/// </summary>
[ProtoContract]
public class StackablesSavePacket
{
    [ProtoMember(1)] public float GlobalMultiplier { get; set; }
    [ProtoMember(2)] public Dictionary<string, int> ItemOverrides  { get; set; } = new();
    [ProtoMember(3)] public Dictionary<string, int> BlockOverrides { get; set; } = new();
}

/// <summary>
/// Server → Client: acknowledge a save request.
/// </summary>
[ProtoContract]
public class StackablesAckPacket
{
    [ProtoMember(1)] public bool   Success      { get; set; }
    [ProtoMember(2)] public string ErrorMessage { get; set; } = "";
}
