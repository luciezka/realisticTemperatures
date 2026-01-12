namespace RealisticTemperatures;

public static class Utils
{
    
    public static float GuardFinite(this float value, float defaultValue = 0f) => float.IsFinite(value) ? value : defaultValue;

}