using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace RealisticTemperatures.assets;

public partial class EntityBehaviorsBodyTemperatureHot(Entity entity) : EntityBehavior(entity)
{
    
    public override string PropertyName() => "bodytemperaturehot";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
       
    }
    
    
}