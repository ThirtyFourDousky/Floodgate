using Menu.Remix;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Floodgate.UI;

public static class RemixModList
{
    //this may not be that accurate
    public static DateTime watcherRelease = new DateTime(2025, 3, 28);
    public static TimeSpan elapsedTime => new DateTime(2025, 5, 16) - watcherRelease;
    //readonly, no need to assign twice
    public static readonly ConditionalWeakTable<MenuModList.ModButton, InfoDot> InfoDots = new();

    static bool applied = false;
    public static void Apply()
    {
        if (applied) { return; }

        On.Menu.Remix.MenuModList.ModButton.ctor += ModButton_ctor;
        On.Menu.Remix.MenuModList.ModButton.GrafUpdate += ModButton_GrafUpdate;

        applied = true;
    }

    private static void ModButton_GrafUpdate(On.Menu.Remix.MenuModList.ModButton.orig_GrafUpdate orig, MenuModList.ModButton self, float timeStacker)
    {
        orig(self, timeStacker);
        var v = self.infoDot();
        v.pixel.alpha = self._label.alpha;
    }

    private static void ModButton_ctor(On.Menu.Remix.MenuModList.ModButton.orig_ctor orig, MenuModList.ModButton self, MenuModList list, int index)
    {
        orig(self, list, index);
        InfoDots.Add(self, new(self, list));
    }

    public class InfoDot
    {
        public MenuModList.ModButton ModButton { get; set; }
        public MenuModList MenuModList { get; set; }
        public FSprite pixel { get; set; }
        public ModManager.Mod mod { get; set; }
        public InfoDot(MenuModList.ModButton ModButton, MenuModList MenuModList)
        {
            this.ModButton = ModButton;
            this.MenuModList = MenuModList;
            mod = ModButton.itf.mod;

            pixel = new FSprite("pixel")
            {
                scaleX = 10,
                scaleY = 4,
                anchorX = 0.15f,
                anchorY = 0.4f,
            };
            //check plugins
            bool targetedPlugin = Directory.Exists(Path.Combine(mod.TargetedPath, "plugins"));
            bool newestPlugin = FloodgatePatcher.ModLoader.IsLatest && Directory.Exists(Path.Combine(mod.NewestPath, "plugins"));
            bool hasPlugin = Directory.Exists(Path.Combine(mod.path, "plugins"));
            string msg;
            if (targetedPlugin)
            {
                pixel.color = Color.blue;
                msg = "Correct Version";
                goto FINISH;
            }
            if (newestPlugin)
            {
                string path = Path.Combine(mod.NewestPath, "plugins");
                if (Directory.GetFiles(path).Length > 0)
                {
                    float timeDiff = Mathf.Clamp((float)((Directory.GetFiles(Path.Combine(mod.NewestPath, "plugins")).Max(File.GetCreationTimeUtc) - watcherRelease).TotalMilliseconds / elapsedTime.TotalMilliseconds), 0, 1);
                    pixel.color = timeDiff > 0.4? Color.Lerp(Color.yellow, Color.cyan, timeDiff - 0.4f) : Color.Lerp(new Color(0.8f, 0.2f, 0.1f), Color.yellow, timeDiff);
                    msg = timeDiff > 0.4 ? "Very Possibly updated for Watcher" : timeDiff > 0.2 ? "Possibly updated for Watcher" : timeDiff > 0.1 ? "Maybe updated for Watcher" : timeDiff != 0 ? "Possibly NOT updated for Watcher" : "Not Updated For Watcher";
                    goto FINISH;
                }
            }
            if (hasPlugin)
            {
                string path = Path.Combine(mod.path, "plugins");
                if (Directory.GetFiles(path).Length > 0)
                {
                    float timeDiff = Mathf.Clamp((float)((Directory.GetFiles(Path.Combine(mod.path, "plugins")).Max(File.GetCreationTimeUtc) - watcherRelease).TotalMilliseconds / elapsedTime.TotalMilliseconds), 0, 1);
                    pixel.color = timeDiff > 0.4 ? Color.Lerp(Color.yellow, Color.cyan, timeDiff - 0.4f) : Color.Lerp(new Color(0.8f, 0.2f, 0.1f), Color.yellow, timeDiff);
                    msg = timeDiff > 0.4 ? "Very Possibly updated for Watcher" : timeDiff > 0.2 ? "Possibly updated for Watcher" : timeDiff > 0.1 ? "Maybe updated for Watcher" : timeDiff != 0 ? "Possibly NOT updated for Watcher" : "Not Updated For Watcher";
                    goto FINISH;
                }
            }
            pixel.color = Color.gray;
            msg = "Not a Code Mod";

        FINISH:
            ModButton.myContainer.AddChild(pixel);
            ModButton.description += "\n" + msg;
        }
    }

    public static InfoDot infoDot(this MenuModList.ModButton self)
    {
        InfoDot dot;
        if (!InfoDots.TryGetValue(self, out dot))
        {
            InfoDots.Add(self, dot = new(self, self._list));
        }
        return dot;
    }
}
