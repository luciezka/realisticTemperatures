using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

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
    /// /Define wetnes string to be appended
    /// </summary>
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
            dsc.Append((inSlot.Itemstack.Attributes.GetFloat("wetness", 0)*100).ToString());
            dsc.Append("%");
            dsc.AppendLine(FONT_CLOSE_TAG);
        }
    }

}