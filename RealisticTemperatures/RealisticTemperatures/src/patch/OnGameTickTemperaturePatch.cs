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



[HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "updateBodyTemperature")]
public static class UpdateBodyTemperaturePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions);

        // Apply Rain slower: 0.06f -> 0.012f
        matcher.Start().MatchStartForward(
                new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && Math.Abs((float)i.operand - 0.06f) < 0.1f))
            .ThrowIfNotMatch("Could not find 0.06f rain constant")
            .SetOperandAndAdvance(0.012f);


        // Shockfreezing: -6 -> -50
        matcher.Start()
            .MatchStartForward(new CodeMatch(OpCodes.Ldc_R4, -6f))
            .SetOperandAndAdvance(-50f);

        // Replace WetnessMultiplier 15f with CalculateDebuffMultiplier call
        matcher.Start()
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldc_R4, 15f))
            .RemoveInstruction().Insert(
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call,
                    typeof(UpdateBodyTemperaturePatch).GetMethod("CalculateDebuffMultiplier")));

        matcher.Start()
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld,
                    AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "inEnclosedRoom")),
                new CodeMatch(i => i.opcode == OpCodes.Brtrue || i.opcode == OpCodes.Brtrue_S)
            )
            .ThrowIfNotMatch("Could not find inEnclosedRoom branch");
        matcher.Advance(3);

        matcher.MatchStartForward(
                new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && (float)i.operand == 1f)
            )
            .ThrowIfNotMatch("Could not find 1f operand after branch")
            .SetOperandAndAdvance(0f);

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


[HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "OnGameTick")]
public static class OnGameTickPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions);
        
        // Change damage: 0.2f -> 2f
        matcher.Start().MatchStartForward(
            new CodeMatch(OpCodes.Ldc_R4, 0.2f));
        if (matcher.IsValid)
        {
            matcher.SetOperandAndAdvance(2f);
        }

        // Change time until freeze damage applies: 3.0f -> 1f (for damagingFreezeHours field)
        matcher.Start()
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "damagingFreezeHours")),
                new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && Math.Abs((float)i.operand - 3f) < 0.0001f)
            )
            .ThrowIfNotMatch("Could not find damagingFreezeHours > 3f check")
            .Advance(1)
            .SetOperandAndAdvance(0.5f);
        
        return matcher.InstructionEnumeration();
    }
    
    
    


}