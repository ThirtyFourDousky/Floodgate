using BepInEx;
using BepInEx.MultiFolderLoader;
using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;

namespace FloodgatePatcher;
public static class ModLoader
{
    public static readonly string LatestVersion = "v1.11.8";
    public static string CurrentVersion = "";
    public static bool IsLatest = false;
    public static string FloodgateMergedPath = null;
    public static DirectoryInfo FloodgateMergedInfo = null;

    public static readonly HashSet<string> LoadedAssemblies = new(StringComparer.OrdinalIgnoreCase);

    const string newest = "newest";
    const string plugins = "plugins";
    const string patchers = "patchers";

    public static string FloodgatePath = "";

    public static string LogPath = "";

    public static string CacheLocation = "";

    public static Assembly AssemblyCSharp;

    internal static List<IDetour> Hooks = new();

    public static bool debug = false;

    public static void Init()
    {
        LogPath = (Paths.GameRootPath + Path.DirectorySeparatorChar + "RainWorld_Data" + Path.DirectorySeparatorChar + "StreamingAssets" + Path.DirectorySeparatorChar + "FloodgateLog.txt");
        Patcher.logger.LogInfo("Log Path: " + LogPath);
        using (StreamWriter writer = File.CreateText(LogPath))
        {
            writer.WriteLine("### Floodgate ###");
            writer.WriteLine(DateTime.UtcNow.ToString("u") + " UTC");
            CustomLog.active = true;
        }

        //load current game version
        CurrentVersion = File.ReadAllText((Paths.GameRootPath + Path.DirectorySeparatorChar + "RainWorld_Data" + Path.DirectorySeparatorChar + "StreamingAssets" + Path.DirectorySeparatorChar + "GameVersion.txt"));
        CacheLocation = (Paths.GameRootPath + Path.DirectorySeparatorChar + "RainWorld_Data" + Path.DirectorySeparatorChar + "StreamingAssets" + Path.DirectorySeparatorChar + "FloodgatePatchedAssemblies");
        FloodgateMergedInfo = new DirectoryInfo(FloodgateMergedPath = (Paths.GameRootPath + Path.DirectorySeparatorChar + "RainWorld_Data" + Path.DirectorySeparatorChar + "StreamingAssets" + Path.DirectorySeparatorChar + "FloodgateMergedMods"));
        if (!Directory.Exists(CacheLocation))
        {
            CustomLog.Log("Creating cached assemblies location at " + CacheLocation);
            Directory.CreateDirectory(CacheLocation);
        }
        if (!Directory.Exists(FloodgateMergedPath))
        {
            CustomLog.Log("Creating Floodgate Merged Mods at "+ FloodgateMergedPath);
            //Directory.CreateDirectory(FloodgateMergedPath);
            //FloodgateMergedInfo.Refresh();
            FloodgateMergedInfo.Create();
        }
        else
        {
            FloodgateMergedInfo.Refresh();
            if (FloodgateMergedInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                CustomLog.Log("Floodgate Merged Mods directory at " + FloodgateMergedPath + " is a symbolic link or junction. if this is intended by you, do NOT.");
                FloodgateMergedInfo.Delete();
                FloodgateMergedInfo.Create();
            }
        }
        foreach(string file in Directory.EnumerateFiles(FloodgateMergedInfo.FullName, "*.*", SearchOption.AllDirectories))
        {
            string path = file.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if(path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".sys", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase))
            {
                CustomLog.LogError("Floodgate merged files ["+ FloodgateMergedPath +"] contains executables, disabling it\nPlease check it");
                FloodgateMergedInfo = null;
                break;
            }
        }
        //override MultiFolderLoader assembly resolving
        Hooks.Add(new Hook(typeof(ModManager).GetMethod("ResolveModDirectories", BindingFlags.NonPublic | BindingFlags.Static), ResolveModDirectories));
        Hooks.Add(new Hook(typeof(Utility).GetMethod("TryResolveDllAssembly", BindingFlags.Public | BindingFlags.Static, null, [typeof(AssemblyName), typeof(string), typeof(Assembly).MakeByRefType()], null), TryResolveDllAssemblyOverride));
        if (File.Exists((Paths.GameRootPath + Path.DirectorySeparatorChar + "FloodgateDebug.txt")))
        {
            debug = true;

            //Reflection.Assembly hooks
            Hooks.Add(new ILHook(typeof(Assembly).GetMethod("LoadFile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null), IL_Assembly_LoadFile));
            Hooks.Add(new ILHook(typeof(Assembly).GetMethod("LoadFile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(Evidence) }, null), IL_Assembly_LoadFile));
        }
        //Hooks.Add(new ILHook(typeof(Utility).GetMethod("TryResolveDllAssembly", BindingFlags.Public | BindingFlags.Static, null, [typeof(AssemblyName), typeof(string), typeof(Assembly).MakeByRefType()], null), IL_TryResolveDllAssembly));
        
        IsLatest = ParseLatestVersion(LatestVersion, CurrentVersion);

        //get Floodgate Path
        DirectoryInfo PatcherDir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
        //while(!PatcherDir.GetFiles().Any(i=>i.Name == "modinfo.json"))
        while(!File.Exists(PatcherDir.FullName + Path.DirectorySeparatorChar + "modinfo.json"))
        {
            PatcherDir = PatcherDir.Parent;
        }
        FloodgatePath = PatcherDir.FullName;

        CustomLog.Log("Floodgate Patcher initiated. Latest Game Version: " + LatestVersion + " - Current Version: " + CurrentVersion);
        CustomLog.Log("Is latest version: " + IsLatest);
        CustomLog.Log("Current Floodgate path is: " + FloodgatePath);
    }
    public delegate bool delLoader<T>(AssemblyName assemblyName, string dir, out T assembly);
    public static bool FetchAssembly<T>(delLoader<T> loader, AssemblyName assemblyName, out T assembly) where T : class
    {
        foreach (Mod mod in ModManager.Mods)
        {
            string dir;
            if (!string.IsNullOrWhiteSpace(CurrentVersion) &&
                ((Directory.Exists(dir = (mod.ModDir + Path.DirectorySeparatorChar + CurrentVersion + Path.DirectorySeparatorChar + plugins)) &&
                loader(assemblyName, dir, out assembly)) ||
                (Directory.Exists(dir = (mod.ModDir + Path.DirectorySeparatorChar + CurrentVersion + Path.DirectorySeparatorChar + patchers)) &&
                loader(assemblyName, dir, out assembly))))
            {
                return true;
            }
            else if (IsLatest &&
                ((Directory.Exists(dir = (mod.ModDir + Path.DirectorySeparatorChar + newest + Path.DirectorySeparatorChar + plugins)) &&
                loader(assemblyName, dir, out assembly)) ||
                (Directory.Exists(dir = (mod.ModDir + Path.DirectorySeparatorChar + newest + Path.DirectorySeparatorChar + patchers)) &&
                loader(assemblyName, dir, out assembly))))
            {
                return true;
            }
            else if ((Directory.Exists(dir = (mod.ModDir + Path.DirectorySeparatorChar + plugins)) &&
                loader(assemblyName, dir, out assembly)) ||
                (Directory.Exists(dir = (mod.ModDir + Path.DirectorySeparatorChar + patchers)) &&
                loader(assemblyName, dir, out assembly)))
            {
                return true;
            }
        }
        assembly = null;
        return false;
    }
#nullable enable
    public static bool ResolveAssembly(AssemblyName assemblyName, string dir, out string? path)
    {
        path = (dir + Path.DirectorySeparatorChar + assemblyName.Name + ".dll");
        if (File.Exists(path))
        {
            return true;
        }
        foreach (string item in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
        {
            path = (item + Path.DirectorySeparatorChar + assemblyName.Name + ".dll");
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
        return (FetchAssembly(Utility.TryResolveDllAssembly, new AssemblyName(args.Name), out Assembly? assembly) ? assembly : orig(sender, args))!;
    }

    public static bool Patch(string path, AssemblyName assemblyName, out string? patchPath)
    {
        try
        {
            bool patched = false;
            string formattedAssemblyName = assemblyName.Name.Replace(" ", "");
            List<Type> patchers = AppDomain.CurrentDomain.GetAssemblies().Select(i => i?.GetType("FloodgatePatchers." + formattedAssemblyName, false, true)).Where(ix => ix != null).ToList()!;

        TRYAGAIN:
            AssemblyDefinition? assembly = AssemblyDefinition.ReadAssembly(path);
            try
            {
                for (int i = 0; i < patchers.Count; i++)
                {
                    try
                    {
                        MethodInfo? patcher = patchers[i]?.GetMethod("Patcher", BindingFlags.Public | BindingFlags.Static, null, [typeof(AssemblyDefinition).MakeByRefType(), typeof(string), typeof(bool)], null);
                        if (patcher != null)
                        {
                            object[] param = [assembly, CurrentVersion, IsLatest];
                            CustomLog.Log("Trying to patch " + assemblyName.Name + " using " + patchers[i].Assembly.FullName + " -- " + patchers[i].FullName);
                            patcher.Invoke(null, param);
                            CustomLog.Log("Sucessfully patched " + assemblyName.Name + " using " + patchers[i].AssemblyQualifiedName);
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
            catch (Exception ex)
            {
                CustomLog.LogError(ex.ToString());
                patched = false;
            }
            if (patched)
            {
                patchPath = (CacheLocation + Path.DirectorySeparatorChar + assemblyName.Name + ".dll");
                assembly.Write(patchPath);
            }
            else
            {
                patchPath = null;
            }

            return patched;
        }catch (Exception ex)
        {
            CustomLog.LogError("Assembly Patcher Fucked Up\n"+ex.ToString());
            patchPath = null;
            return false;
        }
    }

    public static Dictionary<string, string?> OverridenPaths = new();
    public delegate string orig_PluginInfo_Location(PluginInfo self);
    public static string On_PluginInfo_Location(orig_PluginInfo_Location orig, PluginInfo self)
    {
        string fallback = orig(self);
        if (OverridenPaths.ContainsKey(fallback))
        {
            if (debug)
            {
                CustomLog.Log("PluginInfo location called; " + fallback + " -> " + OverridenPaths[fallback] ?? "null");
            }
            return OverridenPaths[fallback] ?? fallback;
        }
        AssemblyName asmName = AssemblyName.GetAssemblyName(fallback);
        bool overridFound = FetchAssembly(ResolveAssembly, asmName, out string? overrid);
        string res = overrid ?? fallback;
        if (IsLatest && fallback.ToLowerInvariant().Contains("newest") && overridFound && fallback != overrid)
        {
            CustomLog.Log("Mod Loader Fucked Up, reverting from " + res + " to " + fallback);
            res = fallback;
        }
        if (OverrideAssembly(res, asmName, out string asmOverride))
        {
            res = asmOverride;
        }
        bool overriden = res != fallback;

        if (!overridFound)
        {
            CustomLog.Log("Alternate path null for `" + fallback + "`, expected at least equal");
        }
        if (overriden)
        {
            CustomLog.Log("Found different assembly path from " + fallback + " at " +  res);
        }
        AssemblyName assemblyName = AssemblyName.GetAssemblyName(res);
        try
        {
            if (Patch(res, assemblyName, out string? patchPath))
            {
                CustomLog.Log("Setting up patched assembly " + assemblyName.Name + " from " + res);
                OverridenPaths[fallback] = patchPath!;
                return (patchPath!);
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

    //version is the latest version of compiling, target is the game's current version. true means it's the latest version (based on the last version i updated this)
    public static bool ParseLatestVersion(string version, string target)
    {
        string[] versionsplit = version.Split('.');
        string[] targetsplit = target.Split('.');
        
        int min = Math.Min(versionsplit.Length, targetsplit.Length);
        for (int i = 0; i < min; i++)
        {
            int ver = int.Parse(new(versionsplit[i].Where(char.IsDigit).ToArray()));
            int tar = int.Parse(new(targetsplit[i].Where(char.IsDigit).ToArray()));
            if (ver != tar)
            {
                return tar > ver;
            }
        }
        //reaching this point is assuming the versions are the same

        return targetsplit.Length >= versionsplit.Length;
    }

#nullable disable

    public delegate bool orig_TryResolveDllAssembly(AssemblyName assemblyName, string dir, out Assembly assembly);
    public static bool TryResolveDllAssemblyOverride(orig_TryResolveDllAssembly orig, AssemblyName assemblyName, string dir, out Assembly assembly)
    {
        assembly = null;

        if(AppDomain.CurrentDomain.GetAssemblies().Any(i=>i.GetName().Name == assemblyName.Name))
        {
            assembly = AppDomain.CurrentDomain.GetAssemblies().First(i => i.GetName().Name == assemblyName.Name);
            return true;
        }

        foreach(var i in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories).Prepend(dir))
        {
            string text;
            if(OverrideAssembly((text = (i + Path.DirectorySeparatorChar + assemblyName.Name + ".dll")), assemblyName, out string asmOverride))
            {
                text = asmOverride;
            }
            if (File.Exists(text))
            {
                bool loaded = false;
                try
                {
                    if (Patch(text, assemblyName, out string patchPath))
                    {
                        assembly = Assembly.LoadFile(patchPath);
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
    public static void IL_TryResolveDllAssembly(ILContext il)
    {
        try
        {
            ILCursor c = new(il);
            if (c.TryGotoNext(MoveType.After, i => i.MatchNop()))
            {
                c.Emit(Mono.Cecil.Cil.OpCodes.Ldloca_S, (byte)1);
                c.EmitDelegate(static delegate (ref string text2)
                {
                    if (OverrideAssembly(text2, AssemblyName.GetAssemblyName(text2), out string asmOverride))
                    {
                        text2 = asmOverride;
                    }
                });
            }
            else
            {
                CustomLog.Log("Couldn't IL hook TryResolveDllAssembly");
            }
        }
        catch (Exception ex)
        {
            CustomLog.LogError(ex.ToString());
        }
    }

    public static readonly Dictionary<string,string> OverridenAssembliesPaths = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    public static bool OverrideAssembly(string path, AssemblyName assemblyName, out string assembly)
    {
        try
        {
            if (!File.Exists(path))
            {
                assembly = null;
                return false;
            }
            string hash = null;
            using (FileStream fs = File.OpenRead(path))
            using (System.Security.Cryptography.SHA512 sha512 = System.Security.Cryptography.SHA512.Create())
            {
                hash = string.Join("", sha512.ComputeHash(fs).Select(x => x.ToString("x2")));
            }
            string overridepath;
            if (IsLatest && hash is not null &&
                (File.Exists((overridepath = (FloodgatePath + Path.DirectorySeparatorChar + "AssemblyOverride" + Path.DirectorySeparatorChar + hash + Path.DirectorySeparatorChar + assemblyName.Name + ".fdll")))
                || File.Exists((overridepath = (FloodgatePath + Path.DirectorySeparatorChar + "AssemblyOverride" + Path.DirectorySeparatorChar + hash + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(path) + ".fdll")))
                || File.Exists((overridepath = (FloodgatePath + Path.DirectorySeparatorChar + "AssemblyOverride" + Path.DirectorySeparatorChar + hash + ".fdll")))))
            {
                CustomLog.Log("Overriding assembly path `" + path + "` with `" + overridepath + "`");
                OverridenAssembliesPaths[(assembly = overridepath)] = path;
                return true;
            }
            else
            {
                assembly = null;
                return false;
            }
        }
        catch (Exception e)
        {
            CustomLog.LogError(e.ToString());
            assembly = null;
            return false;
        }
    }


    public static void IL_Assembly_LoadFile(ILContext context)
    {
        try
        {
            ILCursor c = new(context);
            c.Goto(0);
            c.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
            c.EmitDelegate(static delegate (string path)
            {
                CustomLog.Log("[DEBUG] Trying to load Assembly from path: " + path + "\n" + Environment.StackTrace);
            });
        }
        catch(Exception e)
        {
            CustomLog.LogError(e.ToString());
        }
    }
}
