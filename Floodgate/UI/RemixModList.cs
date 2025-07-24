using Menu.Remix;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Floodgate.UI;

public static class RemixModList
{
    //this may not be that accurate
    public static DateTime watcherRelease = new DateTime(2025, 3, 28);
    public static TimeSpan elapsedTime => new DateTime(2025, 6, 16) - watcherRelease;
    //readonly, no need to assign twice
    public static readonly ConditionalWeakTable<MenuModList.ModButton, InfoDot> InfoDots = new();

    public static readonly List<IDetour> hooks = new();

    static bool applied = false;
    public static void Apply()
    {
        if (applied) { return; }

        On.Menu.Remix.MenuModList.ModButton.ctor += ModButton_ctor;
        On.Menu.Remix.MenuModList.ModButton.GrafUpdate += ModButton_GrafUpdate;
        On.Menu.Remix.MenuModList.ctor += MenuModList_ctor;
        On.Menu.Remix.MenuModList.ModButton.Update += ModButton_Update;

        hooks.Add(new Hook(typeof(ModManager.Mod).GetProperty("LocalizedDescription", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetGetMethod(),
            LocalizedDesc));

        applied = true;
    }

    public static string workshopurl = @"https://steamcommunity.com/workshop/filedetails/?id=";
    private static void ModButton_Update(On.Menu.Remix.MenuModList.ModButton.orig_Update orig, MenuModList.ModButton self)
    {
        orig(self);
        if (SteamManager.Initialized && self.itf.mod.workshopMod)
        {
            var v = self.infoDot();
            if (self.MenuMouseMode)
            {
                if (self.MouseOver)
                {
                    if (!v.rheld && Input.GetMouseButton(1))
                    {
                        v.rheld = true;
                        self.openWorkshop();
                    }
                    else if (v.rheld && !Input.GetMouseButton(1))
                    {
                        v.rheld = false;
                    }
                }
            }
        }
    }

    public static void openWorkshop(this MenuModList.ModButton self)
    {
        self.PlaySound(self.soundClick);
        Plugin.logger.LogInfo("Opening Workshop URL for " + self.itf.mod.id);
        Steamworks.SteamFriends.ActivateGameOverlayToWebPage(workshopurl + self.itf.mod.workshopId);
    }

    public delegate string orig_Mod_LocalizedDescription(ModManager.Mod self);
    public static string LocalizedDesc(orig_Mod_LocalizedDescription orig, ModManager.Mod self)
    {
        string or = orig(self);
        if (Plugin.RemixOptions.ShowWorkshopDate.Value && Steam.Workshop.ModLastUpdatedDT.TryGetValue(self.workshopId, out var DT))
        {
            or += Environment.NewLine + Environment.NewLine + "[[ " + DT.ToString("g") + " ]]";
        }
        return or;
    }

    private static void MenuModList_ctor(On.Menu.Remix.MenuModList.orig_ctor orig, MenuModList self, ConfigMenuTab tab)
    {
        orig(self, tab);
        Steam.Workshop.TryFetch();
    }

    private static void ModButton_GrafUpdate(On.Menu.Remix.MenuModList.ModButton.orig_GrafUpdate orig, MenuModList.ModButton self, float timeStacker)
    {
        orig(self, timeStacker);
        var v = self.infoDot();
        v.pixel.alpha = Plugin.RemixOptions.ShowWorkshopDate.Value ? self._label.alpha : 0f;
        v.steamPixel.alpha = Plugin.RemixOptions.ShowWorkshopDate.Value ? self._label.alpha : 0f;

        if (!Plugin.RemixOptions.ShowWorkshopDate.Value)
        {
            return;
        }

        if (v.SteamDateAdded || !v.mod.workshopMod)
        {
            return;
        }

        if(v.SteamUpdate <= 0)
        {
            if (SteamManager.Initialized && v.mod.workshopMod && Steam.Workshop.ModLastUpdatedDT.TryGetValue(v.mod.workshopId, out var lastUpdated))
            {

                float timeDiff = Mathf.Clamp((float)((lastUpdated - watcherRelease).TotalMilliseconds / elapsedTime.TotalMilliseconds), 0, 1);
                v.steamPixel.color = timeDiff > 0.4 ? Color.Lerp(Color.yellow, Color.cyan, Mathf.InverseLerp(0.4f, 1f, timeDiff)) : timeDiff != 0 ? Color.Lerp(new Color(0.8f, 0.2f, 0.1f), Color.yellow, Mathf.InverseLerp(0f, 0.4f, timeDiff)) : Color.red;
                v.SteamDateAdded = true;
                self.description += "\n" + lastUpdated.ToString("g");
            }
            v.SteamUpdate = 60;
        }
        else
        {
            v.SteamUpdate--;
        }
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
        public FSprite steamPixel { get; set; }
        public ModManager.Mod mod { get; set; }

        public bool rheld = false;

        public int SteamUpdate = 60;
        public bool SteamDateAdded = false;

        public InfoDot(MenuModList.ModButton ModButton, MenuModList MenuModList)
        {
            this.ModButton = ModButton;
            this.MenuModList = MenuModList;
            mod = ModButton.itf.mod;
            pixel = new FSprite("pixel")
            {
                scaleX = 8,
                scaleY = 4,
                anchorX = 0f,
                anchorY = 0.4f,
            };

            steamPixel = new FSprite("pixel")
            {
                scaleX = 8,
                scaleY = 4,
                anchorX = 0.95f,
                anchorY = 0.4f,
            };
            //check plugins
            bool targetedPlugin = Directory.Exists(Path.Combine(mod.TargetedPath, "plugins"));
            bool newestPlugin = FloodgatePatcher.ModLoader.IsLatest && Directory.Exists(Path.Combine(mod.NewestPath, "plugins"));
            bool hasPlugin = Directory.Exists(Path.Combine(mod.path, "plugins"));
            if (targetedPlugin)
            {
                pixel.color = Color.blue;
                goto FINISH;
            }
            if (newestPlugin)
            {
                string path = Path.Combine(mod.NewestPath, "plugins");
                if (Directory.GetFiles(path).Length > 0)
                {
                    float timeDiff = Mathf.Clamp((float)((Directory.GetFiles(Path.Combine(mod.NewestPath, "plugins")).Max(File.GetCreationTimeUtc) - watcherRelease).TotalMilliseconds / elapsedTime.TotalMilliseconds), 0, 1);
                    pixel.color = timeDiff > 0.4 ? Color.Lerp(Color.yellow, Color.cyan, Mathf.InverseLerp(0.4f, 1f, timeDiff)) : timeDiff != 0 ? Color.Lerp(new Color(0.8f, 0.2f, 0.1f), Color.yellow, Mathf.InverseLerp(0f, 0.4f, timeDiff)) : Color.red;
                    goto FINISH;
                }
            }
            if (hasPlugin)
            {
                string path = Path.Combine(mod.path, "plugins");
                if (Directory.GetFiles(path).Length > 0)
                {
                    float timeDiff = Mathf.Clamp((float)((Directory.GetFiles(Path.Combine(mod.path, "plugins")).Max(File.GetCreationTimeUtc) - watcherRelease).TotalMilliseconds / elapsedTime.TotalMilliseconds), 0, 1);
                    pixel.color = timeDiff > 0.4 ? Color.Lerp(Color.yellow, Color.cyan, Mathf.InverseLerp(0.4f, 1f, timeDiff)) : timeDiff != 0 ? Color.Lerp(new Color(0.8f, 0.2f, 0.1f), Color.yellow, Mathf.InverseLerp(0f, 0.4f, timeDiff)) : Color.red;
                    goto FINISH;
                }
            }
            pixel.color = Color.gray;

        FINISH:
            ModButton.myContainer.AddChild(pixel);

            if (SteamManager.Initialized && mod.workshopMod)
            {
                if(Steam.Workshop.ModLastUpdatedDT.TryGetValue(mod.workshopId, out var lastUpdated))
                {
                    float timeDiff = Mathf.Clamp((float)((lastUpdated - watcherRelease).TotalMilliseconds / elapsedTime.TotalMilliseconds), 0, 1);
                    steamPixel.color = timeDiff > 0.4 ? Color.Lerp(Color.yellow, Color.cyan, Mathf.InverseLerp(0.4f, 1f, timeDiff)) : timeDiff != 0 ? Color.Lerp(new Color(0.8f, 0.2f, 0.1f), Color.yellow, Mathf.InverseLerp(0f, 0.4f, timeDiff)) : Color.red;
                    SteamDateAdded = true;
                }
                else
                {
                    steamPixel.color = Color.black;
                }
                ModButton.myContainer.AddChild(steamPixel);
                pixel.MoveInFrontOfOtherNode(steamPixel);
            }
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
