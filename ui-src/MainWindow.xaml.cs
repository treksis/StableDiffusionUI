﻿using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using SD_FXUI.Debug;
using SD_FXUI.Utils;
using SD_FXUI.Utils.Models;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SD_FXUI
{
    public partial class MainWindow : HandyControl.Controls.BlurWindow
    {
        Config Data = null;
        ObservableCollection<ListViewItemsData> ListViewItemsCollections = new ObservableCollection<ListViewItemsData>();
        string currentImage = null;
        ModelCMD SafeCMD = null;

        public class ListViewItemsData
        {
            public string? GridViewColumnName_ImageSource { get; set; }
            public string? GridViewColumnName_LabelContent { get; set; }
        }

        public bool CPUUse = false;
        public MainWindow()
        {
            InitializeComponent();
            
            Install.SetupDirs();

            Log.InitLogFile();

            cbUpscaler.SelectedIndex = 0;
            cbModel.SelectedIndex = 0;

            cbSampler.SelectedIndex = 0;
            cbDevice.SelectedIndex = 0;


            GlobalVariables.Form = this;

            GlobalVariables.UIHost = new HostForm();
            GlobalVariables.UIHost.Hide();
            Host.Print("\n");

            GlobalVariables.GPUID = new GPUInfo();

            // Load App data
            Data = new Config();
            Load();
            ChangeTheme();

            Install.WrapPoserPath();
            Install.WrapHedPath();
            Install.WrapMlsdPath();

            gridImg.Visibility = Visibility.Collapsed;
            brImgPane.Visibility = Visibility.Collapsed;
            btnDDB.Visibility = Visibility.Collapsed;
            GlobalVariables.NoImageData = ViewImg.Source;
            GlobalVariables.SafeMaskFreeImg = imgMask.Source;

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                Notification.ToastBtnClickManager(toastArgs);
            };

            SafeCMD = new ModelCMD();
            cbExtractPoseSelector.SelectedIndex = 4;

            GlobalVariables.MakeInfo.LoRA = new System.Collections.Generic.List<Helper.LoRAData>();
            GlobalVariables.MakeInfo.TI = new System.Collections.Generic.List<Helper.LoRAData>();
            GlobalVariables.MakeInfo.TINeg = new System.Collections.Generic.List<Helper.LoRAData>();

            // FX: A hack to see the entire form in the constructor.
            grMain.Margin = new Thickness(0, 0, 0.0, 0.0);

            FileDownloader.Initial();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (cbModel.Text.Length == 0)
            {
                Notification.MsgBox("Select model!");
                return;
            }

            Directory.CreateDirectory(GlobalVariables.ImgPath);
            btnDDB.Visibility = Visibility.Collapsed;

            if (chRandom.IsChecked.Value)
            {
                var rand = new Random();
                tbSeed.Text = rand.Next().ToString();
            }

            ValidateSize();

            currentImage = null;
            ClearImages();

            if (GlobalVariables.DrawMode == Helper.DrawingMode.Vid2Vid)
            {
                GlobalVariables.DrawMode = Helper.DrawingMode.Img2Img;
                GlobalVariables.LastVideoData.ActiveRender = true;

                VideoFrame.ReadVideo(GlobalVariables.InputImagePath, (int)slFPS.Value);
                Task.Run(() =>
                {
                    RunVideoRender();
                });

            }
            else
            {
                if (!MakeCommandObject())
                {
                    return;
                }

                MakeImage();
            }
        }

        private void Slider_Denoising(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbDenoising != null)
                tbDenoising.Text = slDenoising.Value.ToString();
        }
        private void Slider_FPS(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbFPS != null)
                tbFPS.Text = slFPS.Value.ToString();
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbSteps != null)
                tbSteps.Text = slSteps.Value.ToString();
        }

        private void tbSteps2_TextChanged(object sender, TextChangedEventArgs e)
        {
            double Val = 0;
            double.TryParse(tbCFG.Text, out Val);
            slCFG.Value = Val;
        }
        
        private void tbFPS_TextChanged(object sender, TextChangedEventArgs e)
        {
            int Val = 0;
            int.TryParse(tbFPS.Text, out Val);
            slFPS.Value = Val;
        }

        private void Slider2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbCFG != null)
                tbCFG.Text = slCFG.Value.ToString();
        }

        private void slUpscale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lbUpscale != null)
                lbUpscale.Content = "x" + (slUpscale.Value + 1).ToString();
        }
        private void slDenoise_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lbDenoise != null)
                lbDenoise.Content = "x" + (slDenoise.Value).ToString();

            GlobalVariables.Denoise = (int)slDenoise.Value;
        }

        private void tbSteps_TextChanged(object sender, TextChangedEventArgs e)
        {
            double Val = 0;
            double.TryParse(tbSteps.Text, out Val);
            slSteps.Value = Val;
        }

        private void btFolder_ValueChanged(object sender, MouseButtonEventArgs e)
        {
            string argument = "/select, \"" + GlobalVariables.ImgPath + "\"";
            Host Explorer = new Host("", "explorer.exe");
            Explorer.Start(argument);
        }
        private void btCmd_ValueChanged(object sender, MouseButtonEventArgs e)
        {
            GlobalVariables.UIHost.Hide();
            GlobalVariables.UIHost.Show();
        }

        private void OnClose(object sender, EventArgs e)
        {
            GlobalVariables.UIHost.Close();
            Save();
        }

        private void Button_ClickBreak(object sender, RoutedEventArgs e)
        {
            SafeCMD.Exit(true);

            string WorkingPath = FS.GetWorkingDir() + "/repo/";
            if (GlobalVariables.Mode == Helper.ImplementMode.DiffCUDA)
            {
                WorkingPath += "cuda.venv";
            }
            else
            {
                WorkingPath += "onnx.venv";
            }

            if (!Directory.Exists(WorkingPath))
            {
                if (GlobalVariables.Mode == Helper.ImplementMode.DiffCUDA)
                    Install.CheckAndInstallCUDA();
                else
                    Install.CheckAndInstallONNX();

                return;
            }

            foreach (Host Proc in GlobalVariables.SecondaryProcessList)
            {
                Proc.Kill();
            }

            Host.Print("\n All task aborted (」°ロ°)」");
            GlobalVariables.SecondaryProcessList.Clear();
            InvokeProgressUpdate(0);
        }

        private void Button_Click_Import_Model(object sender, RoutedEventArgs e)
        {
            Utils.SharkModelImporter Importer = new Utils.SharkModelImporter();
            Importer.Show();
        }

        private void chRandom_Checked(object sender, RoutedEventArgs e)
        {
            if (tbSeed != null)
                tbSeed.IsEnabled = false;
        }
        private void chRandom_Unchecked(object sender, RoutedEventArgs e)
        {
            tbSeed.IsEnabled = true;
        }

        private void btnONNX_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalVariables.Mode != Helper.ImplementMode.ONNX)
            {
                tsCN.IsChecked = false;

                GlobalVariables.Mode = Helper.ImplementMode.ONNX;
                Install.CheckAndInstallONNX();

                Brush Safe = new SolidColorBrush(Colors.Black);

                btnDiffCuda.Background = Safe;
                btnONNX.Background = new LinearGradientBrush(Colors.DarkOrchid, Colors.MediumOrchid, 0.5);
                btnDiffCpu.Background = Safe;

                UpdateModelsList();

                cbDevice.Items.Clear();

                foreach (var item in GlobalVariables.GPUID.GPUs)
                {
                    cbDevice.Items.Add(item);
                }

                cbFf16.Visibility = Visibility.Hidden;

                grDevice.Visibility = Visibility.Visible;
                brDevice.Visibility = Visibility.Visible;

                foreach (string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler", "UniPCMultistep");
                cbDevice.Text = Data.Get("device");

                if (cbDevice.Text.Length == 0)
                    cbDevice.SelectedItem = cbDevice.Items[cbDevice.Items.Count - 1];

                Title = "Stable Diffusion XUI : ONNX venv";

                btnTIApply.IsEnabled = true;
            }
        }
        private void btnDiffCuda_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalVariables.Mode != Helper.ImplementMode.DiffCUDA)
            {
                GlobalVariables.Mode = Helper.ImplementMode.DiffCUDA;

                Install.CheckAndInstallCUDA();

                Brush Safe = new SolidColorBrush(Colors.Black);
                                
                btnDiffCuda.Background = new LinearGradientBrush(Colors.DarkGreen, Colors.Black, 0.5);
                btnONNX.Background = Safe;
                btnDiffCpu.Background = Safe;

                UpdateModelsList();

                grDevice.Visibility = Visibility.Collapsed;
                brDevice.Visibility = Visibility.Collapsed;

                cbFf16.Visibility = Visibility.Visible;
                CPUUse = false;

                cbSampler.Items.Clear();
                foreach (string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler", "UniPCMultistep");

                foreach (var item in GlobalVariables.GPUID.GPUs)
                {
                    if (item.Contains("nvidia"))
                    {
                        cbDevice.Items.Add(item);
                    }
                }

                if (cbDevice.Items.Count == 0)
                {
                    cbDevice.Items.Add("None");
                }

                cbDevice.Text = Data.Get("device");

                if (cbDevice.Text.Length == 0)
                    cbDevice.SelectedItem = 0;

                Title = "Stable Diffusion XUI : CUDA venv";

                btnTIApply.IsEnabled = true;
            }
        }

        private void btnDiffCpu_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalVariables.Mode != Helper.ImplementMode.DiffCPU)
            {
                GlobalVariables.Mode = Helper.ImplementMode.DiffCPU;
                Install.CheckAndInstallONNX();

                Brush Safe = new SolidColorBrush(Colors.Black);

                btnDiffCuda.Background = Safe;
                btnONNX.Background = Safe;
                btnDiffCpu.Background = new LinearGradientBrush(Colors.Blue, Colors.Red, 0.5);

                UpdateModelsList();

                grDevice.Visibility = Visibility.Collapsed;
                brDevice.Visibility = Visibility.Collapsed;

                cbFf16.Visibility = Visibility.Visible;

                CPUUse = true;

                cbSampler.Items.Clear();
                foreach (string Name in Schedulers.Diffusers)
                {
                    cbSampler.Items.Add(Name);
                }

                cbSampler.Text = Data.Get("sampler", "UniPCMultistep");
                cbDevice.Text = Data.Get("device");

                Title = "Stable Diffusion XUI : CPU venv";

                btnTIApply.IsEnabled = false;
            }
        }
        private void lvImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GlobalVariables.ImgList.Count > 0)
            {
                currentImage = (GlobalVariables.ImgList[lvImages.SelectedIndex]);

                ViewImg.Source = CodeUtils.BitmapFromUri(new Uri(currentImage));
                string NewCurrentImage = currentImage.Replace("_upscale.", ".");

                if (File.Exists(NewCurrentImage))
                {
                    currentImage = NewCurrentImage;
                }

                string Name = FS.GetImagesDir() + "best\\" + Path.GetFileName(GlobalVariables.ImgList[lvImages.SelectedIndex]);

                if (File.Exists(Name))
                {
                    GlobalVariables.ActiveImageState = Helper.ImageState.Favor;
                    btnFavor.Source = imgFavor.Source;
                }
                else
                {
                    GlobalVariables.ActiveImageState = Helper.ImageState.Free;
                    btnFavor.Source = imgNotFavor.Source;
                }
            }
        }
        private void cbDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDevice.SelectedItem == null)
                return;

            if (GlobalVariables.Mode == Helper.ImplementMode.ONNX)
            {
                Install.WrapONNXGPU(cbDevice.SelectedIndex > 0);
            }
        }

        private void btnImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog OpenDlg = new OpenFileDialog();
            OpenDlg.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.mp4| PNG (*.png)|*.png|MP4 (*.mp4)|*.mp4|JPG (*.jpg)|*.jpg|All files (*.*)|*.*";
            OpenDlg.Multiselect = false;

            bool? IsOpened = OpenDlg.ShowDialog();
            if (IsOpened.Value)
            {
                GlobalVariables.InputImagePath = OpenDlg.FileName;
                gridImg.Visibility = Visibility.Visible;
                brImgPane.Visibility = Visibility.Visible;

                if (GlobalVariables.InputImagePath.EndsWith(".mp4"))
                {
                    GlobalVariables.DrawMode = Helper.DrawingMode.Vid2Vid;
                    imgLoaded.Source = VideoFrame.GetPreviewPic();

                    imgMask.Visibility = Visibility.Collapsed;

                    slFPS.Visibility = Visibility.Visible;
                    tbFPS.Visibility = Visibility.Visible;
                    lbFPS.Visibility = Visibility.Visible;

                    cbControlNetMode.IsEnabled = false;
                }
                else
                {
                    imgMask.Visibility = Visibility.Visible;

                    lbFPS.Visibility = Visibility.Collapsed;
                    slFPS.Visibility = Visibility.Collapsed;
                    tbFPS.Visibility = Visibility.Collapsed;

                    cbControlNetMode.IsEnabled = true;

                    imgLoaded.Source = CodeUtils.BitmapFromUri(new Uri(GlobalVariables.InputImagePath));
                    GlobalVariables.DrawMode = Helper.DrawingMode.Img2Img;
                }

                tbMeta.Text = CodeUtils.MetaData(GlobalVariables.InputImagePath);
            }
        }

        private void btnZoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (currentImage == null || currentImage.Length < 5)
                return;

            Utils.ImageView ImgViewWnd = new Utils.ImageView();
            ImgViewWnd.SetImage(currentImage);
            ImgViewWnd.Show();
        }

        private void btnToImg_Click(object sender, MouseButtonEventArgs e)
        {
            if (currentImage == null && GlobalVariables.ImgList.Count <= 0)
            {
                return;
            }
            else if (currentImage != null)
            {
                GlobalVariables.InputImagePath = currentImage;
                imgLoaded.Source = CodeUtils.BitmapFromUri(new Uri(currentImage));
            }
            else
            {
                int Idx = lvImages.SelectedIndex;
                if (lvImages.SelectedIndex == -1)
                {
                    Idx = lvImages.Items.Count - 1;
                }

                GlobalVariables.InputImagePath = GlobalVariables.ImgList[Idx];
                imgLoaded.Source = CodeUtils.BitmapFromUri(new Uri(GlobalVariables.InputImagePath));
            }

            tbMeta.Text = CodeUtils.MetaData(GlobalVariables.InputImagePath);

            gridImg.Visibility = Visibility.Visible;
            brImgPane.Visibility = Visibility.Visible;
            GlobalVariables.DrawMode = Helper.DrawingMode.Img2Img;
        }

        private void tbDenoising_TextChanged(object sender, TextChangedEventArgs e)
        {
            slDenoising.Value = float.Parse(tbDenoising.Text.Replace('.', ','));
        }

        private void btnImageClear_Click(object sender, RoutedEventArgs e)
        {
            gridImg.Visibility = Visibility.Collapsed;
            brImgPane.Visibility = Visibility.Collapsed;

            GlobalVariables.DrawMode = Helper.DrawingMode.Text2Img;
            imgLoaded.Source = GlobalVariables.NoImageData;

            // Mask clear
            imgMask.Source = GlobalVariables.SafeMaskFreeImg;
            GlobalVariables.ImgMaskPath = string.Empty;
            imgMask.Visibility = Visibility.Collapsed;
        }

        private void btnHistory_Click(object sender, MouseButtonEventArgs e)
        {
            Utils.HistoryList HistoryWnd = new Utils.HistoryList();
            HistoryWnd.ShowDialog();
        }

        private void BlurWindow_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void btnFavorClick(object sender, MouseButtonEventArgs e)
        {
            if (lvImages.Items.Count == 0)
            {
                return;
            }

            int CurrentSel = lvImages.SelectedItem != null ? lvImages.SelectedIndex : 0;

            if (Helper.ImageState.Favor == GlobalVariables.ActiveImageState)
            {
                string Name = Path.GetFileName(GlobalVariables.ImgList[CurrentSel]);
                File.Delete(FS.GetImagesDir() + "best\\" + Name);
                GlobalVariables.ActiveImageState = Helper.ImageState.Free;

                btnFavor.Source = imgNotFavor.Source;
            }
            else
            {
                string Name = Path.GetFileName(GlobalVariables.ImgList[CurrentSel]);
                File.Copy(GlobalVariables.ImgList[CurrentSel], FS.GetImagesDir() + "best\\" + Name);
                GlobalVariables.ActiveImageState = Helper.ImageState.Favor;

                btnFavor.Source = imgFavor.Source;
            }
        }

        private void cbUpscaler_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GlobalVariables.CurrentUpscalerType = (Helper.UpscalerType)cbUpscaler.SelectedIndex;

            // Waifu and SRMD
            if (cbUpscaler.SelectedIndex > 3 && cbUpscaler.SelectedIndex < 7)
            {
                lbDenoiseName.IsEnabled = true;
                lbDenoise.IsEnabled = true;
                slDenoise.IsEnabled = true;
                slDenoise.Maximum = 3;
            }
            else if (cbUpscaler.SelectedIndex == 8)
            {
                lbDenoiseName.IsEnabled = true;
                lbDenoise.IsEnabled = true;
                slDenoise.IsEnabled = true;
                slDenoise.Maximum = 10;
            }
            else
            {
                lbDenoiseName.IsEnabled = false;
                lbDenoise.IsEnabled = false;
                slDenoise.IsEnabled = false;
            }
        }

        private void cbGfpgan_SelectionChanged(object sender, RoutedEventArgs e)
        {
            GlobalVariables.EnableGFPGAN = cbGfpgan.IsChecked.Value;
        }

        private void btnDeepDanbooru_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => CMD.DeepDanbooruProcess(GlobalVariables.InputImagePath));
        }

        private void Button_Click_DeepDanbooru(object sender, RoutedEventArgs e)
        {
            if (currentImage != null && currentImage != "")
            {
                Task.Run(() => CMD.DeepDanbooruProcess(currentImage));
            }
        }

        private void gridDrop(object sender, DragEventArgs e)
        {
            if (null != e.Data && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                imgView_Drop(sender, e);
            }
        }

        private void imgView_Drop(object sender, DragEventArgs e)
        {
            // Note that you can have more than one file.
            string dropedFile = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];

            if (dropedFile.ToLower().EndsWith(".png") || dropedFile.ToLower().EndsWith(".jpg") || dropedFile.ToLower().EndsWith(".jpeg"))
            {
                currentImage = dropedFile;
                ViewImg.Source = CodeUtils.BitmapFromUri(new Uri(dropedFile));
                btnDDB.Visibility = Visibility.Visible;
            }
        }

        private void btnBestOpen_Click(object sender, RoutedEventArgs e)
        {
            string Path = FS.GetImagesDir() + "\\best";

            if (!Directory.Exists(Path))
                return;

            currentImage = null;
            ClearImages();

            var Files = FS.GetFilesFrom(Path, new string[] { "png", "jpg" }, false);
            foreach (string file in Files)
            {
                SetImg(file);
            }
        }

        private void slEMA_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbETA != null)
                tbETA.Text = slETA.Value.ToString();
        }

        private void tbEMA_TextChanged(object sender, TextChangedEventArgs e)
        {
            slETA.Value = float.Parse(tbETA.Text.Replace('.', ','));
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void FloatNumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (e.Text.Length > 1)
            {
                float SkipFlt = 0;
                e.Handled = !float.TryParse(e.Text.Replace('.', ','), out SkipFlt);
            }
            else if (e.Text == ",")
            {
                e.Handled = false;
                return;
            }

            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void btnImageClearMask_Click(object sender, RoutedEventArgs e)
        {
            btnImageClearMask.Visibility = Visibility.Collapsed;

            imgMask.Source = GlobalVariables.SafeMaskFreeImg;
            GlobalVariables.ImgMaskPath = string.Empty;
        }

        private void btnSettingsClick(object sender, RoutedEventArgs e)
        {
            Utils.Settings SettingsWnd = new Utils.Settings();
            SettingsWnd.Show();
        }

        private void gridImg_Drop(object sender, DragEventArgs e)
        {
            string dropedFile = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];

            if (dropedFile.ToLower().EndsWith(".png") || dropedFile.ToLower().EndsWith(".jpg") || dropedFile.ToLower().EndsWith(".jpeg"))
            {
                GlobalVariables.ImgMaskPath = dropedFile;
                imgMask.Source = CodeUtils.BitmapFromUri(new Uri(dropedFile));
                btnImageClearMask.Visibility = Visibility.Visible;
            }
        }

        private void slW_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbW != null)
            {
                tbW.Text = slW.Value.ToString();

                if (lbRatio != null)
                    lbRatio.Content = GetRatio(slW.Value, slH.Value);
            }
        }

        private void slH_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (tbH != null)
            {
                tbH.Text = slH.Value.ToString();

                if (lbRatio != null)
                    lbRatio.Content = GetRatio(slW.Value, slH.Value);
            }
        }

        private void tbW_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tbW.Text.Length == 0)
                return;

            slW.Value = float.Parse(tbW.Text.Replace('.', ','));

            if (lbRatio != null)
                lbRatio.Content = GetRatio(slW.Value, slH.Value);
        }

        private void tbH_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tbH.Text.Length == 0)
                return;

            float NewValue = float.Parse(tbH.Text);
            slH.Value = NewValue;

            if (lbRatio != null)
                lbRatio.Content = GetRatio(slW.Value, slH.Value);
        }

        private void cbModel_Copy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void cbModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            string PickPathName = FS.GetModelDir() + (GlobalVariables.Mode != Helper.ImplementMode.ONNX ? "Diffusers\\" : "onnx\\") + e.AddedItems[0] + "\\logo.";

            if (System.IO.File.Exists(PickPathName + "png"))
            {
                imgModelPrivew.Source = CodeUtils.BitmapFromUri(new Uri(PickPathName + "png"));
            }
            else if (System.IO.File.Exists(PickPathName + "jpg"))
            {
                imgModelPrivew.Source = CodeUtils.BitmapFromUri(new Uri(PickPathName + "jpg"));
            }
            else
            {
                imgModelPrivew.Source = GlobalVariables.NoImageData;
            }
        }

        private void pbGen_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (pbGen.Value == 100 || pbGen.Value == 0)
                pbGen.Visibility = Visibility.Collapsed;
            else
                pbGen.Visibility = Visibility.Visible;
        }

        private void btnInImgPose_Click(object sender, RoutedEventArgs e)
        {
            string CurrentImg = GlobalVariables.InputImagePath;

            ControlNetBase CN = HelperControlNet.GetType(cbExtractPoseSelector.Text);
            Task.Run(() => CMD.PoserProcess(CurrentImg, CN));

            cbControlNetMode.SelectedIndex = cbExtractPoseSelector.SelectedIndex;

            UpdateModelsListControlNet();
        }

        private void cbPose_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HelperControlNet.Current == null)
                return;

            if (e.AddedItems.Count == 0)
            {
                GlobalVariables.CurrentPose = null;
                return;
            }

            string ImgPath = HelperControlNet.Current.Outdir();
            ImgPath += e.AddedItems[0];

            if (!ImgPath.EndsWith(".jpg"))
                ImgPath += ".png";

            if (File.Exists(ImgPath))
                imgPose.Source = CodeUtils.BitmapFromUri(new Uri(ImgPath));

            GlobalVariables.CurrentPose = ImgPath;
        }

        private void tsCN_Checked(object sender, RoutedEventArgs e)
        {
            cbPose.IsEnabled = tsCN.IsChecked.Value;
            imgPose.IsEnabled = tsCN.IsChecked.Value;
            cbSampler.IsEnabled = !tsCN.IsChecked.Value;

            if (tsCN.IsChecked.Value)
            {
                cbSampler.Text = "UniPCMultistep";
            }

            if (GlobalVariables.Mode != Helper.ImplementMode.ONNX)
                return;

            if (tsCN.IsChecked == true)
            {
                brLoRA.Visibility = Visibility.Collapsed;
                grLoRA.Visibility = Visibility.Collapsed;
            }
            else
            {
                brLoRA.Visibility = Visibility.Visible;
                grLoRA.Visibility = Visibility.Visible;
            }
        }

        private void tsTTA_Checked(object sender, RoutedEventArgs e)
        {
            GlobalVariables.TTA = tsTTA.IsChecked.Value;
        }

        private void cbPix2Pix_Checked(object sender, RoutedEventArgs e)
        {
            if (cbSampler == null)
                return;

            if (cbPix2Pix.IsChecked.Value)
            {
                imgMask.Visibility = Visibility.Collapsed;
                cbSampler.Text = "EulerAncestralDiscrete";
                cbSampler.IsEnabled = false;
                cbModel.IsEnabled = false;

                slDenoising.Minimum = 0;
                slDenoising.Maximum = 200;
            }
            else
            {
                imgMask.Visibility = Visibility.Visible;
                cbSampler.IsEnabled = true;
                cbModel.IsEnabled = true;

                slDenoising.Minimum = 5;
                slDenoising.Maximum = 100;
            }
        }

        private void cbLoRA_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            string LoRAName = e.AddedItems[0].ToString();

            string LoRASubFolder = "";

            if (cbLoRACat.Text != "None")
            {
                LoRASubFolder = cbLoRACat.Text;
            }

            LoRAName = LoRASubFolder + "/" + LoRAName.Replace(".safetensors", string.Empty);

            string TokenFilePath = FS.GetModelDir(FS.ModelDirs.LoRA) + LoRAName + ".txt";

            cbLoRAUserTokens.Items.Clear();

            if (File.Exists(TokenFilePath))
            {
                string[] Contents = File.ReadAllLines(TokenFilePath);
                //cbLoRAUserTokens.Text = Contents
                foreach(var x in Contents)
                {
                    cbLoRAUserTokens.Items.Add(x);
                }

                cbLoRAUserTokens.IsEnabled = true;
            }
            else
            {
                cbLoRAUserTokens.IsEnabled = false;
                cbLoRAUserTokens.Text = "";
            }
        }

        private void cbPreprocess_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void cbControlNetMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            string NewMode = ((ComboBoxItem)e.AddedItems[0]).Content.ToString();

            HelperControlNet.Current = HelperControlNet.GetType(NewMode);

            UpdateModelsListControlNet();
            cbPose.IsEnabled = true;
        }

        private void btnMore(object sender, RoutedEventArgs e)
        {
            ModelSelector form  = new ModelSelector();
            form.ShowDialog();

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (cbLoRA.Text.Length == 0)
                return;

            string LoraSubFolder = cbLoRACat.Text;

            if (LoraSubFolder == "None")
            {
                LoraSubFolder = "";
            }
            else
            {
                LoraSubFolder += "/";
            }
            string Temporary = $"<lora:{LoraSubFolder}{cbLoRA.Text}:{tbLorastrength.Text}>, ";
            Temporary += CodeUtils.GetRichText(tbPrompt);

            CodeUtils.SetRichText(tbPrompt, Temporary);
        }

        private void cbLoRACat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            cbLoRA.Items.Clear();

            string NewItm = (string)e.AddedItems[0];

            if (NewItm != "None")
                UpdateLoRAModels(FS.GetModelDir(FS.ModelDirs.LoRA) + e.AddedItems[0]);
            else
                UpdateLoRAModels(FS.GetModelDir(FS.ModelDirs.LoRA));

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

            if (cbTI.Text.Length == 0)
                return;

            string Temporary = $"<ti:{cbTI.Text}:{tbTIAlpha.Text}>, ";
            Temporary += CodeUtils.GetRichText(tbPrompt);

            CodeUtils.SetRichText(tbPrompt, Temporary);
        }

        private void cbLoRAUserTokens_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           if (e.AddedItems.Count == 0)
           {               
               return;
           }

            string Temporary = $"";
            Temporary += CodeUtils.GetRichText(tbPrompt);

            string Token2Apply = $"({cbLoRAUserTokens.SelectedItem}) ";

            if (!Temporary.Contains(Token2Apply))
            {
                CodeUtils.SetRichText(tbPrompt, Temporary);
                tbPrompt.AppendText(Token2Apply);
            }
        }

        private void cbLoRAUserTokens_DropDownOpened(object sender, EventArgs e)
        {
            cbLoRAUserTokens.SelectedItem = null;
        }

        private void btnFAQClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start
            (
                new ProcessStartInfo("https://github.com/ForserX/StableDiffusionUI/wiki/How-to---ONNX") 
                { 
                    UseShellExecute = true 
                }
            );
        }

        private void btnDisClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start
            (
                new ProcessStartInfo("https://discord.gg/HMG82cYNrA")
                {
                    UseShellExecute = true
                }
            );
        }
    }
}