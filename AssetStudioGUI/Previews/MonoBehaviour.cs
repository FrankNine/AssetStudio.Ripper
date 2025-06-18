namespace AssetStudioGUI;

using System.IO;
using AssetRipper.Assets;
using AssetRipper.Assets.IO.Writing;
using AssetRipper.SourceGenerated.Classes.ClassID_114;

internal partial class AssetStudioGUIForm
{
    private string PreviewMonoBehaviour(IUnityObjectBase unityObject, IMonoBehaviour monoBehaviour)
    {
        var stream = new MemoryStream();
        var assetWriter = new AssetWriter(stream, unityObject.Collection); 
        monoBehaviour.WriteRelease(assetWriter);
        
        PreviewText(stream.ToString());

        return string.Empty;
    } 
}