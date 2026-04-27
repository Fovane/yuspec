using System;

namespace Yuspec.Unity
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class YuspecMutableAttribute : Attribute
    {
    }
}
