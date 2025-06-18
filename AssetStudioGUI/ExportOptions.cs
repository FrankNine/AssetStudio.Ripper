using AssetStudio;
using System;
using System.Linq;
using System.Windows.Forms;

namespace AssetStudioGUI
{
    public partial class ExportOptions : Form
    {
        public ExportOptions()
        {
            InitializeComponent();
            assetGroupOptions.SelectedIndex = Properties.Settings.Default.assetGroupOption;
            filenameFormatComboBox.SelectedIndex = Properties.Settings.Default.filenameFormat;
            restoreExtensionName.Checked = Properties.Settings.Default.restoreExtensionName;
            converttexture.Checked = Properties.Settings.Default.convertTexture;
            exportSpriteWithAlphaMask.Checked = Properties.Settings.Default.exportSpriteWithMask;
            convertAudio.Checked = Properties.Settings.Default.convertAudio;
            openAfterExport.Checked = Properties.Settings.Default.openAfterExport;
            var maxParallelTasks = Environment.ProcessorCount;
            var taskCount = Properties.Settings.Default.parallelExportCount;
            parallelExportUpDown.Maximum = maxParallelTasks;
            parallelExportUpDown.Value = taskCount <= 0 ? maxParallelTasks : Math.Min(taskCount, maxParallelTasks);
            parallelExportMaxLabel.Text += maxParallelTasks;
            parallelExportCheckBox.Checked = Properties.Settings.Default.parallelExport;
           
            l2dAssetSearchByFilenameCheckBox.Checked = Properties.Settings.Default.l2dAssetSearchByFilename;
            l2dForceBezierCheckBox.Checked = Properties.Settings.Default.l2dForceBezier;

            SetFromFbxSettings();
        }

        private void OKbutton_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.assetGroupOption = assetGroupOptions.SelectedIndex;
            Properties.Settings.Default.filenameFormat = filenameFormatComboBox.SelectedIndex;
            Properties.Settings.Default.restoreExtensionName = restoreExtensionName.Checked;
            Properties.Settings.Default.convertTexture = converttexture.Checked;
            Properties.Settings.Default.exportSpriteWithMask = exportSpriteWithAlphaMask.Checked;
            Properties.Settings.Default.convertAudio = convertAudio.Checked;
            var checkedImageType = (RadioButton)panel1.Controls.Cast<Control>().First(x => ((RadioButton)x).Checked);
            Properties.Settings.Default.openAfterExport = openAfterExport.Checked;
            Properties.Settings.Default.parallelExport = parallelExportCheckBox.Checked;
            Properties.Settings.Default.parallelExportCount = (int)parallelExportUpDown.Value;

            Properties.Settings.Default.l2dAssetSearchByFilename = l2dAssetSearchByFilenameCheckBox.Checked;
            var checkedMotionMode = (RadioButton)l2dMotionExportMethodPanel.Controls.Cast<Control>().First(x => ((RadioButton)x).Checked);
            Properties.Settings.Default.l2dForceBezier = l2dForceBezierCheckBox.Checked;

        

            Properties.Settings.Default.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void parallelExportCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            parallelExportUpDown.Enabled = parallelExportCheckBox.Checked;
        }

        private void uvIndicesCheckedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (exportAllUvsAsDiffuseMaps.Checked)
                return;
        }

        private void uvTypesListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedUv = uvIndicesCheckedListBox.SelectedIndex;
        }

        private void exportAllUvsAsDiffuseMaps_CheckedChanged(object sender, EventArgs e)
        {
            uvTypesListBox.Enabled = !exportAllUvsAsDiffuseMaps.Checked;
            uvIndicesCheckedListBox.Enabled = !exportAllUvsAsDiffuseMaps.Checked;
        }

        private void SetFromFbxSettings()
        {
            uvTypesListBox.Enabled = !exportAllUvsAsDiffuseMaps.Checked;
            uvIndicesCheckedListBox.Enabled = !exportAllUvsAsDiffuseMaps.Checked;
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            SetFromFbxSettings();
            uvIndicesCheckedListBox_SelectedIndexChanged(sender, e);
        }
    }
}
