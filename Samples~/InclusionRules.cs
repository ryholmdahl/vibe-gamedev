using VibeGamedev;
using UnityEngine;
using System.Linq;

public static class InclusionRulesSample
{
    public static bool ShouldFieldBeSerialized(Component component, System.Reflection.FieldInfo field)
    {
        return Serialization.fieldInclusionRules.All(rule => rule(component, field));
    }

    public static bool ShouldPropertyBeSerialized(Component component, System.Reflection.PropertyInfo property)
    {
        return Serialization.propertyInclusionRules.All(rule => rule(component, property));
    }

    public static void RequireFieldToHaveASillyName()
    {
        Serialization.fieldInclusionRules.Add((c, f) => f.Name.Contains("silly"));
    }

    public static void RequirePropertyToHaveASillyName()
    {
        Serialization.propertyInclusionRules.Add((c, p) => p.Name.Contains("silly"));
    }
}