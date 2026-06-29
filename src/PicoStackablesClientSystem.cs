using System;
using PicoStackables.Gui;
using PicoStackables.Network;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PicoStackables;

public class PicoStackablesClientSystem : ModSystem
{
    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

    private ICoreClientAPI      capi          = null!;
    private IClientNetworkChannel clientChannel = null!;
    private GuiDialogStackables? dialog;

    internal StackablesInitPacket? ServerData { get; private set; }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        this.capi = capi;

        clientChannel = capi.Network
            .RegisterChannel(Channel.Name)
            .RegisterMessageType<StackablesInitPacket>()
            .RegisterMessageType<StackablesSavePacket>()
            .RegisterMessageType<StackablesAckPacket>()
            .SetMessageHandler<StackablesInitPacket>(OnInitReceived)
            .SetMessageHandler<StackablesAckPacket>(OnAckReceived);

        capi.ChatCommands
            .Create("picostackables")
            .WithDescription("Open the PicoStackables stack size config dialog")
            .HandleWith(_ => { ToggleDialog(); return TextCommandResult.Success(); });
    }

    internal void SendSave(StackablesSavePacket packet) => clientChannel.SendPacket(packet);

    private void OnInitReceived(StackablesInitPacket packet)
    {
        ServerData = packet;
        ApplyToWorld(packet);
        dialog?.RefreshFromData(packet);
    }

    /// <summary>
    /// Apply the server's stack-size config to this client's own item/block
    /// registry. The server-side ModSystem only runs on the server, so on a
    /// remote multiplayer client nothing would otherwise update MaxStackSize and
    /// the client would keep enforcing vanilla stack limits. The math mirrors
    /// PicoStackablesModSystem.ApplyConfig exactly, computed from the vanilla
    /// originals carried in the packet so it is idempotent across re-sends.
    /// </summary>
    private void ApplyToWorld(StackablesInitPacket data)
    {
        float mult = Math.Max(0.01f, data.GlobalMultiplier);

        foreach (var item in capi.World.Items)
        {
            if (item?.Code == null) continue;
            string code = item.Code.ToString();

            if (data.ItemOverrides.TryGetValue(code, out int ov))
                item.MaxStackSize = Math.Max(1, ov);
            else if (data.OriginalItemStacks.TryGetValue(code, out int orig))
                item.MaxStackSize = Math.Max(1, (int)Math.Round(orig * mult));
        }

        foreach (var block in capi.World.Blocks)
        {
            if (block?.Code == null) continue;
            string code = block.Code.ToString();

            if (data.BlockOverrides.TryGetValue(code, out int ov))
                block.MaxStackSize = Math.Max(1, ov);
            else if (data.OriginalBlockStacks.TryGetValue(code, out int orig))
                block.MaxStackSize = Math.Max(1, (int)Math.Round(orig * mult));
        }
    }

    private void OnAckReceived(StackablesAckPacket packet)
    {
        if (!packet.Success)
            capi.ShowChatMessage($"[PicoStackables] Save failed: {packet.ErrorMessage}");
        // success is silent — the dialog already shows the new values
    }

    private void ToggleDialog()
    {
        if (dialog?.IsOpened() == true)
        {
            dialog.TryClose();
            return;
        }

        if (ServerData == null)
        {
            capi.ShowChatMessage("[PicoStackables] Still waiting for server data…");
            return;
        }

        dialog = new GuiDialogStackables(capi, this);
        dialog.TryOpen();
    }
}
