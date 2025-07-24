using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FloodgatePatcher;

public static class OtherHooks
{
    static readonly List<IDetour> hooks = new();
    static Assembly? assemblyCSharp = null;
    public static void Apply()
    {
        assemblyCSharp = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(i => i.GetName().Name == "Assembly-CSharp");
        hooks.Add(new Hook(assemblyCSharp.GetType("StaticWorld").GetMethod("InitCustomTemplates", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic), InitCustomTemplates));
    }

    public delegate void orig_InitCustomTemplates();
    static int ictCount = 0;
    public static void InitCustomTemplates(orig_InitCustomTemplates orig)
    {
        orig();
        if (ictCount > 10 && ictCount < 20)
        {
            Patcher.logger.LogInfo("Custom Templates Stack Below:\n" + new System.Diagnostics.StackTrace().ToString());
        }
        if(ictCount > 100)
        {
            throw new Exceptions.LoopException("Unexpected loop of the current method");
        }
        ictCount++;
    }
}
