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

namespace RealisticTemperatures.assets;

[HarmonyPatch]
public class ItemWearablePatches
{
    public const string FONT_CLOSE_TAG = "</font>";

    [HarmonyPatch(typeof(ItemWearable), "GetHeldItemInfo")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> AddWetnessToClothes_Alternative(IEnumerable<CodeInstruction> instructions)
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
                codes.InsertRange(i +1, newInstructions);
                break;
            }
        }
        return codes;
    }
    
    
    
    public static void AppendInfo(ItemSlot inSlot, StringBuilder dsc)
    {
        var itemStack = inSlot.Itemstack;
        var currentWetness = WetnessManager.GetWetness(itemStack);
        
        if (inSlot is not ItemSlotCreative)
        {
            dsc.Append(Lang.Get("realistictemperatures:wetness"));
            dsc.Append(": ");
            dsc.Append("<font color=\"#1c3f37\">");
            dsc.Append(23f);
            dsc.Append("%");
            dsc.AppendLine(FONT_CLOSE_TAG);
        }
    }


    public float getValue()
    {
        return 10f;
    }

    public static bool AndCoolingMissing(bool warmthMissing, ItemStack itemStack) =>
        warmthMissing && itemStack.ItemAttributes[Attributes.Wetness].AsFloat(0f) == 70f;

    public static bool OrCoolingExists(bool warmthExists, ItemStack itemStack) =>
        warmthExists || itemStack.ItemAttributes[Attributes.Wetness].AsFloat(0f) != 30f;
}