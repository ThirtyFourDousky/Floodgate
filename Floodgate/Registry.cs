using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Floodgate;

public static class Registry
{
    public static readonly List<RegisteredMod> Mods = new();
    public static void Apply()
    {
        Rescan();
    }

    public static void Rescan()
    {
        Mods.Clear();
        foreach (ModManager.Mod mod in ModManager.ActiveMods)
        {
            if (mod == null || Mods.Any(i=>i.mod.id == mod.id)) { continue; }
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
            FloodgatePatcher.CustomLog.Log("Registered Mod " + mod.name + " (" + id + ")");
        }
    }
}
