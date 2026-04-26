using UnityEngine;

namespace Yuspec.Unity
{
    public sealed class YuspecSpecAsset : ScriptableObject
    {
        [SerializeField] private string sourcePath;
        [SerializeField] private string sourceText;

        public string SourcePath => sourcePath;
        public string SourceText => sourceText;

        public void SetSource(string path, string text)
        {
            sourcePath = path;
            sourceText = text;
        }
    }
}
