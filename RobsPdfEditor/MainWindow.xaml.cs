using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RobsPdfEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<PdfPageInfo> _pdfPageList = new ObservableCollection<PdfPageInfo>();
        private BitmapImage splitIconOff;
        private BitmapImage splitIconOn;
        private BitmapImage deleteIconOff;
        private BitmapImage deleteIconOn;
        const int THUMBNAIL_HEIGHT = 400;
        const int POINTS_PER_INCH = 50;
        private PdfRasterizer _pdfRasterizer;

        public MainWindow()
        {
            string thumbPath = @"\\MACALLAN\Admin\ScanAdmin\ScannedDocImgs\201212\";
            string pdfPath = @"\\MACALLAN\Admin\ScanAdmin\ScanDocBackups";
            string pdfName = @"2012_04_05_16_04_09_Binder1.pdf";

            InitializeComponent();
            pageThumbs.ItemsSource = _pdfPageList;

            splitIconOff = new BitmapImage(new Uri("res/scissorsgray.png", UriKind.Relative));
            splitIconOn = new BitmapImage(new Uri("res/scissorsred.png", UriKind.Relative));
            deleteIconOff = new BitmapImage(new Uri("res/appbar.delete.gray.png", UriKind.Relative));
            deleteIconOn = new BitmapImage(new Uri("res/appbar.delete.red.png", UriKind.Relative));

            _pdfRasterizer = new PdfRasterizer(System.IO.Path.Combine(pdfPath, pdfName), POINTS_PER_INCH);

            for (int i = 0; i < _pdfRasterizer.NumPages(); i++)
            {
                BitmapImage bitmap = ConvertToBitmap(_pdfRasterizer.GetPageImage(i+1));
                PdfPageInfo pgInfo = new PdfPageInfo();
                pgInfo.PageNumStr = (i + 1).ToString();
                pgInfo.ThumbBitmap = bitmap;
                pgInfo.SplitLineVisibility = Visibility.Hidden;
                pgInfo.ThumbWidth = bitmap.Width;
                pgInfo.ThumbHeight = bitmap.Height;
                pgInfo.SplitIconImg = splitIconOff;
                pgInfo.DeleteIconImg = deleteIconOff;
                if (i == 1)
                    pgInfo.PageRotation = 0;
                if (i == 29)
                    pgInfo.SplitIconVisibility = Visibility.Hidden;
                _pdfPageList.Add(pgInfo);
            }


            //string[] filePaths = Directory.GetFiles(thumbPath, "*.png");
            //for (int i = 0; i < 30; i++)
            //{
            //    if (i >= filePaths.Length)
            //        break;
            //    BitmapImage bitmap = LoadThumbnail(filePaths[i], THUMBNAIL_HEIGHT);
            //    PdfPageInfo pgInfo = new PdfPageInfo();
            //    pgInfo.PageNumStr = (i + 1).ToString();
            //    pgInfo.ThumbBitmap = bitmap;
            //    pgInfo.SplitLineVisibility = Visibility.Hidden;
            //    pgInfo.ThumbWidth = bitmap.Width;
            //    pgInfo.ThumbHeight = bitmap.Height;
            //    pgInfo.SplitIconImg = splitIconOff;
            //    pgInfo.DeleteIconImg = deleteIconOff;
            //    if (i == 1)
            //        pgInfo.PageRotation = 0;
            //    if (i == 29)
            //        pgInfo.SplitIconVisibility = Visibility.Hidden;
            //    _pdfPageList.Add(pgInfo);
            //}


        }

        private BitmapImage ConvertToBitmap(System.Drawing.Image img)
        {
            MemoryStream ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            System.Windows.Media.Imaging.BitmapImage bImg = new System.Windows.Media.Imaging.BitmapImage();
            bImg.BeginInit();
            bImg.StreamSource = new MemoryStream(ms.ToArray());
            bImg.EndInit();
            return bImg;
        }

        public BitmapImage LoadThumbnail(string imgFileName, int heightOfThumbnail)
        {
            // The thumbnailStr can be either a string like "uniqName~pageNum" OR a full file path
            BitmapImage bitmap = null;
            if ((imgFileName == "") || (!File.Exists(imgFileName)))
            {
                //                logger.Info("Thumbnail file doesn't exist for {0}", imgFileName);
            }
            else
            {
                try
                {
                    bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri("File:" + imgFileName);
                    bitmap.DecodePixelHeight = heightOfThumbnail;
                    bitmap.EndInit();
                }
                catch (Exception excp)
                {
                    //                    logger.Error("Loading thumbnail file {0} excp {1}", imgFileName, excp.Message);
                    bitmap = null;
                }
            }
            return bitmap;
        }

        class PdfPageInfo : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            private BitmapImage _thumbnailBitmap;
            public BitmapImage ThumbBitmap
            {
                get { return _thumbnailBitmap; }
                set { _thumbnailBitmap = value; NotifyPropertyChanged("ThumbBitmap"); }
            }
            private int _pageNum;
            public string PageNumStr
            {
                get { return _pageNum.ToString(); }
                set { Int32.TryParse(value, out _pageNum); NotifyPropertyChanged("PageNumStr"); }
            }
            private Visibility _splitLineVisibility = Visibility.Hidden;
            public Visibility SplitLineVisibility
            {
                get { return _splitLineVisibility; }
                set { _splitLineVisibility = value; NotifyPropertyChanged("SplitLineVisibility"); }
            }
            private double _thumbWidth = 0;
            public double ThumbWidth
            {
                get { return _thumbWidth; }
                set { _thumbWidth = value; NotifyPropertyChanged("ThumbWidth"); }
            }
            private double _thumbHeight;
            public double ThumbHeight
            {
                get { return _thumbHeight; }
                set { _thumbHeight = value; NotifyPropertyChanged("ThumbHeight"); }
            }
            private Visibility _splitIconVisibility = Visibility.Visible;
            public Visibility SplitIconVisibility
            {
                get { return _splitIconVisibility; }
                set { _splitIconVisibility = value; NotifyPropertyChanged("SplitIconVisibility"); }
            }
            private BitmapImage _splitIconImg = null;
            public BitmapImage SplitIconImg
            {
                get { return _splitIconImg; }
                set { _splitIconImg = value; NotifyPropertyChanged("SplitIconImg"); }
            }
            private BitmapImage _deleteIconImg = null;
            public BitmapImage DeleteIconImg
            {
                get { return _deleteIconImg; }
                set { _deleteIconImg = value; NotifyPropertyChanged("DeleteIconImg"); }
            }
            private Visibility _pageDeleteVisibility = Visibility.Hidden;
            public Visibility PageDeleteVisibility
            {
                get { return _pageDeleteVisibility; }
                set { _pageDeleteVisibility = value; NotifyPropertyChanged("PageDeleteVisibility"); }
            }
            private double _pageRotation = 0;
            public double PageRotation
            {
                get { return _pageRotation; }
                set { _pageRotation = value; NotifyPropertyChanged("PageRotation"); NotifyPropertyChanged("CellWidth"); NotifyPropertyChanged("CellHeight"); }
            }
            public double CellWidth
            {
                get 
                {
                    return Math.Max(_thumbHeight, ThumbWidth) + 20;
                    //int pgRot = (int)_pageRotation;
                    //if ((pgRot == 90) || (pgRot == 270))
                    //    return _thumbHeight + 20;
                    //return _thumbWidth + 20;
                }
            }
            public double CellHeight
            {
                get 
                {
                    return THUMBNAIL_HEIGHT;
                    //return Math.Max(_thumbHeight, ThumbWidth);
                    //int pgRot = (int)_pageRotation;
                    //if ((pgRot == 90) || (pgRot == 270))
                    //    return _thumbWidth;
                    //return _thumbHeight;
                }
            }
        }

        private void SplitIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string tag = ((Image)sender).Tag.ToString();
            int splitAfterPageNum = 0;
            if (Int32.TryParse(tag, out splitAfterPageNum))
            {
                ToggleSplitDocAfterPage(splitAfterPageNum);
            }
        }

        private void ToggleSplitDocAfterPage(int pageNum)
        {
            int pageIdx = pageNum - 1;
            if ((pageIdx >= 0) && (pageIdx < _pdfPageList.Count))
            {
                if (_pdfPageList[pageIdx].SplitLineVisibility == System.Windows.Visibility.Hidden)
                {
                    _pdfPageList[pageIdx].SplitLineVisibility = System.Windows.Visibility.Visible;
                    _pdfPageList[pageIdx].SplitIconImg = splitIconOn;
                }
                else
                {
                    _pdfPageList[pageIdx].SplitLineVisibility = System.Windows.Visibility.Hidden;
                    _pdfPageList[pageIdx].SplitIconImg = splitIconOff;
                }
            }
        }

        private void PageImage_MouseMove(object sender, MouseEventArgs e)
        {
            Image fromImg = sender as Image;
            if (fromImg != null && e.LeftButton == MouseButtonState.Pressed)
            {
                DragDrop.DoDragDrop(fromImg, fromImg.Tag, DragDropEffects.Move);
            }
        }

        private void PageImage_DragEnter(object sender, DragEventArgs e)
        {
            Image toImg = sender as Image;
            if (toImg != null)
            {
                // If the DataObject contains string data, extract it
                if (e.Data.GetDataPresent(DataFormats.StringFormat))
                {
                    string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
                    Console.WriteLine("DragEnter dragging from " + dataString + " to " + toImg.Tag);
                }
            }
        }

        private void PageImage_DragLeave(object sender, DragEventArgs e)
        {
            Image toImg = sender as Image;
            if (toImg != null)
            {
                Console.WriteLine("DragLeave " + toImg.Tag);
                // restore previous values
            }
        }

        private void PageImage_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            // If the DataObject contains string data, extract it. 
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string dataString = (string)e.Data.GetData(DataFormats.StringFormat);

                e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            } 
        }

        private void PageImage_Drop(object sender, DragEventArgs e)
        {
            Image toImg = sender as Image;
            if (toImg != null)
            {
                // If the DataObject contains string data, extract it
                if (e.Data.GetDataPresent(DataFormats.StringFormat))
                {
                    string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
                    Console.WriteLine("Drop dragged from " + dataString + " to " + toImg.Tag);

                    // Find the items in the collection
                    int fromPageIdx = _pdfPageList.IndexOf(_pdfPageList.Where(X => X.PageNumStr == dataString).FirstOrDefault());
                    if (fromPageIdx < 0)
                        return;
                    int toPageIdx = _pdfPageList.IndexOf(_pdfPageList.Where(X => X.PageNumStr == toImg.Tag.ToString()).FirstOrDefault());
                    if (toPageIdx < 0)
                        return;
                    
                    // Update the list
                    _pdfPageList.Move(fromPageIdx, toPageIdx);
                }
            }
        }

        private void DeleteIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string tag = ((Image)sender).Tag.ToString();
            int pageNum = 0;
            if (Int32.TryParse(tag, out pageNum))
            {
                ToggleDeletePage(pageNum);
            }
        }

        private void ToggleDeletePage(int pageNum)
        {
            int pageIdx = pageNum - 1;
            if ((pageIdx >= 0) && (pageIdx < _pdfPageList.Count))
            {
                if (_pdfPageList[pageIdx].DeleteIconImg == deleteIconOff)
                {
                    _pdfPageList[pageIdx].DeleteIconImg = deleteIconOn;
                    _pdfPageList[pageIdx].PageDeleteVisibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    _pdfPageList[pageIdx].DeleteIconImg = deleteIconOff;
                    _pdfPageList[pageIdx].PageDeleteVisibility = System.Windows.Visibility.Hidden;
                }
            }
        }

        private void RotateACWIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string tag = ((Image)sender).Tag.ToString();
            int pageNum = 0;
            if (Int32.TryParse(tag, out pageNum))
            {
                RotatePage(pageNum, -90);
            }
        }

        private void RotateCWIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string tag = ((Image)sender).Tag.ToString();
            int pageNum = 0;
            if (Int32.TryParse(tag, out pageNum))
            {
                RotatePage(pageNum, 90);
            }
        }

        private void RotatePage(int pageNum, double angle)
        {
            int pageIdx = pageNum - 1;
            if ((pageIdx >= 0) && (pageIdx < _pdfPageList.Count))
            {
                double reqdRotation = _pdfPageList[pageIdx].PageRotation + angle;
                while (reqdRotation < 0)
                    reqdRotation += 360;
                reqdRotation = reqdRotation % 360;
                _pdfPageList[pageIdx].PageRotation = reqdRotation;
            }
        }
    }
}

