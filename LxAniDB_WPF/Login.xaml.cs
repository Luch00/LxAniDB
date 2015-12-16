using System.Windows;

namespace LxAniDB_WPF
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (txtUsername.Text.Length < 1 || txtPassword.Password.Length < 1)
            {
                lblWarn.Content = "FILL ALL FIELDS!";
            }
            else
            {
                Properties.Settings.Default.username = txtUsername.Text;
                Properties.Settings.Default.password = ObfuscatePW.EncryptString(txtPassword.SecurePassword);
                this.DialogResult = true;
            }
        }
    }
}