using System;
using System.Collections.Generic;
using PicoStackables.Network;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PicoStackables;

public class PicoStackablesModSystem : ModSystem
{
    private const string ConfigFilename = "picostackables.json";

    private ICoreAPI           api     = null!;
    private ICoreServerAPI     sapi    = null!;
    private PicoStackablesConfig config = new();

    private IServerNetworkChannel serverChannel = null!;

    private readonly Dictionary<string, int> originalItemStacks  = new();
    private readonly Dictionary<string, int> originalBlockStacks = new();
    private bool originalsRecorded;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        this.api  = sapi;
        this.sapi = sapi;

        LoadConfig();

        serverChannel = sapi.Network
            .RegisterChannel(Channel.Name)
            .RegisterMessageType<StackablesInitPacket>()
            .RegisterMessageType<StackablesSavePacket>()
            .RegisterMessageType<StackablesAckPacket>()
            .SetMessageHandler<StackablesSavePacket>(OnSaveReceived);

        sapi.Event.PlayerNowPlaying += OnPlayerNowPlaying;

#if CONFIGLIB
        ConfigLibIntegration.Register(sapi, config, OnConfigReloaded);
#endif
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        RecordOriginals(api);
        ApplyConfig(api);
    }

    // -------------------------------------------------------------------------
    // Network handlers
    // -------------------------------------------------------------------------

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        serverChannel.SendPacket(BuildInitPacket(), player);
    }

    private void OnSaveReceived(IServerPlayer player, StackablesSavePacket packet)
    {
        if (!player.HasPrivilege(Privilege.controlserver))
        {
            serverChannel.SendPacket(
                new StackablesAckPacket { Success = false, ErrorMessage = "Insufficient privilege." },
                player);
            return;
        }

        config.GlobalMultiplier = Math.Max(0.01f, packet.GlobalMultiplier);
        config.ItemOverrides    = packet.ItemOverrides  ?? new();
        config.BlockOverrides   = packet.BlockOverrides ?? new();

        api.StoreModConfig(config, ConfigFilename);
        ApplyConfig(api);

        // Push refreshed init data to all online players so their dialogs stay in sync
        var initPacket = BuildInitPacket();
        foreach (var p in sapi.World.AllOnlinePlayers)
            serverChannel.SendPacket(initPacket, p as IServerPlayer);

        serverChannel.SendPacket(new StackablesAckPacket { Success = true }, player);
        api.Logger.Notification("[PicoStackables] Config saved by {0}.", player.PlayerName);
    }

    // -------------------------------------------------------------------------
    // Config helpers
    // -------------------------------------------------------------------------

    private void LoadConfig()
    {
        config = api.LoadModConfig<PicoStackablesConfig>(ConfigFilename) ?? new();
        api.StoreModConfig(config, ConfigFilename);
    }

    private void OnConfigReloaded(PicoStackablesConfig newConfig)
    {
        config = newConfig;
        api.StoreModConfig(config, ConfigFilename);
        ApplyConfig(api);
        api.Logger.Notification("[PicoStackables] Config reloaded via ConfigLib.");
    }

    private StackablesInitPacket BuildInitPacket() => new()
    {
        GlobalMultiplier    = config.GlobalMultiplier,
        ItemOverrides       = new Dictionary<string, int>(config.ItemOverrides),
        BlockOverrides      = new Dictionary<string, int>(config.BlockOverrides),
        OriginalItemStacks  = new Dictionary<string, int>(originalItemStacks),
        OriginalBlockStacks = new Dictionary<string, int>(originalBlockStacks),
    };

    // -------------------------------------------------------------------------
    // Stack-size logic
    // -------------------------------------------------------------------------

    private void RecordOriginals(ICoreAPI worldApi)
    {
        if (originalsRecorded) return;
        originalsRecorded = true;

        foreach (var item in worldApi.World.Items)
        {
            if (item?.Code == null) continue;
            originalItemStacks[item.Code.ToString()] = item.MaxStackSize;
        }

        foreach (var block in worldApi.World.Blocks)
        {
            if (block?.Code == null) continue;
            originalBlockStacks[block.Code.ToString()] = block.MaxStackSize;
        }
    }

    private void ApplyConfig(ICoreAPI worldApi)
    {
        float mult = Math.Max(0.01f, config.GlobalMultiplier);

        foreach (var item in worldApi.World.Items)
        {
            if (item?.Code == null) continue;
            string code = item.Code.ToString();

            item.MaxStackSize = config.ItemOverrides.TryGetValue(code, out int ov)
                ? Math.Max(1, ov)
                : originalItemStacks.TryGetValue(code, out int orig)
                    ? Math.Max(1, (int)Math.Round(orig * mult))
                    : item.MaxStackSize;
        }

        foreach (var block in worldApi.World.Blocks)
        {
            if (block?.Code == null) continue;
            string code = block.Code.ToString();

            block.MaxStackSize = config.BlockOverrides.TryGetValue(code, out int ov)
                ? Math.Max(1, ov)
                : originalBlockStacks.TryGetValue(code, out int orig)
                    ? Math.Max(1, (int)Math.Round(orig * mult))
                    : block.MaxStackSize;
        }
    }
}
