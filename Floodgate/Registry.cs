using System.Collections.Generic;
using System.IO;

namespace Floodgate;

public static class Registry
{
    public static List<RegisteredMod> Mods { get; private set; } = new();
    public static void Apply()
    {
        foreach(ModManager.Mod mod in ModManager.ActiveMods)
        {
            if(mod == null) {  continue; }
            string floodgatepath = Path.Combine(mod.TargetedPath, "floodgate");
            if (mod.hasTargetedVersionFolder && Directory.Exists(floodgatepath))
            {
                Mods.Add(new(floodgatepath, mod));
            }
            floodgatepath = Path.Combine(mod.NewestPath, "floodgate");
            if (mod.hasNewestFolder && Directory.Exists(floodgatepath))
            {
                Mods.Add(new(floodgatepath, mod));
            }
            floodgatepath = Path.Combine(mod.path, "floodgate");
            if (Directory.Exists(floodgatepath))
            {
                Mods.Add(new(floodgatepath, mod));
            }
        }
    }



    public class RegisteredMod
    {
        public string floodgate;
        public string id;
        public ModManager.Mod mod;

        public RegisteredMod(string floodgatepath, ModManager.Mod mod)
        {
            floodgate = floodgatepath;
            id = mod.id;
            this.mod = mod;
            Plugin.logger.LogDebug("Registered Mod " + mod.name + " (" + id + ")");
        }
    }
}
