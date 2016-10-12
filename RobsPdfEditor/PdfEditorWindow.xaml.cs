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
    /// Interaction logic for PdfEditorWindow.xaml
    /// </summary>
    public partial class PdfEditorWindow
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private ObservableCollection<PdfPageInfo> _pdfPageList = new ObservableCollection<PdfPageInfo>();
        public static BitmapImage splitIconOff;
        public static BitmapImage splitIconOn;
        public static BitmapImage deleteIconOff;
        public static BitmapImage deleteIconOn;
        const int THUMBNAIL_HEIGHT = 600;
        const int POINTS_PER_INCH = 80;
        private PdfRasterizer _pdfRasterizer;
        private BackgroundWorker _bwThreadForPages;
        private List<string> _curFileNames = new List<string>();
        private int _curBackgroundLoadingFileIdx = 0;
        private string _windowTitle = "Rob's PDF Editor";
        private bool _changesMade = false;
        private bool _bRunningEmbedded = false;
        public delegate void SaveCompleteCallback(string originalFileName, List<string> fileNamesSaved);
        private SaveCompleteCallback _saveCompleteCallback;
        private string _saveToFolder = "";

        public PdfEditorWindow()
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
            _bwThreadForPages.ProgressChanged += new ProgressChangedEventHandler(AddPages_ProgressChanged);
            _bwThreadForPages.RunWorkerCompleted += new RunWorkerCompletedEventHandler(AddPages_Completed);
        }

        public void OpenEmbeddedPdfEditor(string fileName, SaveCompleteCallback saveCompleteCallback, string saveToFolder)
        {
            btnOpenFile.IsEnabled = false;
            btnReplaceFile.Visibility = System.Windows.Visibility.Hidden;
            _bRunningEmbedded = true;
            _saveCompleteCallback = saveCompleteCallback;
            _saveToFolder = saveToFolder;
            OpenFile(fileName);
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

        private void Rotate180Icon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            int pageIdx = ExtractFileAndPageIdxs(sender);
            if (pageIdx < 0)
                return;
            RotatePage(pageIdx, 180);
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

        private void PdfListViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            PdfListViewer.ScrollToHorizontalOffset(PdfListViewer.HorizontalOffset - e.Delta);
        }

        private void MagnifyIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            int pageIdx = ExtractFileAndPageIdxs(sender);
            if (pageIdx < 0)
                return;
            if ((pageIdx >= 0) && (pageIdx < _pdfPageList.Count))
            {
                popupPageMagnifyImage.Source = _pdfPageList[pageIdx].ThumbBitmap;
                popupPageMagnify.PlacementTarget = (UIElement)sender;
                if (!popupPageMagnify.IsOpen)
                    popupPageMagnify.IsOpen = true;
            }
        }

        private void btnRotateAllACWFile_Click(object sender, RoutedEventArgs e)
        {
            RotateAllPages(-90);
        }

        private void btnRotateAllCWFile_Click(object sender, RoutedEventArgs e)
        {
            RotateAllPages(90);
        }

        private void RotateAllPages(int angle)
        {
            for (int pageIdx = 0; pageIdx < _pdfPageList.Count; pageIdx++)
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
            // Check not busy
            if (CheckIfThreadBusy())
                return;

            if (_curFileNames.Count < 1)
                return;

            // Find how many output files
            int fileNum = 1;
            int pageNum = 1;
            int pageTotal = 0;
            GetFileAndPageOfLastOutDoc(out fileNum, out pageNum, out pageTotal, true);

            // Generate suggested file names
            List<string> outputFileNames = new List<string>();
            for (int fileIdx = 0; fileIdx < fileNum; fileIdx++)
            {
                string outFileName = GenOutFileName(_curFileNames[0], fileIdx);
                outputFileNames.Add(outFileName);
            }

            // Display save-as dialog (unless running embedded)
            if (!_bRunningEmbedded)
            {
                OutputFilenames outFileNamesForm = new OutputFilenames(outputFileNames);
                outFileNamesForm.ShowDialog();
                if (!outFileNamesForm.okClicked)
                    return;
                outputFileNames = outFileNamesForm.GetOutputFileNames();
            }

            bool bSaveOk = false;
            using (new WaitCursor())
            {
#if TEST_SAVING_OF_PDF
                for (int i = 0; i < outputFileNames.Count; i++ )
                {
                    outputFileNames[i] = @"c:\users\rob_2\documents\1-testpdfedit\testf" + i.ToString() + ".pdf";
                    try
                    {
                        if (File.Exists(outputFileNames[i]))
                            File.Delete(outputFileNames[i]);
                    }
                    catch
                    {
                        MessageDialog.Show("Can't delete one or more output files", "", "", "Ok", null, this);
                        return;
                    }
                bSaveOk = SaveFiles(outputFileNames);
            if (!bSaveOk)
            {
                MessageDialog.Show("Problem saving files, check the error log", "", "", "Ok", null, this);
                return;
            }
                }
                return;
#endif
                bSaveOk = SaveFiles(outputFileNames);
            }
            if (!bSaveOk)
            {
                MessageDialog.Show("Problem saving files, check the error log", "", "", "Ok", null, this);
                return;
            }

            // If running embedded then call the callback to say work is done
            if (_bRunningEmbedded)
            {
                _saveCompleteCallback(_curFileNames[0], outputFileNames);
                Close();
            }
        }

        private void btnReplaceFile_Click(object sender, RoutedEventArgs e)
        {
            // Need to save to a temporary file name and then swap over
            if (CheckIfThreadBusy())
                return;

            if (_curFileNames.Count < 1)
                return;

            // Get info on output files
            int fileNum = 1;
            int pageNum = 1;
            int pageTotal = 0;
            GetFileAndPageOfLastOutDoc(out fileNum, out pageNum, out pageTotal, true);

            // Check there is only one file
            if ((fileNum != 1) || (pageTotal == 0))
                return;

            // Save to temporary file
            string tempFileName = System.IO.Path.GetTempFileName();
            List<string> outFileNames = new List<string>();
            outFileNames.Add(tempFileName);
            bool bSaveOk = false;
            using (new WaitCursor())
                bSaveOk = SaveFiles(outFileNames);
            if (!bSaveOk)
            {
                MessageDialog.Show("Problem saving file, check the error log", "Problem", "", "Ok", null, this);
                return;
            }

            // Replace original file with 
            try
            {
                System.IO.File.Copy(tempFileName, _curFileNames[0], true);
            }
            catch (Exception excp)
            {
                logger.Error("Failed to replace original file {0} excp {1}", _curFileNames[0], excp.Message);
                MessageDialog.Show("Problem replacing file, check the error log", "Problem", "", "Ok", null, this);                
            }

            // Delete temp file
            try
            {
                System.IO.File.Delete(tempFileName);
            }
            catch (Exception excp)
            {
                logger.Error("Failed to delete temp file {0} excp {1}", tempFileName, excp.Message);
            }
        }

        #endregion

        #region Save Files

        private bool SaveFiles(List<string> outputFileNames)
        {
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
                    return false;
                }
            }

            // Check an output file isn't an input file
            foreach (string fileName in _curFileNames)
            {
                if (outputFileNames.Contains(fileName))
                {
                    MessageDialog md = new MessageDialog("At least one output file is an input file - cannot continue", "Ok", "", "", null, this);
                    md.Show();
                    return false;
                }
            }

            // Loop through PDFs to be created
            int pdfOutFileIdx = 0;
            bool filesSavedOk = true;
            for (int pdfPageListIdx = 0; pdfPageListIdx < _pdfPageList.Count; pdfPageListIdx++)
            {
                // Skip deleted pages
                if (_pdfPageList[pdfPageListIdx].DeletePage)
                    continue;

                // Output file name
                string outFileName = outputFileNames[pdfOutFileIdx++];

                // Read the pages and process
                try
                {
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
                                        iTextSharp.text.Rectangle newPageSize = pdfReaders[fileIdx].GetPageSizeWithRotation(pageNum);
                                        if ((int)_pdfPageList[pdfPageListIdx].PageRotation == 90 || (int)_pdfPageList[pdfPageListIdx].PageRotation == 270)
                                            newPageSize = new iTextSharp.text.Rectangle(pageSize.Height, pageSize.Width);
                                        inDoc.SetPageSize(newPageSize);
                                        inDoc.NewPage();

                                        // Get original page
                                        PdfImportedPage importedPage = outputWriter.GetImportedPage(pdfReaders[fileIdx], pageNum);

                                        // Handle rotation
                                        // Fixed based on information in this question - http://stackoverflow.com/questions/3579058/rotating-pdf-in-c-sharp-using-itextsharp
                                        switch (pageRotation)
                                        {
                                            case 0:
                                            default:
                                                outputWriter.DirectContent.AddTemplate(importedPage, 1f, 0, 0, 1f, 0, 0);
                                                break;

                                            case 90:
                                                outputWriter.DirectContent.AddTemplate(importedPage, 0, -1f, 1f, 0, 0, pageSize.Width);
                                                break;

                                            case 180:
                                                outputWriter.DirectContent.AddTemplate(importedPage, -1f, 0, 0, -1f, pageSize.Width, pageSize.Height);
                                                break;

                                            case 270:
                                                outputWriter.DirectContent.AddTemplate(importedPage, 0, 1f, -1f, 0, pageSize.Height, 0);
                                                break;
                                        }

                                        Console.WriteLine("FileIdx {0}, PdfPageListIdx {1}, PageNum {2}, GetPageRotation {3}, userRotation {4}, pageSizeWidth {5},  pageSizeHeight {6},  pageSizeRotation {7}, newSizeWidth {8},  newSizeHeight {9},  newSizeRotation {10}, finalRotation {11}", 
                                                      fileIdx, pdfPageListIdx, pageNum, pdfReaders[fileIdx].GetPageRotation(pageNum), (int)_pdfPageList[pdfPageListIdx].PageRotation,
                                                      pdfReaders[fileIdx].GetPageSizeWithRotation(pageNum).Width, pdfReaders[fileIdx].GetPageSizeWithRotation(pageNum).Height, pdfReaders[fileIdx].GetPageSizeWithRotation(pageNum).Rotation,
                                                      newPageSize.Width, newPageSize.Height, newPageSize.Rotation, pageRotation);
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
                catch (Exception excp)
                {
                    logger.Error("Exception saving file {0} excp {1}", outFileName, excp.Message);
                    filesSavedOk = false;
                    break;
                }
            }

            try
            {
                foreach (PdfReader pdfReader in pdfReaders)
                    pdfReader.Close();
                foreach (Stream strm in inPDFStreams)
                    strm.Close();
            }
            catch (Exception excp)
            {
                logger.Error("Exception closing pdfReaders excp {0}", excp.Message);
            }

            _changesMade = !filesSavedOk;
            UpdateWindowTitle();

            return filesSavedOk;
        }

        #endregion

        #region Open / Add Files

        public void OpenFile(string fileName)
        {
            if (CheckIfThreadBusy())
            {
                MessageBox.Show("ThreadBusy");
                return;
            }

            // Add pages to list of pages
            _pdfPageList.Clear();
            _curFileNames.Clear();
            _curFileNames.Add(fileName);
            _curBackgroundLoadingFileIdx = 0;
            _changesMade = false;
            _bwThreadForPages.RunWorkerAsync();
            UpdateWindowTitle();
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
            _bwThreadForPages.RunWorkerAsync();
            UpdateWindowTitle();
        }

        private void AddPages_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            try
            {
                _pdfRasterizer = new PdfRasterizer(_curFileNames[_curBackgroundLoadingFileIdx], POINTS_PER_INCH);
            }
            catch(Exception excp)
            {
                logger.Error("PDF Editor requires Ghostscript {0}", excp.Message);
                MessageBox.Show("PDF Editor requires Ghostscript to be installed");
                return;
            }

            try
            {
            int startNewPageNum = 1;
            int startNewFileNum = 1;
            int pageTotal = 0;
            GetFileAndPageOfLastOutDoc(out startNewFileNum, out startNewPageNum, out pageTotal);

                // Extract page images
            for (int i = 0; i < _pdfRasterizer.NumPages(); i++)
            {
                if ((worker.CancellationPending == true))
                {
                    e.Cancel = true;
                    break;
                }

                    System.Drawing.Image pageImg = _pdfRasterizer.GetPageImage(i + 1, false);

                    object[] args = new object[4];
                    args[0] = i;
                    args[1] = startNewPageNum;
                    args[2] = startNewFileNum;
                    args[3] = pageImg;

                    this.Dispatcher.BeginInvoke((Action<int, int, int, System.Drawing.Image>)delegate(int pageIdx, int startNewPgNum, int startNewFilNum, System.Drawing.Image pagImg)
                {
                        BitmapImage bitmap = ConvertToBitmap(pagImg);
                    PdfPageInfo pgInfo = new PdfPageInfo();
                        pgInfo.PageNum = pageIdx + 1;
                    pgInfo.FileIndex = _curBackgroundLoadingFileIdx;
                    pgInfo.ThumbBitmap = bitmap;
                    pgInfo.SplitAfter = false;
                    pgInfo.DeletePage = false;
                    pgInfo.PageRotation = 0;
                    pgInfo.ShowFileNum = (_curBackgroundLoadingFileIdx > 0);
                        pgInfo.NewDocPageNum = pageIdx + startNewPgNum;
                        pgInfo.NewDocFileNum = startNewFilNum;
                    _pdfPageList.Add(pgInfo);
                    }, args);
                Thread.Sleep(50);
                (sender as BackgroundWorker).ReportProgress(i * 100 / _pdfRasterizer.NumPages(), null);
            }
        }
            catch(Exception excp)
            {
                logger.Error("PDF Editor AddPages_DoWork failed excp {0}", excp.Message);
            }
            finally
            {
                // Close file
                _pdfRasterizer.Close();
                _pdfRasterizer = null;
            }
        }

        private void AddPages_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            UpdateWindowTitle();
        }

        private void AddPages_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateWindowTitle();
        }

        #endregion

        #region Utility Functions

        private class WaitCursor : IDisposable
        {
            private Cursor _previousCursor;

            public WaitCursor()
            {
                _previousCursor = Mouse.OverrideCursor;

                Mouse.OverrideCursor = Cursors.Wait;
            }

            #region IDisposable Members

            public void Dispose()
            {
                Mouse.OverrideCursor = _previousCursor;
            }

            #endregion
        }

        private void UpdateWindowTitle(bool changesMade = false)
        {
            if (changesMade)
                _changesMade = true;
            RobsPDFEditor.Title = _windowTitle + ((_curFileNames.Count > 0) ? "" : (" - " + System.IO.Path.GetFileName(_curFileNames[0]) + (_changesMade ? " *" : "")));

            // In and out file status
            bool isBusy = _bwThreadForPages.IsBusy;
            int numOutFiles = 1;
            int pageNum = 1;
            int pageTotal = 0;
            GetFileAndPageOfLastOutDoc(out numOutFiles, out pageNum, out pageTotal, false);
            if (_pdfPageList.Count == 0)
                numOutFiles = 0;
            curInFileInfo.Content = "Input: " + _curFileNames.Count.ToString() + " file" + (_curFileNames.Count == 1 ? "" : "s") + ", " + _pdfPageList.Count.ToString() + " page" + (_pdfPageList.Count == 1 ? "" : "s");
            if (isBusy)
                curOutFileInfo.Content = "Busy ...";
            else
                curOutFileInfo.Content = "Output: " + numOutFiles.ToString() + " file" + (numOutFiles == 1 ? "" : "s") + ", " + pageTotal.ToString() + " page" + (pageTotal == 1 ? "" : "s");

            // Button enables
            btnAddFile.IsEnabled = (_curFileNames.Count > 0) & !isBusy;
            btnSaveFile.IsEnabled = (numOutFiles > 0) & !isBusy;
            btnReplaceFile.IsEnabled = (numOutFiles == 1) & !isBusy & !_bRunningEmbedded;
            btnRotateAllACWFile.IsEnabled = (_pdfPageList.Count > 0) & !isBusy;
            btnRotateAllCWFile.IsEnabled = (_pdfPageList.Count > 0) & !isBusy;
        }

        private string GenOutFileName(string curFileName, int fileIdx)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(curFileName) + "_" + fileIdx.ToString() + System.IO.Path.GetExtension(curFileName);
            string destFolder = System.IO.Path.GetDirectoryName(curFileName);
            if (_saveToFolder != "")
                destFolder = _saveToFolder;
            return System.IO.Path.Combine(destFolder, fileName);
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
            if (img == null)
                return new BitmapImage();

            try
            {
            MemoryStream ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            System.Windows.Media.Imaging.BitmapImage bImg = new System.Windows.Media.Imaging.BitmapImage();
            bImg.BeginInit();
            bImg.StreamSource = new MemoryStream(ms.ToArray());
            bImg.EndInit();
            return bImg;
        }
            catch (Exception excp)
            {
                logger.Error("Failed to convert bitmap {0}", excp.Message);
            }
            return new BitmapImage();
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
            int fileNum = 1;
            int pageNum = 1;
            int pageTotal = 0;
            GetFileAndPageOfLastOutDoc(out fileNum, out pageNum, out pageTotal, true);
        }


        private void GetFileAndPageOfLastOutDoc(out int fileNum, out int pageNum, out int pageTotal, bool rewrite = false)
        {
            pageTotal = 0;
            fileNum = 1;
            pageNum = 1;
            for (int pageIdx = 0; pageIdx < _pdfPageList.Count; pageIdx++ )
            {
                PdfPageInfo ppi = _pdfPageList[pageIdx];
                if (rewrite)
                {
                    if (ppi.NewDocPageNum != pageNum)
                        ppi.NewDocPageNum = pageNum;
                    if (ppi.NewDocFileNum != fileNum)
                        ppi.NewDocFileNum = fileNum;
                }
                if (ppi.SplitAfter && pageIdx != _pdfPageList.Count-1)
                {
                    fileNum++;
                    pageNum = 1;
                    if (!ppi.DeletePage)
                        pageTotal++;
                }
                else
                {
                    if (!ppi.DeletePage)
                    {
                        pageNum++;
                        pageTotal++;
                    }
                }
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
                set { _splitAfter = value; NotifyPropertyChanged("SplitAfter"); NotifyPropertyChanged("SplitLineVisibility"); NotifyPropertyChanged("SplitIconImg"); NotifyPropertyChanged("NewDocPageInfoStr"); }
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
                set { _deletePage = value; NotifyPropertyChanged("DeletePage"); NotifyPropertyChanged("PageDeleteVisibility"); NotifyPropertyChanged("DeleteIconImg"); NotifyPropertyChanged("NewDocPageInfoStr"); }
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
                get { return "Original Page " + _pageNum.ToString() + (_showFileNum ? (" of File " + (_fileIdx + 1).ToString()) : "");  }
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
            private int _newDocFileNum = 0;
            public int NewDocFileNum
            {
                get { return _newDocFileNum; }
                set { _newDocFileNum = value; NotifyPropertyChanged("NewDocFileNum"); NotifyPropertyChanged("NewDocPageInfoStr"); }
            }
            public string NewDocPageInfoStr
            {
                get 
                {
                    if (DeletePage)
                        return "Deleted";
                    return "New Page " + _newDocPageNum.ToString() + " in File " + _newDocFileNum.ToString(); 
                }
            }
        }

        #endregion

    }
}

