using FloodgatePatcher;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using System;
using System.Reflection;

namespace ModCompat.CRS;

public static class IndexedEntranceClass
{
    //public readonly static List<IDetour> hooks = new List<IDetour>();
    internal static void Apply()
    {
        //hooks.Add(new ILHook(typeof(global::CustomRegions.CustomWorld.IndexedEntranceClass).GetMethod("AbstractRoom_ExitIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic), IL_AbstractRoom_ExitIndex));
        InlineIL.IL.Emit.Ldtoken(new InlineIL.MethodRef(typeof(global::CustomRegions.CustomWorld.IndexedEntranceClass), "AbstractRoom_ExitIndex"));
        InlineIL.IL.Pop<RuntimeMethodHandle>(out RuntimeMethodHandle handle);
        HookEndpointManager.Modify(MethodBase.GetMethodFromHandle(handle) , IL_AbstractRoom_ExitIndex);
    }
    public static void IL_AbstractRoom_ExitIndex(ILContext il)
    {
        try
        {
            ILCursor c = new(il);
            ILLabel tryIL = c.MarkLabel();
            ILLabel catchIL = c.DefineLabel();
            ILLabel endIL = c.DefineLabel();

            //ignore directly if targetRoom is -1, exceptions are bad for performance. but the try will still be here cuz im untrustworthy
            c.Emit(OpCodes.Ldarg_2);
            c.Emit(OpCodes.Ldc_I4_M1);
            c.Emit(OpCodes.Beq_S, endIL);

            c.GotoNext(MoveType.Before, x => x.MatchLdarg(0));
            c.Emit(OpCodes.Leave_S, endIL);
            c.MarkLabel(catchIL);
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Leave_S, endIL);
            c.MarkLabel(endIL);
            il.Body.ExceptionHandlers.Add(new(ExceptionHandlerType.Catch)
            {
                TryStart = tryIL.Target,
                TryEnd = catchIL.Target,
                HandlerStart = catchIL.Target,
                HandlerEnd = endIL.Target,
                CatchType = il.Import(typeof(Exception))
            });
        }
        catch (Exception ex)
        {
            CustomLog.LogError("[MOD COMPAT - CRS] IndexedEntranceClass hook failed\n" + ex.ToString());
        }
    }
}
