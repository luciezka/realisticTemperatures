using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RealisticTemperatures.assets;

[HarmonyPatch]
public static class OnGameTickTemperaturePatch
{
    public static float noClothesDryingSpeedMul = 1.5f;


    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "OnGameTick")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions);
        for (int i = 0; i < code.Count; i++)
        {
            // Look for WetnessMultiplier
            if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 15f)
            {
                code[i] = new CodeInstruction(OpCodes.Ldarg_0);

                // Replace max value 15 with CalculateDebuffMultiplier for Wettnessmultiplication
                code.Insert(i + 1, new CodeInstruction(
                    OpCodes.Call,
                    typeof(OnGameTickTemperaturePatch).GetMethod("CalculateDebuffMultiplier")));
                i++;
            }

            // change damage to 2 instead of 0.2 MAYBE rewrk that
            else if (code[i].opcode == OpCodes.Ldc_R4 && (float)code[i].operand == 0.2f)
            {
                code[i].operand = 2f;
            }

            // lets the ambientChange be quite big
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


    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "OnGameTick")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        var codes = new List<CodeInstruction>(instructions);


        int startReplace = 0;
        int endReplace = 0;


        var code = new List<CodeInstruction>(instructions);
        for (int i = 0; i < code.Count; i++)
        {
            // Look for Rainfall
            if (code[i].opcode == OpCodes.Ldfld && ((FieldInfo)code[i].operand).Name == "Rainfall")
            {
                startReplace = i - 1;
            }

            if (code[i].opcode == OpCodes.Call &&
                (codes[i].Calls(AccessTools.Method(typeof(EntityBehaviorBodyTemperature), "set_Wetness"))))
            {
                endReplace = i;
            }
        }

        if (startReplace == -1 || endReplace == -1)
            return code;


        var newInstructions = new List<CodeInstruction>
        {
            // Load all parameters for RealisticWetnessOverride
            new CodeInstruction(OpCodes.Ldarg_0), // this (get api from instance)
            new CodeInstruction(OpCodes.Call,
                AccessTools.PropertyGetter(typeof(EntityBehaviorBodyTemperature), "entity")),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(EntityBehavior), "entity")),

            new CodeInstruction(OpCodes.Ldarg_0), // entity
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "entity")),

            new CodeInstruction(OpCodes.Ldloc_S, 4), // eplr (adjust index as needed)
            new CodeInstruction(OpCodes.Ldloc_0), // conds
            new CodeInstruction(OpCodes.Ldarg_0), // this.Wetness
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "Wetness")),
            new CodeInstruction(OpCodes.Ldloc_1), // rainExposed
            new CodeInstruction(OpCodes.Ldarg_0), // this.LastWetnessUpdateTotalHours
            new CodeInstruction(OpCodes.Ldfld,
                AccessTools.Field(typeof(EntityBehaviorBodyTemperature), "LastWetnessUpdateTotalHours")),
            new CodeInstruction(OpCodes.Ldloc_3), // nearHeatSourceStrength

            // Call our custom function
            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(OnGameTickTemperaturePatch),
                    nameof(OnGameTickTemperaturePatch.RealisticWetnessOverride))),
        };


        code.RemoveRange(startReplace, endReplace - startReplace + 1);

        // Insert your function call
        codes.InsertRange(startReplace, newInstructions);
        
        
        return code;
    }


    public static void RealisticWetnessOverride(ICoreAPI api, Entity entity, EntityPlayer eplr, ClimateCondition conds,
        float Wetness, bool rainExposed, double LastWetnessUpdateTotalHours, float nearHeatSourceStrength)
    {
        //  Define all Wetnessclothing slots 
        var charInv = eplr.Player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);

        var clothingSlotTypes = new[]
        {
            EnumCharacterDressType.Head,
            EnumCharacterDressType.UpperBodyOver,
            EnumCharacterDressType.UpperBody,
            EnumCharacterDressType.LowerBody,
            EnumCharacterDressType.Foot
        };

        List<ItemStack> clothingArray = clothingSlotTypes
            .Select(type => charInv?.FirstOrDefault(slot => (slot as ItemSlotCharacter)?.Type == type))
            .Where(slot => slot?.Itemstack != null)
            .Select(slot => slot.Itemstack)
            .ToList();


        // made rain apply 50% slower
        float wetnessFromRain =
            conds.Rainfall * (rainExposed ? 0.012f : 0) *
            (conds.Temperature < -1 ? 0.05f : 1); /* Get wet 20 times slower with snow */

        // add itemprotection from rain
        if (wetnessFromRain > 0 && eplr != null)
        {
            float totalRainProtection = 0f;

            // added held rainprotection for items
            if (eplr.RightHandItemSlot?.Itemstack != null && !eplr.RightHandItemSlot.Empty)
                totalRainProtection += eplr.RightHandItemSlot.Itemstack.ItemAttributes["rainProtectionPerc"].AsFloat(0);

            if (eplr.LeftHandItemSlot?.Itemstack != null && !eplr.LeftHandItemSlot.Empty)
                totalRainProtection += eplr.LeftHandItemSlot.Itemstack.ItemAttributes["rainProtectionPerc"].AsFloat(0);

            // Add protection from head slot (hat/helmet)
            if (clothingArray.Count > 0 && clothingArray[0] != null)
                totalRainProtection += clothingArray[0].ItemAttributes["rainProtectionPerc"].AsFloat(0);

            wetnessFromRain *= GameMath.Clamp(1 - totalRainProtection, 0, 1);
        }


        // Apply Wetness to clothes 
        if (entity.Swimming || wetnessFromRain > 0)
        {
            float wetnessfromWorld = wetnessFromRain + (entity.Swimming ? 1 : 0);

            foreach (ItemStack itemStack in clothingArray)
            {
                float currentWetness = itemStack.ItemAttributes["Wetness"].AsFloat();
                float newWetness = GameMath.Clamp(currentWetness + wetnessfromWorld, 0, 1);

                itemStack.Attributes.SetFloat("wetness", newWetness);
            }
        }


        // Apply Wetness from Clothes to body
        if (clothingArray.Count > 0)
        {
            var clothWetness = 0f;
            clothWetness = clothingArray.Average(item => item.ItemAttributes["wetness"].AsFloat(0));
            Wetness = clothWetness;
        } // If no clothes are worn default to vanilla logic 
        else
        {
            Wetness = GameMath.Clamp(
                Wetness
                + wetnessFromRain
                + (entity.Swimming ? 1 : 0)
                - (float)Math.Max(0,
                    (api.World.Calendar.TotalHours - LastWetnessUpdateTotalHours) * noClothesDryingSpeedMul *
                    GameMath.Clamp(nearHeatSourceStrength, 1, 4))
                , 0, 1);
        }


        // TODO add hat soaking mechanic ? maybe who knows 
    }
}