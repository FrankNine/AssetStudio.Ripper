namespace AssetStudioGUI;

using System.Text;

using AssetRipper.SourceGenerated.Classes.ClassID_49;

internal partial class AssetStudioGUIForm
{
    private string PreviewTextAsset(ITextAsset textAsset)
    {
        var text = Encoding.UTF8.GetString(textAsset.Script_C49);
        text = text.Replace("\n", "\r\n").Replace("\0", "");
        PreviewText(text);

        return string.Empty;
    } 
}