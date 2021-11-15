using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace KlayFlight
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string className, string windowName);

        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32")]
        public static extern int GetWindowRect(int hwnd, ref RECT lpRect);

        public int RoiWidth { get; set; }
        public int RoiHeight { get; set; }

        public int BetweenBar { get; set; }

        public int CharacerSizeError = 7;
        public int CoinSizeError = 3;
        public int BarWidthError = 4;
        public int BarMinHeight = 50;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Task t = new Task(new Action(ShowBoxes));
            t.Start();
        }

        public Bitmap Copy(Rect crop)
        {
            // 주화면의 크기 정보 읽기
            Rectangle rect = Screen.PrimaryScreen.Bounds;
            // 2nd screen = Screen.AllScreens[1]

            // 픽셀 포맷 정보 얻기 (Optional)
            int bitsPerPixel = Screen.PrimaryScreen.BitsPerPixel;
            PixelFormat pixelFormat = PixelFormat.Format32bppArgb;
            if (bitsPerPixel <= 16)
            {
                pixelFormat = PixelFormat.Format16bppRgb565;
            }
            if (bitsPerPixel == 24)
            {
                pixelFormat = PixelFormat.Format24bppRgb;
            }

            // 화면 크기만큼의 Bitmap 생성
            System.Drawing.Point leftTop = new System.Drawing.Point();
            leftTop.X = crop.Left;
            leftTop.Y = crop.Top;
            Rectangle cropRect = new Rectangle(leftTop,
                                               new System.Drawing.Size(crop.Width, crop.Height));

            //Bitmap bmp = new Bitmap(rect.Width, rect.Height, pixelFormat);
            Bitmap bmp = new Bitmap(cropRect.Width, cropRect.Height, pixelFormat);

            // Bitmap 이미지 변경을 위해 Graphics 객체 생성
            using (Graphics gr = Graphics.FromImage(bmp))
            {
                // 화면을 그대로 카피해서 Bitmap 메모리에 저장
                //gr.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size);
                gr.CopyFromScreen(cropRect.Left, cropRect.Top, 0, 0, cropRect.Size);
            }
            // Bitmap 데이타를 파일로 저장
            //bmp.Save(outputFilename);
            //bmp.Dispose();
            return bmp;
        }

        public RECT GetTargetWindowRect()
        {
            RECT r = new RECT();
            Process[] allProcs = Process.GetProcesses();
            foreach (Process proc in allProcs)
            {
                if (proc.ProcessName.Contains("whale"))
                {
                    if (proc.MainWindowTitle.Contains("KLAYGAMES"))
                    {
                        GetWindowRect(proc.MainWindowHandle.ToInt32(), ref r);
                        return r;
                    }
                }
            }
            return r;
        }
        public Mat GetScreenMat()
        {
            RECT wndRect = GetTargetWindowRect();
            Rect rect = new Rect(wndRect.left, wndRect.top, wndRect.right - wndRect.left, wndRect.bottom - wndRect.top);
            Bitmap screen = Copy(rect);
            Mat img = OpenCvSharp.Extensions.BitmapConverter.ToMat(screen);
            Rect targetRect = GetTargetArea(img);
            Mat roi = new Mat();
            roi = img.SubMat(targetRect);
            roi = roi.CvtColor(ColorConversionCodes.BGRA2BGR);
            screen.Dispose();
            RoiWidth = roi.Width;
            RoiHeight = roi.Height;
            BetweenBar = (int)(RoiHeight / 4.95);
            return roi;
        }

        public void ShowBoxes()
        {
            while(true)
            {
                Mat screenMat = GetScreenMat();
                Rect charRect = FindCharacter(ref screenMat);
                FindContours(ref screenMat);
                Cv2.Rectangle(screenMat, charRect, new Scalar(120, 120, 120), 2);
                Cv2.ImShow("test", screenMat);
                Cv2.WaitKey(30);
            }
        }

        public Rect FindCharacter(ref Mat screenMat)
        {
            Rect charRect = new Rect();
            Mat result = new Mat();
            Cv2.InRange(screenMat, new Scalar(70, 0, 150), new Scalar(200, 255, 255), result);

            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] h;
            Cv2.FindContours(result, out contours, out h, RetrievalModes.List, ContourApproximationModes.ApproxNone);
            if (contours != null && contours.Length != 0)
            {
                int characterWidth = (int)(RoiHeight / 10);
                int characterHeight = characterWidth;

                List<Rect> rests = new List<Rect>();
                for (int j = 0; j < contours.Length; j++)
                {
                    Rect r = Cv2.BoundingRect(contours[j]);
                    if (r.Width > characterWidth - CharacerSizeError &&
                        r.Width < characterWidth + CharacerSizeError &&
                        r.Height > characterHeight - CharacerSizeError &&
                        r.Height < characterHeight + CharacerSizeError)
                    {
                        rests.Add(r);
                    }
                }
                if (rests.Count != 0)
                {
                    Console.WriteLine("Rect Count : " + rests.Count.ToString());
                    rests = rests.OrderByDescending(r => r.Width * r.Height).ToList();
                    charRect = rests[0];
                }
            }
            return charRect;
        }

        public void FindContours(ref Mat src, bool showThres = false)
        {
            Mat[] channels = src.Split();
            for(int i = 0; i < channels.Length; i++)
            {
                Mat thresMat = channels[i].Threshold(160, 255, ThresholdTypes.Binary);
                int rowCount = thresMat.Rows;
                thresMat.Row(0).SetTo(new Scalar(255, 255, 255));
                thresMat.Row(1).SetTo(new Scalar(255, 255, 255));
                thresMat.Row(2).SetTo(new Scalar(255, 255, 255));
                thresMat.Row(3).SetTo(new Scalar(255, 255, 255));
                thresMat.Row(rowCount - 4).SetTo(new Scalar(255, 255, 255));
                thresMat.Row(rowCount - 3).SetTo(new Scalar(255, 255, 255));
                thresMat.Row(rowCount - 2).SetTo(new Scalar(255, 255, 255));
                thresMat.Row(rowCount - 1).SetTo(new Scalar(255, 255, 255));

                if (showThres == true)
                {
                    //Cv2.ImShow("channels[i]", channels[i]);
                    string windowName = String.Format("thres {0}", i);
                    Cv2.ImShow(windowName, thresMat);
                    Cv2.WaitKey(30);
                }

                if (i == 0) // 코인처리
                {
                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] h;
                    Cv2.FindContours(thresMat, out contours, out h, RetrievalModes.List, ContourApproximationModes.ApproxNone);
                    if (contours != null && contours.Length != 0)
                    {
                        for (int j = 0; j < contours.Length; j++)
                        {
                            Rect r = Cv2.BoundingRect(contours[j]);
                            int coinWidth = (int)(RoiHeight / 10);
                            int coinHeight = coinWidth;

                            if (r.Width > coinWidth - CoinSizeError &&
                               r.Width < coinWidth + CoinSizeError &&
                               r.Height > coinHeight - CoinSizeError &&
                               r.Height < coinHeight + CoinSizeError)
                            {
                                Cv2.Rectangle(src, r, new Scalar(255, 255, 0), 2);
                            }
                        }
                    }
                }
                else if(i == 1) // 장애물처리
                {
                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] h;
                    Cv2.FindContours(thresMat, out contours, out h, RetrievalModes.List, ContourApproximationModes.ApproxNone);
                    if (contours != null && contours.Length != 0)
                    {
                        for (int j = 0; j < contours.Length; j++)
                        {
                            Rect r = Cv2.BoundingRect(contours[j]);
                            int barWidth = (int)(RoiHeight / 50);

                            if (r.Top > RoiHeight * 0.17 &&
                                r.Width > barWidth - BarWidthError &&
                                r.Width < barWidth + BarWidthError &&
                                r.Height > BarMinHeight)
                            {
                                Cv2.Rectangle(src, r, new Scalar(0, 255, 255), 2);

                                Rect oppositeBar = new Rect();
                                oppositeBar.Left = r.Left - 5;
                                oppositeBar.Width = r.Width + 5;
                                oppositeBar.Top = 0;
                                oppositeBar.Height = r.Top;
                                Mat oppositeBarRoi = thresMat.SubMat(oppositeBar);

                                OpenCvSharp.Point[][] conts;
                                HierarchyIndex[] h2;
                                Cv2.FindContours(oppositeBarRoi, out conts, out h2,
                                                 RetrievalModes.List, ContourApproximationModes.ApproxNone);

                                if(conts != null && conts.Length != 0)
                                {
                                    for(int k = 0; k < conts.Length; k++)
                                    {
                                        Rect r2 = Cv2.BoundingRect(conts[j]);
                                        Cv2.Rectangle(src, r2, new Scalar(80, 150, 250), 2);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private Rect GetTargetArea(Mat src)
        {
            Rect targetArea = new Rect();
            Mat thres = new Mat(src.Size(), MatType.CV_8UC1);
            Cv2.CvtColor(src, thres, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(thres, thres, 40, 255, ThresholdTypes.BinaryInv);
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] h;
            Cv2.FindContours(thres, out contours, out h, RetrievalModes.List, ContourApproximationModes.ApproxNone);
            if (contours != null && contours.Length != 0)
            {
                List<Rect> rects = new List<Rect>();
                for (int j = 0; j < contours.Length; j++)
                {
                    Rect r = Cv2.BoundingRect(contours[j]);
                    rects.Add(r);
                }
                rects = rects.OrderByDescending(r => (r.Width * r.Height)).ToList();
                targetArea = rects[1];
                //Cv2.Rectangle(screenMat, rects[1], new Scalar(255, 255, 0), 2);
            }
            //Cv2.ImShow("area", screenMat);
            //Cv2.WaitKey(0);
            return targetArea;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Mat img = Cv2.ImRead(@"F:\Programming\KlayFlight\KlayFlight\testimage\5.png", ImreadModes.AnyColor);
            MessageBox.Show(img.Type().ToString());
            Mat result = new Mat();
            Cv2.InRange(img, new Scalar(70, 0, 150), new Scalar(200, 255, 255), result);
            Cv2.ImShow("test", result);
            Cv2.WaitKey(0);

            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] h;
            Cv2.FindContours(result, out contours, out h, RetrievalModes.List, ContourApproximationModes.ApproxNone);
            if (contours != null && contours.Length != 0)
            {
                List<Rect> rects = new List<Rect>();
                int characterWidth = (int)(RoiHeight / 10);
                int characterHeight = characterWidth;

                for (int j = 0; j < contours.Length; j++)
                {
                    Rect r = Cv2.BoundingRect(contours[j]);

                    if (r.Width > characterWidth - CharacerSizeError &&
                        r.Height > characterHeight - CharacerSizeError)
                    {
                        rects.Add(r);
                    }
                }

                rects = rects.OrderByDescending(r => r.Width * r.Height).ToList();
                Cv2.Rectangle(img, rects[0], new Scalar(255, 0, 255), 2);
                string msg = String.Format("{0} , {1}, {2}, {3}", rects[0].Width, rects[0].Height, RoiHeight, characterWidth);
                MessageBox.Show(msg);
            }
            Cv2.ImShow("test", img);
            Cv2.WaitKey(0);

        }

        private void button3_Click(object sender, EventArgs e)
        {
            Mat mat = GetScreenMat();
            Cv2.ImShow("test", mat);
            Cv2.WaitKey(0);
        }

    }
}
