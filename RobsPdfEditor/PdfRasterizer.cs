using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ghostscript.NET;
using Ghostscript.NET.Rasterizer;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using NLog;
using System.Drawing;
using iTextSharp.text.pdf;

namespace RobsPdfEditor
{
    class PdfRasterizer
    {
        private GhostscriptVersionInfo _lastInstalledVersion = null;
        private static GhostscriptRasterizer _rasterizer = new GhostscriptRasterizer();
        private Dictionary<int, System.Drawing.Image> _pageCache = new Dictionary<int, System.Drawing.Image>();
        private string _inputPdfPath;
        private List<int> _pageRotationInfo = new List<int>();
        private List<iTextSharp.text.Rectangle> _pageSizes = new List<iTextSharp.text.Rectangle>();
        private int _pointsPerInch = 0;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public PdfRasterizer(string inputPdfPath, int pointsPerInch)
        {
            _pointsPerInch = pointsPerInch;
            // Extract info from pdf using iTextSharp
            try
            {
            using (Stream newpdfStream = new FileStream(inputPdfPath, FileMode.Open, FileAccess.Read))
            {
                using (PdfReader pdfReader = new PdfReader(newpdfStream))
                {
                    int numPagesToUse = pdfReader.NumberOfPages;
                    for (int pageNum = 1; pageNum <= numPagesToUse; pageNum++)
                    {
                        iTextSharp.text.Rectangle pageRect = pdfReader.GetPageSize(pageNum);
                        _pageSizes.Add(pageRect);
                        int pageRot = pdfReader.GetPageRotation(pageNum);
                        _pageRotationInfo.Add(pageRot);
                    }
                }
            }
            }
            catch (Exception excp)
            {
                logger.Error("Cannot open PDF with iTextSharp {0} excp {1}", inputPdfPath, excp.Message);
            }

            _lastInstalledVersion =
                GhostscriptVersionInfo.GetLastInstalledVersion(
                        GhostscriptLicense.GPL | GhostscriptLicense.AFPL,
                        GhostscriptLicense.GPL);

            try
            {
                _rasterizer.Open(inputPdfPath.Replace("/",@"\"), _lastInstalledVersion, false);
            }
            catch (Exception excp)
            {
                logger.Error("Cannot open PDF with ghostscript {0} excp {1}", inputPdfPath, excp.Message);
            }

            _inputPdfPath = inputPdfPath;

        }

        public void Close()
        {
            _rasterizer.Close();
        }

        public int NumPages()
        {
            return _pageRotationInfo.Count;
        }

        public System.Drawing.Image GetPageImage(int pageNum, bool rotateBasedOnText)
        {
            // Return from cache if available
            if (_pageCache.ContainsKey(pageNum))
                return _pageCache[pageNum];

            // Fill cache
            System.Drawing.Image img = null;
            try
            {
                img = _rasterizer.GetPage(_pointsPerInch, _pointsPerInch, pageNum);
                // Rotate image as required
                if (rotateBasedOnText)
                {
                int pageIdx = pageNum - 1;
                if (pageIdx < _pageRotationInfo.Count)
                    if (_pageRotationInfo[pageIdx] != 0)
                        img = RotateImageWithoutCrop(img, _pageRotationInfo[pageIdx]);
                    }
                _pageCache.Add(pageNum, img);
            }
            catch (Exception excp)
            {
                Console.WriteLine("Failed to create image of page {0}", _inputPdfPath, excp.Message);
            }

            return img;
        }

        private Bitmap RotateImage(System.Drawing.Image inputImage, float angle)
        {
            int outWidth = inputImage.Width;
            int outHeight = inputImage.Height;
            if ((angle > 60 && angle < 120) || (angle > 240 && angle < 300))
            {
                outWidth = inputImage.Height;
                outHeight = inputImage.Width;
            }

            Bitmap rotatedImage = new Bitmap(outWidth, outHeight);
            using (Graphics g = Graphics.FromImage(rotatedImage))
            {
                g.TranslateTransform(inputImage.Width / 2, inputImage.Height / 2); //set the rotation point as the center into the matrix
                g.RotateTransform(angle); //rotate
                g.TranslateTransform(-inputImage.Width / 2, -inputImage.Height / 2); //restore rotation point into the matrix
                g.DrawImage(inputImage, new Point(0, 0)); //draw the image on the new bitmap
            }

            return rotatedImage;
        }

        public Image RotateImageWithoutCrop(Image b, float angle)
        {
            if (angle > 0)
            {
                int l = b.Width;
                int h = b.Height;
                double an = angle * Math.PI / 180;
                double cos = Math.Abs(Math.Cos(an));
                double sin = Math.Abs(Math.Sin(an));
                int nl = (int)(l * cos + h * sin);
                int nh = (int)(l * sin + h * cos);
                Bitmap returnBitmap = new Bitmap(nl, nh);
                Graphics g = Graphics.FromImage(returnBitmap);
                g.TranslateTransform((float)(nl - l) / 2, (float)(nh - h) / 2);
                g.TranslateTransform((float)b.Width / 2, (float)b.Height / 2);
                g.RotateTransform(angle);
                g.TranslateTransform(-(float)b.Width / 2, -(float)b.Height / 2);
                g.DrawImage(b, new Point(0, 0));
                return returnBitmap;
            }
            else return b;
        }

    }
}
