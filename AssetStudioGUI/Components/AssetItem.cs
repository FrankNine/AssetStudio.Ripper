using System.Collections.Generic;
using AssetRipper.SourceGenerated.Enums;

namespace AssetStudioGUI;

using AssetRipper.Assets;

internal record struct AssetItem
(
    IUnityObjectBase UnityObject,
    string Name,
    string TypeString,
    TextureFormat TextureFormat,
    long UniqueID,
    long PathID,
    ulong FullSize,
    ulong CompressedSizeEstimate
);

internal class SceneTreeNode
{
    public string Name;
    public int IndexInAssetCollection;
    public SceneTreeNode Parent;
    public List<SceneTreeNode> Children;
}