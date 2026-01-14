using HarmonyLib;
using RealisticTemperatures.assets;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RealisticTemperatures;

public class RealisticTemperaturesModSystem : ModSystem
{
    private static Harmony harmony;

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.Start(api);
        var harmony = new Harmony(Mod.Info.ModID);
        harmony.PatchAll();
    }

    public override void Start(ICoreAPI api)
    {
        ICoreServerAPI sapi = api as ICoreServerAPI;
        if (sapi == null)
            return;


        sapi.RegisterCommand("debugwetness", "Log wetness data", "",
            (ServerChatCommandDelegate)((player, group, args) =>
            {
                EntityBehaviorBodyTemperature behavior = player.Entity.GetBehavior<EntityBehaviorBodyTemperature>();
                ClimateCondition climateAt = sapi.World.BlockAccessor.GetClimateAt(player.Entity.Pos.AsBlockPos);
                player.SendMessage(group,
                    $"Temperature: {(climateAt != null ? climateAt.Temperature : 0.0f)}°C\nWetness: {behavior.Wetness}\nDebuff Multiplier: {OnGameTickTemperaturePatch.CalculateDebuffMultiplier(behavior)}",
                    EnumChatType.Notification);
            }), Privilege.controlserver);


        sapi.RegisterCommand("setWetnessClothes", "sets wetness on held item", "[wetness value]",
            (ServerChatCommandDelegate)((player, group, args) =>
            {
                ItemStack heldItem = player.Entity.RightHandItemSlot?.Itemstack;
                if (float.TryParse(args[0], out float wetnessValue))
                {
                    heldItem.Attributes.SetFloat("wetness", wetnessValue);
                    player.Entity.RightHandItemSlot.MarkDirty();
                    player.SendMessage(group, $"Set wetness to {wetnessValue}", EnumChatType.CommandSuccess);
                }
            }), Privilege.controlserver);
    }


    public static bool Prefix(ref string message)
    {
        message = "Realistic Temperatures";
        return false;
    }
}