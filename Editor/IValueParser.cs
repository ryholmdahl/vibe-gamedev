using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace VibeGamedev
{
    /// <summary>
    /// An interface for objects that can serialize and deserialize a certain type.
    /// </summary>
    public interface IValueParser
    {
        /// <summary>
        /// The type that this parser can handle.
        /// </summary>
        Type ParsedType { get; }
        /// <summary>
        /// Converts a value of the parsed type to a string.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A string representation of the value.</returns>
        string ToString(object value);
        /// <summary>
        /// Converts a string representing a value of the parsed type back to that type.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <param name="type">The type to convert to.</param>
        /// <returns>A value of the parsed type.</returns>
        object Parse(string value, Type type);

        /// <summary>
        /// Registers a parser for a type. Call this when you implement the interface for a new type.
        /// Adding a parser for an already-parseable type overrides the previous parser.
        /// </summary>
        /// <param name="parser">The parser to register.</param>
        public static void RegisterParser(IValueParser parser)
        {
            foreach (var existingParser in parsers.Where(p => p.ParsedType == parser.ParsedType).ToArray())
            {
                parsers.Remove(existingParser);
            }
            parsers.Add(parser);
        }

        private static readonly List<IValueParser> parsers = new() {
            new StringParser(),
            new IntParser(),
            new FloatParser(),
            new BoolParser(),
            new EnumParser(),
            new Vector2Parser(),
            new Vector3Parser(),
            new Vector4Parser(),
            new Vector2IntParser(),
            new BoundsParser(),
            new ColorParser(),
            new SpriteParser(),
            new ComponentParser(),
            new GameObjectParser(),
        };

        private static readonly HashSet<string> unsupportedTypesFound = new();

        public static IValueParser GetParser(Type vType)
        {
            // exact match > subclass > higher-level subclass > interface
            IValueParser bestParser = null;
            bool isBestParserSubclass = false;
            foreach (var parser in parsers)
            {
                if (vType == parser.ParsedType)
                {
                    // exact match, return immediately
                    return parser;
                }
                else if (vType.IsSubclassOf(parser.ParsedType) && (!isBestParserSubclass || parser.ParsedType.IsSubclassOf(bestParser.ParsedType)))
                {
                    bestParser = parser;
                    isBestParserSubclass = true;
                }
                else if (vType.GetInterfaces().Contains(parser.ParsedType) && !isBestParserSubclass)
                {
                    bestParser = parser;
                    isBestParserSubclass = false;
                }
            }
            if (bestParser != null)
            {
                return bestParser;
            }
            if (vType.IsArray || (vType.IsGenericType && vType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                Type elementType;
                if (vType.IsGenericType && vType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    elementType = vType.GetGenericArguments().First();
                }
                else if (vType.IsArray)
                {
                    elementType = vType.GetElementType();
                }
                else
                {
                    unsupportedTypesFound.Add(GetFriendlyTypeName(vType));
                    throw new NotImplementedException($"No parser found for type: {GetFriendlyTypeName(vType)}");
                }
                var collectionParser = new CollectionParser(elementType);
                RegisterParser(collectionParser);
                return collectionParser;
            }
            unsupportedTypesFound.Add(GetFriendlyTypeName(vType));
            throw new NotImplementedException($"No parser found for type: {GetFriendlyTypeName(vType)}");
        }

        public static List<string> UnsupportedTypesFound => unsupportedTypesFound.ToList();

        private static string GetFriendlyTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
                return $"{type.Name.Split('`')[0]}<{genericArgs}>";
            }
            return type.Name;
        }

        private class StringParser : IValueParser
        {
            public Type ParsedType => typeof(string);
            public string ToString(object value) => (string)value;
            public object Parse(string value, Type type) => value;
        }

        private class IntParser : IValueParser
        {
            public Type ParsedType => typeof(int);
            public string ToString(object value) => ((int)value).ToString();
            public object Parse(string value, Type type) => int.Parse(value);
        }

        private class FloatParser : IValueParser
        {
            public Type ParsedType => typeof(float);
            public string ToString(object value) => ((float)value).ToString();
            public object Parse(string value, Type type) => float.Parse(value);
        }

        private class BoolParser : IValueParser
        {
            public Type ParsedType => typeof(bool);
            public string ToString(object value) => ((bool)value).ToString();
            public object Parse(string value, Type type) => bool.Parse(value);
        }

        private class EnumParser : IValueParser
        {
            public Type ParsedType => typeof(Enum);
            public string ToString(object value) => ((Enum)value).ToString();
            public object Parse(string value, Type type) => Enum.Parse(type, value);
        }

        private class Vector2Parser : IValueParser
        {
            public Type ParsedType => typeof(Vector2);
            public string ToString(object value)
            {
                var vector = (Vector2)value;
                return $"Vector2({vector.x},{vector.y})";
            }
            public object Parse(string value, Type type)
            {
                if (value.StartsWith("Vector2(") && value.EndsWith(")"))
                {
                    value = value[8..^1];
                }
                var parts = value.Split(',');
                return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
            }
        }

        private class Vector3Parser : IValueParser
        {
            public Type ParsedType => typeof(Vector3);
            public string ToString(object value)
            {
                var vector = (Vector3)value;
                return $"Vector3({vector.x},{vector.y},{vector.z})";
            }
            public object Parse(string value, Type type)
            {
                if (value.StartsWith("Vector3(") && value.EndsWith(")"))
                {
                    value = value[8..^1];
                }
                var parts = value.Split(',');
                return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
            }
        }

        private class Vector4Parser : IValueParser
        {
            public Type ParsedType => typeof(Vector4);
            public string ToString(object value)
            {
                var vector = (Vector4)value;
                return $"Vector4({vector.x},{vector.y},{vector.z},{vector.w})";
            }
            public object Parse(string value, Type type)
            {
                if (value.StartsWith("Vector4(") && value.EndsWith(")"))
                {
                    value = value[8..^1];
                }
                var parts = value.Split(',');
                return new Vector4(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
            }
        }

        private class Vector2IntParser : IValueParser
        {
            public Type ParsedType => typeof(Vector2Int);
            public string ToString(object value) => $"Vector2Int({((Vector2Int)value).x},{((Vector2Int)value).y})";
            public object Parse(string value, Type type)
            {
                if (value.StartsWith("Vector2Int(") && value.EndsWith(")"))
                {
                    value = value[11..^1];
                }
                var parts = value.Split(',');
                return new Vector2Int(int.Parse(parts[0]), int.Parse(parts[1]));
            }
        }

        private class BoundsParser : IValueParser
        {
            public Type ParsedType => typeof(Bounds);
            public string ToString(object value) => $"Bounds({((Bounds)value).center.x},{((Bounds)value).center.y},{((Bounds)value).size.x},{((Bounds)value).size.y})";
            public object Parse(string value, Type type)
            {
                if (value.StartsWith("Bounds(") && value.EndsWith(")"))
                {
                    value = value[7..^1];
                }
                var parts = value.Split(',');
                return new Bounds(new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), 0), new Vector3(float.Parse(parts[2]), float.Parse(parts[3]), 0));
            }
        }

        private class ColorParser : IValueParser
        {
            public Type ParsedType => typeof(Color);
            public string ToString(object value) => $"RGBA({((Color)value).r},{((Color)value).g},{((Color)value).b},{((Color)value).a})";
            public object Parse(string value, Type type)
            {
                if (value.StartsWith("RGBA(") && value.EndsWith(")"))
                {
                    value = value[5..^1];
                }
                var parts = value.Split(',');
                return new Color(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
            }
        }

        private class SpriteParser : IValueParser
        {
            public Type ParsedType => typeof(Sprite);
            public string ToString(object value) => AssetDatabase.GetAssetPath((Sprite)value);
            public object Parse(string value, Type type) => AssetDatabase.LoadAssetAtPath<Sprite>(value);
        }

        private class ComponentParser : IValueParser
        {
            public Type ParsedType => typeof(Component);
            public string ToString(object value)
            {
                Component component = (Component)value;
                if (component == null || component.gameObject == null)
                {
                    return null;
                }
                return ChangeExecutor.ObjectToID(component.gameObject);
            }
            public object Parse(string value, Type type)
            {
                try
                {
                    return ChangeExecutor.IDToObject(value).GetComponent(type);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

        private class GameObjectParser : IValueParser
        {
            public Type ParsedType => typeof(GameObject);
            public string ToString(object value)
            {
                GameObject gameObject = (GameObject)value;
                if (gameObject == null)
                {
                    return null;
                }
                return ChangeExecutor.ObjectToID(gameObject);
            }
            public object Parse(string value, Type type)
            {
                try
                {
                    return ChangeExecutor.IDToObject(value);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

        private class CollectionParser : IValueParser
        {
            private readonly Type elementType;
            private readonly IValueParser subparser;
            public CollectionParser(Type elementType)
            {
                this.elementType = elementType;
                subparser = GetParser(elementType);
            }

            public Type ParsedType => typeof(IEnumerable<>).MakeGenericType(elementType);
            public string ToString(object value)
            {
                if (value == null)
                {
                    return null;
                }
                var collection = ((System.Collections.IEnumerable)value).Cast<object>();
                if (subparser == null)
                {
                    unsupportedTypesFound.Add(GetFriendlyTypeName(elementType));
                    throw new NotImplementedException($"Collection element type {GetFriendlyTypeName(elementType)} not supported");
                }
                string[] strings = collection.Select(o => o == null ? null : subparser.ToString(o)).ToArray();
                return "[" + string.Join(",", strings) + "]";
            }
            public object Parse(string value, Type type)
            {
                if (value == null)
                {
                    return null;
                }
                var collection = ParseCollectionItems(value);
                if (type.IsArray)
                {
                    var array = Array.CreateInstance(elementType, collection.Count);
                    for (int i = 0; i < collection.Count; i++)
                    {
                        array.SetValue(subparser.Parse(collection[i], elementType), i);
                    }
                    return array;
                }
                else
                {
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = (System.Collections.IList)Activator.CreateInstance(listType);
                    foreach (var item in collection)
                    {
                        list.Add(subparser.Parse(item, elementType));
                    }
                    return list;
                }
            }

            private static List<string> ParseCollectionItems(string s)
            {
                var items = new List<string>();
                var currentItem = new System.Text.StringBuilder();
                var bracketCount = 0;
                var parenthesisCount = 0;

                for (int i = 1; i < s.Length - 1; i++)
                {
                    var ch = s[i];
                    if (ch == '[' || ch == '{')
                        bracketCount++;
                    else if (ch == ']' || ch == '}')
                        bracketCount--;
                    else if (ch == '(')
                        parenthesisCount++;
                    else if (ch == ')')
                        parenthesisCount--;
                    else if (ch == ',' && bracketCount == 0 && parenthesisCount == 0)
                    {
                        items.Add(currentItem.ToString().Trim());
                        currentItem.Clear();
                        continue;
                    }
                    currentItem.Append(ch);
                }
                if (currentItem.Length > 0)
                    items.Add(currentItem.ToString().Trim());

                return items;
            }
        }
    }
}