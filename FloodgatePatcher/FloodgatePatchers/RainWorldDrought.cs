/*
using FloodgatePatcher;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FloodgatePatchers;

public static class RainWorldDrought
{
    public static void Patcher(ref AssemblyDefinition assembly)
    {
        ModuleDefinition module = assembly.MainModule;
        //////////ModOverwritePreprocesors edit
        TypeDefinition ModOverwritePreprocessor = module.GetType("Rain_World_Drought.OverWorld.ModOverwritePreprocessor");
        //Apply; replace old customConditions
        var ModOverwriteMethod = ModOverwritePreprocessor.Methods.First(i => i.Name == "ModOverwrite");
        ModOverwriteMethod.ReturnType = module.ImportReference(typeof(bool?));
        ModOverwriteMethod.Parameters.Add(new ParameterDefinition("_", Mono.Cecil.ParameterAttributes.Unused, module.ImportReference(ModLoader.AssemblyCSharp.GetType("RainWorldGame", true, true))));
        ILProcessor MOP_Apply = ModOverwritePreprocessor.Methods.First(i => i.Name == "Apply").Body.GetILProcessor();
        while (MOP_Apply.Body.Instructions.Count > 0)
        {
            MOP_Apply.Remove(MOP_Apply.Body.Instructions[0]);
        }
        var preprocessorConditionsField = MOP_Apply.Import(ModLoader.AssemblyCSharp.GetType("WorldLoader", true, true).GetNestedType("Preprocessing")
            .GetField("preprocessorConditions", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
        MOP_Apply.Emit(OpCodes.Ldsfld, preprocessorConditionsField);
        MOP_Apply.Emit(OpCodes.Ldnull);
        MOP_Apply.Emit(OpCodes.Ldftn, ModOverwriteMethod);
        var PreprocessorConditionType = MOP_Apply.Import(ModLoader.AssemblyCSharp.GetType("WorldLoader").GetNestedType("Preprocessing").GetNestedType("PreprocessorCondition").GetConstructors()[0]);
        MOP_Apply.Emit(OpCodes.Newobj, PreprocessorConditionType);
        MOP_Apply.Emit(OpCodes.Callvirt, module.ImportReference(preprocessorConditionsField.FieldType.Resolve().Methods.First(i => i.Name == "Add")));
        MOP_Apply.Emit(OpCodes.Ret);


        /////////////Rain_World_Drought.Effects.DRCamoBeetle
        TypeDefinition DRCamoBeetleType = module.GetType("Rain_World_Drought.Effects.DRCamoBeetle");
        //update
        ILProcessor DRCB_Update = DRCamoBeetleType.Methods.First(i => i.Name == "Update").Body.GetILProcessor();
        DRCB_Update.Remove(DRCB_Update.Body.Instructions.First(i => i.MatchStfld("UnityEngine.Vector2", "y")));
        DRCB_Update.Replace(DRCB_Update.Body.Instructions.First(i => i.MatchLdflda("CosmeticSprite", "pos")),
            DRCB_Update.Create(OpCodes.Ldfld, DRCB_Update.Import(ModLoader.AssemblyCSharp.GetType("CosmeticSprite", true, true)
            .GetField("pos", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));
        DRCB_Update.Replace(DRCB_Update.Body.Instructions.First(i => i.MatchCallvirt("Room", "FloatWaterLevel")),
            DRCB_Update.Create(OpCodes.Callvirt, DRCB_Update.Import(ModLoader.AssemblyCSharp.GetType("Room", true, true)
            .GetMethod("FloatWaterLevel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));

        //////////////Drought OLOracleBehaviour
        TypeDefinition OLOracleBehavior = module.GetType("Rain_World_Drought.Moon.OLOracleBehavior");
        //TypeOfMiscItem
        ILProcessor OLOB_TypeOfMistItem = OLOracleBehavior.Methods.First(i => i.Name == "TypeOfMiscItem").Body.GetILProcessor();
        OLOB_TypeOfMistItem.Replace(OLOB_TypeOfMistItem.Body.Instructions.First(i => i.MatchLdsfld("MoreSlugcats.MoreSlugcatsEnums/AbstractObjectType", "Seed")),
            OLOB_TypeOfMistItem.Create(OpCodes.Ldsfld, OLOB_TypeOfMistItem.Import(ModLoader.AssemblyCSharp.GetType("DLCSharedEnums", true, true).GetNestedType("AbstractObjectType").GetField("Seed"))));
        //Update
        ILProcessor OLOB_Update = OLOracleBehavior.Methods.First(i => i.Name == "Update").Body.GetILProcessor();
        Instruction OLOBtarget = OLOB_Update.Body.Instructions.First(i => i.MatchLdarg(0) && i.Next.MatchCall("OracleBehavior", "get_rainWorld"));
        Instruction OLOBtarget2 = OLOBtarget.Next;
        OLOB_Update.Remove(OLOBtarget2);
        OLOB_Update.Replace(OLOBtarget, OLOB_Update.Create(OpCodes.Ldsfld, OLOB_Update.Import(ModLoader.AssemblyCSharp.GetType("RWCustom.Custom", true, true)
            .GetField("rainWorld", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic))));

        /////////System.Void Rain_World_Drought.OverWorld.RainWorldGameHK
        TypeDefinition OWRWGHKType = module.GetType("Rain_World_Drought.OverWorld.RainWorldGameHK");
        //WinHK
        var OWRWGHK_WinHK = OWRWGHKType.Methods.First(i => i.Name == "WinHK");
        OWRWGHK_WinHK.Parameters.Add(new ParameterDefinition("fromWarpPoint", Mono.Cecil.ParameterAttributes.None, module.ImportReference(typeof(bool))));
        ILProcessor OWRWGHK_WinHK_processor = OWRWGHK_WinHK.Body.GetILProcessor();
        Instruction OWRWGHKtarget = OWRWGHK_WinHK_processor.Body.Instructions.First(i => i.MatchLdarg(0) && i.Next.MatchLdarg(1) && i.Next.Next.MatchLdarg(2));
        OWRWGHK_WinHK_processor.InsertAfter(OWRWGHKtarget.Next.Next, OWRWGHK_WinHK_processor.Create(OpCodes.Ldarg_3));

        ////////////Rain_World_Drought.RainSystem.RoomRainHK
        TypeDefinition RSRoomRainHK = module.GetType("Rain_World_Drought.RainSystem.RoomRainHK");
        //BulletDripStrikeHK
        ILProcessor RoomRainDRIPSTRIKEHK = RSRoomRainHK.Methods.First(i => i.Name == "BulletDripStrikeHK").Body.GetILProcessor();
        Instruction RRtarget = RoomRainDRIPSTRIKEHK.Body.Instructions.First(i => i.MatchLdflda("BulletDrip", "pos") && i.Next.MatchLdfld("UnityEngine.Vector2", "x")
        && i.Next.Next.MatchCallvirt("Room", "FloatWaterLevel"));
        RoomRainDRIPSTRIKEHK.Remove(RRtarget.Next);
        RoomRainDRIPSTRIKEHK.Replace(RRtarget, RoomRainDRIPSTRIKEHK.Create(OpCodes.Ldfld, RoomRainDRIPSTRIKEHK.Import(ModLoader.AssemblyCSharp.GetType("BulletDrip", true, true)
            .GetField("pos", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));

        Instruction RRtarget2 = RoomRainDRIPSTRIKEHK.Body.Instructions.First(i => i.MatchLdflda("BulletDrip", "pos") && i.Next.MatchLdfld("UnityEngine.Vector2", "x")
        && i.Next.Next.Next.Next.Next.Next.MatchCallvirt("Water", "WaterfallHitSurface"));
        RoomRainDRIPSTRIKEHK.Remove(RRtarget2.Next);
        RoomRainDRIPSTRIKEHK.Replace(RRtarget2, RoomRainDRIPSTRIKEHK.Create(OpCodes.Ldfld, RoomRainDRIPSTRIKEHK.Import(ModLoader.AssemblyCSharp.GetType("BulletDrip", true, true)
            .GetField("pos", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));

        //////////Rain_World_Drought.PlacedObjects.AbstractPhysicalObjectHK
        TypeDefinition AbsPhHK = module.GetType("Rain_World_Drought.PlacedObjects.AbstractPhysicalObjectHK");
        //SaveState_AbstractPhysicalObjectFromString
        ILProcessor AbsPH_SaveState_AbstractPhysicalObjectFromString = AbsPhHK.Methods.First(i => i.Name == "SaveState_AbstractPhysicalObjectFromString").Body.GetILProcessor();
        VariableDefinition AbsphkOBbool = new VariableDefinition(AbsPH_SaveState_AbstractPhysicalObjectFromString.Import(typeof(bool)));
        VariableDefinition AbsphkOBarray = new VariableDefinition(AbsPH_SaveState_AbstractPhysicalObjectFromString.Import(typeof(string[])));

        AbsPH_SaveState_AbstractPhysicalObjectFromString.Body.Variables.Add(AbsphkOBbool);
        AbsPH_SaveState_AbstractPhysicalObjectFromString.Body.Variables.Add(AbsphkOBarray);

        AbsPH_SaveState_AbstractPhysicalObjectFromString.Body.InitLocals = true;
        Instruction AbsphSStarget = AbsPH_SaveState_AbstractPhysicalObjectFromString.Body.Instructions.First(i => i.MatchLdloc(0) && i.Next.MatchLdcI4(0) && i.Next.Next.MatchLdelemRef()).Next.Next;
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldstr, "<oB>"));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Callvirt, AbsPH_SaveState_AbstractPhysicalObjectFromString.Import(typeof(System.String)
            .GetMethod("Contains", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Stloc_S, AbsphkOBbool));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldloc_S, AbsphkOBbool));
        //4 next is also brfalse
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Nop));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldarg_2));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldstr, "<oB>"));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Call, AbsPH_SaveState_AbstractPhysicalObjectFromString.Import(typeof(System.Text.RegularExpressions.Regex).GetMethod("Split", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null))));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Stloc_S, AbsphkOBarray));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldloc_S, AbsphkOBarray));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldc_I4_0));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldelem_Ref));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Call, AbsPH_SaveState_AbstractPhysicalObjectFromString.Import(ModLoader.AssemblyCSharp.GetType("EntityID").GetMethod("FromString", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Stloc_2));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Nop));
        //next has a br.s
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Nop));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldloc_0));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldc_I4_0));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Ldelem_Ref));
        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Nop));

        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Br_S, AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next));

        AbsPH_SaveState_AbstractPhysicalObjectFromString.InsertAfter(AbsphSStarget.Next.Next.Next.Next,
            AbsPH_SaveState_AbstractPhysicalObjectFromString.Create(OpCodes.Brfalse, AbsphSStarget.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next.Next));

        //////////////Rain_World_Drought.PlacedObjects.GravityAmplifier
        TypeDefinition GravityAmplifier_Type = module.GetType("Rain_World_Drought.PlacedObjects.GravityAmplifier");
        //GravityForce
        ILProcessor GAGravityForce = GravityAmplifier_Type.Methods.First(i => i.Name == "GravityForce").Body.GetILProcessor();
        var importedwaterwawa = module.ImportReference(ModLoader.AssemblyCSharp.GetType("Water"));
        TypeDefinition importerwawawawa = importedwaterwawa.Resolve().NestedTypes.First(i => i.Name == "Surface");
        var _importedSurface = importerwawawawa.Resolve().Methods.First(i=>i.Name=="PreviousPoint");
        var importedSurface = module.ImportReference(_importedSurface);
        var watertarget0i = GAGravityForce.Create(OpCodes.Callvirt, importedSurface);


        Instruction waterTarget00 = GAGravityForce.Body.Instructions.First(i => i.MatchLdarg(0) && i.Next.MatchLdarg(1) && i.Next.Next.MatchCallvirt("Water", "PreviousSurfacePoint"));
        GAGravityForce.Replace(waterTarget00.Next.Next,
            watertarget0i);
        GAGravityForce.InsertAfter(waterTarget00,
            GAGravityForce.Create(OpCodes.Call, GAGravityForce.Import(ModLoader.AssemblyCSharp.GetType("Water")
            .GetMethod("get_MainSurface", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));

        var watertarget1i = GAGravityForce.Create(OpCodes.Callvirt, importedSurface);

        Instruction waterTarget01 = GAGravityForce.Body.Instructions.First(i => i.MatchLdarg(0) && i.Next.MatchLdarg(2) && i.Next.Next.MatchCallvirt("Water", "PreviousSurfacePoint"));
        GAGravityForce.Replace(waterTarget01.Next.Next,
            watertarget1i);
        GAGravityForce.InsertAfter(waterTarget01,
            GAGravityForce.Create(OpCodes.Call, GAGravityForce.Import(ModLoader.AssemblyCSharp.GetType("Water")
            .GetMethod("get_MainSurface", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));

        Instruction waterTarget2 = GAGravityForce.Body.Instructions.First(i => i.MatchLdarg(0) && i.Next.MatchLdfld("Water", "surface") && i.Next.Next.MatchLdcI4(0)).Next;
        GAGravityForce.Replace(waterTarget2,
            GAGravityForce.Create(OpCodes.Ldfld, GAGravityForce.Import(ModLoader.AssemblyCSharp.GetType("Water")
            .GetField("surfaces", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));

        Instruction waterTarget3 = GAGravityForce.Body.Instructions.First(i => i.MatchLdarg(0) && i.Next.MatchLdfld("Water", "surface") && i.Next.Next.MatchLdloc(3)).Next;
        
        FieldReference watertarget3imported = module.ImportReference(module.ImportReference(ModLoader.AssemblyCSharp.GetType("Water")).Resolve().NestedTypes.First(i => i.Name == "Surface").Resolve().Fields.First(i=>i.Name== "points"));
        
        GAGravityForce.InsertAfter(waterTarget3,
            GAGravityForce.Create(OpCodes.Ldfld, watertarget3imported));
        GAGravityForce.Replace(waterTarget3,
            GAGravityForce.Create(OpCodes.Callvirt, GAGravityForce.Import(ModLoader.AssemblyCSharp.GetType("Water")
                .GetMethod("get_MainSurface", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))));

    }
}

*/