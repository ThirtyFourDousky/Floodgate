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
        ModuleDefinition module = assembly.MainModule;
        module.GetType("MoreSlugcats.MoreSlugcatsEnums").NestedTypes.First(i => i.Name == "AbstractObjectType")
            .Fields.Add(new("SingularityBomb", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
            module.ImportReference(module.GetType("AbstractPhysicalObject").NestedTypes.First(i => i.Name == "AbstractObjectType"))));
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

        ModLoader.AssemblyCSharp.GetType("MoreSlugcats.MoreSlugcatsEnums", false, true)?.GetNestedType("AbstractObjectType", BindingFlags.Public | BindingFlags.Static)?
            .GetField("SingularityBomb", BindingFlags.Public | BindingFlags.Static)?.SetValue(null,
            ModLoader.AssemblyCSharp.GetType("DLCSharedEnums", false, true)?.GetNestedType("AbstractObjectType", BindingFlags.Public | BindingFlags.Static)?
            .GetField("SingularityBomb", BindingFlags.Public | BindingFlags.Static)?.GetValue(null));

        OtherHooks.Apply();
    }
}
