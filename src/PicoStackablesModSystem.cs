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
    private bool configLoaded;

    /// <summary>
    /// Collectibles (by code path) that are managed even though the normal
    /// exclusion rules would drop them — e.g. items with a vanilla stack size of
    /// 1 that players still want to be able to stack.
    /// </summary>
    private static readonly HashSet<string> ForceManagePaths = new(StringComparer.Ordinal)
    {
        "gear-temporal",   // Temporal gear — vanilla stack size 1, opted in on request
    };

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        this.api  = sapi;
        this.sapi = sapi;

        EnsureConfigLoaded();

        serverChannel = sapi.Network
            .RegisterChannel(Channel.Name)
            .RegisterMessageType<StackablesInitPacket>()
            .RegisterMessageType<StackablesSavePacket>()
            .RegisterMessageType<StackablesAckPacket>()
            .SetMessageHandler<StackablesSavePacket>(OnSaveReceived);

        sapi.Event.PlayerNowPlaying += OnPlayerNowPlaying;
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        // AssetsFinalize can run before StartServerSide in the mod lifecycle, so
        // make sure the saved config is loaded here before we apply it — otherwise
        // a fresh world would come up with default stack sizes until the player
        // re-saved from the dialog.
        this.api ??= api;
        EnsureConfigLoaded();

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
        config.UseFlatSize      = packet.UseFlatSize;
        config.FlatStackSize    = Math.Max(1, packet.FlatStackSize);
        config.PreventShrinking = packet.PreventShrinking;

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

    private void EnsureConfigLoaded()
    {
        if (configLoaded) return;
        config = api.LoadModConfig<PicoStackablesConfig>(ConfigFilename) ?? new();
        api.StoreModConfig(config, ConfigFilename);
        configLoaded = true;
    }

    private StackablesInitPacket BuildInitPacket() => new()
    {
        GlobalMultiplier    = config.GlobalMultiplier,
        ItemOverrides       = new Dictionary<string, int>(config.ItemOverrides),
        BlockOverrides      = new Dictionary<string, int>(config.BlockOverrides),
        OriginalItemStacks  = new Dictionary<string, int>(originalItemStacks),
        OriginalBlockStacks = new Dictionary<string, int>(originalBlockStacks),
        UseFlatSize         = config.UseFlatSize,
        FlatStackSize       = config.FlatStackSize,
        PreventShrinking    = config.PreventShrinking,
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
            if (!ShouldManage(item)) continue;
            originalItemStacks[item.Code.ToString()] = item.MaxStackSize;
        }

        foreach (var block in worldApi.World.Blocks)
        {
            if (!ShouldManage(block)) continue;
            originalBlockStacks[block.Code.ToString()] = block.MaxStackSize;
        }
    }

    /// <summary>
    /// Whether this collectible's stack size should be managed by the mod.
    /// Excludes things players never freely stack in their inventory so they
    /// neither get rescaled nor clutter the config dialog.
    /// </summary>
    private static bool ShouldManage(CollectibleObject obj)
    {
        if (obj?.Code == null) return false;

        // Explicit opt-ins win over every exclusion rule below.
        if (ForceManagePaths.Contains(obj.Code.Path)) return true;

        // Leave unstackable items unstackable: tools, weapons and armour have a
        // vanilla stack size of 1 (and/or durability). Multiplying that would
        // wrongly turn a single non-stackable item into a stack.
        if (obj.MaxStackSize <= 1) return false;
        if (obj.Durability    > 1) return false;

        // Internal / non-collectible placeholders players never carry:
        //   game:air / game:item-air – empty-slot placeholder
        //   game:creature-*          – the creative "spawn creature" items
        //                              (traders, wildlife, monsters, tamed animals…)
        string path = obj.Code.Path;
        if (path == "air" || path == "item-air") return false;
        if (path.StartsWith("creature", StringComparison.Ordinal)) return false;

        return true;
    }

    private void ApplyConfig(ICoreAPI worldApi)
    {
        float mult = Math.Max(0.01f, config.GlobalMultiplier);

        foreach (var item in worldApi.World.Items)
        {
            if (item?.Code == null) continue;
            string code = item.Code.ToString();
            if (!originalItemStacks.TryGetValue(code, out int orig)) continue; // unmanaged

            bool hasOv = config.ItemOverrides.TryGetValue(code, out int ov);
            item.MaxStackSize = StackSizeCalc.Final(
                orig, hasOv, ov,
                config.UseFlatSize, config.FlatStackSize, mult, config.PreventShrinking);
        }

        foreach (var block in worldApi.World.Blocks)
        {
            if (block?.Code == null) continue;
            string code = block.Code.ToString();
            if (!originalBlockStacks.TryGetValue(code, out int orig)) continue; // unmanaged

            bool hasOv = config.BlockOverrides.TryGetValue(code, out int ov);
            block.MaxStackSize = StackSizeCalc.Final(
                orig, hasOv, ov,
                config.UseFlatSize, config.FlatStackSize, mult, config.PreventShrinking);
        }
    }
}
