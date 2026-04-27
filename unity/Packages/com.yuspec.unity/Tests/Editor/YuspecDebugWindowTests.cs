using NUnit.Framework;
using UnityEditor;

namespace Yuspec.Unity.Editor.Tests
{
    public sealed class YuspecDebugWindowTests
    {
        [Test]
        public void DebugWindow_Opens()
        {
            var window = EditorWindow.GetWindow<YuspecDebugWindow>();
            Assert.That(window, Is.Not.Null);
            window.Close();
        }
    }
}
