#if CONFIGLIB
using System;
using Vintagestory.API.Server;

// Compiled only when CONFIGLIB is defined in the .csproj (see instructions there).
// Registers PicoStackables config with ConfigLib so server operators can also
// edit it through ConfigLib's built-in config panel.

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
