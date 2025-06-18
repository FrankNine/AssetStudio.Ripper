using System.Text;
using System.Windows.Forms;

namespace AssetStudioGUI;

internal class TypeTreeItem : ListViewItem
{
    public TypeTreeItem(int typeID)
    {
        Text = "TypeTreeItem";
        SubItems.Add(typeID.ToString());
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        return sb.ToString();
    }
}