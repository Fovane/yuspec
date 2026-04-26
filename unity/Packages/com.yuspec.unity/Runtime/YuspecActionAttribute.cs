using System;

namespace Yuspec.Unity
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class YuspecActionAttribute : Attribute
    {
        private readonly string name;


        public string Name => name;


        public YuspecActionAttribute(string name)
        {
            this.name = name;
        }

    }

}

