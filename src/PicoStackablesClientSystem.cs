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

        capi.RegisterCommand(
            "picostackables",
            "Open the PicoStackables config dialog",
            "",
            (groupId, args) => ToggleDialog());
    }

    internal void SendSave(StackablesSavePacket packet) => clientChannel.SendPacket(packet);

    private void OnInitReceived(StackablesInitPacket packet)
    {
        ServerData = packet;
        dialog?.RefreshFromData(packet);
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
