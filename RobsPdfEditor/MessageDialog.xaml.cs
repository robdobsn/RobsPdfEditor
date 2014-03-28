using MahApps.Metro.Controls;
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

namespace RobsPdfEditor
{
    /// <summary>
    /// Interaction logic for MessageDialog.xaml
    /// </summary>
    public partial class MessageDialog : MetroWindow
    {
        public enum MsgDlgRslt
        {
            RSLT_NONE, RSLT_YES, RSLT_NO, RSLT_CANCEL
        }
        public MsgDlgRslt dlgResult = MsgDlgRslt.RSLT_NONE;
        private UIElement _fromElem;
        private Window _fromWin;

        public MessageDialog(string message, string yesButtonText, string noButtonText, string cancelButtonText, UIElement fromElem, Window fromWin)
        {
            InitializeComponent();
            txtMessage.Text = message;
            btnYes.Content = yesButtonText;
            btnYes.Visibility = (yesButtonText.Trim() != "") ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
            btnNo.Content = noButtonText;
            btnNo.Visibility = (noButtonText.Trim() != "") ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
            btnCancel.Content = cancelButtonText;
            btnCancel.Visibility = (cancelButtonText.Trim() != "") ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
            _fromElem = fromElem;
            _fromWin = fromWin;
            this.Owner = fromWin;
        }

        public static MsgDlgRslt Show(string message, string yesButtonText, string noButtonText, string cancelButtonText, UIElement fromElem, Window fromWin)
        {
            MessageDialog msgDlg = new MessageDialog(message, yesButtonText, noButtonText, cancelButtonText, fromElem, fromWin);
            msgDlg.ShowDialog();
            return msgDlg.dlgResult;
        }

        private void btnYes_Click(object sender, RoutedEventArgs e)
        {
            dlgResult = MsgDlgRslt.RSLT_YES;
            Close();
        }

        private void btnNo_Click(object sender, RoutedEventArgs e)
        {
            dlgResult = MsgDlgRslt.RSLT_NO;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            dlgResult = MsgDlgRslt.RSLT_CANCEL;
            Close();
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_fromElem != null)
            {
                var positionTransform = _fromElem.TransformToAncestor(_fromWin);
                var areaPosition = positionTransform.Transform(new Point(0, 0));
                Application curApp = Application.Current;
                Window mainWindow = curApp.MainWindow;
                this.Left = mainWindow.Left + areaPosition.X + 100 - this.ActualWidth;
                this.Top = mainWindow.Top + areaPosition.Y + 150;
            }
            else
            {
                Left = _fromWin.Left + (_fromWin.ActualWidth - ActualWidth) / 2;
                Top = _fromWin.Top + (_fromWin.ActualHeight - ActualHeight) / 2;
            }
        }

    }
}

