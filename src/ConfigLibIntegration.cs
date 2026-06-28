#if CONFIGLIB
using System;
using Vintagestory.API.Server;

// Wraps the ConfigLib API so the main mod system stays clean.
// This file is only compiled when the CONFIGLIB constant is defined in the .csproj.
// See PicoStackables.csproj for instructions on how to enable it.

namespace PicoStackables;

internal static class ConfigLibIntegration
{
    internal static void Register(
        ICoreServerAPI sapi,
        PicoStackablesConfig current,
        Action<PicoStackablesConfig> onReload)
    {
        var configLib = sapi.ModLoader.GetModSystem<ConfigLib.ConfigLibModSystem>();
        if (configLib == null) return;

        configLib.RegisterCustomConfig(
            "PicoStackables",
            current,
            (newConfig) =>
            {
                if (newConfig is PicoStackablesConfig typed)
                    onReload(typed);
            });
    }
}
#endif
