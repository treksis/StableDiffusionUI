﻿using System.Collections.Generic;
using System.Windows.Media;

namespace SD_FXUI
{
    internal class Helper
    {
        public enum UpscalerType
        {
            ESRGAN,
            ESRGAN_X4,
            ESRGAN_ANIME,
            ESRGAN_NET,
            WAIFU_CU,
            WAIFU_UP_PHOTO,
            WAIFU_UP_ART,
            SR,
            SRMD,
            None
        }
        public enum DrawingMode
        {
            Text2Img,
            Img2Img,
            Inpaint
        }

        public enum ImplementMode
        {
            DiffCUDA,
            ONNX,
            Shark,
            DiffCPU,
            InvokeAI,

            IDK
        }

        public enum ImageState
        {
            Free,
            Favor
        }

        public enum VENV
        {
            DiffCUDA,
            DiffONNX,
            Shark,
            DiffCPU,
            Any
        }

        public struct MetaInfo
        {
            public string Prompt;
            public string NegPrompt;
            public int StartSeed;
            public int Steps;
            public float CFG;
            public string Model;
        }

        public static List<Host> SecondaryProcessList = new List<Host>();

        public static string CachePath = string.Empty;
        public static string ImgPath = string.Empty;
        public static string ImgMaskPath = string.Empty;
        public static ImageSource SafeMaskFreeImg = null;
        public static MainWindow Form = null;
        public static HostForm UIHost = null;
        public static GPUInfo GPUID = null;
        public static ImplementMode Mode = ImplementMode.IDK;
        public static DrawingMode DrawMode = DrawingMode.Text2Img;
        public static string InputImagePath = string.Empty;
        public static List<string> PromHistory= new List<string>();
        public static ImageState ActiveImageState = ImageState.Free;
        public static bool EnableGFPGAN = false;
        public static UpscalerType CurrentUpscalerType = UpscalerType.None;

        public static List<string> ImgList = new List<string>();
        public static string CurrentTI = null;
        public static string CurrentPose = null;

        public static int Denoise = 0;
        public static bool TTA = false;
        public static MetaInfo MakeInfo = new Helper.MetaInfo();
    }
}
