using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PicoStackables;

public class PicoStackablesModSystem : ModSystem
{
    private const string ConfigFilename = "picostackables.json";

    private ICoreAPI api = null!;
    private PicoStackablesConfig config = new();

    // Tracks the original stack sizes so we can re-apply after a config reload
    // without compounding multipliers across multiple applications.
    private System.Collections.Generic.Dictionary<string, int> originalItemStacks = new();
    private System.Collections.Generic.Dictionary<string, int> originalBlockStacks = new();
    private bool originalsRecorded;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        this.api = sapi;
        LoadConfig();

#if CONFIGLIB
        ConfigLibIntegration.Register(sapi, config, OnConfigReloaded);
#endif
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        // AssetsFinalize runs after all JSON patches have been applied and the
        // item/block registries are fully populated – the right moment to tweak
        // MaxStackSize values.
        RecordOriginals(api);
        ApplyConfig(api);
    }

    // -------------------------------------------------------------------------
    // Config helpers
    // -------------------------------------------------------------------------

    private void LoadConfig()
    {
        config = api.LoadModConfig<PicoStackablesConfig>(ConfigFilename) ?? new PicoStackablesConfig();
        api.StoreModConfig(config, ConfigFilename);
    }

    private void OnConfigReloaded(PicoStackablesConfig newConfig)
    {
        config = newConfig;
        api.StoreModConfig(config, ConfigFilename);
        // Re-apply from originals so changes take effect immediately.
        ApplyConfig(api);
        api.Logger.Notification("[PicoStackables] Config reloaded – stack sizes updated.");
    }

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

            if (config.ItemOverrides.TryGetValue(code, out int overrideSize))
            {
                item.MaxStackSize = Math.Max(1, overrideSize);
            }
            else if (originalItemStacks.TryGetValue(code, out int original))
            {
                item.MaxStackSize = Math.Max(1, (int)Math.Round(original * mult));
            }
        }

        foreach (var block in worldApi.World.Blocks)
        {
            if (block?.Code == null) continue;
            string code = block.Code.ToString();

            if (config.BlockOverrides.TryGetValue(code, out int overrideSize))
            {
                block.MaxStackSize = Math.Max(1, overrideSize);
            }
            else if (originalBlockStacks.TryGetValue(code, out int original))
            {
                block.MaxStackSize = Math.Max(1, (int)Math.Round(original * mult));
            }
        }
    }
}
