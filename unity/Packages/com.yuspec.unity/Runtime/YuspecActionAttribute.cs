using System;

namespace Yuspec.Unity
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class YuspecActionAttribute : Attribute
    {
        public string Name { get; }

        public YuspecActionAttribute(string name)
        {
            Name = name;
        }
    }
}
