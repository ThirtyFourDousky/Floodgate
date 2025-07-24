using BepInEx;
using BepInEx.Logging;

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

        FloodgatePatcher.CustomLog.Log("Floodgate plugin initialized");

        woke = true;
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
    }
}
