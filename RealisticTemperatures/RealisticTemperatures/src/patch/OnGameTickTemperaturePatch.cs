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
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        for (int i = 0; i < code.Count; i++)
        {
            // Apply Rain slower
            if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 0.06f)
            {
                code[i].operand = 0.012f;
            }

            // Look for WetnessMultiplier
            else if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 15f)
            {
                code[i] = new CodeInstruction(OpCodes.Ldarg_0);

                // Replace max value 15 with CalculateDebuffMultiplier for Wettnessmultiplication
                code.Insert(i + 1, new CodeInstruction(
                    OpCodes.Call,
                    typeof(OnGameTickTemperaturePatch).GetMethod("CalculateDebuffMultiplier")));
                i++;
            }

            // change damage to 2 instead of 0.2 
            else if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 0.2f)
            {
                code[i].operand = 2f;
            }

            // lets the ambientChange be quite big / Shockfreezing
            else if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == -6.0f)
            {
                code[i].operand = -50.0f;
            }

            //change time until freeze dmg applies 
            else if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 3.0 &&
                     code[i + 1].opcode == OpCodes.Stfld &&
                     ((FieldInfo)code[i + 1].operand).Name == "damagingFreezeHours")
            {
                code[i].operand = 1f;
            }

            // Rooms shouldnt cool food and warm you at the same time 
            // Check for Room
            else if (code[i].opcode == OpCodes.Ldfld &&
                     code[i].operand is FieldInfo field &&
                     field.Name == "inEnclosedRoom" &&
                     (code[i + 1].opcode == OpCodes.Brtrue || code[i + 1].opcode == OpCodes.Brtrue_S))
            {
                for (int j = i + 1; j < i + 16; j++)
                {
                    // check if true value
                    if ((code[j].opcode == OpCodes.Br || code[j].opcode == OpCodes.Br_S)
                        && code[j + 1].opcode == OpCodes.Ldc_R4
                        && (float)code[j + 1].operand == 1f)
                    {
                        // no more Heating 
                        code[j + 1].operand = 0f;
                        break;
                    }
                }
            }
        }
        return code;
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