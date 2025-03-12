using VibeGamedev;
using System;
using UnityEngine;
using System.Collections.Generic;

public static class CustomPropertyOverrideSample
{
    public class RectTransformOverride : IComponentPropertiesOverride
    {
        public Type Type => typeof(RectTransform);

        public Serialization.SerializedProperty[] GetProperties(Component component)
        {
            var rectTransform = (RectTransform)component;
            return new Serialization.SerializedProperty[] {
                new("x", rectTransform.localPosition.x),
                new("y", rectTransform.localPosition.y),
                new("width", rectTransform.rect.width),
                new("height", rectTransform.rect.height),
            };
        }

        public List<string> SerializedPropertyNames => new() { "x", "y", "width", "height" };
    }

    // You should do this in an editor script and make sure to run it on load.
    public static void RegisterCustomPropertyOverride()
    {
        IComponentPropertiesOverride.RegisterOverride(new RectTransformOverride());
    }
}