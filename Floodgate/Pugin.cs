using BepInEx;
using BepInEx.Logging;
using FloodgatePatcher;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Floodgate;

[BepInPlugin(GUID, Name, Version)]
public partial class Plugin : BaseUnityPlugin
{
    public const string GUID = "floodgate";
    public const string Name = "Floodgate";
    public const string Version = "0.1.30";

    public static Plugin? Instance { get; private set; }

    public static bool woke = false;

    public static ManualLogSource logger;

    public static RemixInterface RemixOptions;
    public void Awake()
    {
        Instance = this;
        if (woke)
        {
            return;
        }
        OtherHooks.ResolveFilePathAlt = TurboAssetManager.AssetManager_ResolveFilePath_string_bool_bool;
        logger = base.Logger;

        On.RainWorld.PostModsInit += RainWorld_PostModsInit;
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        On.ModManager.ModApplyer.Start += ModApplyer_Start;

        On.Menu.EndgameMeter.NotchMeter.ctor += NotchMeter_ctor;

        IL.ModManager.CheckInitIssues += ModManager_CheckInitIssues;

        FloodgatePatcher.CustomLog.Log("Floodgate plugin initialized");

        World.Map.Apply();

        woke = true;
    }

    private void ModManager_CheckInitIssues(ILContext il)
    {
        try
        {
            FieldReference displayclass = null;
            ILCursor c = new ILCursor(il);

            c.GotoNext(MoveType.After, x => x.MatchCallvirt(typeof(System.Reflection.Assembly).GetProperty("Location", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod()));
            c.EmitDelegate<Func<string, string>>(static delegate (string assemblyPath)
            {
                if (ModLoader.OverridenAssembliesPaths.TryGetValue(assemblyPath, out string realPath))
                {
                    return realPath;
                }
                return assemblyPath;
            });

            c.GotoNext(x => x.MatchLdloc(0), x => x.MatchLdfld(out displayclass), x => x.MatchLdfld("Options", "modChecksums"), x => x.MatchCallvirt(out var value) && value.Name == "Clear");
            c.Emit(OpCodes.Ldloc_0);
            c.Emit(OpCodes.Ldfld, displayclass);
            c.EmitDelegate(static delegate (Options options)
            {
                //disable mods with missing dependencies
                HashSet<string> allenabledmods = new(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < ModManager.ActiveMods.Count; i++)
                {
                    allenabledmods.Add(ModManager.ActiveMods[i].id);
                }
                HashSet<string> brokenMods = new(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < ModManager.ActiveMods.Count; i++)
                {
                    if (brokenMods.Contains(ModManager.ActiveMods[i].id))
                    {
                        continue;
                    }
                    for (int ii = 0; ii < ModManager.ActiveMods[i].requirements.Length; ii++)
                    {
                        if (brokenMods.Contains(ModManager.ActiveMods[i].requirements[ii]) || !allenabledmods.Contains(ModManager.ActiveMods[i].requirements[ii]))
                        {
                            brokenMods.Add(ModManager.ActiveMods[i].id);
                            i = -1;
                            break;
                        }
                    }
                }

                for (int i = 0; i < ModManager.ActiveMods.Count; i++)
                {
                    if (brokenMods.Contains(ModManager.ActiveMods[i].id))
                    {
                        ModManager.ActiveMods.RemoveAt(i);
                        i--;
                    }
                }

                for (int i = 0; i < options.enabledMods.Count; i++)
                {
                    if (brokenMods.Contains(options.enabledMods[i]))
                    {
                        options.enabledMods.RemoveAt(i);
                        i--;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            CustomLog.LogError("CheckInitIssues hook fucking failed. Please report this\n" + ex.ToString());
            UnityEngine.Debug.LogError("CheckInitIssues hook fucking failed. Please report this\n" + ex.ToString());
        }
    }

    private void ModApplyer_Start(On.ModManager.ModApplyer.orig_Start orig, ModManager.ModApplyer self, bool filesInBadState)
    {
        //FloodgatePatcher.ModLoader.ResetMergedMods();
        TurboAssetManager.accessfgmerged = false;
        orig(self, filesInBadState);
    }

    private void NotchMeter_ctor(On.Menu.EndgameMeter.NotchMeter.orig_ctor orig, Menu.EndgameMeter.NotchMeter self, Menu.EndgameMeter owner)
    {
        if (self == null || owner == null) return;

        WinState.ListTracker tracker = owner.tracker as WinState.ListTracker;

        if (ModManager.MSC)
        {
            if (owner.tracker.ID == MoreSlugcats.MoreSlugcatsEnums.EndgameID.Nomad)
            {
                int regionCount = Region.GetFullRegionOrder().Count;
                for (int i = 0; i < tracker.myList.Count; i++)
                {
                    if (tracker.myList[i] >= regionCount)
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
        TurboAssetManager.accessfgmerged = false;
        Registry.Apply();
        //TurboAssetManager.Map();
        if (onmodsinit)
        {
            try
            {
                orig(self);
            }
            catch (Exception origException)
            {
                CustomLog.LogError("OnModsInit failed at other mod. please report this, some hooks possibly failed and your game may not run well\n" + origException.ToString());
            }
            Registry.Merge();
            return;
        }
        onmodsinit = true;
        //TurboAssetManager.Apply();
        _Modules.DevTools.Objects.ObjectsMenu.Enable();
        ModCompat._UnRegEx.Apply();
        bool fasterworld = FGTools.IsModActive("gelbi.faster-world");
        bool fasterworldextra = FGTools.IsModActive("gelbi.faster-world-extra");
        bool GSL = FGTools.IsModActive("0gelbi.silly-lib");
        if (fasterworld || fasterworldextra)
        {
            bool failed = false;
            if (fasterworld)
            {
                CustomLog.Log("Faster World apply" + (GSL ? " with GSL" : "") + "...");
                if (GSL)
                {
                    try
                    {
                        ModCompat.FasterWorldStuff.GSL_Apply();
                    }
                    catch (Exception ex2)
                    {
                        failed = true;
                        CustomLog.LogError("Faster World compat with GSL failed\n" + ex2.ToString());
                    }
                }
                else
                {
                    try
                    {
                        ModCompat.FasterWorldStuff.Apply();
                    }
                    catch (Exception ex2)
                    {
                        failed = true;
                        CustomLog.LogError("Faster World compat failed\n" + ex2.ToString());
                    }
                }
            }
            if (fasterworldextra)
            {
                //CustomLog.Log("Faster World Extra apply...");
                //Registry.FasterWorldCache = ModCompat.FasterWorldStuff.CacheFloodgate;
                CustomLog.Log("Faster World Extra apply" + (GSL ? " with GSL" : "") + "...");
                if (GSL)
                {
                    try
                    {
                        ModCompat.FasterWorldStuff.GSL_Apply_Extra();
                    }
                    catch (Exception ex2)
                    {
                        failed = true;
                        CustomLog.LogError("Faster World Extra compat with GSL failed\n" + ex2.ToString());
                    }
                }
                else
                {
                    try
                    {
                        ModCompat.FasterWorldStuff.Apply_Extra();
                    }
                    catch (Exception ex2)
                    {
                        failed = true;
                        CustomLog.LogError("Faster World Extra compat failed\n" + ex2.ToString());
                    }
                }
            }
            if (failed)
            {
                CustomLog.Log("As one of Faster World hooks failed, Floodgate will override it...\nthis means any faster world present will be essentially disabled\nThis does NOT mean everything will be ok, i can't predict this scenario");
                TurboAssetManager.Apply();
                On.WorldLoader.FindRoomFile += On_WorldLoader_FindRoomFile;
            }
            else if (GSL)
            {
                ModCompat.FasterWorldStuff.GSL_ApplyRest();
            }
            else
            {
                ModCompat.FasterWorldStuff.ApplyRest();
            }
        }
        else
        {
            TurboAssetManager.Apply();
        }
        if (FGTools.IsModActive("preservatory"))
        {
            try
            {
                ModCompat.Preservatory.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("Preservatory hook failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("SnowBee.Snow"))
        {
            try
            {
                ModCompat.SnowyWorld.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("Winter's End hook failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("randombuff"))
        {
            try
            {
                ModCompat.RandomBuff.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("randombuff hook failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("randombuff") && FGTools.IsModActive("preservatory"))
        {
            try
            {
                ModCompat.RandomPreservatory.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("randombuff preservatory compat failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("drought"))
        {
            try
            {
                ModCompat.DroughtStuff.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("Drought compat failed, your game will break\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("yeliah.slugpupFieldtrip") && FGTools.IsModActive("sprobgik.desecratinggraves"))
        {
            try
            {
                ModCompat.NotScugPlaySpupSafari.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("Not Slugcat Playables + Slugpup Safari compat failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("iteratorCreator") && FGTools.IsModActive("emgtx"))
        {
            try
            {
                ModCompat.EmgTxIteratorCreator.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("EmgTx + Iterator Creator compat failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("javadog.gateNames"))
        {
            try
            {
                ModCompat.WDTGGcompat.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("WDTGG apply failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("lb-fgf-m4r-ik.modpack"))
        {
            try
            {
                ModCompat.LBspecific.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("M4rblelous Entity Pack specific apply failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("regionkit"))
        {
            try
            {
                ModCompat.RegionKit.RegionKitApply.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("RegionKit specific apply failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("nacu.lunacy"))
        {
            try
            {
                ModCompat.Lunacy.CustomTokens.LunacyTokens.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("Lunacy specific apply failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("crs"))
        {
            try
            {
                ModCompat.CRS.IndexedEntranceClass.Apply();
                Floodgate.Optimization.CRS.RegionProperties.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("CRS specific apply failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("rainworldlastwish"))
        {
            try
            {
                ModCompat._LastWish.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("LastWish apply failed\n" + e.ToString());
            }
        }
        if (FGTools.IsModActive("willowwisp.bellyplus"))
        {
            try
            {
                ModCompat._RotundWorld.Apply();
            }
            catch (Exception e)
            {
                CustomLog.LogError("Rotund World apply failed\n" + e.ToString());
            }
        }
        //before orig
        try
        {
            orig(self); //yes im blind
        }
        catch (Exception origException)
        {
            CustomLog.LogError("OnModsInit failed at other mod. please report this, some hooks possibly failed and your game may not run well\n" + origException.ToString());
        }
        //after orig
        Optimization._ConfigContainer.Apply();
        try
        {
            MachineConnector.SetRegisteredOI(GUID, RemixOptions = new());
            RemixOptions._LoadConfigFile();
        }
        catch (System.Exception e)
        {
            FloodgatePatcher.CustomLog.LogError(e.ToString());
        }
    }

    bool postmodsinit = false;
    private void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
    {
        //var mapTask = System.Threading.Tasks.Task.Run(TurboAssetManager.preload);
        orig(self);
        Registry.Merge();
        //mapTask.GetAwaiter().GetResult();
        NotEnums.EnabledMods.Apply();
        NotEnums.CreatureTemplateType.PostModsInit();

        if (postmodsinit)
        {
            return;
        }
        postmodsinit = true;
        World.CustomMerger.Apply();
        World.Optimization.Apply();
        FG_Expedition.CreatureMergerTools.Apply();
        UI.RemixModList.Apply();
        Steam.Workshop.Apply();
        ExHooks.HookManager.ApplyHooks();
        _Modules.ExtendedEchoExtender.Hooks.Apply();
    }

    //this is my silly attempt of forcefully disable faster world NativeDetour on fail
    public static string On_WorldLoader_FindRoomFile(On.WorldLoader.orig_FindRoomFile orig, string roomName, bool includeRootDirectory, string additionalAppend, bool showWarning)
    {
        return orig(roomName, includeRootDirectory, additionalAppend, showWarning);
    }
}
