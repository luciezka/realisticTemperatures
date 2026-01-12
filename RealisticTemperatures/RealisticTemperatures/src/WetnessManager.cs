using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
namespace RealisticTemperatures;

public class WetnessManager
{
    public static float GetWetness(ItemStack itemStack)
    {
        var attributes = itemStack?.Collectible.Attributes;
        if (attributes is null) return 100f;

        return attributes.Token.Value<float>(Attributes.Wetness).GuardFinite();
    }
    
  
}
