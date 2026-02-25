using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BluemapConcat
{
    /// <summary>
    /// FromPosDialog.xaml 的交互逻辑
    /// </summary>
    public partial class FromPosDialog : Window
    {
        public FromPosDialog()
        {
            InitializeComponent();
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
