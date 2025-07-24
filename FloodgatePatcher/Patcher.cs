using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FloodgatePatcher;

public static class Patcher
{
    public static ManualLogSource logger { get; private set; }
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

    public static void Patch(ref AssemblyDefinition assembly)
    {
    }

    public static void Initialize()
    {
        logger = Logger.CreateLogSource("FloodgatePatcher");
        ModLoader.Init();
    }

    public static void Finish()
    {
        CustomLog.Log("Running post Patchers");
        ModLoader.Hooks.Add(new Hook(typeof(PluginInfo).GetProperty("Location", BindingFlags.Public | BindingFlags.Instance).GetGetMethod(), ModLoader.On_PluginInfo_Location));
        ModLoader.AssemblyCSharp = AppDomain.CurrentDomain.GetAssemblies().First(i => i.GetName().Name == "Assembly-CSharp");

        OtherHooks.Apply();
    }
}
