namespace AssetStudioGUI;

using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

using AssetRipper.Export.Modules.Textures;
using AssetRipper.SourceGenerated.Classes.ClassID_187;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using BuildTarget = AssetRipper.IO.Files.BuildTarget;

internal partial class AssetStudioGUIForm
{
    private string PreviewTexture2D(AssetItem assetItem, ITexture2D texture2D)
    {
        bool isConverted = TextureConverter.TryConvertToBitmap(texture2D, out var directBitmap);
        if (isConverted)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Width: {texture2D.Width_C28}");
            sb.AppendLine($"Height: {texture2D.Height_C28}");
            sb.AppendLine($"Format: {texture2D.Format_C28E.ToString()}");
                
            switch (texture2D.TextureSettings_C28.FilterMode)
            {
                case 0: sb.AppendLine("Filter mode: Point"); break;
                case 1: sb.AppendLine("Filter mode: Bilinear"); break;
                case 2: sb.AppendLine("Filter mode: Trilinear"); break;
            }

            sb.AppendLine($"Anisotropic level: {texture2D.TextureSettings_C28.Aniso}");
            sb.AppendLine($"Mip map bias: {texture2D.TextureSettings_C28.MipBias}");
                
            switch (texture2D.TextureSettings_C28.WrapMode)
            {
                case 0: sb.AppendLine("Wrap mode: Repeat"); break;
                case 1: sb.AppendLine("Wrap mode: Clamp"); break;
            }
                
            sb.Append("Channels: ");
            int validChannel = 0;
            for (int i = 0; i < 4; i++)
            {
                if (textureChannels[i])
                {
                    sb.Append(textureChannelNames[i]);
                    validChannel++;
                }
            }
            if (validChannel == 0)
                sb.Append("None");
            if (validChannel != 4)
            {
                var bytes = directBitmap.Bits;
                for (int i = 0; i < directBitmap.Height; i++)
                {
                    int offset = Math.Abs(directBitmap.Width * 4) * i;
                    for (int j = 0; j < directBitmap.Width; j++)
                    {
                        bytes[offset + 0] = textureChannels[0] ? bytes[offset + 0] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                        bytes[offset + 1] = textureChannels[1] ? bytes[offset + 1] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                        bytes[offset + 2] = textureChannels[2] ? bytes[offset + 2] : validChannel == 1 && textureChannels[3] ? byte.MaxValue : byte.MinValue;
                        bytes[offset + 3] = textureChannels[3] ? bytes[offset + 3] : byte.MaxValue;
                        offset += 4;
                    }
                }
            }
                
            var switchSwizzled = texture2D.PlatformBlob_C28?.Length != 0;
            sb.Append(assetItem.UnityObject.Collection.Platform == BuildTarget.Switch 
                ? $"\nUses texture swizzling: {switchSwizzled}" 
                : string.Empty);
                
            PreviewTexture(directBitmap);

            StatusStripUpdate("'Ctrl'+'R'/'G'/'B'/'A' for Channel Toggle");
            return sb.ToString();
        }
        else
        {
            StatusStripUpdate("Unsupported image for preview");
            return string.Empty;
        }
    }
        
    private void PreviewTexture(DirectBitmap directBitmap)
    {
        using var stream = new MemoryStream();
        directBitmap.SaveAsPng(stream);
        previewPanel.Image = new Bitmap(stream);
            
        if (directBitmap.Width > previewPanel.Width || directBitmap.Height > previewPanel.Height)
            previewPanel.SizeMode = PictureBoxSizeMode.Zoom;
        else
            previewPanel.SizeMode = PictureBoxSizeMode.CenterImage;
    }
        
    private string PreviewTexture2DArray(ITexture2DArray texture2DArray) 
        => $"Width: {texture2DArray.Width}\n" +
           $"Height: {texture2DArray.Height}\n" +
           $"Graphics format: {texture2DArray.Format}\n" +
           $"Texture count: {texture2DArray.Depth}";
}