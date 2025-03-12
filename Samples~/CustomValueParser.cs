using VibeGamedev;
using System;

public static class CustomValueParserSample
{
    public class UInt64Parser : IValueParser
    {
        public Type ParsedType => typeof(ulong);

        public string ToString(object value)
        {
            return "UINT64(" + ((ulong)value).ToString() + ")";
        }

        public object Parse(string value, Type type)
        {
            if (value.StartsWith("UINT64(") && value.EndsWith(")"))
            {
                value = value[7..^1];
                return ulong.Parse(value);
            }
            return ulong.Parse(value);
        }
    }

    // You should do this in an editor script and make sure to run it on load.
    public static void RegisterCustomParser()
    {
        IValueParser.RegisterParser(new UInt64Parser());
    }
}