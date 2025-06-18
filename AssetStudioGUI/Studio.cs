using System.Windows.Documents;
using AssetRipper.Assets;
using AssetRipper.Assets.Collections;
using AssetRipper.Assets.Generics;
using AssetRipper.IO.Files.SerializedFiles.TypeTrees;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_1;
using AssetRipper.SourceGenerated.Classes.ClassID_114;
using AssetRipper.SourceGenerated.Classes.ClassID_147;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.Subclasses.PPtr_Object;
using SharpGLTF.Schema2;

namespace AssetStudioGUI;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using AssetRipper.Assets.Metadata;
using AssetRipper.Primitives;
using AssetRipper.SourceGenerated.Classes.ClassID_137;
using AssetRipper.SourceGenerated.Classes.ClassID_142;
using AssetRipper.SourceGenerated.Classes.ClassID_150;
using AssetRipper.SourceGenerated.Classes.ClassID_152;
using AssetRipper.SourceGenerated.Classes.ClassID_187;
using AssetRipper.SourceGenerated.Classes.ClassID_221;
using AssetRipper.SourceGenerated.Classes.ClassID_329;
using AssetRipper.SourceGenerated.Classes.ClassID_33;
using AssetRipper.SourceGenerated.Classes.ClassID_74;
using AssetRipper.SourceGenerated.Classes.ClassID_91;
using AssetRipper.SourceGenerated.Classes.ClassID_95;

using AssetStudio;

internal enum GuiColorTheme
{
    System,
    Light,
    Dark
}

internal enum ExportType
{
    Convert,
    Raw,
    Dump
}

internal enum ExportFilter
{
    All,
    Selected,
    Filtered
}

internal enum ExportL2DFilter
{
    All,
    Selected,
    SelectedWithFadeList,
    SelectedWithFade,
    SelectedWithClips
}

internal enum ExportListType
{
    XML
}

internal enum AssetGroupOption
{
    TypeName,
    ContainerPath,
    ContainerPathFull,
    SourceFileName,
    SceneHierarchy
}

internal enum ListSearchFilterMode
{
    Include,
    Exclude,
    RegexName,
    RegexContainer
}

[Flags]
internal enum SelectedAssetType
{
    Animator = 0x01,
    AnimationClip = 0x02,
    MonoBehaviourMoc = 0x04,
    MonoBehaviourFade = 0x08,
    MonoBehaviourFadeLst = 0x10
}

internal static class Studio
{
    public static SceneTreeNode[] BuildSceneTree(AssetCollection[] assetCollections)
    {
        var roots = new List<SceneTreeNode>();
        foreach (var collection in assetCollections)
        {
            var collectionRoot = new SceneTreeNode();
            collectionRoot.Name = collection.Name;
            collectionRoot.Children = new List<SceneTreeNode>();

            int index = 0;
            var sceneTreeDictionary = new Dictionary<IGameObject, SceneTreeNode>();
            foreach (var gameObject in collection.Where(a => a.ClassID == (int)ClassIDType.GameObject)
                                                 .OfType<IGameObject>())
            {
                sceneTreeDictionary.Add
                (
                    gameObject, 
                    new SceneTreeNode{Name = gameObject.Name, IndexInAssetCollection = index, Children = [] }
                );
                index++;
            }

            foreach (var (gameObject, sceneTreeNode) in sceneTreeDictionary)
            {
                if (gameObject.GetTransform().Father_C4P != null &&
                    sceneTreeDictionary.TryGetValue(gameObject.GetTransform().Father_C4P.GameObject_C4P,
                        out var parent))
                {
                    parent.Children.Add(sceneTreeNode);
                    sceneTreeNode.Parent = parent;
                } 
            }

            collectionRoot.Children = sceneTreeDictionary.Values.Where(t => t.Parent == null).ToList(); 
            if(collectionRoot.Children.Count != 0)
                roots.Add(collectionRoot);
        }

        return roots.ToArray();
    }

    public static TreeNode[] BuildTreeNodes(SceneTreeNode[] sceneTreeRoots) 
        => sceneTreeRoots.Select(BuildTreeNodeRecursiveOrdered).ToArray();

    private static TreeNode BuildTreeNodeRecursiveOrdered(SceneTreeNode sceneTreeNode)
    { 
        sceneTreeNode.Children.Sort((i1, i2) => i1.IndexInAssetCollection - i2.IndexInAssetCollection);
        return new TreeNode(sceneTreeNode.Name, sceneTreeNode.Children.Select(BuildTreeNodeRecursiveOrdered).ToArray());
    }

    
    
    public static AssetItem[] BuildAssetItems(AssetCollection[] assetCollections)
    {
        Logger.Info("Building tree nodes...");

        var assetItems = new List<AssetItem>();
        AccessListBase<IPPtr_Object> preloadTable = null;
        var containers = new List<(IPPtr_Object, string)>();
        var texture2DArrayList = new List<ITexture2DArray>();
        
        var totalAssetCount = assetCollections.Length;
        var assetIndex = 0;
        Progress.Reset();
        foreach (var collection in assetCollections)
        {
            foreach (var (key, asset) in collection.Assets)
            {
                var assetItem = new AssetItem
                {
                    UnityObject = asset,
                    Name = asset.GetBestName(), 
                    UniqueID = key,
                    PathID = asset.PathID
                };

                switch (asset.ClassID)
                {
                    case (int)ClassIDType.PreloadData:
                        assetItem.TypeString = "PreloadData";
                        if (asset is not IPreloadData preloadData) { break; }
                        
                        preloadTable = preloadData.Assets;
                        break;
                    case (int)ClassIDType.GameObject:
                        assetItem.TypeString = "GameObject";
                        break;
                    case (int)ClassIDType.Texture2D:
                        assetItem.TypeString = "Texture2D";
                        if (asset is not ITexture2D texture2D) { break; }
                        
                        if (!string.IsNullOrEmpty(texture2D.StreamData_C28?.Path))
                            assetItem.FullSize = texture2D.StreamData_C28.Size;
                        
                        assetItem.Name = texture2D.GetBestName();
                        assetItem.TextureFormat = texture2D.Format_C28E;
                        break;
                    case (int)ClassIDType.Texture2DArray:
                        assetItem.TypeString = "Texture2DArray"; 
                        if (asset is not ITexture2DArray texture2DArray) { break; }
                        
                        if (!string.IsNullOrEmpty(texture2DArray.StreamData?.Path))
                            assetItem.FullSize = texture2DArray.StreamData.Size;
                        
                        assetItem.Name = texture2DArray.GetBestName();
                        texture2DArrayList.Add(texture2DArray);
                        break;
                    case (int)ClassIDType.AudioClip:
                        assetItem.TypeString = "AudioClip"; 
                        if (asset is not IAudioClip audioClip) { break; }
                        
                        if (!string.IsNullOrEmpty(audioClip.Resource.Source))
                            assetItem.FullSize = audioClip.Resource.Size;
                        
                        assetItem.Name = audioClip.GetBestName();
                        break;
                    case (int)ClassIDType.VideoClip_327:
                    case (int)ClassIDType.VideoClip_329:
                        assetItem.TypeString = "VideoClip"; 
                        if (asset is not IVideoClip videoClip) { break; }

                        if (!string.IsNullOrEmpty(videoClip.OriginalPath))
                            assetItem.FullSize = videoClip.ExternalResources.Size;
                        
                        assetItem.Name = videoClip.GetBestName();
                        break;
                    case (int)ClassIDType.Shader:
                        assetItem.TypeString = "Shader"; 
                        if (asset is not IShader shader) { break; }
                        assetItem.Name = shader.ParsedForm?.Name ?? shader.GetBestName();
                        break;
                    case (int)ClassIDType.Mesh:
                        assetItem.TypeString = "Mesh";
                        break;
                    case (int)ClassIDType.TextAsset:
                        assetItem.TypeString = "TextAsset";
                        break;
                    case (int)ClassIDType.AnimationClip:
                        assetItem.TypeString = "AnimationClip";
                        break;
                    case (int)ClassIDType.Font:
                        assetItem.TypeString = "Font";
                        break;
                    case (int)ClassIDType.MovieTexture:
                        assetItem.TypeString = "MovieTexture";
                        break;
                    case (int)ClassIDType.Sprite:
                        assetItem.TypeString = "Sprite";
                        break;
                    case (int)ClassIDType.Animator:
                        assetItem.TypeString = "Animator"; 
                        if (asset is not IAnimator animator) { break; }
                        
                        // TODO: GameObject Name
                        
                        break;
                    case (int)ClassIDType.MonoBehaviour:
                        assetItem.TypeString = "MonoBehaviour"; 
                        if (asset is not IMonoBehaviour monoBehaviour) { break; }

                        if (monoBehaviour.Script != null)
                        {
                            if (string.IsNullOrEmpty(assetItem.Name))
                            {
                                // TODO: Class Name
                                assetItem.Name = "Missing class name";
                            }
                            // TODO: CubismMoc
                        }
                        break;
                    // TODO: PlayerSettings
                    case (int)ClassIDType.AssetBundle:
                        assetItem.TypeString = "AssetBundle"; 
                        if (asset is not IAssetBundle assetBundle) { break; }

                        if (!assetBundle.IsStreamedSceneAssetBundle)
                            preloadTable = assetBundle.PreloadTable;

                        assetItem.Name = string.IsNullOrEmpty(assetBundle.AssetBundleName)
                            ? assetBundle.Name
                            : assetBundle.AssetBundleName;

                        foreach (var (assetBundleKey, assetBundleAssetInfo) in assetBundle.Container)
                        {
                            var preloadIndex = assetBundleAssetInfo.PreloadIndex;
                            var preloadSize = assetBundle.IsStreamedSceneAssetBundle
                                ? preloadTable.Count
                                : assetBundleAssetInfo.PreloadSize;
                            var preloadEnd = preloadIndex + preloadSize;
                            for (var k = preloadIndex; k < preloadEnd; k++)
                            {
                                containers.Add((preloadTable[k], assetBundleKey));
                            } 
                        } 
                        break;
                    case (int)ClassIDType.ResourceManager:
                        assetItem.TypeString = "ResourceManager"; 
                        if (asset is not IResourceManager resourceManager) { break; }

                        foreach (var (resourceManagerKey, resourceManagerAssetItem) in resourceManager.Container)
                        {
                            containers.Add((resourceManagerAssetItem, resourceManagerKey));
                        }

                        break;
                    default:
                        continue;
                }

                if (string.IsNullOrEmpty(assetItem.Name))
                {
                    assetItem.Name = assetItem.UniqueID.ToString();
                }

                assetItems.Add(assetItem);
            }
            
            Progress.Report(assetIndex++, totalAssetCount);
        }

        return assetItems.ToArray();
    }

    public static List<ListViewItem> BuildListViewItems(AssetItem[] assetItems)
    {
        var listViewItems = new List<ListViewItem>();
        foreach (var assetItem in assetItems)
        {
            var listViewItem = new ListViewItem();
            listViewItem.Text = assetItem.Name;
            listViewItem.SubItems.AddRange
            (
                "",
                assetItem.TypeString,
                assetItem.PathID.ToString(),
                assetItem.FullSize.ToString(),
                assetItem.TextureFormat != 0 ? assetItem.TextureFormat.ToString() : ""
            );
            listViewItem.Tag = assetItem;
            listViewItems.Add(listViewItem);
        }
        return listViewItems;
    }

    
    
    internal static Action<string> StatusStripUpdate = x => { };
    public static AssetsManager assetsManager = new();

    public static int ExtractFolder(string path, string savePath)
    {
        int extractedCount = 0;
        Progress.Reset();
        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            var file = files[i];
            var fileOriPath = Path.GetDirectoryName(file);
            var fileSavePath = fileOriPath.Replace(path, savePath);
            extractedCount += ExtractFile(file, fileSavePath);
            Progress.Report(i + 1, files.Length);
        }
        return extractedCount;
    }

    public static int ExtractFile(string[] fileNames, string savePath)
    {
        int extractedCount = 0;
        Progress.Reset();
        for (var i = 0; i < fileNames.Length; i++)
        {
            var fileName = fileNames[i];
            extractedCount += ExtractFile(fileName, savePath);
            Progress.Report(i + 1, fileNames.Length);
        }
        return extractedCount;
    }

    public static int ExtractFile(string fileName, string savePath)
    {
        int extractedCount = 0;
           
        return extractedCount;
    }
    
    public static Dictionary<UnityVersion, SortedDictionary<int, TypeTreeItem>> BuildClassStructure()
    {
        var typeMap = new Dictionary<UnityVersion, SortedDictionary<int, TypeTreeItem>>();
        return typeMap;
    }

    public static string DumpAsset(Object obj)
    {
        return string.Empty;
    }

    public static JsonDocument DumpAssetToJsonDoc(Object obj)
    {
        return null;
    }

    public static void OpenFolderInExplorer(string path)
    {
        if (!path.EndsWith($"{Path.DirectorySeparatorChar}"))
            path += Path.DirectorySeparatorChar;
        if (!Directory.Exists(path))
            return;

        var info = new ProcessStartInfo(path);
        info.UseShellExecute = true;
        Process.Start(info);
    }
}