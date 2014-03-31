using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using System.Collections.ObjectModel;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.IO;

namespace RobsPdfEditor
{
    /// <summary>
    /// Interaction logic for OutputFilenames.xaml
    /// </summary>
    public partial class OutputFilenames : MetroWindow
    {
        public ObservableCollection<OutputFileRecord> _outputFileNameRecords = new ObservableCollection<OutputFileRecord>();
        public bool okClicked = false;

        public OutputFilenames(List<string> suggestedFileNames)
        {
            InitializeComponent();
            OutputFileNamesGrid.ItemsSource = _outputFileNameRecords;
            for (int i = 0; i < suggestedFileNames.Count; i++)
            {
                OutputFileRecord rec = new OutputFileRecord();
                rec.FileNumber = i + 1;
                rec.FileName = suggestedFileNames[i];
                _outputFileNameRecords.Add(rec);
            }
        }

        public List<string> GetOutputFileNames()
        {
            List<string> strs = new List<string>();
            foreach (OutputFileRecord rec in _outputFileNameRecords)
                strs.Add(rec.FileName);
            return strs;
        }

        private void btnSaveAsFormOk_Click(object sender, RoutedEventArgs e)
        {
            // Check file names
            bool bOneFails = false;
            foreach (OutputFileRecord rec in _outputFileNameRecords)
            {
                if (File.Exists(rec.FileName))
                {
                    bOneFails = true;
                    break;
                }
            }
            if (bOneFails)
            {
                // Ask the user if they are sure
                MessageDialog.MsgDlgRslt rslt = MessageDialog.Show("At least one output file already exists - overwrite?", "Yes", "No", "Cancel", null, this);
                if (rslt != MessageDialog.MsgDlgRslt.RSLT_YES)
                    return;
            }

            okClicked = true;
            Close();
        }

        private void btnSaveAsFormCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetFileName(object sender, RoutedEventArgs e)
        {
            OutputFileRecord outFileRec = (OutputFileRecord)OutputFileNamesGrid.SelectedItem;
            if (outFileRec == null)
                return;

            CommonSaveFileDialog cofd = new CommonSaveFileDialog("Save as file name");
            cofd.InitialDirectory = System.IO.Path.GetDirectoryName(outFileRec.FileName);
            cofd.DefaultFileName = outFileRec.FileName;
            cofd.DefaultDirectory = System.IO.Path.GetDirectoryName(outFileRec.FileName);
            cofd.Filters.Add(new CommonFileDialogFilter("PDF File", ".pdf"));
            CommonFileDialogResult result = cofd.ShowDialog(this);
            if (result == CommonFileDialogResult.Ok)
            {
                outFileRec.FileName = cofd.FileName;
            }

        }
    }

    public class OutputFileRecord : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        
        public int _fileNumber = 1;
        public int FileNumber
        {
            get { return _fileNumber; }
            set { _fileNumber = value; NotifyPropertyChanged("FileNumber"); }
        }
        public string _fileName = "";
        public string FileName
        {
            get { return _fileName; }
            set { _fileName = value; NotifyPropertyChanged("FileName"); }
        }
    }
}
