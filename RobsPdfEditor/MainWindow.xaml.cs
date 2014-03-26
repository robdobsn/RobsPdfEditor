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
        ObservableCollection<PdfPageInfo> _pdfPageList = new ObservableCollection<PdfPageInfo>();
        BitmapImage splitIconOff;
        BitmapImage splitIconOn;

        public MainWindow()
        {
            const int THUMBNAIL_HEIGHT = 600;
            string thumbPath = @"\\MACALLAN\Admin\ScanAdmin\ScannedDocImgs\201212\";

            InitializeComponent();
            pageThumbs.ItemsSource = _pdfPageList;

            splitIconOff = new BitmapImage(new Uri("res/scissorsgray.png", UriKind.Relative));
            splitIconOn = new BitmapImage(new Uri("res/scissorsred.png", UriKind.Relative));

            string[] filePaths = Directory.GetFiles(thumbPath, "*.png");
            for (int i = 0; i < 30; i++)
            {
                if (i >= filePaths.Length)
                    break;
                BitmapImage bitmap = LoadThumbnail(filePaths[i], THUMBNAIL_HEIGHT);
                PdfPageInfo pgInfo = new PdfPageInfo();
                pgInfo.PageNumStr = (i + 1).ToString();
                pgInfo.ThumbBitmap = bitmap;
                pgInfo.SplitLineVisibility = Visibility.Hidden;
                pgInfo.ThumbWidth = bitmap.Width;
                pgInfo.ThumbHeight = bitmap.Height;
                pgInfo.SplitIconImg = splitIconOff;
                if (i == 3)
                {
                    pgInfo.SplitLineVisibility = Visibility.Visible;
                    pgInfo.SplitIconImg = splitIconOn;
                }
                if (i == 29)
                    pgInfo.SplitIconVisibility = Visibility.Hidden;
                _pdfPageList.Add(pgInfo);
            }


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


        }

        private void SplitIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string tag = ((Image)sender).Tag.ToString();
            int splitPos = 0;
            if (Int32.TryParse(tag, out splitPos))
            {
                ToggleSplitDocAfterPage(splitPos);
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
    }
}

