using AssetStudio;
using OpenTK.Graphics.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using AssetRipper.Assets.Collections;
using AssetRipper.Export.Modules.Shaders.ShaderBlob;
using AssetRipper.Export.Modules.Textures;
using AssetRipper.Export.UnityProjects.Configuration;
using AssetRipper.Import.Structure;
using AssetRipper.Primitives;
using AssetRipper.Processing;
using AssetRipper.SourceGenerated;
using AssetRipper.SourceGenerated.Classes.ClassID_187;
using AssetRipper.SourceGenerated.Classes.ClassID_28;
using AssetRipper.SourceGenerated.Classes.ClassID_329;
using AssetRipper.SourceGenerated.Classes.ClassID_48;
using AssetRipper.SourceGenerated.Classes.ClassID_83;
using AssetRipper.SourceGenerated.Enums;
using AssetRipper.SourceGenerated.Extensions;
using AssetRipper.SourceGenerated.NativeEnums.Fmod;
using K4os.Compression.LZ4;
using static AssetStudioGUI.Studio;
using Microsoft.WindowsAPICodePack.Taskbar;
using Action = System.Action;
using BuildTarget = AssetRipper.IO.Files.BuildTarget;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;
using Matrix4 = OpenTK.Mathematics.Matrix4;

using Logger = AssetStudio.Logger;
using ShaderType = OpenTK.Graphics.OpenGL.ShaderType;

namespace AssetStudioGUI
{
    partial class AssetStudioGUIForm : Form
    {
        private AssetItem lastSelectedItem;
        private AssetItem lastPreviewItem;
        private string tempClipboard;
        private bool isDarkMode;

#region TexControl
        private static char[] textureChannelNames = ['B', 'G', 'R', 'A'];
        private bool[] textureChannels = [true, true, true, true];
#endregion

#region GLControl
        private bool glControlLoaded;
        private int mdx, mdy;
        private bool lmdown, rmdown;
        private int pgmID, pgmColorID, pgmBlackID;
        private int attributeVertexPosition;
        private int attributeNormalDirection;
        private int attributeVertexColor;
        private int uniformModelMatrix;
        private int uniformViewMatrix;
        private int uniformProjMatrix;
        private int vao;
        private Vector3[] vertexData;
        private Vector3[] normalData;
        private Vector3[] normal2Data;
        private Vector4[] colorData;
        private Matrix4 modelMatrixData;
        private Matrix4 viewMatrixData;
        private Matrix4 projMatrixData;
        private int[] indiceData;
        private int wireFrameMode;
        private int shadeMode;
        private int normalMode;
#endregion

        //asset list sorting
        private int sortColumn = -1;
        private bool reverseSort;

        private AlphanumComparatorFastNet alphanumComparator = new();

        //asset list selection
        private List<int> selectedIndicesPrevList = [];
        private List<AssetItem> selectedAnimationAssetsList = [];

        //asset list filter
        private System.Timers.Timer delayTimer;
        private bool enableFiltering;

        //tree search
        private int nextGObject;
        private List<TreeNode> treeSrcResults = [];
        private List<ListViewItem> _visibleAssets = [];

        //tree selection
        private List<TreeNode> treeNodeSelectedList = [];
        private bool treeRecursionEnabled = true;
        private bool isRecursionEvent;

        private string openDirectoryBackup = string.Empty;

        private GUILogger logger;

        private TaskbarManager taskbar = TaskbarManager.Instance;

        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        private string guiTitle;

        public AssetStudioGUIForm()
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            ConsoleWindow.RunConsole(Properties.Settings.Default.showConsole);
            InitializeComponent();
            ApplyColorTheme(out isDarkMode);

            var appAssembly = typeof(Program).Assembly.GetName();
            guiTitle = $"{appAssembly.Name} v{appAssembly.Version}";
            Text = guiTitle;

            delayTimer = new System.Timers.Timer(800);
            delayTimer.Elapsed += delayTimer_Elapsed;
            displayAll.Checked = Properties.Settings.Default.displayAll;
            displayInfo.Checked = Properties.Settings.Default.displayInfo;
            enablePreview.Checked = Properties.Settings.Default.enablePreview;
            showConsoleToolStripMenuItem.Checked = Properties.Settings.Default.showConsole;
            buildTreeStructureToolStripMenuItem.Checked = Properties.Settings.Default.buildTreeStructure;
            useAssetLoadingViaTypetreeToolStripMenuItem.Checked = Properties.Settings.Default.useTypetreeLoading;
            useDumpTreeViewToolStripMenuItem.Checked = Properties.Settings.Default.useDumpTreeView;
            autoPlayAudioAssetsToolStripMenuItem.Checked = Properties.Settings.Default.autoplayAudio;
            customBlockCompressionComboBoxToolStripMenuItem.SelectedIndex = 0;
            customBlockInfoCompressionComboBoxToolStripMenuItem.SelectedIndex = 0;
            FMODinit();
            listSearchFilterMode.SelectedIndex = 0;
            if (string.IsNullOrEmpty(Properties.Settings.Default.fbxSettings))
            {
            }

            logger = new GUILogger(StatusStripUpdate);
            Logger.Default = logger;
            writeLogToFileToolStripMenuItem.Checked = Properties.Settings.Default.useFileLogger;

            Progress.Default = new Progress<int>(SetProgressBarValue);
            Studio.StatusStripUpdate = StatusStripUpdate;
        }
        
        private void AssetStudioGUIForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void AssetStudioGUIForm_DragDrop(object sender, DragEventArgs e)
        {
            var paths = (string[])e.Data?.GetData(DataFormats.FileDrop);
            if (paths?.Length == 0)
                return;

            LoadPath(paths);
        }
        
        private void loadFile_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog { InitialDirectory = openDirectoryBackup };
            if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                return;

            LoadPath(openFileDialog.FileNames);
        }

        private void loadFolder_Click(object sender, EventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog { InitialFolder = openDirectoryBackup };
            if (openFolderDialog.ShowDialog(this) != DialogResult.OK)
                return;

            LoadPath(openFolderDialog.Folder);
        }
        
        private void LoadPath(params IReadOnlyList<string> paths)
        {
            ResetForm();
            var settings = new LibraryConfiguration();
            settings.ImportSettings.DefaultVersion = UnityVersion.Parse(specifyUnityVersion.Text); 
            var gameStructure = GameStructure.Load(paths, settings);
            var gameData = GameData.FromGameStructure(gameStructure);
            BuildView(gameData); 
        }

        private void extractFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog { InitialDirectory = openDirectoryBackup };
            if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                return;
            
            var saveFolderDialog = new OpenFolderDialog { Title = "Select the save folder" };
            if (saveFolderDialog.ShowDialog(this) != DialogResult.OK)
                return;

            var openFileNames = openFileDialog.FileNames;
            var saveFolder = saveFolderDialog.Folder; 
            var extractedCount = ExtractFile(openFileNames, saveFolder);
                
            Logger.Info($"Finished extracting {extractedCount} files.");
        }

        private void extractFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog { InitialFolder = openDirectoryBackup };
            if (openFolderDialog.ShowDialog(this) != DialogResult.OK)
                return;
            
            var saveFolderDialog = new OpenFolderDialog { Title = "Select the save folder" };
            if (saveFolderDialog.ShowDialog(this) != DialogResult.OK)
                return;

            var openFolder = openFolderDialog.Folder;
            var saveFolder = saveFolderDialog.Folder; 
            var extractedCount = ExtractFile(openFolder, saveFolder);
                
            Logger.Info($"Finished extracting {extractedCount} files.");
        }


        private void BuildView(GameData gameData)
        {
            var assetCollections = _GetAssetCollections(gameData);

            if (assetCollections.Length == 0)
            {
                Logger.Info("No Unity file can be loaded.");
                return;
            }

            var sceneTreeRoots = BuildSceneTree(assetCollections);
            var treeNodes = BuildTreeNodes(sceneTreeRoots); 
            sceneTreeView.BeginUpdate();
            sceneTreeView.Nodes.AddRange(treeNodes);
            sceneTreeView.EndUpdate();
            
            var assetItems = BuildAssetItems(assetCollections);
            _visibleAssets = BuildListViewItems(assetItems);
            assetListView.VirtualListSize = _visibleAssets.Count;
        }

        private static AssetCollection[] _GetAssetCollections(GameData gameData)
            => gameData.GameBundle.Bundles.Where(b => !b.Name.StartsWith("Generated"))
                                          .SelectMany(b => b.Collections)
                                          .ToArray();

        private void selectAsset(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            previewPanel.Image = Properties.Resources.preview;
            previewPanel.SizeMode = PictureBoxSizeMode.CenterImage;
            classTextBox.Visible = false;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = string.Empty;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            FMODpanel.Visible = false;
            glControl1.Visible = false;
            StatusStripUpdate(string.Empty);

            lastSelectedItem = (AssetItem)_visibleAssets[e.ItemIndex].Tag;

            if (!e.IsSelected) 
                return;
            
            switch (tabControl2.SelectedIndex)
            {
                case 0: //Preview
                    if (enablePreview.Checked)
                    {
                        string info = PreviewAssetItem(lastSelectedItem);
                        if (displayInfo.Checked && !string.IsNullOrEmpty(info))
                        {
                            assetInfoLabel.Text = info;
                            assetInfoLabel.Visible = true;
                        }
                    }
                    break;
                case 1: //Dump
                    DumpAsset(lastSelectedItem);
                    break;
            }
        }
        
        private void typeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var typeItem = (ToolStripMenuItem)sender;
            if (typeItem != allToolStripMenuItem)
            {
                allToolStripMenuItem.Checked = false;

                var monoBehaviourItemArray = filterTypeToolStripMenuItem.DropDownItems.Find("MonoBehaviour", false);
                var monoBehaviourMocItemArray = filterTypeToolStripMenuItem.DropDownItems.Find("MonoBehaviour (Live2D Model)", false);
                if (monoBehaviourItemArray.Length > 0 && monoBehaviourMocItemArray.Length > 0)
                {
                    var monoBehaviourItem = (ToolStripMenuItem)monoBehaviourItemArray[0];
                    var monoBehaviourMocItem = (ToolStripMenuItem)monoBehaviourMocItemArray[0];
                    if (typeItem == monoBehaviourItem && monoBehaviourItem.Checked)
                    {
                        monoBehaviourMocItem.Checked = false;
                    }
                    else if (typeItem == monoBehaviourMocItem && monoBehaviourMocItem.Checked)
                    {
                        monoBehaviourItem.Checked = false;
                    }
                }
            }
            else if (allToolStripMenuItem.Checked)
            {
                for (var i = 1; i < filterTypeToolStripMenuItem.DropDownItems.Count; i++)
                {
                    var item = (ToolStripMenuItem)filterTypeToolStripMenuItem.DropDownItems[i];
                    item.Checked = false;
                }
            }
            FilterAssetList();
        }

        private void AssetStudioForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (glControl1.Visible)
            {
                if (e.Control)
                {
                    switch (e.KeyCode)
                    {
                        case Keys.W:
                            //Toggle WireFrame
                            wireFrameMode = (wireFrameMode + 1) % 3;
                            glControl1.Invalidate();
                            break;
                        case Keys.S:
                            //Toggle Shade
                            shadeMode = (shadeMode + 1) % 2;
                            glControl1.Invalidate();
                            break;
                        case Keys.N:
                            //Normal mode
                            normalMode = (normalMode + 1) % 2;
                            CreateVAO();
                            glControl1.Invalidate();
                            break;
                    }
                }
            }
            else if (previewPanel.Visible)
            {
                if (e.Control)
                {
                    var need = false;
                    
                    if (need)
                    {
                        if (lastSelectedItem != null)
                        {
                            string info = PreviewAssetItem(lastSelectedItem);
                            assetInfoLabel.Text = info;
                        }
                    }
                }
            }
        }

        private void exportClassStructuresMenuItem_Click(object sender, EventArgs e)
        {
            if (classesListView.Items.Count > 0)
            {
                var saveFolderDialog = new OpenFolderDialog();
                if (saveFolderDialog.ShowDialog(this) == DialogResult.OK)
                {
                    var savePath = saveFolderDialog.Folder;
                    var count = classesListView.Items.Count;
                    int i = 0;
                    Progress.Reset();
                    foreach (TypeTreeItem item in classesListView.Items)
                    {
                        var versionPath = Path.Combine(savePath, item.Group.Header);
                        Directory.CreateDirectory(versionPath);

                        var saveFile = $"{versionPath}{Path.DirectorySeparatorChar}{item.SubItems[1].Text} {item.Text}.txt";
                        File.WriteAllText(saveFile, item.ToString());

                        Progress.Report(++i, count);
                    }

                    Logger.Info("Finished exporting class structures");
                }
            }
        }

        private void displayAll_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.displayAll = displayAll.Checked;
            Properties.Settings.Default.Save();
        }

        private void enablePreview_Check(object sender, EventArgs e)
        {
            Properties.Settings.Default.enablePreview = enablePreview.Checked;
            Properties.Settings.Default.Save();
        }

        private void displayAssetInfo_Check(object sender, EventArgs e)
        {
            if (displayInfo.Checked && assetInfoLabel.Text != null)
            {
                assetInfoLabel.Visible = true;
            }
            else
            {
                assetInfoLabel.Visible = false;
            }

            Properties.Settings.Default.displayInfo = displayInfo.Checked;
            Properties.Settings.Default.Save();
        }

        private void showExpOpt_Click(object sender, EventArgs e)
        {
            var exportOpt = new ExportOptions();
            exportOpt.ShowDialog(this);
        }

        private void assetListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            e.Item = _visibleAssets[e.ItemIndex]; 
        }

        private void tabPageSelected(object sender, TabControlEventArgs e)
        {
            switch (e.TabPageIndex)
            {
                case 0:
                    sceneTreeView.Select();
                    break;
                case 1:
                    assetListView.Select();
                    break;
            }
        }

        private void treeSearch_Enter(object sender, EventArgs e)
        {
            if (treeSearch.Text == " Search ")
            {
                treeSearch.Text = "";
                treeSearch.ForeColor = SystemColors.WindowText;
            }
        }

        private void treeSearch_Leave(object sender, EventArgs e)
        {
            if (treeSearch.Text == "")
            {
                treeSearch.Text = " Search ";
                treeSearch.ForeColor = SystemColors.GrayText;
            }
        }

        private void treeSearch_TextChanged(object sender, EventArgs e)
        {
            treeSrcResults.Clear();
            nextGObject = 0;
        }

        private void treeSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (treeSrcResults.Count == 0)
                {
                    var isExactSearch = sceneExactSearchCheckBox.Checked;
                    foreach (TreeNode node in sceneTreeView.Nodes)
                    {
                        TreeNodeSearch(node, isExactSearch);
                    }
                }
                if (treeSrcResults.Count > 0)
                {
                    if (nextGObject >= treeSrcResults.Count)
                    {
                        nextGObject = 0;
                    }
                    treeSrcResults[nextGObject].EnsureVisible();
                    sceneTreeView.SelectedNode = treeSrcResults[nextGObject];
                    nextGObject++;
                }
            }
        }

        private void TreeNodeSearch(TreeNode treeNode, bool isExactSearch)
        {
            if (isExactSearch && string.Equals(treeNode.Text, treeSearch.Text, StringComparison.InvariantCultureIgnoreCase))
            {
                treeSrcResults.Add(treeNode);
            }
            else if (!isExactSearch && treeNode.Text.IndexOf(treeSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                treeSrcResults.Add(treeNode);
            }

            foreach (TreeNode node in treeNode.Nodes)
            {
                TreeNodeSearch(node, isExactSearch);
            }
        }

        private void sceneExactSearchCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            treeSearch_TextChanged(sender, e);
        }

        private void sceneTreeView_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (!treeRecursionEnabled)
                return;

            if (!isRecursionEvent)
            {
                if (e.Node.Checked)
                {
                    treeNodeSelectedList.Add(e.Node);
                }
                else
                {
                    treeNodeSelectedList.Remove(e.Node);
                }
            }

            foreach (TreeNode childNode in e.Node.Nodes)
            {
                isRecursionEvent = true;
                bool wasChecked = childNode.Checked;
                childNode.Checked = e.Node.Checked;
                if (!wasChecked && childNode.Checked)
                {
                    treeNodeSelectedList.Add(childNode);
                }
                else if (!childNode.Checked)
                {
                    treeNodeSelectedList.Remove(childNode);
                }
            }
            isRecursionEvent = false;

            StatusStripUpdate($"Selected {treeNodeSelectedList.Count} object(s).");
        }

        private void listSearch_Enter(object sender, EventArgs e)
        {
            if (listSearch.Text == " Filter ")
            {
                listSearch.Text = "";
                listSearch.ForeColor = SystemColors.WindowText;
                BeginInvoke(new Action(() => { enableFiltering = true; }));
            }
        }

        private void listSearch_Leave(object sender, EventArgs e)
        {
            if (listSearch.Text == "")
            {
                enableFiltering = false;
                listSearch.Text = " Filter ";
                listSearch.ForeColor = SystemColors.GrayText;
                listSearch.BackColor = SystemColors.Window;
            }
        }

        private void ListSearchTextChanged(object sender, EventArgs e)
        {
            if (enableFiltering)
            {
                if (delayTimer.Enabled)
                {
                    delayTimer.Stop();
                    delayTimer.Start();
                }
                else
                {
                    delayTimer.Start();
                }
            }
        }

        private void delayTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            delayTimer.Stop();
            ListSearchHistoryAdd();
            Invoke(new Action(FilterAssetList));
        }

        private void ListSearchHistoryAdd()
        {
            BeginInvoke(new Action(() =>
            {
                if (listSearch.Text != "" && listSearch.Text != " Filter ")
                {
                    if (listSearchHistory.Items.Count == listSearchHistory.MaxDropDownItems)
                    {
                        listSearchHistory.Items.RemoveAt(listSearchHistory.MaxDropDownItems - 1);
                    }
                    listSearchHistory.Items.Insert(0, listSearch.Text);
                }
            }));
        }

        private void assetListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sortColumn != e.Column)
            {
                reverseSort = false;
            }
            else
            {
                reverseSort = !reverseSort;
            }
            sortColumn = e.Column;
            assetListView.BeginUpdate();
            assetListView.SelectedIndices.Clear();
            selectedIndicesPrevList.Clear();
            selectedAnimationAssetsList.Clear();
            if (sortColumn == 5) //Compressed Size Estimate
            {
                _visibleAssets.Sort((ali, bli) =>
                {
                    var a = (AssetItem)ali.Tag;
                    var b = (AssetItem)bli.Tag;
                    var asf = a.CompressedSizeEstimate;
                    var bsf = b.CompressedSizeEstimate;
                    
                    return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                });
            }
            else if (sortColumn == 4) //FullSize
            {
                _visibleAssets.Sort((ali, bli) =>
                {
                    var a = (AssetItem)ali.Tag;
                    var b = (AssetItem)bli.Tag;
                    var asf = a.FullSize;
                    var bsf = b.FullSize;
                    
                    return reverseSort ? bsf.CompareTo(asf) : asf.CompareTo(bsf);
                });
            }
            else if (sortColumn == 3) // PathID
            {
                _visibleAssets.Sort((ali, bli) =>
                {
                    var a = (AssetItem)ali.Tag;
                    var b = (AssetItem)bli.Tag;
                    long pathIdX = a.PathID;
                    long pathIdY = b.PathID;
                    
                    return reverseSort ? pathIdY.CompareTo(pathIdX) : pathIdX.CompareTo(pathIdY);
                });
            }
            else if (sortColumn == 0) // Name
            {
                _visibleAssets.Sort((a, b) =>
                {
                    var at = a.SubItems[sortColumn].Text;
                    var bt = b.SubItems[sortColumn].Text;
                    
                    return reverseSort 
                        ? alphanumComparator.Compare(bt, at) 
                        : alphanumComparator.Compare(at, bt);
                });
            }
            else
            {
                _visibleAssets.Sort((a, b) =>
                {
                    var at = a.SubItems[sortColumn].Text.AsSpan();
                    var bt = b.SubItems[sortColumn].Text.AsSpan();

                    return reverseSort 
                        ? bt.CompareTo(at, StringComparison.OrdinalIgnoreCase) 
                        : at.CompareTo(bt, StringComparison.OrdinalIgnoreCase);
                });
            }
            assetListView.EndUpdate();
        }

       

        private void DumpAsset(AssetItem assetItem)
        {
            if (assetItem == null)
                return;

            if (useDumpTreeViewToolStripMenuItem.Checked)
            {
                using (var jsonDoc = DumpAssetToJsonDoc(assetItem.UnityObject))
                {
                    dumpTreeView.LoadFromJson(jsonDoc, assetItem.Name);
                }
            }
            else
            {
                dumpTextBox.Text = Studio.DumpAsset(assetItem.UnityObject);
            }
        }

        private void classesListView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            classTextBox.Visible = true;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            FMODpanel.Visible = false;
            glControl1.Visible = false;
            StatusStripUpdate("");
            if (e.IsSelected)
            {
                classTextBox.Text = ((TypeTreeItem)classesListView.SelectedItems[0]).ToString();
                lastSelectedItem = new AssetItem();
            }
        }

        private void preview_Resize(object sender, EventArgs e)
        {
            if (glControlLoaded && glControl1.Visible)
            {
                ChangeGLSize(glControl1.Size);
                glControl1.Invalidate();
            }
        }

        private string PreviewAssetItem(AssetItem assetItem)
        {
            try
            {
                switch (assetItem.UnityObject.ClassID)
                {
                    case (int)ClassIDType.Texture2D:
                        return PreviewTexture2D(assetItem, assetItem.UnityObject as ITexture2D);
                    case (int)ClassIDType.Texture2DArray:
                        return PreviewTexture2DArray(assetItem.UnityObject as ITexture2DArray);
                    case (int)ClassIDType.AudioClip:
                        return PreviewAudioClip(assetItem, assetItem.UnityObject as IAudioClip);
                    case (int)ClassIDType.Shader:
                        PreviewShader(assetItem, assetItem.UnityObject as IShader);
                        break;
                    /*case (int)ClassIDType.TextAsset:
                        PreviewTextAsset(assetItem.Asset as TextAsset);
                        break;
                    case (int)ClassIDType.MonoBehaviour:
                        var m_MonoBehaviour = (MonoBehaviour)assetItem.Asset;
                        if (m_MonoBehaviour.m_Script.TryGet(out var m_Script))
                        {
                            if (m_Script.m_ClassName == "CubismMoc")
                            {
                                PreviewMoc(assetItem, m_MonoBehaviour);
                                break;
                            }
                        }
                        PreviewMonoBehaviour(m_MonoBehaviour);
                        break;
                    case (int)ClassIDType.Font:
                        PreviewFont(assetItem.Asset as Font);
                        break;
                    case (int)ClassIDType.Mesh:
                        PreviewMesh(assetItem.Asset as Mesh);
                        break;
                    case (int)ClassIDType.VideoClip:
                        PreviewVideoClip(assetItem, assetItem.Asset as VideoClip);
                        break;
                    case (int)ClassIDType.MovieTexture:
                        StatusStripUpdate("Only supported export.");
                        break;
                    case (int)ClassIDType.Sprite:
                        PreviewSprite(assetItem, assetItem.Asset as Sprite);
                        break;
                    case (int)ClassIDType.Animator:
                        StatusStripUpdate("Can be exported to FBX file.");
                        break;
                    case (int)ClassIDType.AnimationClip:
                        StatusStripUpdate("Can be exported with Animator or Objects");
                        break;
                    default:
                        var str = assetItem.Asset.Dump();
                        if (str != null)
                        {
                            textPreviewBox.Text = str;
                            textPreviewBox.Visible = true;
                        }
                        break;
                        */
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($@"Preview {assetItem.TypeString}:{assetItem.Name} error\r\n{e.Message}\r\n{e.StackTrace}");
            }
            
            return string.Empty;
        }
        
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
                
                PreviewTexture(directBitmap, texture2D.Format_C28);

                StatusStripUpdate("'Ctrl'+'R'/'G'/'B'/'A' for Channel Toggle");
                return sb.ToString();
            }
            else
            {
                StatusStripUpdate("Unsupported image for preview");
                return string.Empty;
            }
        }
        
        private void PreviewTexture(DirectBitmap directBitmap, int textureFormat)
        {
            var dataSize = directBitmap.Width * directBitmap.Height * directBitmap.PixelSize;
            var bits = ArrayPool<byte>.Shared.Rent(dataSize);
            // TODO: Why RGBA32 need to flip R-B
            if (textureFormat == (int)TextureFormat.RGBA32)
            {
                for (var y = 0; y < directBitmap.Height; y++)
                {
                    for (var x = 0; x < directBitmap.Width; x++)
                    {
                        var baseIndex = (y * directBitmap.Width + x) * directBitmap.PixelSize;
                        bits[baseIndex + 0] = directBitmap.Bits[baseIndex + 2];
                        bits[baseIndex + 1] = directBitmap.Bits[baseIndex + 1];
                        bits[baseIndex + 2] = directBitmap.Bits[baseIndex + 0];
                        bits[baseIndex + 3] = directBitmap.Bits[baseIndex + 3];
                    }
                }
            }
            else
            {
                directBitmap.Bits.CopyTo(bits); 
            }

            var handle = GCHandle.Alloc(bits, GCHandleType.Pinned);
            var bitmap = new Bitmap
            (
                directBitmap.Width, 
                directBitmap.Height, 
                directBitmap.Width * directBitmap.PixelSize,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb, 
                handle.AddrOfPinnedObject()
            );
            previewPanel.Image = bitmap;
            
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

        
        private FMOD.System system;
        private FMOD.Sound sound;
        private FMOD.Channel channel;
        private FMOD.SoundGroup masterSoundGroup;
        private FMOD.MODE loopMode = FMOD.MODE.LOOP_OFF;
        private uint FMODlenms;
        private uint FMODloopstartms;
        private uint FMODloopendms;
        private float FMODVolume = 0.8f;
        private string PreviewAudioClip(AssetItem assetItem, IAudioClip audioClip)
        {
            var sb = new StringBuilder();

            if (audioClip.Has_CompressionFormat())
            {
                sb.Append("Compression format: ");
                switch (audioClip.CompressionFormat)
                {
                    case 0:
                        sb.AppendLine("PCM");
                        break;
                    case 1:
                        sb.AppendLine("Vorbis");
                        break;
                    case 2:
                        sb.AppendLine("ADPCM");
                        break;
                    case 3:
                        sb.AppendLine("MP3");
                        break;
                    case 4:
                        sb.AppendLine("VAG");
                        break;
                    case 5:
                        sb.AppendLine("HEVAG");
                        break;
                    case 6:
                        sb.AppendLine("XMA");
                        break;
                    case 7:
                        sb.AppendLine("AAC");
                        break;
                    case 8:
                        sb.AppendLine("GCADPCM");
                        break;
                    case 9:
                        sb.AppendLine("ATRAC9");
                        break;
                    default:
                        sb.AppendLine("Unknown");
                        break;
                }
            }
            
            if (audioClip.Has_Format())
            {
                sb.Append("Format: ");
                switch (audioClip.GetSoundFormat())
                {
                    case FmodSoundFormat.None:
                        sb.AppendLine("None");
                        break;
                    case FmodSoundFormat.Pcm8:
                        sb.AppendLine("PCM 8");
                        break;
                    case FmodSoundFormat.Pcm16:
                        sb.AppendLine("PCM 16");
                        break;
                    case FmodSoundFormat.Pcm24:
                        sb.AppendLine("PCM 24");
                        break;
                    case FmodSoundFormat.Pcm32:
                        sb.AppendLine("PCM 32");
                        break;
                    case FmodSoundFormat.Pcmfloat:
                        sb.AppendLine("PCM Float");
                        break;
                    case FmodSoundFormat.Gcadpcm:
                        sb.AppendLine("Nintendo 3DS/Wii DSP GCADPCM");
                        break;
                    case FmodSoundFormat.Imaadpcm:
                        sb.AppendLine("IMAADPCM"); 
                        break;
                    case FmodSoundFormat.Vag:
                        sb.AppendLine("PlayStation 2 / PlayStation Portable ADPCM VAG");
                        break;
                    case FmodSoundFormat.Hevag:
                        sb.AppendLine("PSVita ADPCM");
                        break;
                    case FmodSoundFormat.Xma:
                        sb.AppendLine("Xbox 360 XMA");
                        break;
                    case FmodSoundFormat.Mpeg:
                        sb.AppendLine("MP2/MP3 MPEG");
                        break;
                    case FmodSoundFormat.Celt:
                        sb.AppendLine("CELT"); 
                        break;
                    case FmodSoundFormat.At9:
                        sb.AppendLine("NGP ATRAC 9");
                        break;
                    case FmodSoundFormat.Xwma:
                        sb.AppendLine("Xbox 360 XWMA");
                        break;
                    case FmodSoundFormat.Vorbis:
                        sb.AppendLine("OGG Vorbis"); 
                        break;
                    case FmodSoundFormat.Max:
                        sb.AppendLine("Max");  
                        break;
                    case FmodSoundFormat.Forceint:
                        sb.AppendLine("Force Int");   
                        break;
                    default:
                        sb.AppendLine( $"Unknown ({audioClip.GetSoundFormat()})");
                        break;
                }
            }

            if (audioClip.Has_Type())
            {
                sb.Append("Type: ");
                switch (audioClip.GetSoundType())
                {
                    case FmodSoundType.Unknown:
                        sb.AppendLine("Unknown");
                        break;
                    case FmodSoundType.Acc:
                        sb.AppendLine("ACC");
                        break;
                    case FmodSoundType.Aiff:
                        sb.AppendLine("AIFF");
                        break;
                    case FmodSoundType.Asf:
                        sb.AppendLine("ASF");
                        break;
                    case FmodSoundType.At3:
                        sb.AppendLine("AT3");
                        break;
                    case FmodSoundType.Cdda:
                        sb.AppendLine("CDDA");
                        break;
                    case FmodSoundType.Dls:
                        sb.AppendLine("DLS");
                        break;
                    case FmodSoundType.Flac:
                        sb.AppendLine("FLAC");
                        break;
                    case FmodSoundType.Fsb:
                        sb.AppendLine("FSB");
                        break;
                    case FmodSoundType.Gcadpcm:
                        sb.AppendLine("GCADPCM");
                        break;
                    case FmodSoundType.It:
                        sb.AppendLine("IT");
                        break;
                    case FmodSoundType.Midi:
                        sb.AppendLine("MIDI");
                        break;
                    case FmodSoundType.Mod:
                        sb.AppendLine("MOD");
                        break;
                    case FmodSoundType.Mpeg:
                        sb.AppendLine("MPEG");
                        break;
                    case FmodSoundType.Oggvorbis:
                        sb.AppendLine("OGG Vorbis");
                        break;
                    case FmodSoundType.Playlist:
                        sb.AppendLine("Playlist");
                        break;
                    case FmodSoundType.Raw:
                        sb.AppendLine("Raw");
                        break;
                    case FmodSoundType.S3m:
                        sb.AppendLine("S3M");
                        break;
                    case FmodSoundType.Sf2:
                        sb.AppendLine("SF2");
                        break;
                    case FmodSoundType.User:
                        sb.AppendLine("User");
                        break;
                    case FmodSoundType.Wav:
                        sb.AppendLine("WAV");
                        break;
                    case FmodSoundType.Xm:
                        sb.AppendLine("XM");
                        break;
                    case FmodSoundType.Xma:
                        sb.AppendLine("XMA");
                        break;
                    case FmodSoundType.Vag:
                        sb.AppendLine("VAG");
                        break;
                    case FmodSoundType.Audioqueue:
                        sb.AppendLine("Audio Queue");
                        break;
                    case FmodSoundType.Xwma:
                        sb.AppendLine("XWMA");
                        break;
                    case FmodSoundType.Bcwav:
                        sb.AppendLine("BCWAV");
                        break;
                    case FmodSoundType.At9:
                        sb.AppendLine("AT9");
                        break;
                    case FmodSoundType.Vorbis:
                        sb.AppendLine("Vorbis");
                        break;
                    case FmodSoundType.MediaFoundation:
                        sb.AppendLine("Media Foundation");
                        break;
                    case FmodSoundType.Max:
                        sb.AppendLine("MAX");
                        break;
                    case FmodSoundType.Forceint:
                        sb.AppendLine("Force Int");
                        break;
                    default:
                        sb.AppendLine("Unknown");
                        break;
                }
            }

            if (audioClip.Has_Length())
                sb.AppendLine($"Length: {audioClip.Length:0.0##}");
            if (audioClip.Has_Channels())
                sb.AppendLine($"Channel count: {audioClip.Channels}");
            if(audioClip.Has_Frequency())
                sb.AppendLine($"Sample rate: {audioClip.Frequency}");
            if (audioClip.Has_BitsPerSample())
                sb.AppendLine($"Bit depth: {audioClip.BitsPerSample}");
            
            var audioData = audioClip.GetAudioData();
            
            if (audioData.Length == 0)
                return sb.ToString();
            
            var exinfo = new FMOD.CREATESOUNDEXINFO();

            exinfo.cbsize = Marshal.SizeOf(exinfo);
            exinfo.length = (uint)audioData.Length;

            var result = system.createSound(audioData, FMOD.MODE.OPENMEMORY | loopMode, ref exinfo, out sound);
            if (ERRCHECK(result)) return sb.ToString();

            sound.getNumSubSounds(out var numsubsounds);

            if (numsubsounds > 0)
            {
                result = sound.getSubSound(0, out var subSound);
                if (result == FMOD.RESULT.OK)
                {
                    sound = subSound;
                }
            }

            result = sound.getLength(out FMODlenms, FMOD.TIMEUNIT.MS);
            if (ERRCHECK(result)) return sb.ToString();

            result = sound.getLoopPoints(out FMODloopstartms, FMOD.TIMEUNIT.MS, out FMODloopendms, FMOD.TIMEUNIT.MS);
            if (result == FMOD.RESULT.OK)
            {
                sb.AppendLine($"Loop Start: {(FMODloopstartms / 1000 / 60):00}:{(FMODloopstartms / 1000 % 60):00}.{(FMODloopstartms / 10 % 100):00}");
                sb.AppendLine($"Loop End: {(FMODloopendms / 1000 / 60):00}:{(FMODloopendms / 1000 % 60):00}.{(FMODloopendms / 10 % 100):00}");
            }
            
            _ = system.getMasterChannelGroup(out var channelGroup);
            result = system.playSound(sound, channelGroup, true, out channel);
            if (ERRCHECK(result)) return sb.ToString();

            FMODpanel.Visible = true;

            result = channel.getFrequency(out var frequency);
            if (ERRCHECK(result)) return sb.ToString();

            FMODinfoLabel.Text = frequency + " Hz";
            FMODtimerLabel.Text = $"00:00.00 / {(FMODlenms / 1000 / 60):00}:{(FMODlenms / 1000 % 60):00}.{(FMODlenms / 10 % 100):00}";

            return sb.ToString();
        }
        
        private void FMODinit()
        {
            FMODreset();

            var result = FMOD.Factory.System_Create(out system);
            if (ERRCHECK(result)) { return; }

            result = system.getVersion(out var version);
            ERRCHECK(result);
            if (version < FMOD.VERSION.number)
            {
                Logger.Error($"Error! You are using an old version of FMOD {version:X}. This program requires {FMOD.VERSION.number:X}.");
                Application.Exit();
            }

            result = system.init(2, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
            if (ERRCHECK(result)) { return; }

            result = system.getMasterSoundGroup(out masterSoundGroup);
            if (ERRCHECK(result)) { return; }

            result = masterSoundGroup.setVolume(FMODVolume);
            if (ERRCHECK(result)) { return; }
        }
        
        private bool ERRCHECK(FMOD.RESULT result)
        {
            if (result != FMOD.RESULT.OK)
            {
                FMODreset();
                Logger.Warning($"FMOD error! {result} - {FMOD.Error.String(result)}");
                return true;
            }
            return false;
        }

        private void FMODreset()
        {
            timer.Stop();
            FMODprogressBar.Value = 0;
            FMODtimerLabel.Text = "00:00.00 / 00:00.00";
            FMODstatusLabel.Text = "Stopped";
            FMODinfoLabel.Text = string.Empty;

            if (sound.hasHandle())
            {
                var result = sound.release();
                ERRCHECK(result);
                sound.clearHandle();
            }
        }
        
        private void FMODplayButton_Click(object sender, EventArgs e)
        {
            if (sound.hasHandle() && channel.hasHandle())
            {
                _ = system.getMasterChannelGroup(out var channelGroup);
                timer.Start();
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.stop();
                    if (ERRCHECK(result)) { return; }

                    result = system.playSound(sound, channelGroup, false, out channel);
                    if (ERRCHECK(result)) { return; }

                    FMODpauseButton.Text = "Pause";
                }
                else
                {
                    result = system.playSound(sound, channelGroup, false, out channel);
                    if (ERRCHECK(result)) { return; }
                    FMODstatusLabel.Text = "Playing";

                    if (FMODprogressBar.Value > 0)
                    {
                        uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;

                        result = channel.setPosition(newms, FMOD.TIMEUNIT.MS);
                        if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                        {
                            if (ERRCHECK(result)) { return; }
                        }

                    }
                }
            }
        }
        
        private void FMODpauseButton_Click(object sender, EventArgs e)
        {
            if (sound.hasHandle() && channel.hasHandle())
            {
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.getPaused(out var paused);
                    if (ERRCHECK(result)) { return; }
                    result = channel.setPaused(!paused);
                    if (ERRCHECK(result)) { return; }

                    if (paused)
                    {
                        FMODstatusLabel.Text = "Playing";
                        FMODpauseButton.Text = "Pause";
                        timer.Start();
                    }
                    else
                    {
                        FMODstatusLabel.Text = "Paused";
                        FMODpauseButton.Text = "Resume";
                        timer.Stop();
                    }
                }
            }
        }

        private void FMODstopButton_Click(object sender, EventArgs e)
        {
            if (channel.hasHandle())
            {
                var result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing)
                {
                    result = channel.stop();
                    if (ERRCHECK(result)) { return; }
                    //channel = null;
                    //don't FMODreset, it will nullify the sound
                    timer.Stop();
                    FMODprogressBar.Value = 0;
                    FMODtimerLabel.Text = "00:00.00 / 00:00.00";
                    FMODstatusLabel.Text = "Stopped";
                    FMODpauseButton.Text = "Pause";
                }
            }
        }

        private void FMODloopButton_CheckedChanged(object sender, EventArgs e)
        {
            FMOD.RESULT result;

            loopMode = FMODloopButton.Checked ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF;

            if (sound.hasHandle())
            {
                result = sound.setMode(loopMode);
                if (ERRCHECK(result)) { return; }
            }

            if (channel.hasHandle())
            {
                result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                result = channel.getPaused(out var paused);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing || paused)
                {
                    result = channel.setMode(loopMode);
                    if (ERRCHECK(result)) { return; }
                }
            }
        }

        private void FMODvolumeBar_ValueChanged(object sender, EventArgs e)
        {
            FMODVolume = Convert.ToSingle(FMODvolumeBar.Value) / 10;

            var result = masterSoundGroup.setVolume(FMODVolume);
            if (ERRCHECK(result)) { return; }
        }

        private void FMODprogressBar_Scroll(object sender, EventArgs e)
        {
            if (channel.hasHandle())
            {
                uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;
                FMODtimerLabel.Text = $@"{newms / 1000 / 60:00}:{newms / 1000 % 60:00}.{newms / 10 % 100:00} / {FMODlenms / 1000 / 60:00}:{FMODlenms / 1000 % 60:00}.{FMODlenms / 10 % 100:00}";
            }
        }

        private void FMODprogressBar_MouseDown(object sender, MouseEventArgs e)
        {
            timer.Stop();
        }

        private void FMODprogressBar_MouseUp(object sender, MouseEventArgs e)
        {
            if (channel.hasHandle())
            {
                uint newms = FMODlenms / 1000 * (uint)FMODprogressBar.Value;

                var result = channel.setPosition(newms, FMOD.TIMEUNIT.MS);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }


                result = channel.isPlaying(out var playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    if (ERRCHECK(result)) { return; }
                }

                if (playing) { timer.Start(); }
            }
        }
        
        private void timer_Tick(object sender, EventArgs e)
        {
            uint ms = 0;
            bool playing = false;
            bool paused = false;

            if (channel.hasHandle())
            {
                var result = channel.getPosition(out ms, FMOD.TIMEUNIT.MS);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }

                result = channel.isPlaying(out playing);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }

                result = channel.getPaused(out paused);
                if ((result != FMOD.RESULT.OK) && (result != FMOD.RESULT.ERR_INVALID_HANDLE))
                {
                    ERRCHECK(result);
                }
            }

            FMODtimerLabel.Text = $@"{ms / 1000 / 60:00}:{ms / 1000 % 60:00}.{ms / 10 % 100:00} / {FMODlenms / 1000 / 60:00}:{FMODlenms / 1000 % 60:00}.{FMODlenms / 10 % 100:00}";
#if NETFRAMEWORK
            FMODprogressBar.Value = (int)Math.Max(0, Math.Min(ms * 1000f / FMODlenms, 1000));
#else
            FMODprogressBar.Value = (int)Math.Clamp(ms * 1000f / FMODlenms, 0, 1000);
#endif
            FMODstatusLabel.Text = paused ? "Paused " : playing ? "Playing" : "Stopped";

            if (system.hasHandle() && channel.hasHandle())
            {
                system.update();
            }
        }
        
        private void PreviewShader(AssetItem assetItem, IShader shader)
        {
            var sb = new StringBuilder();
            sb.AppendLine(shader.Name);
            var decompressedBlobSize = shader.DecompressedSize;
            var decompressedBytes = new byte[decompressedBlobSize];
            LZ4Codec.Decode(shader.CompressedBlob, decompressedBytes);
            ShaderSubProgram
            
            string str = shader.Script;
            PreviewText(str == null ? "Serialized Shader can't be read" : str.Replace("\n", "\r\n"));
        }
        
        private void PreviewText(string text)
        {
            textPreviewBox.Text = text;
            textPreviewBox.Visible = true;
        }

        private void SetProgressBarValue(int value)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => 
                {
                    progressBar1.Value = value;
                    progressBar1.Style = ProgressBarStyle.Continuous;
                }));
            }
            else
            {
                progressBar1.Style = ProgressBarStyle.Continuous;
                progressBar1.Value = value;
            }

            BeginInvoke(new Action(() => 
            {
                var max = progressBar1.Maximum;
                taskbar.SetProgressValue(value, max);
                if (value == max)
                    taskbar.SetProgressState(TaskbarProgressBarState.NoProgress);
                else
                    taskbar.SetProgressState(TaskbarProgressBarState.Normal);
            }));
        }

        private void StatusStripUpdate(string statusText)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => { toolStripStatusLabel1.Text = statusText; }));
            }
            else
            {
                toolStripStatusLabel1.Text = statusText;
            }
        }

        private void ResetForm()
        {
            Text = guiTitle;
            sceneTreeView.Nodes.Clear();
            assetListView.VirtualListSize = 0;
            assetListView.Items.Clear();
            classesListView.Items.Clear();
            classesListView.Groups.Clear();
            selectedAnimationAssetsList.Clear();
            selectedIndicesPrevList.Clear();
            previewPanel.Image = Properties.Resources.preview;
            previewPanel.SizeMode = PictureBoxSizeMode.CenterImage;
            assetInfoLabel.Visible = false;
            assetInfoLabel.Text = null;
            textPreviewBox.Visible = false;
            fontPreviewBox.Visible = false;
            glControl1.Visible = false;
            lastSelectedItem = new AssetItem();
            sortColumn = -1;
            reverseSort = false;
            enableFiltering = false;
            listSearch.Text = " Filter ";
            listSearch.ForeColor = SystemColors.GrayText;
            listSearch.BackColor = SystemColors.Window;
            if (tabControl1.SelectedIndex == 1)
                assetListView.Select();

            var count = filterTypeToolStripMenuItem.DropDownItems.Count;
            for (var i = 1; i < count; i++)
            {
                filterTypeToolStripMenuItem.DropDownItems.RemoveAt(1);
            }

            taskbar.SetProgressState(TaskbarProgressBarState.NoProgress);
        }

        private void tabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (tabControl2.SelectedIndex)
            {
                case 0: //Preview
                    if (lastPreviewItem != lastSelectedItem)
                    {
                        string info = PreviewAssetItem(lastSelectedItem);
                        if (displayInfo.Checked && !string.IsNullOrEmpty(info))
                        {
                            assetInfoLabel.Text = info;
                            assetInfoLabel.Visible = true;
                        }
                    }
                    break;
                case 1: //Dump
                    DumpAsset(lastSelectedItem);
                    break;
            }
        }

        private void assetListView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && assetListView.SelectedIndices.Count > 0)
            {
                goToSceneHierarchyToolStripMenuItem.Visible = false;
                showOriginalFileToolStripMenuItem.Visible = false;
                exportAnimatorWithSelectedAnimationClipMenuItem.Visible = false;
                exportAsLive2DModelToolStripMenuItem.Visible = false;
                exportL2DWithFadeLstToolStripMenuItem.Visible = false;
                exportL2DWithFadeToolStripMenuItem.Visible = false;
                exportL2DWithClipsToolStripMenuItem.Visible = false;

                if (assetListView.SelectedIndices.Count == 1)
                {
                    goToSceneHierarchyToolStripMenuItem.Visible = true;
                    showOriginalFileToolStripMenuItem.Visible = true;
                }
                if (assetListView.SelectedIndices.Count >= 1)
                {
                    var selectedAssets = GetSelectedAssets();

                    var selectedTypes = (SelectedAssetType)0;
                    foreach (var asset in selectedAssets)
                    {
                        switch (asset.UnityObject)
                        {
                         
                        }
                    }
                    exportAnimatorWithSelectedAnimationClipMenuItem.Visible = (selectedTypes & SelectedAssetType.Animator) !=0 && (selectedTypes & SelectedAssetType.AnimationClip) != 0;
                    exportAsLive2DModelToolStripMenuItem.Visible = (selectedTypes & SelectedAssetType.MonoBehaviourMoc) != 0;
                    exportL2DWithFadeLstToolStripMenuItem.Visible = (selectedTypes & SelectedAssetType.MonoBehaviourMoc) !=0 && (selectedTypes & SelectedAssetType.MonoBehaviourFadeLst) != 0;
                    exportL2DWithFadeToolStripMenuItem.Visible = (selectedTypes & SelectedAssetType.MonoBehaviourMoc) != 0 && (selectedTypes & SelectedAssetType.MonoBehaviourFade) !=0;
                    exportL2DWithClipsToolStripMenuItem.Visible = (selectedTypes & SelectedAssetType.MonoBehaviourMoc) !=0 && (selectedTypes & SelectedAssetType.AnimationClip) != 0;
                }

                var selectedElement = assetListView.HitTest(new Point(e.X, e.Y));
                var subItemIndex = selectedElement.Item.SubItems.IndexOf(selectedElement.SubItem);
                tempClipboard = selectedElement.SubItem.Text;
                copyToolStripMenuItem.Text = $"Copy {assetListView.Columns[subItemIndex].Text}";
                contextMenuStrip1.Show(assetListView, e.X, e.Y);
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(tempClipboard);
        }

        private void exportSelectedAssetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Convert);
        }

        private void dumpSelectedAssetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Dump);
        }

        private void showOriginalFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
           
        }

        private void exportAnimatorWithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
           
        }

        private void exportSelectedObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportObjects(false);
        }

        private void exportObjectsWithAnimationClipMenuItem_Click(object sender, EventArgs e)
        {
            ExportObjects(true);
        }

        private void ExportObjects(bool animation)
        {
           
        }

        private void exportSelectedObjectsMergeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportMergeObjects(false);
        }

        private void exportSelectedObjectsMergeWithAnimationClipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportMergeObjects(true);
        }

        private void ExportMergeObjects(bool animation)
        {
        
        }

        private void goToSceneHierarchyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectAsset = (AssetItem)assetListView.Items[assetListView.SelectedIndices[0]].Tag;
            /*if (selectAsset.TreeNode != null)
            {
                sceneTreeView.SelectedNode = selectAsset.TreeNode;
                tabControl1.SelectedTab = tabPage1;
            }*/
        }

        private void exportAllAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Convert);
        }

        private void exportSelectedAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Convert);
        }

        private void exportFilteredAssetsMenuItem_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Convert);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Raw);
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Raw);
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Raw);
        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.All, ExportType.Dump);
        }

        private void toolStripMenuItem8_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Selected, ExportType.Dump);
        }

        private void toolStripMenuItem9_Click(object sender, EventArgs e)
        {
            ExportAssets(ExportFilter.Filtered, ExportType.Dump);
        }

        private void toolStripMenuItem11_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.All);
        }

        private void toolStripMenuItem12_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.Selected);
        }

        private void toolStripMenuItem13_Click(object sender, EventArgs e)
        {
            ExportAssetsList(ExportFilter.Filtered);
        }

        private void exportAllObjectsSplitToolStripMenuItem1_Click(object sender, EventArgs e)
        {
           
        }

        private void assetListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            ProcessSelectedItems();
        }

        private void assetListView_VirtualItemsSelectionRangeChanged(object sender, ListViewVirtualItemsSelectionRangeChangedEventArgs e)
        {
            ProcessSelectedItems();
        }

        private void ProcessSelectedItems()
        {
           
        }

        private List<AssetItem> GetSelectedAssets()
        {
            var selectedAssets = new List<AssetItem>(assetListView.SelectedIndices.Count);
            foreach (int index in assetListView.SelectedIndices)
            {
                selectedAssets.Add((AssetItem)assetListView.Items[index].Tag);
            }

            return selectedAssets;
        }

        private void FilterAssetList()
        {
            assetListView.BeginUpdate();
            
            assetListView.EndUpdate();
        }

        private void ExportAssets(ExportFilter type, ExportType exportType)
        {
          
        }

        private void ExportAssetsList(ExportFilter type)
        {
           
        }

        private void toolStripMenuItem15_Click(object sender, EventArgs e)
        {
            GUILogger.ShowDebugMessage = toolStripMenuItem15.Checked;
        }

        private void sceneTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                sceneTreeView.SelectedNode = e.Node;
                sceneContextMenuStrip.Show(sceneTreeView, e.Location.X, e.Location.Y);
            }
        }

        private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (TreeNode node in sceneTreeView.Nodes)
            {
                node.Checked = true;
            }
        }

        private void clearSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeRecursionEnabled = false;
            for (var i = 0; i < treeNodeSelectedList.Count; i++)
            {
                treeNodeSelectedList[i].Checked = false;
            }
            treeRecursionEnabled = true;
            treeNodeSelectedList.Clear();
            StatusStripUpdate($"Selected {treeNodeSelectedList.Count} object(s).");
        }

        private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sceneTreeView.Nodes.Count > 500)
            {
                MessageBox.Show("Too many elements.");
                return;
            }

            sceneTreeView.BeginUpdate();
            foreach (TreeNode node in sceneTreeView.Nodes)
            {
                node.ExpandAll();
            }
            sceneTreeView.EndUpdate();
        }

        private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            sceneTreeView.BeginUpdate();
            foreach (TreeNode node in sceneTreeView.Nodes)
            {
                node.Collapse(ignoreChildren: false);
            }
            sceneTreeView.EndUpdate();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var aboutForm = new AboutForm();
            aboutForm.ShowDialog(this);
        }

        private void listSearchFilterMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            listSearch.BackColor = SystemColors.Window;
            if (listSearch.Text != " Filter ")
            {
                FilterAssetList();
            }
        }

        private void listSearchHistory_SelectedIndexChanged(object sender, EventArgs e)
        {
            listSearch.Text = listSearchHistory.Text;
            listSearch.Focus();
            listSearch.SelectionStart = listSearch.Text.Length;
        }

        private void selectRelatedAsset(object sender, EventArgs e)
        {
            var selectedItem = (ToolStripMenuItem)sender;
            var index = int.Parse(selectedItem.Name.Split('_')[0]);

            assetListView.SelectedIndices.Clear();
            tabControl1.SelectedTab = tabPage2;
            var assetItem = assetListView.Items[index];
            assetItem.Selected = true;
            assetItem.EnsureVisible();
        }

        private void selectAllRelatedAssets(object sender, EventArgs e)
        {
          
        }

        private void showRelatedAssetsToolStripMenuItem_Click(object sender, EventArgs e)
        {
           
        }

        private void contextMenuStrip2_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
          
        }

        private void showConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var showConsole = showConsoleToolStripMenuItem.Checked;
            if (showConsole)
                ConsoleWindow.ShowConsoleWindow();
            else
                ConsoleWindow.HideConsoleWindow();

            Properties.Settings.Default.showConsole = showConsole;
            Properties.Settings.Default.Save();
        }

        private void writeLogToFileToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            var useFileLogger = writeLogToFileToolStripMenuItem.Checked;
            logger.UseFileLogger = useFileLogger;

            Properties.Settings.Default.useFileLogger = useFileLogger;
            Properties.Settings.Default.Save();
        }

        private void AssetStudioGUIForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Logger.Verbose("Closing AssetStudio");
        }

        private void buildTreeStructureToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.buildTreeStructure = buildTreeStructureToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }

        private void exportAllL2D_Click(object sender, EventArgs e)
        {
           
        }

        private void exportSelectedL2D_Click(object sender, EventArgs e)
        {
            ExportSelectedL2DModels(ExportL2DFilter.Selected);
        }

        private void exportSelectedL2DWithClips_Click(object sender, EventArgs e)
        {
            ExportSelectedL2DModels(ExportL2DFilter.SelectedWithClips);
        }

        private void exportSelectedL2DWithFadeMotions_Click(object sender, EventArgs e)
        {
            ExportSelectedL2DModels(ExportL2DFilter.SelectedWithFade);
        }

        private void exportSelectedL2DWithFadeList_Click(object sender, EventArgs e)
        {
            ExportSelectedL2DModels(ExportL2DFilter.SelectedWithFadeList);
        }

        private void ExportSelectedL2DModels(ExportL2DFilter l2dExportMode)
        {
            

        }


        private void customBlockCompressionComboBoxToolStripMenuItem_SelectedIndexChanged(object sender, EventArgs e)
        {
          
        }

        private void customBlockInfoCompressionComboBoxToolStripMenuItem_SelectedIndexChanged(object sender, EventArgs e)
        {
           
        }

        private void useAssetLoadingViaTypetreeToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
           
        }

        private void ApplyColorTheme(out bool isDarkMode)
        {
            isDarkMode = false;
            if (SystemInformation.HighContrast)
                return;

#if NET9_0_OR_GREATER
#pragma warning disable WFO5001 //for evaluation purposes only
            var currentTheme = Properties.Settings.Default.guiColorTheme;
            colorThemeToolStripMenu.Visible = true;
            try
            {
                switch (currentTheme)
                {
                    case GuiColorTheme.System:
                        Application.SetColorMode(SystemColorMode.System);
                        colorThemeAutoToolStripMenuItem.Checked = true;
                        isDarkMode = Application.IsDarkModeEnabled;
                        break;
                    case GuiColorTheme.Light:
                        colorThemeLightToolStripMenuItem.Checked = true;
                        break;
                    case GuiColorTheme.Dark:
                        Application.SetColorMode(SystemColorMode.Dark);
                        colorThemeDarkToolStripMenuItem.Checked = true;
                        isDarkMode = true;
                        break;
                }
            }
            catch (Exception)
            {
                //skip
            }
#pragma warning restore WFO5001
#endif
            if (isDarkMode)
            {
                assetListView.GridLines = false;
            }
            else
            {
                FMODloopButton.UseVisualStyleBackColor = true;
            }
        }

        private void colorThemeAutoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!colorThemeAutoToolStripMenuItem.Checked)
            {
                colorThemeAutoToolStripMenuItem.Checked = true;
                colorThemeLightToolStripMenuItem.Checked = false;
                colorThemeDarkToolStripMenuItem.Checked = false;
                Properties.Settings.Default.guiColorTheme = GuiColorTheme.System;
                Properties.Settings.Default.Save();
                ShowThemeChangingMsg();
            }
        }

        private void colorThemeLightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!colorThemeLightToolStripMenuItem.Checked)
            {
                colorThemeAutoToolStripMenuItem.Checked = false;
                colorThemeLightToolStripMenuItem.Checked = true;
                colorThemeDarkToolStripMenuItem.Checked = false;
                Properties.Settings.Default.guiColorTheme = GuiColorTheme.Light;
                Properties.Settings.Default.Save();
                ShowThemeChangingMsg();
            }
        }

        private void colorThemeDarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!colorThemeDarkToolStripMenuItem.Checked)
            {
                colorThemeAutoToolStripMenuItem.Checked = false;
                colorThemeLightToolStripMenuItem.Checked = false;
                colorThemeDarkToolStripMenuItem.Checked = true;
                Properties.Settings.Default.guiColorTheme = GuiColorTheme.Dark;
                Properties.Settings.Default.Save();
                ShowThemeChangingMsg();
            }
        }

        private static void ShowThemeChangingMsg()
        {
            var msg = "Color theme will be changed after restarting the application.\n\n" +
                      "Dark theme support for WinForms is not yet fully implemented and is for evaluation purposes only.\n" +
                      "Better Dark theme support should be added in future .NET versions.";
            MessageBox.Show(msg, "Info", MessageBoxButtons.OK);
        }

        private void DumpTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dumpTreeView.SelectedNode = e.Node;
                tempClipboard = string.IsNullOrEmpty((string)e.Node.Tag)
                    ? e.Node.Text
                    : $"{e.Node.Name}: {e.Node.Tag}";
                dumpTreeViewContextMenuStrip.Show(dumpTreeView, e.Location.X, e.Location.Y);
            }
        }

        private void copyToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(tempClipboard);
        }

        private void expandAllToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            dumpTreeView.BeginUpdate();
            foreach (TreeNode node in dumpTreeView.Nodes)
            {
                node.ExpandAll();
            }
            dumpTreeView.EndUpdate();
        }

        private void collapseAllToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            dumpTreeView.BeginUpdate();
            foreach (TreeNode node in dumpTreeView.Nodes)
            {
                node.Collapse(ignoreChildren: false);
            }
            dumpTreeView.EndUpdate();
        }

        private void useDumpTreeViewToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            var isTreeViewEnabled = useDumpTreeViewToolStripMenuItem.Checked;
            dumpTreeView.Visible = isTreeViewEnabled;
            Properties.Settings.Default.useDumpTreeView = isTreeViewEnabled;
            Properties.Settings.Default.Save();
            if (tabControl2.SelectedIndex == 1)
            {
                DumpAsset(lastSelectedItem);
            }
        }

        private void autoPlayAudioAssetsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.autoplayAudio = autoPlayAudioAssetsToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }

        #region GLControl
        private void InitOpenTK()
        {
            ChangeGLSize(glControl1.Size);
            GL.ClearColor(System.Drawing.Color.CadetBlue);
            pgmID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmID, out int vsID);
            LoadShader("fs", ShaderType.FragmentShader, pgmID, out int fsID);
            GL.LinkProgram(pgmID);

            pgmColorID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmColorID, out vsID);
            LoadShader("fsColor", ShaderType.FragmentShader, pgmColorID, out fsID);
            GL.LinkProgram(pgmColorID);

            pgmBlackID = GL.CreateProgram();
            LoadShader("vs", ShaderType.VertexShader, pgmBlackID, out vsID);
            LoadShader("fsBlack", ShaderType.FragmentShader, pgmBlackID, out fsID);
            GL.LinkProgram(pgmBlackID);

            attributeVertexPosition = GL.GetAttribLocation(pgmID, "vertexPosition");
            attributeNormalDirection = GL.GetAttribLocation(pgmID, "normalDirection");
            attributeVertexColor = GL.GetAttribLocation(pgmColorID, "vertexColor");
            uniformModelMatrix = GL.GetUniformLocation(pgmID, "modelMatrix");
            uniformViewMatrix = GL.GetUniformLocation(pgmID, "viewMatrix");
            uniformProjMatrix = GL.GetUniformLocation(pgmID, "projMatrix");
        }

        private static void LoadShader(string filename, ShaderType type, int program, out int address)
        {
            address = GL.CreateShader(type);
            var str = (string)Properties.Resources.ResourceManager.GetObject(filename);
            GL.ShaderSource(address, str);
            GL.CompileShader(address);
            GL.AttachShader(program, address);
            GL.DeleteShader(address);
        }

        private static void CreateVBO(out int vboAddress, Vector3[] data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboAddress);
            GL.BufferData(BufferTarget.ArrayBuffer,
                                    (IntPtr)(data.Length * Vector3.SizeInBytes),
                                    data,
                                    BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(address, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(address);
        }

        private static void CreateVBO(out int vboAddress, Vector4[] data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboAddress);
            GL.BufferData(BufferTarget.ArrayBuffer,
                                    (IntPtr)(data.Length * Vector4.SizeInBytes),
                                    data,
                                    BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(address, 4, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(address);
        }

        private static void CreateVBO(out int vboAddress, Matrix4 data, int address)
        {
            GL.GenBuffers(1, out vboAddress);
            GL.UniformMatrix4(address, false, ref data);
        }

        private static void CreateEBO(out int address, int[] data)
        {
            GL.GenBuffers(1, out address);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, address);
            GL.BufferData(BufferTarget.ElementArrayBuffer,
                            (IntPtr)(data.Length * sizeof(int)),
                            data,
                            BufferUsageHint.StaticDraw);
        }

        private void CreateVAO()
        {
            GL.DeleteVertexArray(vao);
            GL.GenVertexArrays(1, out vao);
            GL.BindVertexArray(vao);
            CreateVBO(out var vboPositions, vertexData, attributeVertexPosition);
            if (normalMode == 1)
            {
                CreateVBO(out var vboNormals, normal2Data, attributeNormalDirection);
            }
            else
            {
                if (normalData != null)
                    CreateVBO(out var vboNormals, normalData, attributeNormalDirection);
            }
            CreateVBO(out var vboColors, colorData, attributeVertexColor);
            CreateVBO(out var vboModelMatrix, modelMatrixData, uniformModelMatrix);
            CreateVBO(out var vboViewMatrix, viewMatrixData, uniformViewMatrix);
            CreateVBO(out var vboProjMatrix, projMatrixData, uniformProjMatrix);
            CreateEBO(out var eboElements, indiceData);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
        }

        private void ChangeGLSize(Size size)
        {
            GL.Viewport(0, 0, size.Width, size.Height);

            if (size.Width <= size.Height)
            {
                float k = 1.0f * size.Width / size.Height;
                projMatrixData = Matrix4.CreateScale(1, k, 1);
            }
            else
            {
                float k = 1.0f * size.Height / size.Width;
                projMatrixData = Matrix4.CreateScale(k, 1, 1);
            }
        }

        private void glControl1_Load(object sender, EventArgs e)
        {
            InitOpenTK();
            glControlLoaded = true;
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.BindVertexArray(vao);
            if (wireFrameMode == 0 || wireFrameMode == 2)
            {
                GL.UseProgram(shadeMode == 0 ? pgmID : pgmColorID);
                GL.UniformMatrix4(uniformModelMatrix, false, ref modelMatrixData);
                GL.UniformMatrix4(uniformViewMatrix, false, ref viewMatrixData);
                GL.UniformMatrix4(uniformProjMatrix, false, ref projMatrixData);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.DrawElements(BeginMode.Triangles, indiceData.Length, DrawElementsType.UnsignedInt, 0);
            }
            //Wireframe
            if (wireFrameMode == 1 || wireFrameMode == 2)
            {
                GL.Enable(EnableCap.PolygonOffsetLine);
                GL.PolygonOffset(-1, -1);
                GL.UseProgram(pgmBlackID);
                GL.UniformMatrix4(uniformModelMatrix, false, ref modelMatrixData);
                GL.UniformMatrix4(uniformViewMatrix, false, ref viewMatrixData);
                GL.UniformMatrix4(uniformProjMatrix, false, ref projMatrixData);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(BeginMode.Triangles, indiceData.Length, DrawElementsType.UnsignedInt, 0);
                GL.Disable(EnableCap.PolygonOffsetLine);
            }
            GL.BindVertexArray(0);
            GL.Flush();
            glControl1.SwapBuffers();
        }

        private void glControl1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (glControl1.Visible)
            {
                viewMatrixData *= Matrix4.CreateScale(1 + e.Delta / 1000f);
                glControl1.Invalidate();
            }
        }

        private void glControl1_MouseDown(object sender, MouseEventArgs e)
        {
            mdx = e.X;
            mdy = e.Y;
            if (e.Button == MouseButtons.Left)
            {
                lmdown = true;
            }
            if (e.Button == MouseButtons.Right)
            {
                rmdown = true;
            }
        }

        private void glControl1_MouseMove(object sender, MouseEventArgs e)
        {
            if (lmdown || rmdown)
            {
                float dx = mdx - e.X;
                float dy = mdy - e.Y;
                mdx = e.X;
                mdy = e.Y;
                if (lmdown)
                {
                    dx *= 0.01f;
                    dy *= 0.01f;
                    viewMatrixData *= Matrix4.CreateRotationX(dy);
                    viewMatrixData *= Matrix4.CreateRotationY(dx);
                }
                if (rmdown)
                {
                    dx *= 0.003f;
                    dy *= 0.003f;
                    viewMatrixData *= Matrix4.CreateTranslation(-dx, dy, 0);
                }
                glControl1.Invalidate();
            }
        }

        private void glControl1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                lmdown = false;
            }
            if (e.Button == MouseButtons.Right)
            {
                rmdown = false;
            }
        }
        #endregion
    }
}
