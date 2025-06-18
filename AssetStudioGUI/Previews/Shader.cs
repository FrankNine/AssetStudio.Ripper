namespace AssetStudioGUI;

using AssetRipper.IO.Files;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Shaders;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.SourceGenerated.Classes.ClassID_48;

internal partial class AssetStudioGUIForm
{
    private string PreviewShader(AssetItem assetItem, IShader shader)
    {
        var virtualFileSystem = new VirtualFileSystem();
        var uscShaderExporter = new USCShaderExporter();
        uscShaderExporter.TryCreateCollection(shader, out var exportCollection);
        
        var projectExporter = new ProjectExporter(_LibraryConfiguration, new BaseManager(_ => { }));
        var projectAssetContainer = new ProjectAssetContainer
        (
            projectExporter, 
            new CoreConfiguration(),
            [assetItem.UnityObject], 
            [exportCollection]
        );
        uscShaderExporter.Export(projectAssetContainer, assetItem.UnityObject, "dummyShaderPath", virtualFileSystem);
        var str = virtualFileSystem.File.ReadAllText("dummyShaderPath");
        
        PreviewText(string.IsNullOrEmpty(str) ? "Serialized Shader can't be read" : str.Replace("\n", "\r\n"));

        return string.Empty;
    } 
}