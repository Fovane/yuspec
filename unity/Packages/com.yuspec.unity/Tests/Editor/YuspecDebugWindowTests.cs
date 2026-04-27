using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Yuspec.Unity.Editor.Tests
{
    public sealed class YuspecDebugWindowTests
    {
        [Test]
        public void DebugWindow_Opens()
        {
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = Application.isBatchMode;

            try
            {
                var window = EditorWindow.GetWindow<YuspecDebugWindow>();
                Assert.That(window, Is.Not.Null);
                window.Close();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }
    }
}
