using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace LxAniDB_WPF
{
    /// <summary>
    /// Interaction logic for History.xaml
    /// </summary>
    public partial class History : Window
    {
        private BindingList<string> history;
        public History(ref BindingList<string> history)
        {
            InitializeComponent();
            this.history = history;
            listHistory.ItemsSource = this.history;
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            string[] delete = new string[listHistory.SelectedItems.Count];
            listHistory.SelectedItems.CopyTo(delete, 0);
            foreach (string item in delete)
            {
                this.history.Remove(item);
            }
        }

        private void btnClearAll_Click(object sender, RoutedEventArgs e)
        {
            this.history.Clear();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            listHistory.ItemsSource = null;
            this.history = null;
        }
    }
}
