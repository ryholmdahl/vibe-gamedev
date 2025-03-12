using UnityEngine;
using System;
using System.Collections.Generic;

namespace VibeGamedev
{
    /// <summary>
    /// An interface to specify which fields/properties of a Component should be serialized.
    /// </summary>
    public interface IComponentPropertiesOverride
    {
        /// <summary>
        /// The type of component that this override applies to.
        /// </summary>
        Type Type { get; }
        /// <summary>
        /// The names of the fields/properties that should be serialized.
        /// </summary>
        List<string> SerializedPropertyNames { get; }
        /// <summary>
        /// Registers a property override for a type. Call this on each new implementation of the interface.
        /// Adding a property override for an already-overridden type overrides the previous property override.
        /// </summary>
        /// <param name="propertyOverride">The property override to register.</param>
        public static void RegisterOverride(IComponentPropertiesOverride propertyOverride)
        {
            propertyOverrides.Add(propertyOverride.Type, propertyOverride);
        }

        private static readonly Dictionary<Type, IComponentPropertiesOverride> propertyOverrides = new()
        {
            { typeof(Transform), new TransformPropertyOverride() },
            { typeof(BoxCollider2D), new BoxCollider2DPropertyOverride() },
            { typeof(CircleCollider2D), new CircleCollider2DPropertyOverride() },
            { typeof(SpriteRenderer), new SpriteRendererPropertyOverride() }
        };

        public static IComponentPropertiesOverride GetPropertyOverride(Component component)
        {
            if (propertyOverrides.TryGetValue(component.GetType(), out var propertyOverride))
            {
                return propertyOverride;
            }
            return null;
        }

        private class TransformPropertyOverride : IComponentPropertiesOverride
        {
            public Type Type => typeof(Transform);
            public List<string> SerializedPropertyNames => new() {
                "localPosition",
                "localEulerAngles",
                "localScale"
            };
        }

        private class BoxCollider2DPropertyOverride : IComponentPropertiesOverride
        {
            public Type Type => typeof(BoxCollider2D);
            public List<string> SerializedPropertyNames => new() {
                "isTrigger",
                "size",
                "offset"
            };
        }

        private class CircleCollider2DPropertyOverride : IComponentPropertiesOverride
        {
            public Type Type => typeof(CircleCollider2D);
            public List<string> SerializedPropertyNames => new() {
                "isTrigger",
                "radius",
                "offset"
            };
        }

        private class SpriteRendererPropertyOverride : IComponentPropertiesOverride
        {
            public Type Type => typeof(SpriteRenderer);
            public List<string> SerializedPropertyNames => new() {
                "sprite",
                "color",
                "sortingLayerName",
                "sortingOrder",
                "drawMode",
                "flipX",
                "flipY"
            };
        }
    }
}