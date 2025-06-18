namespace AssetStudio;

using System.Collections.Generic;

using AssetRipper.Processing;
using AssetRipper.Export.UnityProjects;
using AssetRipper.Export.UnityProjects.Configuration;

public class AssetsManager
{
    public static GameData LoadPaths(params IReadOnlyList<string> path)
    {
        var libraryConfiguration = new LibraryConfiguration();
        var exportHandler = new ExportHandler(libraryConfiguration);
        return exportHandler.LoadAndProcess(path);
    }
}