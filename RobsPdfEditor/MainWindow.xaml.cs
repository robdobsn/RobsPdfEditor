using iTextSharp.text;
using iTextSharp.text.pdf;
using MahApps.Metro.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
    public partial class MainWindow
    {
        private ObservableCollection<PdfPageInfo> _pdfPageList = new ObservableCollection<PdfPageInfo>();
        private BitmapImage splitIconOff;
        private BitmapImage splitIconOn;
        private BitmapImage deleteIconOff;
        private BitmapImage deleteIconOn;
        const int THUMBNAIL_HEIGHT = 600;
        const int POINTS_PER_INCH = 50;
        private PdfRasterizer _pdfRasterizer;
        private BackgroundWorker _bwThreadForPages;
        private string _curFileName;

        public MainWindow()
        {
            InitializeComponent();
            pageThumbs.ItemsSource = _pdfPageList;

            splitIconOff = new BitmapImage(new Uri("res/scissorsgray.png", UriKind.Relative));
            splitIconOn = new BitmapImage(new Uri("res/scissorsred.png", UriKind.Relative));
            deleteIconOff = new BitmapImage(new Uri("res/appbar.delete.gray.png", UriKind.Relative));
            deleteIconOn = new BitmapImage(new Uri("res/appbar.delete.red.png", UriKind.Relative));

            // Page filler thread
            _bwThreadForPages = new BackgroundWorker();
            _bwThreadForPages.WorkerSupportsCancellation = true;
            _bwThreadForPages.WorkerReportsProgress = true;
            _bwThreadForPages.DoWork += new DoWorkEventHandler(AddPages_DoWork);
        }

        public void OpenFile(string fileName)
        {
            _bwThreadForPages.CancelAsync();

            // Use a background worker to populate
            for (int i = 0; i < 5; i++)
            {
                if (!_bwThreadForPages.IsBusy)
                    break;
                Thread.Sleep(100);
            }
            if (_bwThreadForPages.IsBusy)
                return;

            _pdfPageList.Clear();
            _curFileName = fileName;
            lblInputFileName.Content = System.IO.Path.GetFileName(fileName);
            _bwThreadForPages.RunWorkerAsync();
        }

        private void AddPages_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            _pdfRasterizer = new PdfRasterizer(_curFileName, POINTS_PER_INCH);

            for (int i = 0; i < _pdfRasterizer.NumPages(); i++)
            {
                if ((worker.CancellationPending == true))
                {
                    e.Cancel = true;
                    break;
                }

                System.Drawing.Image pageImg = _pdfRasterizer.GetPageImage(i + 1);

                this.Dispatcher.BeginInvoke((Action)delegate()
                {
                    BitmapImage bitmap = ConvertToBitmap(pageImg);
                    PdfPageInfo pgInfo = new PdfPageInfo();
                    pgInfo.PageNumStr = (i + 1).ToString();
                    pgInfo.ThumbBitmap = bitmap;
                    pgInfo.SplitLineVisibility = Visibility.Hidden;
                    pgInfo.ThumbWidth = bitmap.Width;
                    pgInfo.ThumbHeight = bitmap.Height;
                    pgInfo.SplitIconImg = splitIconOff;
                    pgInfo.DeleteIconImg = deleteIconOff;
                    _pdfPageList.Add(pgInfo);
                });
                Thread.Sleep(50);
            }
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

        }

        private void SplitIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string tag = ((System.Windows.Controls.Image)sender).Tag.ToString();
            int onPageIdx = _pdfPageList.IndexOf(_pdfPageList.Where(X => X.PageNumStr == tag).FirstOrDefault());
            if (onPageIdx < 0)
                return;
            ToggleSplitDocAfterPage(onPageIdx);
        }

        private void ToggleSplitDocAfterPage(int pageIdx)
        {
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
            System.Windows.Controls.Image fromImg = sender as System.Windows.Controls.Image;
            if (fromImg != null && e.LeftButton == MouseButtonState.Pressed)
            {
                DragDrop.DoDragDrop(fromImg, fromImg.Tag, DragDropEffects.Move);
            }
        }

        private void PageImage_DragEnter(object sender, DragEventArgs e)
        {
            System.Windows.Controls.Image toImg = sender as System.Windows.Controls.Image;
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
            System.Windows.Controls.Image toImg = sender as System.Windows.Controls.Image;
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
            System.Windows.Controls.Image toImg = sender as System.Windows.Controls.Image;
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
            string tag = ((System.Windows.Controls.Image)sender).Tag.ToString();
            int onPageIdx = _pdfPageList.IndexOf(_pdfPageList.Where(X => X.PageNumStr == tag).FirstOrDefault());
            if (onPageIdx < 0)
                return;
            ToggleDeletePage(onPageIdx);
        }

        private void ToggleDeletePage(int pageIdx)
        {
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
            string tag = ((System.Windows.Controls.Image)sender).Tag.ToString();
            int onPageIdx = _pdfPageList.IndexOf(_pdfPageList.Where(X => X.PageNumStr == tag).FirstOrDefault());
            if (onPageIdx < 0)
                return;
            RotatePage(onPageIdx, -90);
        }

        private void RotateCWIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string tag = ((System.Windows.Controls.Image)sender).Tag.ToString();
            int onPageIdx = _pdfPageList.IndexOf(_pdfPageList.Where(X => X.PageNumStr == tag).FirstOrDefault());
            if (onPageIdx < 0)
                return;
            RotatePage(onPageIdx, 90);
        }

        private void RotatePage(int pageIdx, double angle)
        {
            if ((pageIdx >= 0) && (pageIdx < _pdfPageList.Count))
            {
                double reqdRotation = _pdfPageList[pageIdx].PageRotation + angle;
                while (reqdRotation < 0)
                    reqdRotation += 360;
                reqdRotation = reqdRotation % 360;
                _pdfPageList[pageIdx].PageRotation = reqdRotation;
            }
        }

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog cofd = new CommonOpenFileDialog("Select PDF file");
            cofd.Multiselect = false;
            cofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            cofd.Filters.Add(new CommonFileDialogFilter("PDF File", ".pdf"));
            CommonFileDialogResult result = cofd.ShowDialog(this);
            if (result == CommonFileDialogResult.Ok)
            {
                OpenFile(cofd.FileName);
            }
        }

        private void btnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            // Extract info from pdf using iTextSharp
            using (Stream inPDFStream = new FileStream(_curFileName, FileMode.Open, FileAccess.Read))
            {
                using (PdfReader pdfReader = new PdfReader(inPDFStream))
                {
                    int pdfOutFileIdx = 0;

                    // Loop through PDFs to be created
                    for (int pdfPageListIdx = 0; pdfPageListIdx < _pdfPageList.Count; pdfPageListIdx++)
                    {
                        // Skip deleted pages
                        if (_pdfPageList[pdfPageListIdx].PageDeleteVisibility == System.Windows.Visibility.Visible)
                            continue;

                        // Create output PDF
                        string outFileName = GenOutFileName(_curFileName, pdfOutFileIdx++);
                        if (File.Exists(outFileName))
                        {
                            MessageBox.Show("Output File Exists", "Problem Saving", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }

                        using (FileStream fs = new FileStream(outFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            using (Document inDoc = new Document(pdfReader.GetPageSizeWithRotation(1)))
                            {
                                using (PdfWriter outputWriter = PdfWriter.GetInstance(inDoc, fs))
                                {
                                    // Open input
                                    inDoc.Open();

                                    // Go through pages in input PDF
                                    for (; pdfPageListIdx < _pdfPageList.Count; pdfPageListIdx++)
                                    {
                                        // Skip deleted pages
                                        if (_pdfPageList[pdfPageListIdx].PageDeleteVisibility != System.Windows.Visibility.Visible)
                                        {
                                            // Add the page
                                            int pageNum = 0;
                                            Int32.TryParse(_pdfPageList[pdfPageListIdx].PageNumStr, out pageNum);

                                            // Get rotation
                                            int pageRotation = pdfReader.GetPageRotation(pageNum) + (int)_pdfPageList[pdfPageListIdx].PageRotation;
                                            if (pageRotation < 0)
                                                pageRotation = pageRotation + 360;
                                            pageRotation = pageRotation % 360;

                                            // Create a new destination page of the right dimensions
                                            iTextSharp.text.Rectangle pageSize = pdfReader.GetPageSizeWithRotation(pageNum);
                                            if (pageRotation == 90 || pageRotation == 270)
                                                pageSize = new iTextSharp.text.Rectangle(pageSize.Height, pageSize.Width);
                                            inDoc.SetPageSize(pageSize);
                                            inDoc.NewPage();

                                            // Get original page
                                            PdfImportedPage importedPage = outputWriter.GetImportedPage(pdfReader, pageNum);

                                            // Handle rotation

                                            var pageWidth = pdfReader.GetPageSizeWithRotation(pageNum).Width;
                                            var pageHeight = pdfReader.GetPageSizeWithRotation(pageNum).Height;
                                            switch (pageRotation)
                                            {
                                                case 0:
                                                default:
                                                    outputWriter.DirectContent.AddTemplate(importedPage, 1f, 0, 0, 1f, 0, 0);
                                                    break;

                                                case 90:
                                                    outputWriter.DirectContent.AddTemplate(importedPage, 0, -1f, 1f, 0, 0, pageWidth);
                                                    break;

                                                case 180:
                                                    outputWriter.DirectContent.AddTemplate(importedPage, -1f, 0, 0, -1f, pageWidth, pageHeight);
                                                    break;

                                                case 270:
                                                    outputWriter.DirectContent.AddTemplate(importedPage, 0, 1f, -1f, 0, pageHeight, 0);
                                                    break;
                                            }
                                        }

                                        // Check if this is the last page in this PDF
                                        if (_pdfPageList[pdfPageListIdx].SplitLineVisibility == System.Windows.Visibility.Visible)
                                            break;
                                    }

                                    // Close document
                                    inDoc.Close();

                                }
                            }
                        }
                    }
                }
            }
        }

        private string GenOutFileName(string curFileName, int fileIdx)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(curFileName) + "_" + fileIdx.ToString() + System.IO.Path.GetExtension(curFileName);
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(curFileName), fileName);
        }
    }
}

