using BepInEx;
using BepInEx.Logging;
using BepInEx.MultiFolderLoader;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static FloodgatePatcher.ModLoader;

namespace FloodgatePatcher;
public static class ModLoader
{
    public static readonly string LatestVersion = "v1.10.4";
    public static string CurrentVersion = "";
    const string newest = "newest";
    const string plugins = "plugins";
    const string patchers = "patchers";

    public static string LogPath = "";

    public static string CacheLocation = "";

    public static Assembly AssemblyCSharp;

    internal static List<IDetour> Hooks = new();

    public static void Init()
    {
        LogPath = Path.Combine(Paths.GameRootPath, "RainWorld_Data", "StreamingAssets", "FloodgateLog.txt");
        using (StreamWriter writer = File.CreateText(LogPath))
        {
            writer.WriteLine("### Floodgate ###");
            writer.WriteLine(DateTime.UtcNow.ToString("u") + " UTC");
            CustomLog.active = true;
        }

        //load current game version
        CurrentVersion = File.ReadAllText(Path.Combine(Paths.GameRootPath, "RainWorld_Data", "StreamingAssets", "GameVersion.txt"));
        CacheLocation = Path.Combine(Paths.GameRootPath, "RainWorld_Data", "StreamingAssets", "FloodgatePatchedAssemblies");
        if (!Directory.Exists(CacheLocation))
        {
            CustomLog.Log("Creating cached assemblies location at " + CacheLocation);
            Directory.CreateDirectory(CacheLocation);
        }
        //override MultiFolderLoader assembly resolving
        Hooks.Add(new Hook(typeof(ModManager).GetMethod("ResolveModDirectories", BindingFlags.NonPublic | BindingFlags.Static), ResolveModDirectories));
        Hooks.Add(new Hook(typeof(Utility).GetMethod("TryResolveDllAssembly", BindingFlags.Public | BindingFlags.Static, null, [typeof(AssemblyName), typeof(string), typeof(Assembly).MakeByRefType()], null), TryResolveDllAssemblyOverride));
        CustomLog.Log("Floodgate Modloader initiated. Latest Game Version: " + LatestVersion + " - Current Version: " + CurrentVersion);
    }
    public delegate bool delLoader<T>(AssemblyName assemblyName, string dir, out T assembly);
    public static bool FetchAssembly<T>(delLoader<T> loader, AssemblyName assemblyName, out T assembly) where T : class
    {
        foreach (Mod mod in ModManager.Mods)
        {
            string dir;
            if (!string.IsNullOrWhiteSpace(CurrentVersion) &&
                ((Directory.Exists(dir = Path.Combine(mod.ModDir, CurrentVersion, plugins)) &&
                loader(assemblyName, dir, out assembly)) ||
                (Directory.Exists(dir = Path.Combine(mod.ModDir, CurrentVersion, patchers)) &&
                loader(assemblyName, dir, out assembly))))
            {
                return true;
            }
            else if (IsLatest() &&
                ((Directory.Exists(dir = Path.Combine(mod.ModDir, newest, plugins)) &&
                loader(assemblyName, dir, out assembly)) ||
                (Directory.Exists(dir = Path.Combine(mod.ModDir, newest, patchers)) &&
                loader(assemblyName, dir, out assembly))))
            {
                return true;
            }
            else if ((Directory.Exists(dir = Path.Combine(mod.ModDir, plugins)) &&
                loader(assemblyName, dir, out assembly)) ||
                (Directory.Exists(dir = Path.Combine(mod.ModDir, patchers)) &&
                loader(assemblyName, dir, out assembly)))
            {
                return true;
            }
        }
        assembly = null;
        return false;
    }
    public static bool ResolveAssembly(AssemblyName assemblyName, string dir, out string? path)
    {
        List<string> list = [dir, .. Directory.GetDirectories(dir, "*", SearchOption.AllDirectories)];
        foreach (string item in list)
        {
            path = Path.Combine(item, assemblyName.Name + ".dll");
            if (File.Exists(path))
            {
                return true;
            }
        }
        path = null;
        return false;
    }

    public delegate Assembly orig_ResolveModDirectories(object sender, ResolveEventArgs args);
    public static Assembly ResolveModDirectories(orig_ResolveModDirectories orig, object sender, ResolveEventArgs args)
    {   
        return FetchAssembly(Utility.TryResolveDllAssembly, new AssemblyName(args.Name), out Assembly? assembly) ? assembly : orig(sender, args);
    }

    public static bool Patch(string path, AssemblyName assemblyName, out string? patchPath)
    {
        bool patched = false;
        string formattedAssemblyName = assemblyName.Name.Replace(" ", "");
        List<Type> patchers = AppDomain.CurrentDomain.GetAssemblies().Select(i => i?.GetType("FloodgatePatchers." + formattedAssemblyName, false, true)).Where(ix=>ix != null).ToList();

    TRYAGAIN:
        AssemblyDefinition? assembly = AssemblyDefinition.ReadAssembly(path);
        try
        {
            for(int i = 0; i < patchers.Count; i++)
            {
                try
                {
                    MethodInfo? patcher = patchers[i]?.GetMethod("Patcher", BindingFlags.Public | BindingFlags.Static);
                    if (patcher != null)
                    {
                        object[] param = [assembly];
                        CustomLog.Log("Trying to patch " + assemblyName.Name + " using " + patchers[i].FullName);
                        patcher.Invoke(null, param);
                        CustomLog.Log("Sucessfully patched " + assemblyName.Name + " using " + patchers[i].FullName);
                        assembly = (AssemblyDefinition)param[0];
                        patched = true;
                    }
                }
                catch (Exception PatchEx)
                {
                    CustomLog.LogError("skipping " + patchers[i].AssemblyQualifiedName + "\n" + PatchEx.ToString());
                    patchers.RemoveAt(i);
                    patched = false;
                    goto TRYAGAIN;
                }
            }
        }
        catch(Exception ex)
        {
            CustomLog.LogError(ex.ToString());
            patched = false;
        }
        if(patched)
        {
            patchPath = Path.Combine(CacheLocation, assemblyName.Name + ".dll");
            assembly.Write(patchPath);
        }
        else
        {
            patchPath = null;
        }

        return patched;
    }

    public static Dictionary<string, string?> OverridenPaths = new();
    public delegate string orig_PluginInfo_Location(PluginInfo self);
    public static string On_PluginInfo_Location(orig_PluginInfo_Location orig, PluginInfo self)
    {
        string fallback = orig(self);
        if (OverridenPaths.ContainsKey(fallback))
        {
            return OverridenPaths[fallback] ?? fallback;
        }
        bool overridFound = FetchAssembly(ResolveAssembly, AssemblyName.GetAssemblyName(fallback), out string? overrid);
        string res = overrid ?? fallback;
        bool overriden = res != fallback;


        if (!overridFound)
        {
            CustomLog.Log("Alternate path null for `" + fallback + "`, expected at least equal");
        }
        if (overriden)
        {
            CustomLog.Log("Found different assembly path from " + fallback + " at " +  overrid);
        }
        AssemblyName assemblyName = AssemblyName.GetAssemblyName(res);
        try
        {
            if (Patch(res, assemblyName, out string patchPath))
            {
                CustomLog.Log("Setting up patched assembly " + assemblyName.Name + " from " + res);
                OverridenPaths.Add(fallback, patchPath);
                return Path.Combine(patchPath);
            }
        }
        catch (Exception ex)
        {
            CustomLog.LogError(ex.ToString());
            OverridenPaths.Add(fallback, res);
            CustomLog.Log("Loading patched assembly " + assemblyName.Name + "failed, returning to original found assembly from " + res);
            return res;
        }
        OverridenPaths.Add(fallback, res);
        CustomLog.Log("Setting up assembly " + AssemblyName.GetAssemblyName(fallback).Name + " from " + res);
        return res;
    }

    public delegate bool orig_TryResolveDllAssembly(AssemblyName assemblyName, string dir, out Assembly assembly);
    public static bool TryResolveDllAssemblyOverride(orig_TryResolveDllAssembly orig, AssemblyName assemblyName, string dir, out Assembly assembly)
    {
        assembly = null;

        if(AppDomain.CurrentDomain.GetAssemblies().Any(i=>i.GetName().Name == assemblyName.Name))
        {
            assembly = AppDomain.CurrentDomain.GetAssemblies().First(i => i.GetName().Name == assemblyName.Name);
            return true;
        }

        List<string> paths = [dir, .. Directory.GetDirectories(dir, "*", SearchOption.AllDirectories)];
        foreach(var i  in paths)
        {
            string text = Path.Combine(i, assemblyName.Name + ".dll");
            if (File.Exists(text))
            {
                bool loaded = false;
                try
                {
                    if (Patch(text, assemblyName, out string patchPath))
                    {
                        assembly = Assembly.Load(patchPath);
                        loaded = true;
                        CustomLog.Log("Loaded patched assembly " + assemblyName.Name + " from " + text);
                    }
                    else
                    {
                        loaded = false;
                    }
                }
                catch (Exception ex)
                {
                    CustomLog.LogError(ex.ToString());
                    loaded = false;
                }
                if(!loaded)
                {
                    try
                    {
                        assembly = Assembly.LoadFile(text);
                        CustomLog.Log("Loaded assembly " + assemblyName.Name + " from " + text);

                    }
                    catch (Exception ex)
                    {
                        CustomLog.LogError(ex.ToString());
                        continue;
                    }
                }
                return true;
            }
        }
        bool res = orig(assemblyName,dir, out assembly);
        if (res)
        {
            CustomLog.Log("Loaded assembly " + assemblyName.Name + " from " + assembly.Location + " with default loader");
        }
        return res;
    }

    public static bool IsLatest()
    {
        bool IsLatest = CurrentVersion == LatestVersion;
        if (!IsLatest)
        {
            int CurrentVer = int.Parse(new string(CurrentVersion.Where(char.IsDigit).ToArray()));
            int LatestVer = int.Parse(new string(LatestVersion.Where(char.IsDigit).ToArray()));
            IsLatest = CurrentVer > LatestVer;
        }
        return IsLatest;
    }
}
