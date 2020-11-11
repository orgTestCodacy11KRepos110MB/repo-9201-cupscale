using System;
using System.Drawing;
using System.Windows.Forms;
using Cupscale.UI;
using Cyotek.Windows.Forms;
using System.Drawing.Drawing2D;
using Cupscale.Forms;
using Cupscale.Main;
using System.Threading.Tasks;
using System.IO;
using Cupscale.IO;
using Cupscale.Cupscale;
using Cupscale.ImageUtils;
using System.Diagnostics;
using Cupscale.OS;
using Microsoft.WindowsAPICodePack.Dialogs;
using static Cupscale.UI.PreviewUI;
using System.Linq;
using System.Security.RightsManagement;

namespace Cupscale.Main
{
	public partial class MainForm : Form
	{
		public bool resetImageOnMove = false;
		public PreviewState resetState;
		public bool initialized = false;

		public MainForm()
		{
			CheckForIllegalCrossThreadCalls = false;
			InitializeComponent();
			PreviewUI.Init(previewImg, model1TreeBtn, model2TreeBtn, imageOutputFormat, prevOverwriteCombox);
			BatchUpscaleUI.Init(batchOutDir, batchFileList, batchDirLabel);
			VideoUpscaleUI.Init(videoOutDir, videoLogBox, videoPathLabel, videoOutputFormat);
			Program.mainForm = this;
			WindowState = FormWindowState.Maximized;
		}

		private async void MainForm_Load(object sender, EventArgs e)
		{
			// Left Panel
            UIHelpers.InitCombox(prevClipboardTypeCombox, 0);
			UIHelpers.InitCombox(preResizeScale, 1);
			UIHelpers.InitCombox(preResizeMode, 0);
			UIHelpers.FillEnumComboBox(preResizeFilter, typeof(Upscale.Filter), 0);
			// Right Panel
			UIHelpers.InitCombox(prevOverwriteCombox, 0);
			UIHelpers.InitCombox(imageOutputFormat, 0);
			UIHelpers.FillEnumComboBox(imageOutputFormat, typeof(Upscale.ImgExportMode));
			UIHelpers.FillEnumComboBox(videoOutputFormat, typeof(Upscale.VidExportMode));
			UIHelpers.InitCombox(postResizeScale, 1);
			UIHelpers.InitCombox(postResizeMode, 0);
			UIHelpers.FillEnumComboBox(postResizeFilter, typeof(Upscale.Filter), 0);
			// Batch Upscale
			UIHelpers.InitCombox(batchOutMode, 0);
			UIHelpers.InitCombox(preprocessMode, 0);
			await CheckInstallation();
			EmbeddedPython.Init();

			EsrganData.CheckModelDir();
			EsrganData.ReloadModelList();

			NvApi.Init();

			if (OSUtils.IsUserAdministrator())
				Program.ShowMessage("Cupscale is running as administrator.\nThis will break Drag-n-Drop functionality.", "Warning");

			LoadEsrganOptions();

			flowPanelLeft.AutoScroll = false;
			flowPanelLeft.HorizontalScroll.Maximum = 0;
			flowPanelLeft.VerticalScroll.Visible = false;
			flowPanelLeft.AutoScroll = true;

			flowPanelRight.AutoScroll = false;
			flowPanelRight.HorizontalScroll.Maximum = 0;
			flowPanelRight.VerticalScroll.Visible = false;
			flowPanelRight.AutoScroll = true;

			initialized = true;
		}

		public async Task CheckInstallation ()
        {
			await Installer.Init();
			Enabled = true;
		}

		float lastProg;
		string lastStatus = "";
		public void SetProgress(float prog, string statusText = "")
		{
			if(prog >= 0 && lastProg != prog)
            {
				int percent = (int)Math.Round(prog);
				if (percent > 100) percent = 100;
				htProgBar.Value = percent;
				htProgBar.Refresh();
				lastProg = prog;
			}
			if (!string.IsNullOrWhiteSpace(statusText) && lastStatus != statusText)
            {
				statusLabel.Text = statusText;
				if (Logger.doLogStatus) Logger.Log("Status changed: " + statusText);
				lastStatus = statusText;
			}
		}

		public void SetVramLabel (string text, Color color)
        {
			vramLabel.Text = text;
			vramLabel.ForeColor = color;
		}

		public void SetBusy (bool state)
        {
			Program.busy = state;
			upscaleBtn.Enabled = !state;
			refreshPreviewCutoutBtn.Enabled = !state;
			refreshPreviewFullBtn.Enabled = !state;
		}

		private void refreshModelsBtn_Click(object sender, EventArgs e)
		{
			EsrganData.ReloadModelList();
		}

		private void prevToggleFilterBtn_Click(object sender, EventArgs e)
		{
			if (previewImg.InterpolationMode != InterpolationMode.NearestNeighbor)
			{
				previewImg.InterpolationMode = InterpolationMode.NearestNeighbor;
                prevToggleFilterBtn.Text = "Switch To Bicubic Filtering";
                Program.currentFilter = ImageMagick.FilterType.Point;
            }
			else
			{
				previewImg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                prevToggleFilterBtn.Text = "Switch To Point Filtering";
                Program.currentFilter = ImageMagick.FilterType.Catrom;
            }
		}

		private void previewImg_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
			else e.Effect = DragDropEffects.None;
		}

		private void previewImg_DragDrop(object sender, DragEventArgs e)
		{
			string[] array = e.Data.GetData(DataFormats.FileDrop) as string[];
			LoadImages(array);
		}

		private void previewImg_MouseDown(object sender, MouseEventArgs e)
		{
			PreviewMerger.ShowOriginal();
		}

		private void previewImg_MouseUp(object sender, MouseEventArgs e)
		{
			PreviewMerger.ShowOutput();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		int lastZoom;
        private void previewImg_Zoomed(object sender, ImageBoxZoomEventArgs e)
        {
			if (previewImg.Image == null)
				return;
			if (previewImg.Zoom < 25) previewImg.Zoom = 25;
			if(previewImg.Zoom != lastZoom)
            {
				if (resetImageOnMove)
					ResetToLastState();
				lastZoom = previewImg.Zoom;
			}
			UpdatePreviewInfo();
        }

        void UpdatePreviewInfo ()
        {
            PreviewUI.UpdatePreviewLabels(prevZoomLabel, prevSizeLabel, prevCutoutLabel);
        }

        private void settingsBtn_Click(object sender, EventArgs e)
        {
			new SettingsForm().ShowDialog();
        }

        private void singleModelRbtn_CheckedChanged(object sender, EventArgs e)
        {
			UpdateModelMode();
		}

        private void interpRbtn_CheckedChanged(object sender, EventArgs e)
        {
			UpdateModelMode();
		}

        private void chainRbtn_CheckedChanged(object sender, EventArgs e)
        {
			UpdateModelMode();
		}

		public void UpdateModelMode()
		{
			model1TreeBtn.Enabled = !advancedBtn.Checked;
			model2TreeBtn.Enabled = (interpRbtn.Checked || chainRbtn.Checked);
			interpConfigureBtn.Visible = interpRbtn.Checked;
			advancedConfigureBtn.Visible = advancedBtn.Checked;
			if (singleModelRbtn.Checked) PreviewUI.currentMode = PreviewUI.Mode.Single;
			if (interpRbtn.Checked) PreviewUI.currentMode = PreviewUI.Mode.Interp;
			if (chainRbtn.Checked) PreviewUI.currentMode = PreviewUI.Mode.Chain;
			if (advancedBtn.Checked) PreviewUI.currentMode = PreviewUI.Mode.Advanced;
		}

        private void interpConfigureBtn_Click(object sender, EventArgs e)
        {
			
			if (!PreviewUI.HasValidModelSelection())
            {
				Program.ShowMessage("Please select two models for interpolation.", "Message");
				return;
            }

			AdvancedModelsForm interpForm = new AdvancedModelsForm(model1TreeBtn.Text.Trim(), model2TreeBtn.Text.Trim());
			interpForm.ShowDialog();
		}

        private void batchTab_DragEnter(object sender, DragEventArgs e)
        {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
			else e.Effect = DragDropEffects.None;
		}

        private void batchTab_DragDrop(object sender, DragEventArgs e)
        {
			string[] array = e.Data.GetData(DataFormats.FileDrop) as string[];
			LoadImages(array);
		}

		async Task LoadImages (string [] files)
        {
			Logger.Log("[MainUI] Dropped " + files.Length + " file(s), files[0] = " + files[0]);
			IOUtils.ClearDir(Paths.tempImgPath.GetParentDir());
			string path = files[0];
			if (IOUtils.IsPathDirectory(path))
			{
				htTabControl.SelectedIndex = 1;
				BatchUpscaleUI.LoadDir(path);
				return;
			}
			if(files.Length > 1)
            {
				htTabControl.SelectedIndex = 1;
				BatchUpscaleUI.LoadImages(files);
				return;
			}
			upscaleBtn.Text = "Upscale And Save";
			htTabControl.SelectedIndex = 0;
			previewImg.Text = "";
			SetProgress(0f, "Loading image...");
			PreviewUI.ResetCachedImages();
			if (!PreviewUI.DroppedImageIsValid(path))
            {
				SetProgress(0f, "Ready.");
				await Task.Delay(1);
				Program.CloseTempForms();
				return;
			}
			Program.lastImgPath = path;
			ReloadImage(false);
			if (failed) { FailReset(); return; }
			SetHasPreview(false);
			ImageLoadedChanged(true);
			SetProgress(0f, "Ready.");
		}

		public bool failed = false;
		public void FailReset ()
        {
			SetProgress(0f, "Reset after error.");
			Program.CloseTempForms();
			Program.lastImgPath = null;
			failed = false;
        }

		public void SetButtonText (string s)
        {
			if (!string.IsNullOrWhiteSpace(s))
				upscaleBtn.Text = s;
        }

		public void ImageLoadedChanged (bool state = true)
        {
			refreshPreviewCutoutBtn.Enabled = state;
			refreshPreviewFullBtn.Enabled = state;
			reloadImgBtn.Enabled = state;
			openSourceFolderBtn.Enabled = state;
		}

		public async void ReloadImage (bool allowFail = true)	// Returns false on error
        {
			string path = Program.lastImgPath;
			DialogForm loadingBox = new DialogForm($"Loading {Path.GetFileName(path)}", 20);
			await Task.Delay(10);
			try
			{
				File.Copy(path, Paths.tempImgPath, true);
				bool fillAlpha = !bool.Parse(Config.Get("alpha"));
				await ImageProcessing.ConvertImage(path, ImageProcessing.Format.PngRaw, fillAlpha, ImageProcessing.ExtMode.UseNew, false, Paths.tempImgPath, true);
				previewImg.Image = ImgUtils.GetImage(Paths.tempImgPath);
				PreviewUI.currentScale = 1;
				previewImg.ZoomToFit();
				lastZoom = previewImg.Zoom;
			}
			catch (Exception e)
			{
				Logger.Log("Failed to reload image from source path - Maybe image is from clipboard or has been deleted. " + e.Message);
                if (!allowFail)
                {
					Logger.ErrorMessage("Failed to load image:", e);
					failed = true;
				}
			}
			if(loadingBox != null)
				loadingBox.Close();
		}

        private async void upscaleBtn_Click(object sender, EventArgs e)
        {
			if (Program.busy)
				return;

            if (!PreviewUI.HasValidModelSelection())
            {
				Program.ShowMessage("Invalid model selection.\nMake sure you have selected a model and that the file still exists.", "Error");
				return;
            }

			bool useNcnn = (Config.Get("cudaFallback").GetInt() == 2 || Config.Get("cudaFallback").GetInt() == 3);
			if (useNcnn && !Program.mainForm.HasValidNcnnModelSelection())
			{
				Program.ShowMessage("Invalid model selection - NCNN does not support interpolation or chaining.", "Error");
				return;
			}

			if (Config.GetBool("reloadImageBeforeUpscale"))
				ReloadImage();
			UpdateResizeMode();
			if (htTabControl.SelectedIndex == 0) await PreviewUI.UpscaleImage();
			if (htTabControl.SelectedIndex == 1) await BatchUpscaleUI.Run(preprocessMode.SelectedIndex == 0);
			if (htTabControl.SelectedIndex == 2) await VideoUpscaleUI.Run();
		}

		public void UpdateResizeMode ()
        {
			ImageProcessing.postFilter = (Upscale.Filter)Enum.Parse(typeof(Upscale.Filter), postResizeFilter.Text.RemoveSpaces());
			ImageProcessing.postScaleMode = (Upscale.ScaleMode)Enum.Parse(typeof(Upscale.ScaleMode), postResizeMode.Text.RemoveSpaces());
			ImageProcessing.postScaleValue = postResizeScale.GetInt();
			ImageProcessing.postOnlyDownscale = postResizeOnlyDownscale.Checked;

			ImageProcessing.preFilter = (Upscale.Filter)Enum.Parse(typeof(Upscale.Filter), preResizeFilter.Text.RemoveSpaces());
			ImageProcessing.preScaleMode = (Upscale.ScaleMode)Enum.Parse(typeof(Upscale.ScaleMode), preResizeMode.Text.RemoveSpaces());
			ImageProcessing.preScaleValue = preResizeScale.GetInt();
			ImageProcessing.preOnlyDownscale = preResizeOnlyDownscale.Checked;
		}

		public bool HasValidNcnnModelSelection ()
        {
			return singleModelRbtn.Checked;
		}

        private async void refreshPreviewFullBtn_Click(object sender, EventArgs e)
        {
			if (Config.GetBool("reloadImageBeforeUpscale"))
				ReloadImage();
			UpdateResizeMode();
			PreviewUI.UpscalePreview(true);
		}

        private void refreshPreviewCutoutBtn_Click(object sender, EventArgs e)
        {
			UpdateResizeMode();
			PreviewUI.UpscalePreview();
		}

        private void copyCompToClipboardBtn_Click(object sender, EventArgs e)
        {
			if (prevClipboardTypeCombox.SelectedIndex == 0) ClipboardComparison.CopyToClipboardSideBySide(false);
			if (prevClipboardTypeCombox.SelectedIndex == 1) ClipboardComparison.CopyToClipboardSlider(false);
			if (prevClipboardTypeCombox.SelectedIndex == 2) ClipboardComparison.BeforeAfterAnim(false, false);
			if (prevClipboardTypeCombox.SelectedIndex == 3) ClipboardComparison.BeforeAfterAnim(false, true);
			if (prevClipboardTypeCombox.SelectedIndex == 4) ClipboardComparison.OnlyResult(false);
		}

        private void model1TreeBtn_Click(object sender, EventArgs e)
        {
			new ModelSelectForm(model1TreeBtn, 1).ShowDialog();
        }

        private void model2TreeBtn_Click(object sender, EventArgs e)
        {
			new ModelSelectForm(model2TreeBtn, 2).ShowDialog();
		}

        private void savePreviewToFileBtn_Click(object sender, EventArgs e)
        {
			if (prevClipboardTypeCombox.SelectedIndex == 0) ClipboardComparison.CopyToClipboardSideBySide(true);
			if (prevClipboardTypeCombox.SelectedIndex == 1) ClipboardComparison.CopyToClipboardSlider(true);
			if (prevClipboardTypeCombox.SelectedIndex == 2) ClipboardComparison.BeforeAfterAnim(true, false);
			if (prevClipboardTypeCombox.SelectedIndex == 3) ClipboardComparison.BeforeAfterAnim(true, true);
			if (prevClipboardTypeCombox.SelectedIndex == 4) ClipboardComparison.OnlyResult(true);
		}

		public void ResetToLastState ()
        {
			previewImg.Image = resetState.image;
			previewImg.Zoom = resetState.zoom;
			previewImg.AutoScrollPosition = resetState.autoScrollPosition;		// This doesn't work correctly :/
			PreviewUI.ResetCachedImages();
			resetImageOnMove = false;
		}

        private void prevOverwriteCombox_SelectedIndexChanged(object sender, EventArgs e)
        {
			if (prevOverwriteCombox.SelectedIndex == 0)
				Upscale.overwriteMode = Upscale.Overwrite.No;
			if (prevOverwriteCombox.SelectedIndex == 1)
				Upscale.overwriteMode = Upscale.Overwrite.Yes;
		}

        private void postResizeMode_SelectedIndexChanged(object sender, EventArgs e)
        {
			postResizeOnlyDownscale.Enabled = postResizeMode.SelectedIndex != 0;
        }

        private void openOutFolderBtn_Click(object sender, EventArgs e)
        {
			PreviewUI.OpenLastOutputFolder();
        }

		public void AfterFirstUpscale ()
        {
			openOutFolderBtn.Enabled = true;
		}

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Program.Cleanup();
		}

		public void SetHasPreview (bool state)
        {
			copyCompToClipboardBtn.Enabled = state;
			savePreviewToFileBtn.Enabled = state;
			saveMergedPreviewBtn.Enabled = state;
		}

        private async void saveMergedPreviewBtn_Click(object sender, EventArgs e)
        {
			DialogForm loadingForm = new DialogForm("Post-Processing And Saving...");
			await Task.Delay(50);
			Upscale.currentMode = Upscale.UpscaleMode.Single;
			string ext = Path.GetExtension(Program.lastImgPath);
			string outPath = Path.ChangeExtension(Program.lastImgPath, null) + "[temp]" + ext + ".tmp";
			previewImg.Image.Save(outPath);
			await Upscale.PostprocessingSingle(outPath, true);
			string outFilename = Upscale.FilenamePostprocess(PreviewUI.lastOutfile);
			string finalPath = IOUtils.ReplaceInFilename(outFilename, "[temp]", "");
			loadingForm.Close();
			Program.ShowMessage("Saved to " + finalPath + ".", "Message");
		}

        private void batchOutMode_SelectedIndexChanged(object sender, EventArgs e)
        {
			if (batchOutMode.SelectedIndex == 0)
				PostProcessingQueue.copyMode = PostProcessingQueue.CopyMode.KeepStructure;
			if (batchOutMode.SelectedIndex == 1)
				PostProcessingQueue.copyMode = PostProcessingQueue.CopyMode.CopyToRoot;
		}

        private void advancedBtn_CheckedChanged(object sender, EventArgs e)
        {
			UpdateModelMode();
		}

        private void advancedConfigureBtn_Click(object sender, EventArgs e)
        {
			new AdvancedModelForm();
        }

        private async void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
			if (e.KeyData == (Keys.Control | Keys.V))
			{
				if (htTabControl.SelectedIndex != 0 || !Enabled)
					return;
				try
				{
					Image clipboardImg = Clipboard.GetImage();
					string savePath = Path.Combine(Paths.clipboardFolderPath, "Clipboard.png");
					clipboardImg.Save(savePath);
					await LoadImages(new string[] { savePath });
				}
				catch
				{
					Program.ShowMessage("Failed to paste image from clipboard. Make sure you have a raw image (not a file) copied.", "Error");
				}
			}
		}

        private void comparisonToolBtn_Click(object sender, EventArgs e)
        {
			new ModelComparisonForm().ShowDialog();
        }

        private void openModelFolderBtn_Click(object sender, EventArgs e)
        {
			Process.Start("explorer.exe", Config.Get("modelPath"));
		}

        private void selectOutPathBtn_Click(object sender, EventArgs e)
        {
			CommonOpenFileDialog folderDialog = new CommonOpenFileDialog();
			if(Directory.Exists(batchOutDir.Text.Trim()))
				folderDialog.InitialDirectory = batchOutDir.Text;
			folderDialog.IsFolderPicker = true;
			if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
				batchOutDir.Text = folderDialog.FileName;
		}

        private void paypalBtn_Click(object sender, EventArgs e)
        {
			Process.Start("https://www.paypal.com/paypalme/nmkd/10");
		}

        private void htButton1_Click(object sender, EventArgs e)
        {
			new DependencyCheckerForm().ShowDialog();
        }

        private async void offlineInterpBtn_Click(object sender, EventArgs e)
        {
			if (PreviewUI.currentMode == Mode.Interp)
			{
                try
                {
					string mdl1 = Program.currentModel1;
					string mdl2 = Program.currentModel2;
					if (string.IsNullOrWhiteSpace(mdl1) || string.IsNullOrWhiteSpace(mdl2))
						return;
					ModelData mdl = new ModelData(mdl1, mdl2, ModelData.ModelMode.Interp, interpValue);
					DialogForm loadingForm = new DialogForm("Interpolating...");
					await Task.Delay(50);
					string outPath = ESRGAN.Interpolate(mdl);
					loadingForm.Close();
					Program.ShowMessage("Saved interpolated model to:\n\n" + outPath);
				}
				catch (Exception interpException)
                {
					Logger.ErrorMessage("Error trying to create an interpolated model:", interpException);
					Program.CloseTempForms();
                }
			}
            else
            {
				Program.ShowMessage("Please select \"Interpolate Between Two Models\" and select two models.");
            }
        }

        private void previewImg_Click(object sender, EventArgs e)
        {
			MouseEventArgs me = (MouseEventArgs)e;
			if (me.Button == MouseButtons.Right)
            {
				CommonOpenFileDialog fileDialog = new CommonOpenFileDialog();
				fileDialog.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
				fileDialog.IsFolderPicker = false;
				fileDialog.Multiselect = true;
				if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
					LoadImages(fileDialog.FileNames.ToArray());
			}
        }

		public void SaveEsrganOptions ()
        {
			if (!initialized) return;
			Config.SaveGuiElement(tilesize);
			Config.SaveGuiElement(alpha);
			Config.SaveComboxIndex(seamlessMode);
		}

		public void LoadEsrganOptions ()
        {
			Config.LoadGuiElement(tilesize);
			Config.LoadGuiElement(alpha);
			Config.LoadComboxIndex(seamlessMode);
		}

        private void tilesize_SelectedIndexChanged(object sender, EventArgs e)
        {
			SaveEsrganOptions();
        }

        private void alpha_CheckedChanged(object sender, EventArgs e)
        {
			SaveEsrganOptions();
			if(initialized && !string.IsNullOrWhiteSpace(Program.lastImgPath))
				ReloadImage(false);
		}

        private void seamlessMode_SelectedIndexChanged(object sender, EventArgs e)
        {
			SaveEsrganOptions();
		}

        private void reloadImgBtn_Click(object sender, EventArgs e)
        {
			ReloadImage(false);
        }

        private void openSourceFolderBtn_Click(object sender, EventArgs e)
        {
			string dir = Program.lastImgPath.GetParentDir();
			if (Directory.Exists(dir))
				Process.Start("explorer.exe", dir);
			else
				Program.ShowMessage("The source directory does not seem to exist anymore!");
		}

		int lastTabIndex = 0;
        private void htTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Program.busy)
            {
				htTabControl.SelectedIndex = lastTabIndex;
				return;
            }
			lastTabIndex = htTabControl.SelectedIndex;
			videoOutputFormat.Visible = false;
			if (htTabControl.SelectedIndex == 0) PreviewUI.TabSelected();
			if (htTabControl.SelectedIndex == 1) BatchUpscaleUI.TabSelected();
			if (htTabControl.SelectedIndex == 2)
			{
				VideoUpscaleUI.TabSelected();
				videoOutputFormat.Visible = true;
			}
		}

        private void videoTab_DragEnter(object sender, DragEventArgs e)
        {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
			else e.Effect = DragDropEffects.None;
		}

		private void videoTab_DragDrop(object sender, DragEventArgs e)
		{
			string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
			VideoUpscaleUI.LoadFile(files[0]);
        }

        private void videoOutPathBtn_Click(object sender, EventArgs e)
        {
			CommonOpenFileDialog folderDialog = new CommonOpenFileDialog();
			if (Directory.Exists(videoOutDir.Text.Trim()))
				folderDialog.InitialDirectory = videoOutDir.Text;
			folderDialog.IsFolderPicker = true;
			if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
				videoOutDir.Text = folderDialog.FileName;
		}

		bool lastLeftScrollbarState = false;
		bool lastRightScrollbarState = false;
		private void MainForm_Resize(object sender, EventArgs e)
        {
			int scrollbarOffset = SystemInformation.VerticalScrollBarWidth + 2;

			bool leftScrollbar = flowPanelLeft.VerticalScroll.Visible;
			bool rightScrollbar = flowPanelRight.VerticalScroll.Visible;

			Panel[] lPanels = new Panel[] { mdlPanel, esrganPanel, compPanel, prevInfoPanel, prevCtrlPanel, leftSpacer1, leftSpacer2, leftSpacer3, leftSpacer4, };
			Panel[] rPanels = new Panel[] { imgOptsPanel, preResizePanel, postResizePanel, savePanel, upscalePanel, rSpacer1, rSpacer2, rSpacer3, rSpacer4, };

			if (leftScrollbar && !lastLeftScrollbarState)
			{ foreach(Panel p in lPanels) p.Width -= scrollbarOffset; }

			if (!leftScrollbar && lastLeftScrollbarState)
			{ foreach (Panel p in lPanels) p.Width += scrollbarOffset; }

			if (rightScrollbar && !lastRightScrollbarState)
			{ foreach (Panel p in rPanels) p.Width -= scrollbarOffset; }

			if (!rightScrollbar && lastRightScrollbarState)
			{ foreach (Panel p in rPanels) p.Width += scrollbarOffset; }

			lastLeftScrollbarState = leftScrollbar;
			lastRightScrollbarState = rightScrollbar;
		}
    }
}
