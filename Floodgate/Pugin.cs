using BepInEx;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Globalization;

namespace Floodgate;

[BepInPlugin(GUID, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string GUID = "floodgate";
    public const string Name = "Floodgate";
    public const string Version = "0.0.1";

    public static bool woke = false;

    public static ManualLogSource logger;

    public static int ictCount = 0;

    public static RemixInterface RemixOptions;
    public void Awake()
    {
        if(woke)
        {
            return;
        }
        logger = base.Logger;

        On.RainWorld.PostModsInit += RainWorld_PostModsInit;
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        On.StaticWorld.InitCustomTemplates += (On.StaticWorld.orig_InitCustomTemplates orig) =>
        {
            orig();
            if (ictCount > 10 && ictCount < 20)
            {
                logger.LogInfo("Custom Templates Stack Below:\n" + new System.Diagnostics.StackTrace().ToString());
            }
            if (ictCount > 100)
            {
                throw new Exceptions.LoopException("Unexpected loop of the current method");
            }
            ictCount++;
        };

        On.Menu.EndgameMeter.NotchMeter.ctor += NotchMeter_ctor;

        FloodgatePatcher.CustomLog.Log("Floodgate plugin initialized");

        woke = true;
    }

    private void NotchMeter_ctor(On.Menu.EndgameMeter.NotchMeter.orig_ctor orig, Menu.EndgameMeter.NotchMeter self, Menu.EndgameMeter owner)
    {
        if (self == null || owner == null) return;

        WinState.ListTracker tracker = owner.tracker as WinState.ListTracker;

        if (ModManager.MSC)
        {
            if(owner.tracker.ID == MoreSlugcats.MoreSlugcatsEnums.EndgameID.Nomad)
            {
                for(int i = 0; i < tracker.myList.Count; i++)
                {
                    if (tracker.myList[i] >= Region.GetFullRegionOrder().Count)
                    {
                        tracker.myList[i] = 0;
                    }
                }
            }
        }

        orig(self, owner);
    }

    bool onmodsinit = false;
    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (onmodsinit)
        {
            return;
        }
        onmodsinit = true;
        MachineConnector.SetRegisteredOI(GUID, RemixOptions = new());
        RemixOptions._LoadConfigFile();
    }

    bool postmodsinit = false;
    private void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
        orig(self);
        if (postmodsinit)
        {
            return;
        }
        postmodsinit = true;
        World.CustomMerger.Apply();
        Registry.Apply();
        UI.RemixModList.Apply();
        Steam.Workshop.Apply();
        ExHooks.HookManager.ApplyHooks();
    }
}
