using System.ComponentModel;
using System.Windows;

namespace LxAniDB_WPF
{
    /// <summary>
    /// Interaction logic for History.xaml
    /// </summary>
    public partial class History
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