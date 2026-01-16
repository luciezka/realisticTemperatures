using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RealisticTemperatures.assets;

[HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "OnGameTick")]
public static class OnGameTickTemperaturePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions);

        // Apply Rain slower: 0.06f -> 0.012f
        matcher.MatchStartForward(
                new CodeMatch(OpCodes.Ldc_R4, 0.06f))
            .SetOperandAndAdvance(0.012f);

        // Change damage: 0.2f -> 2f
        matcher.Start().MatchStartForward(
                new CodeMatch(OpCodes.Ldc_R4, 0.2f))
            .SetOperandAndAdvance(2f);

        // Shockfreezing: -6.0f -> -50.0f
        matcher.Start()
            .MatchStartForward(new CodeMatch(OpCodes.Ldc_R4, -6.0f))
            .SetOperandAndAdvance(-50.0f);

        // Change time until freeze damage applies: 3.0f -> 1f (for damagingFreezeHours field)
        matcher.Start()
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldfld,AccessTools.Field(null, "damagingFreezeHours")),
                new CodeMatch(OpCodes.Ldc_R4, 3.0f)).Advance(1)
            .SetOperandAndAdvance(1f);
        
        
        // Replace WetnessMultiplier 15f with CalculateDebuffMultiplier call
        matcher.Start()
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldc_R4, 15f))
            .RemoveInstruction().Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call,typeof(OnGameTickTemperaturePatch).GetMethod("CalculateDebuffMultiplier")));


        
        matcher.Start()
            .MatchStartForward(
                new CodeMatch(OpCodes.Call, AccessTools.PropertyGetter(typeof(EntityBehaviorBodyTemperature), "nearHeatSourceStrength")),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "inEnclosedRoom")),
                new CodeMatch(i => i.opcode == OpCodes.Brtrue || i.opcode == OpCodes.Brtrue_S)
            );

        matcher.MatchStartForward(
            new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 1f
            )).SetOperandAndAdvance(0f);
        
        
        return matcher.InstructionEnumeration();
    }
    
    
    

    public static float CalculateDebuffMultiplier(EntityBehaviorBodyTemperature instance)
    {
        if (instance?.entity?.Api?.World?.BlockAccessor == null)
            return 15f;
        BlockPos asBlockPos = instance.entity.Pos.AsBlockPos;
        ClimateCondition climateAt = instance.entity.Api.World.BlockAccessor.GetClimateAt(asBlockPos);
        return climateAt == null ? 15f : GameMath.Clamp((float)(-2 * (double)climateAt.Temperature + 30), 15f, 100f);
    }
}