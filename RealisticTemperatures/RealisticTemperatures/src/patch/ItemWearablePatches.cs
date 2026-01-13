using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace RealisticTemperatures.assets;

[HarmonyPatch]
public class ItemWearablePatches
{
    public const string FONT_CLOSE_TAG = "</font>";
    
    
    
/// <summary>
/// Add information to Item before lore text
/// </summary>
    [HarmonyPatch(typeof(ItemWearable), "GetHeldItemInfo")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> AddWetnessToClothes(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var appendMethod = AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new[] { typeof(string) });


        var newInstructions = new List<CodeInstruction>
        {
            // Load the StringBuilder
            CodeInstruction.LoadArgument(1), // in slot
            CodeInstruction.LoadArgument(2), // dsc

            new CodeInstruction(OpCodes.Call,
                AccessTools.Method(typeof(ItemWearablePatches), nameof(AppendInfo))),
        };
        
        // find last append
        for (int i = codes.Count - 1; i >= 0; i--)
        {
            if (codes[i].opcode == OpCodes.Callvirt &&
                codes[i].operand is MethodInfo method &&
                method == appendMethod)
            {
                codes.InsertRange(i, newInstructions);
                break;
            }
        }
        return codes;
    }



/// <summary>
/// Make clothes wet when changed by interacting   ////// Delete all of this and change logic to make clothes wet IRL time 
/// player jumps in water -> clothes get wet -> player wetness dependent on clothing Wetness 
/// wetness = 5 solts of clothing wetness + then  / 5 makes avrg wetness.
///
/// shift ingame logic to apply a wetState to clothes  
/// </summary>
/// <param name="byEntity"></param>
    [HarmonyPatch(typeof(ItemWearable), "OnHeldInteractStart")]
    [HarmonyPostfix]
    static void Postfix(EntityAgent byEntity)
    {
        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
        var playerbodytemp = byPlayer.Entity.GetBehavior<EntityBehaviorBodyTemperature>();
        ItemStack heldItem = byPlayer.Entity.RightHandItemSlot?.Itemstack;
        
        if (playerbodytemp.Wetness > 0 &&  heldItem != null)
        {
            heldItem.Attributes.SetFloat("wetness", 100);
            playerbodytemp.Wetness -= 0.25f;
           
            byPlayer.Entity.RightHandItemSlot.MarkDirty();
        }
    }
    
    /// <summary>
    /// /Define wetnes string to be appended
    /// </summary>
    /// <param name="inSlot"></param>
    /// <param name="dsc"></param>
    public static void AppendInfo(ItemSlot inSlot, StringBuilder dsc)
    {
        if (inSlot is not ItemSlotCreative && inSlot.Itemstack.Attributes.GetFloat("wetness") != 0)
        {
            // Check if we already added this info
            if (dsc.ToString().Contains(Lang.Get("realistictemperatures:wetness")))
                return;
            dsc.Append(Lang.Get("realistictemperatures:wetness"));
            dsc.Append(": ");
            dsc.Append("<font color=\"#36bdbe\">");
            dsc.Append(inSlot.Itemstack.Attributes.GetFloat("wetness", 0).ToString());
            dsc.Append("%");
            dsc.AppendLine(FONT_CLOSE_TAG);
        }
    }

    
    

}














/* found a better alternative then use a transpiler but i would like to keep it as a look up



    */