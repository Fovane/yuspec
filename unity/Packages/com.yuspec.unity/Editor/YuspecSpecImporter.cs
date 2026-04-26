using System.IO;
using Yuspec.Unity;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Yuspec.Unity.Editor
{
    
    [ScriptedImporter(1, "yuspec")]
    public sealed class YuspecSpecImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var asset = ScriptableObject.CreateInstance<YuspecSpecAsset>();

            asset.SetSource(context.assetPath, File.ReadAllText(context.assetPath));

            context.AddObjectToAsset("YUSPEC Spec", asset);
            context.SetMainObject(asset);
        }

    }

}
