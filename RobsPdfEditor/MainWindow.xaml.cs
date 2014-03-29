using iTextSharp.text;
using iTextSharp.text.pdf;
using MahApps.Metro.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using NLog;
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
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private ObservableCollection<PdfPageInfo> _pdfPageList = new ObservableCollection<PdfPageInfo>();
        public static BitmapImage splitIconOff;
        public static BitmapImage splitIconOn;
        public static BitmapImage deleteIconOff;
        public static BitmapImage deleteIconOn;
        const int THUMBNAIL_HEIGHT = 600;
        const int POINTS_PER_INCH = 50;
        private PdfRasterizer _pdfRasterizer;
        private BackgroundWorker _bwThreadForPages;
        private List<string> _curFileNames = new List<string>();
        private int _curBackgroundLoadingFileIdx = 0;
        private string _windowTitle = "Rob's PDF Editor";
        private bool _changesMade = false;

        public MainWindow()
        {
            InitializeComponent();
            pageThumbs.ItemsSource = _pdfPageList;

            splitIconOff = new BitmapImage(new Uri("res/scissorsgray.png", UriKind.Relative));
            splitIconOn = new BitmapImage(new Uri("res/scissorsred.png", UriKind.Relative));
            deleteIconOff = new BitmapImage(new Uri("res/appbar.delete.gray.png", UriKind.Relative));
            deleteIconOn = new BitmapImage(new Uri("res/appbar.delete.red.png", UriKind.Relative));

            RobsPDFEditor.Title = _windowTitle;

            // Page filler thread
            _bwThreadForPages = new BackgroundWorker();
            _bwThreadForPages.WorkerSupportsCancellation = true;
            _bwThreadForPages.WorkerReportsProgress = true;
            _bwThreadForPages.DoWork += new DoWorkEventHandler(AddPages_DoWork);
        }

        #region Drag and Drop Handling

        private void PageImage_MouseMove(object sender, MouseEventArgs e)
        {
            string elemTag = GetElementTag(sender);
            if (elemTag != "" && e.LeftButton == MouseButtonState.Pressed)
            {
                DragDrop.DoDragDrop((Grid)sender, elemTag, DragDropEffects.Move);
            }
        }

        private void PageImage_DragEnter(object sender, DragEventArgs e)
        {
            string elemTag = GetElementTag(sender);
            if (elemTag != "")
            {
                // If the DataObject contains string data, extract it
                if (e.Data.GetDataPresent(DataFormats.StringFormat))
                {
                    string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
                    Console.WriteLine("DragEnter dragging from " + dataString + " to " + elemTag);
                }
            }
        }

        private void PageImage_DragLeave(object sender, DragEventArgs e)
        {
            string elemTag = GetElementTag(sender);
            if (elemTag != "")
            {
                Console.WriteLine("DragLeave " + elemTag);
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
            string elemTag = GetElementTag(sender);
            if (elemTag != "")
            {
                // If the DataObject contains string data, extract it
                if (e.Data.GetDataPresent(DataFormats.StringFormat))
                {
                    string dataString = (string)e.Data.GetData(DataFormats.StringFormat);
                    Console.WriteLine("Drop dragged from " + dataString + " to " + elemTag);

                    // Find the items in the collection
                    int fromPageIdx = FindTagInPageList(dataString);
                    if (fromPageIdx < 0)
                        return;
                    int toPageIdx = FindTagInPageList(elemTag.ToString());
                    if (toPageIdx < 0)
                        return;
                    
                    // Update the list
                    _pdfPageList.Move(fromPageIdx, toPageIdx);
                }
                RewritePageNumbers();
                UpdateWindowTitle(true);
            }
        }

        #endregion

        #region Mouse Handling

        private void SplitIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            int pageIdx = ExtractFileAndPageIdxs(sender);
            if (pageIdx < 0)
                return;
            ToggleSplitDocAfterPage(pageIdx);
        }

        private void ToggleSplitDocAfterPage(int pageIdx)
        {
            if ((pageIdx >= 0) && (pageIdx < _pdfPageList.Count))
                _pdfPageList[pageIdx].SplitAfter = !_pdfPageList[pageIdx].SplitAfter;
            RewritePageNumbers();
            UpdateWindowTitle(true);
        }

        private void DeleteIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            int pageIdx = ExtractFileAndPageIdxs(sender);
            if (pageIdx < 0)
                return;
            ToggleDeletePage(pageIdx);
        }

        private void ToggleDeletePage(int pageIdx)
        {
            if ((pageIdx >= 0) && (pageIdx < _pdfPageList.Count))
                _pdfPageList[pageIdx].DeletePage = !_pdfPageList[pageIdx].DeletePage;
            RewritePageNumbers();
            UpdateWindowTitle(true);
        }

        private void RotateACWIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            int pageIdx = ExtractFileAndPageIdxs(sender);
            if (pageIdx < 0)
                return;
            RotatePage(pageIdx, -90);
        }

        private void RotateCWIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            int pageIdx = ExtractFileAndPageIdxs(sender);
            if (pageIdx < 0)
                return;
            RotatePage(pageIdx, 90);
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
            UpdateWindowTitle(true);
        }

        #endregion

        #region Button File Open/Add/Save Handling

        private void btnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (CancelIfChangesMade())
                return;

            if (CheckIfThreadBusy())
                return;

            CommonOpenFileDialog cofd = new CommonOpenFileDialog("Select PDF file");
            cofd.Multiselect = false;
            cofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            cofd.Filters.Add(new CommonFileDialogFilter("PDF File", ".pdf"));
            CommonFileDialogResult result = cofd.ShowDialog(this);
            if (result == CommonFileDialogResult.Ok)
                OpenFile(cofd.FileName);
        }
        
        private void btnAddFile_Click(object sender, RoutedEventArgs e)
        {
            if (CheckIfThreadBusy())
                return;

            CommonOpenFileDialog cofd = new CommonOpenFileDialog("Select PDF file");
            cofd.Multiselect = false;
            cofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            cofd.Filters.Add(new CommonFileDialogFilter("PDF File", ".pdf"));
            CommonFileDialogResult result = cofd.ShowDialog(this);
            if (result == CommonFileDialogResult.Ok)
                AddFile(cofd.FileName);

            UpdateWindowTitle(true);
        }
        
        private void btnSaveFile_Click(object sender, RoutedEventArgs e)
        {
            if (CheckIfThreadBusy())
                return;

            if (_curFileNames.Count < 1)
                return;

            // Open pdf readers for each input file
            List<Stream> inPDFStreams = new List<Stream>();
            List<PdfReader> pdfReaders = new List<PdfReader>();
            foreach (string fileName in _curFileNames)
            {
                try
                {
                    Stream inStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                    inPDFStreams.Add(inStream);
                    pdfReaders.Add(new PdfReader(inStream));
                }
                catch (Exception excp)
                {
                    logger.Error("Failed to open input pdf {0} excp {1}", fileName, excp.Message);
                }
            }

            // Loop through PDFs to be created
            int pdfOutFileIdx = 0;
            for (int pdfPageListIdx = 0; pdfPageListIdx < _pdfPageList.Count; pdfPageListIdx++)
            {
                // Skip deleted pages
                if (_pdfPageList[pdfPageListIdx].DeletePage)
                    continue;

                // Create output PDF
                string outFileName = GenOutFileName(_curFileNames[0], pdfOutFileIdx++);
                if (File.Exists(outFileName))
                {
                    // Ask the user if they are sure
                    MessageDialog.Show("Cannot save as output file exists already", "", "", "OK", null, this);
                    return;
                }

                using (FileStream fs = new FileStream(outFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (Document inDoc = new Document(pdfReaders[0].GetPageSizeWithRotation(1)))
                    {
                        using (PdfWriter outputWriter = PdfWriter.GetInstance(inDoc, fs))
                        {
                            // Open input
                            inDoc.Open();

                            // Go through pages in input PDF
                            for (; pdfPageListIdx < _pdfPageList.Count; pdfPageListIdx++)
                            {
                                // Skip deleted pages
                                if (!_pdfPageList[pdfPageListIdx].DeletePage)
                                {
                                    // Add the page
                                    int pageNum = _pdfPageList[pdfPageListIdx].PageNum;
                                    int fileIdx = _pdfPageList[pdfPageListIdx].FileIndex;

                                    // Get rotation
                                    int pageRotation = pdfReaders[fileIdx].GetPageRotation(pageNum) + (int)_pdfPageList[pdfPageListIdx].PageRotation;
                                    if (pageRotation < 0)
                                        pageRotation = pageRotation + 360;
                                    pageRotation = pageRotation % 360;

                                    // Create a new destination page of the right dimensions
                                    iTextSharp.text.Rectangle pageSize = pdfReaders[fileIdx].GetPageSizeWithRotation(pageNum);
                                    if (pageRotation == 90 || pageRotation == 270)
                                        pageSize = new iTextSharp.text.Rectangle(pageSize.Height, pageSize.Width);
                                    inDoc.SetPageSize(pageSize);
                                    inDoc.NewPage();

                                    // Get original page
                                    PdfImportedPage importedPage = outputWriter.GetImportedPage(pdfReaders[fileIdx], pageNum);

                                    // Handle rotation

                                    var pageWidth = pdfReaders[fileIdx].GetPageSizeWithRotation(pageNum).Width;
                                    var pageHeight = pdfReaders[fileIdx].GetPageSizeWithRotation(pageNum).Height;
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
                                if (_pdfPageList[pdfPageListIdx].SplitAfter)
                                    break;
                            }

                            // Close document
                            inDoc.Close();

                        }
                    }
                }
            }

            foreach (PdfReader pdfReader in pdfReaders)
                pdfReader.Close();
            foreach (Stream strm in inPDFStreams)
                strm.Close();

            _changesMade = false;
            UpdateWindowTitle();
        }

        #endregion

        #region Open / Add Files

        public void OpenFile(string fileName)
        {
            if (CheckIfThreadBusy())
                return;

            // Add pages to list of pages
            _pdfPageList.Clear();
            _curFileNames.Clear();
            _curFileNames.Add(fileName);
            _curBackgroundLoadingFileIdx = 0;
            _changesMade = false;
            UpdateWindowTitle();
            _bwThreadForPages.RunWorkerAsync();
        }

        public void AddFile(string fileName)
        {
            if (CheckIfThreadBusy())
                return;

            // Go through existing list of pages indicating file number should now be shown
            foreach (PdfPageInfo info in _pdfPageList)
                info.ShowFileNum = true;

            // Add the file name to the list of edited files
            _curFileNames.Add(fileName);
            _curBackgroundLoadingFileIdx++;
            UpdateWindowTitle();
            _bwThreadForPages.RunWorkerAsync();
        }

        private void AddPages_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            _pdfRasterizer = new PdfRasterizer(_curFileNames[_curBackgroundLoadingFileIdx], POINTS_PER_INCH);

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
                    pgInfo.PageNum = i + 1;
                    pgInfo.FileIndex = _curBackgroundLoadingFileIdx;
                    pgInfo.ThumbBitmap = bitmap;
                    pgInfo.SplitAfter = false;
                    pgInfo.DeletePage = false;
                    pgInfo.PageRotation = 0;
                    pgInfo.ShowFileNum = (_curBackgroundLoadingFileIdx > 0);
                    _pdfPageList.Add(pgInfo);
                });
                Thread.Sleep(50);
            }
            RewritePageNumbers();
        }

        #endregion

        #region Utility Functions

        private void UpdateWindowTitle(bool changesMade = false)
        {
            if (changesMade)
                _changesMade = true;
            RobsPDFEditor.Title = _windowTitle + ((_curFileNames.Count > 0) ? "" : (" - " + System.IO.Path.GetFileName(_curFileNames[0]) + (_changesMade ? " *" : "")));
        }

        private string GenOutFileName(string curFileName, int fileIdx)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(curFileName) + "_" + fileIdx.ToString() + System.IO.Path.GetExtension(curFileName);
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(curFileName), fileName);
        }

        private void RobsPDFEditor_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = CancelIfChangesMade();
        }

        private bool CancelIfChangesMade()
        {
            if (_changesMade)
            {
                // Ask the user if they are sure
                MessageDialog.MsgDlgRslt rslt = MessageDialog.Show("Discard changes?\nAre you sure?", "Yes", "No", "Cancel", null, this);
                if (rslt != MessageDialog.MsgDlgRslt.RSLT_YES)
                    return true;
            }
            return false;
        }

        private bool CheckIfThreadBusy(bool stopFirst=false, int waitSecs=3)
        {
            if (_bwThreadForPages.IsBusy)
            {
                if (stopFirst)
                    _bwThreadForPages.CancelAsync();

                for (int i = 0; i < waitSecs*10; i++)
                {
                    if (!_bwThreadForPages.IsBusy)
                        break;
                    Thread.Sleep(100);
                }

                // Tell the user to wait
                if (_bwThreadForPages.IsBusy)
                {
                    MessageDialog.MsgDlgRslt rslt = MessageDialog.Show("Busy reading file, please try again in a mo ...", "OK", "", "", null, this);
                    return true;
                }
            }
            return false;
        }

        private int ExtractFileAndPageIdxs(object sender)
        {
            string tag = ((System.Windows.Controls.Image)sender).Tag.ToString();
            return FindTagInPageList(tag);
        }

        private int FindTagInPageList(string tag)
        {
            int idxInPageList = _pdfPageList.IndexOf(_pdfPageList.Where(X => X.TagStr == tag).FirstOrDefault());
            if ((idxInPageList < 0) || (idxInPageList >= _pdfPageList.Count))
                return -1;
            return idxInPageList;
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

        private string GetElementTag(object sender)
        {
            System.Windows.Controls.Grid ctrl = sender as System.Windows.Controls.Grid;
            if (ctrl == null || ctrl.Tag == null)
                return "";
            return (string)ctrl.Tag;
        }

        private void RewritePageNumbers()
        {
            for (int i = 0; i < _pdfPageList.Count; i++)
            {
                if (_pdfPageList[i].NewDocPageNum != i + 1)
                    _pdfPageList[i].NewDocPageNum = i + 1;
            }
        }

        #endregion

        #region PdfPageInfo Class

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
            public double ThumbWidth
            {
                get { return _thumbnailBitmap.Width; }
            }
            public double ThumbHeight
            {
                get { return _thumbnailBitmap.Height; }
            }

            // Split line
            private bool _splitAfter = false;
            public bool SplitAfter
            {
                get { return _splitAfter; }
                set { _splitAfter = value; NotifyPropertyChanged("SplitAfter"); NotifyPropertyChanged("SplitLineVisibility"); NotifyPropertyChanged("SplitIconImg"); }
            }
            public Visibility SplitLineVisibility
            {
                get { return _splitAfter ? Visibility.Visible : Visibility.Hidden; }
            }
            public BitmapImage SplitIconImg
            {
                get { return _splitAfter ? splitIconOn : splitIconOff; }
            }

            // Page delete
            private bool _deletePage = false;
            public bool DeletePage
            {
                get { return _deletePage; }
                set { _deletePage = value; NotifyPropertyChanged("DeletePage"); NotifyPropertyChanged("PageDeleteVisibility"); NotifyPropertyChanged("DeleteIconImg"); }
            }
            public Visibility PageDeleteVisibility
            {
                get { return _deletePage ? Visibility.Visible : Visibility.Hidden; }
            }
            public BitmapImage DeleteIconImg
            {
                get { return _deletePage ? deleteIconOn : deleteIconOff; }
            }

            // Rotation
            private double _pageRotation = 0;
            public double PageRotation
            {
                get { return _pageRotation; }
                set { _pageRotation = value; NotifyPropertyChanged("PageRotation"); }
            }

            // Page number, file index and tagstring
            private int _pageNum;
            public int PageNum
            {
                get { return _pageNum; }
                set { _pageNum = value; NotifyPropertyChanged("PageNum"); NotifyPropertyChanged("PageNumStr"); NotifyPropertyChanged("TagStr"); }
            }
            private int _fileIdx = 0;
            public int FileIndex
            {
                get { return _fileIdx; }
                set { _fileIdx = value; NotifyPropertyChanged("FileIndex"); NotifyPropertyChanged("FileIdxStr"); NotifyPropertyChanged("TagStr"); NotifyPropertyChanged("PageNumStr"); }
            }
            public string TagStr
            {
                get { return _fileIdx.ToString() + "_" + _pageNum.ToString(); }
            }
            public string PageNumStr
            {
                get { return "Page " + _pageNum.ToString() + (_showFileNum ? (" of File " + (_fileIdx + 1).ToString()) : "");  }
            }
            public string FileIdxStr
            {
                get { return _fileIdx.ToString(); }
            }

            // Whether file number should be shown
            private bool _showFileNum = false;
            public bool ShowFileNum
            {
                set { _showFileNum = value; NotifyPropertyChanged("ShowFileNum"); NotifyPropertyChanged("PageNumStr"); }
            }

            // Position in the new document
            private int _newDocPageNum = 1;
            public int NewDocPageNum
            {
                get { return _newDocPageNum;  }
                set { _newDocPageNum = value; NotifyPropertyChanged("NewDocPageNum"); NotifyPropertyChanged("NewDocPageInfoStr"); }
            }
            public string NewDocPageInfoStr
            {
                get { return _newDocPageNum.ToString(); }
            }
        }

        #endregion

    }
}

