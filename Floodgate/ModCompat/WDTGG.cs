using FloodgatePatcher;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ModCompat;

public static class WDTGGcompat
{
    internal static List<IDetour> hooks = new();
    internal static Task GateTask = null;

    internal static bool HookFound = false;
    public static void Apply()
    {
        hooks.Add(new ILHook(typeof(WDTGG.Hooks).GetMethod("Player_NewRoom", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), IL_WDTGG_NewRoom2));
        hooks.Add(new Hook(typeof(WDTGG.Hooks).GetMethod("Player_NewRoom", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), hook_WDTGGNewRoom));
    }
    public static void IL_WDTGG_NewRoom2(ILContext IL)
    {
        try
        {
            ILCursor c = new(IL);
            if (c.TryGotoNext(MoveType.Before, x => x.MatchLdarg(0)) && c.Next.Next.Next.Next.MatchCallvirt(out _))
            {
                c.RemoveRange(5);
                c.Goto(0);
                for(;c.Index < c.Instrs.Count; c.Index++)
                {
                    if (c.Next.MatchLdarg(1) && c.Next.Next != null && c.Next.Next.MatchLdfld("UpdatableAndDeletable", "room"))
                    {
                        List<ILLabel> labels = c.IncomingLabels.ToList();
                        c.RemoveRange(2);
                        c.Emit(OpCodes.Ldarg_2);
                        c.Goto(c.Prev);
                        if (labels.Count > 0)
                        {
                            foreach (ILLabel label in labels)
                            {
                                label.Target = c.Next;
                            }
                        }
                    }
                }
                c.Goto(0);
                c.GotoNext(x => x.MatchLdcI4(100));
                c.RemoveRange(3);
                c.Emit(OpCodes.Ldc_I4, 150);
                c.Emit(OpCodes.Ldc_I4_0);
                c.Emit(OpCodes.Ldc_I4_0);
                HookFound = true;
            }
            else
            {
                CustomLog.Log("[WDTGG Threading Stuff] IL hook NewRoom couldn't find injection point\n" + IL.ToString());
                UnHook();
            }
        }
        catch (Exception e)
        {
            CustomLog.LogError("[WDTGG Threading Stuff] IL hook NewRoom failed\n" + e.ToString());
            UnHook();
        }
    }
    public delegate void orig_WDTGGNewRoom(On.Player.orig_NewRoom orig, Player self, Room roomIn);
    public static void hook_WDTGGNewRoom(orig_WDTGGNewRoom _orig, On.Player.orig_NewRoom orig, Player self, Room roomIn)
    {
        if (!HookFound)
        {
            _orig(orig, self, roomIn);
            return;
        }
        try
        {
            if(GateTask != null)
            {
                GateTask.GetAwaiter().GetResult();
                GateTask = null;
            }
            GateTask = Task.Run(() =>
            {
                _orig(null, self, roomIn);
            });
        }
        catch (Exception ex)
        {
            CustomLog.LogError("[WDTGG Threading Stuff] WDTGG hook fucked up\n" + ex.ToString());
        }
        orig(self, roomIn);
    }
    internal static void UnHook()
    {
        CustomLog.Log("[WDTGG Threading Stuff] Unhooking");
        try
        {
            for (int i = 0; i < hooks.Count; i++)
            {
                hooks[i].Dispose();
            }
            hooks.Clear();
        }catch (Exception ex)
        {
            CustomLog.LogError("[WDTGG Threading Stuff] Error Unhooking (HOW????)\n" + ex.ToString());
        }
    }
}
