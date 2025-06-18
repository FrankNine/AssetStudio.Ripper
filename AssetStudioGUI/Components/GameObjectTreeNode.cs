namespace AssetStudioGUI;

using System.Windows.Forms;

using AssetRipper.SourceGenerated.Classes.ClassID_1;

internal class GameObjectTreeNode : TreeNode
{
    public IGameObject gameObject;

    public GameObjectTreeNode(IGameObject gameObject)
    {
        this.gameObject = gameObject;
        Text = gameObject.Name;
    }
}