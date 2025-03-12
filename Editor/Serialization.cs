using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VibeGamedev
{
    public static class Serialization
    {
        /// <summary>
        /// A serializable and deserialzable representation of a GameObject.
        /// </summary>
        /// <param name="go">The GameObject to represent.</param>
        /// <returns></returns>
        [Serializable]
        public class SerializedObject
        {
            public string objectName;
            public string id;
            public bool isActive;
            public string tag;
            public SerializedComponent[] components;
            public SerializedObject(GameObject go)
            {
                objectName = go.name;
                id = ChangeExecutor.ObjectToID(go);
                isActive = go.activeSelf;
                tag = go.tag;
                components = go.GetComponents<Component>().Select(c => new SerializedComponent(c)).ToArray();
            }

            /// <summary>
            /// Serializes this object to a JSON string.
            /// </summary>
            /// <returns>A JSON string representation of this object.</returns>
            public override string ToString()
            {
                return PrettyJson(JsonUtility.ToJson(this));
            }

            /// <summary>
            /// Finds or creates the GameObject that this object represents.
            /// </summary>
            /// <returns>The new or existing GameObject.</returns>
            public GameObject ToGameObject()
            {
                GameObject gameObject = null;
                try
                {
                    gameObject = ChangeExecutor.IDToObject(id);
                }
                catch (ArgumentException) { }

                if (gameObject == null)
                {
                    gameObject = new GameObject(objectName);
                    Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
                    SettingsWindow.Log("Created " + gameObject.name + " (" + id + ")");
                    ChangeExecutor.SetID(gameObject, id);
                }
                Undo.RecordObject(gameObject, "Change properties of " + gameObject.name);
                gameObject.name = objectName;
                gameObject.SetActive(isActive);
                if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(tag))
                {
                    UnityEditorInternal.InternalEditorUtility.AddTag(tag);
                }
                gameObject.tag = tag;
                if (ChangeExecutor.ObjectToID(gameObject) != id)
                {
                    throw new ArgumentException($"{gameObject.name}: Object mismatch: {ChangeExecutor.ObjectToID(gameObject)} != {id}. Please try reopening the scene.");
                }
                if (ChangeExecutor.IDToObject(id) != gameObject)
                {
                    throw new ArgumentException($"{gameObject.name}: Object mismatch: {ChangeExecutor.IDToObject(id).name} != {gameObject.name}. Please try reopening the scene.");
                }
                return gameObject;
            }
        }

        // All of these must be satisfied for a field to be serialized.
        public static readonly List<Func<Component, System.Reflection.FieldInfo, bool>> fieldInclusionRules = new() {
            (c, f) => f.IsPublic || f.GetCustomAttributes(typeof(SerializeField), true).Length > 0,
            (c, f) => f.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length == 0,
            (c, f) => !f.Name.StartsWith("m_"),
        };

        // All of these must be satisfied for a property to be serialized.
        public static readonly List<Func<Component, System.Reflection.PropertyInfo, bool>> propertyInclusionRules = new() {
            (c, p) => p.CanRead && p.CanWrite,
            (c, p) => !p.Name.Contains("material") && !p.Name.Contains("mesh"),
            (c, p) => p.GetCustomAttributes(typeof(SerializeField), true).Length > 0 ||
                    p.GetCustomAttributes(typeof(SerializeReference), true).Length > 0 ||
                    c.GetType().Namespace?.StartsWith("UnityEngine") == true ||
                    c.GetType().Namespace?.StartsWith("TMPro") == true,
            (c, p) => p.GetCustomAttributes(typeof(HideInInspector), true).Length == 0,
            (c, p) => p.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length == 0,
        };

        /// <summary>
        /// A serializable and deserialzable representation of a Component.
        /// 
        /// By default, all of the inspector-facing properties of the component will be represented.
        /// You can change what values are serialized by implementing IPropertyGetter (see Samples/).
        /// 
        /// Only fields/properties that can be serialized and deserialized will be represented.
        /// To serialize a new field type, implement `IValueParser` (see Samples/).
        /// </summary>
        /// <param name="component">The Component to represent.</param>
        [Serializable]
        public class SerializedComponent
        {
            public string componentName;
            public SerializedProperty[] properties;

            public SerializedComponent(Component component)
            {
                componentName = component.GetType().Name;
                var scriptType = component.GetType();
                var propertyList = new List<Func<SerializedProperty>>();

                var propertyOverride = IComponentPropertiesOverride.GetPropertyOverride(component);
                var fields = scriptType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Where(field => propertyOverride == null ? fieldInclusionRules.All(rule => rule(component, field)) : propertyOverride.SerializedPropertyNames.Contains(field.Name));
                foreach (var field in fields)
                {
                    propertyList.Add(() => new SerializedProperty(field.Name, field.GetValue(component)));
                }
                var props = scriptType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(property => propertyOverride == null ? propertyInclusionRules.All(rule => rule(component, property)) : propertyOverride.SerializedPropertyNames.Contains(property.Name));
                foreach (var property in props)
                {
                    propertyList.Add(() => new SerializedProperty(property.Name, property.GetValue(component)));
                }
                // Iterate over the properties as functions instead of instantiating directly so we can catch errors in one place
                properties = propertyList.Select(f =>
                {
                    try
                    {
                        return f();
                    }
                    catch (NotImplementedException)
                    {
                        return null;
                    }
                }).Where(p => p != null).ToArray();
            }

            /// <summary>
            /// Checks if the component this object represents exists on `gameObject`, and adds it if not.
            /// </summary>
            /// <param name="gameObject">The GameObject on which to try adding this component.</param>
            /// <returns>An Action which, when called, sets the property values of the component.</returns>
            public Action TryAddTo(GameObject gameObject)
            {
                Type componentType = ((Type.GetType(componentName) ?? Type.GetType("UnityEngine." + componentName + ", UnityEngine.CoreModule")) ?? AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(assembly => assembly.GetTypes())
                        .FirstOrDefault(type => type.Name == componentName && type.IsSubclassOf(typeof(Component)))) ?? throw new ArgumentException($"{gameObject.name}: Could not find component type: {componentName}");
                Component baseComponent = gameObject.GetComponent(componentType);
                if (baseComponent == null)
                {
                    baseComponent = Undo.AddComponent(gameObject, componentType);
                }
                else
                {
                    Undo.RecordObject(baseComponent, "Update " + componentName + " of " + gameObject.name);
                }

                // We don't immediately execute the property deserialization because properties may point to
                // GameObjects or components that don't yet exist. We'll call this after all objects and components
                // have been created.
                return () =>
                {
                    if (gameObject == null)
                    {
                        return;
                    }
                    foreach (var serializedProperty in properties)
                    {
                        try
                        {
                            serializedProperty.SetOn(baseComponent, componentType);
                        }
                        catch (ArgumentException)
                        {
                            SettingsWindow.Log("No property found for " + serializedProperty.propertyName + " on " + baseComponent.name + " (" + componentType + "), skipping");
                        }
                    }
                };
            }
        }

        /// <summary>
        /// A serializable and deserialzable representation of a property.
        /// Instantiation will raise a NotImplementedException if there is no `IValueParser` that can process the property's type.
        /// New types can be handled by registering new `IValueParser` implementations; see `Samples/`.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="obj">The value of the property.</param>
        [Serializable]
        public class SerializedProperty
        {
            public string propertyName;
            public string value;

            public SerializedProperty(string name, object obj)
            {
                propertyName = name;
                if (obj == null)
                {
                    value = null;
                }
                else
                {
                    var parser = IValueParser.GetParser(obj.GetType());
                    value = parser.ToString(obj);
                }
            }

            /// <summary>
            /// Applies this property to a component, setting the property with matching name to this object's value.
            /// Raises an ArgumentException if no field or property with `name` exists on `baseComponent`.
            /// </summary>
            /// <param name="baseComponent">The component on which to set the property value.</param>
            /// <param name="componentType">The type of `baseComponent`.</param>
            public void SetOn(Component baseComponent, Type componentType)
            {
                var component = Convert.ChangeType(baseComponent, componentType);
                var field = component.GetType().GetField(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                    component.GetType().GetField(propertyName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var property = component.GetType().GetProperty(propertyName);

                Type fieldOrPropertyType;
                Action<object> SetValue;
                if (field != null)
                {
                    fieldOrPropertyType = field.FieldType;
                    SetValue = parsedValue => field.SetValue(component, parsedValue);
                }
                else if (property != null)
                {
                    fieldOrPropertyType = property.PropertyType;
                    SetValue = parsedValue => property.SetValue(component, parsedValue);
                }
                else
                {
                    throw new ArgumentException();
                }
                object parsedValue;
                if (value == null || (value == "" && fieldOrPropertyType != typeof(string)))
                {
                    parsedValue = null;
                }
                else
                {
                    try
                    {
                        var parser = IValueParser.GetParser(fieldOrPropertyType);
                        parsedValue = parser.Parse(value, fieldOrPropertyType);
                    }
                    // This can happen if the agent hallucinates a value for a property that exists but isn't supported.
                    catch (NotImplementedException e)
                    {
                        throw new ArgumentException($"{baseComponent.name}: {propertyName}: {e.Message}");
                    }
                }
                SetValue(parsedValue);
            }
        }

        /// <summary>
        /// Applies beautifying line breaks and indentation to a JSON string.
        /// </summary>
        /// <param name="json">A JSON object represented as a string.</param>
        /// <returns>The same JSON object string, but with nice line breaks and indentation.</returns>
        private static string PrettyJson(string json)
        {
            var indent = 0;
            var quoted = false;
            var sb = new System.Text.StringBuilder();

            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];

                switch (ch)
                {
                    case '"':
                        sb.Append(ch);
                        quoted = !quoted;
                        break;
                    case '{':
                    case '[':
                        sb.Append(ch);
                        // Check if this is an empty array/object
                        if (!quoted)
                        {
                            // Look ahead to see if the next non-whitespace character is the closing bracket
                            var isEmptyCollection = false;
                            for (var j = i + 1; j < json.Length; j++)
                            {
                                if (json[j] == ' ' || json[j] == '\t' || json[j] == '\r' || json[j] == '\n')
                                    continue;

                                isEmptyCollection = (ch == '{' && json[j] == '}') || (ch == '[' && json[j] == ']');
                                break;
                            }

                            if (!isEmptyCollection)
                            {
                                sb.AppendLine();
                                indent++;
                                sb.Append(new string(' ', indent * 2));
                            }
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            // Check if this is closing an empty array/object
                            var isClosingEmptyCollection = false;
                            for (var j = i - 1; j >= 0; j--)
                            {
                                if (json[j] == ' ' || json[j] == '\t' || json[j] == '\r' || json[j] == '\n')
                                    continue;

                                isClosingEmptyCollection = (ch == '}' && json[j] == '{') || (ch == ']' && json[j] == '[');
                                break;
                            }

                            if (!isClosingEmptyCollection)
                            {
                                sb.AppendLine();
                                indent--;
                                sb.Append(new string(' ', indent * 2));
                            }
                        }
                        sb.Append(ch);
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(' ');
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}