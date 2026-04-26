using System;

namespace Yuspec.Unity
{
    public enum YuspecValueType
    {
        Null,
        Boolean,
        Integer,
        Float,
        String,
        Entity,
        Object
    }

    [Serializable]
    public readonly struct YuspecValue
    {
        public YuspecValueType Type { get; }
        public object RawValue { get; }

        public bool IsNull => Type == YuspecValueType.Null;

        private YuspecValue(YuspecValueType type, object rawValue)
        {
            Type = type;
            RawValue = rawValue;
        }

        public static YuspecValue Null() => new YuspecValue(YuspecValueType.Null, null);
        public static YuspecValue From(bool value) => new YuspecValue(YuspecValueType.Boolean, value);
        public static YuspecValue From(int value) => new YuspecValue(YuspecValueType.Integer, value);
        public static YuspecValue From(float value) => new YuspecValue(YuspecValueType.Float, value);
        public static YuspecValue From(string value) => new YuspecValue(YuspecValueType.String, value);
        public static YuspecValue From(YuspecEntity value) => value == null ? Null() : new YuspecValue(YuspecValueType.Entity, value);
        public static YuspecValue FromObject(object value) => value == null ? Null() : new YuspecValue(YuspecValueType.Object, value);

        public override string ToString()
        {
            return RawValue?.ToString() ?? "null";
        }
    }
}
