using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace LxAniDB_WPF
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();

            txtUsername.Text = Properties.Settings.Default.username;
            txtPassword.Password = ObfuscatePW.ToInsecureString(ObfuscatePW.DecryptString(Properties.Settings.Default.password));
            txtServer.Text = Properties.Settings.Default.remoteServer;
            txtLocal.Text = Properties.Settings.Default.localPort.ToString();
            txtRemote.Text = Properties.Settings.Default.remotePort.ToString();
            txtDelay.Text = Properties.Settings.Default.delay.ToString();
        }

        private void txtLocal_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private static bool IsTextAllowed(string text)
        {
            Regex regex = new Regex("[^0-9]+");
            return !regex.IsMatch(text);
        }

        private void txtRemote_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void txtDelay_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private void PastingHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            if (txtUsername.Text.Length < 1 || txtPassword.Password.Length < 1 || txtServer.Text.Length < 1 || txtLocal.Text.Length < 1 || txtRemote.Text.Length < 1 || txtDelay.Text.Length < 1)
            {
                lblWarn.Content = "Fill all fields!";
            }
            else
            {
                int local, remote, delay = 0;
                if (int.TryParse(txtLocal.Text, out local) && int.TryParse(txtRemote.Text, out remote) && int.TryParse(txtDelay.Text, out delay))
                {
                    if (local >= 1024 && local <= 65535 && remote >= 1024 && remote <= 65535)
                    {
                        Properties.Settings.Default.username = txtUsername.Text;
                        Properties.Settings.Default.remoteServer = txtServer.Text;
                        Properties.Settings.Default.localPort = local;
                        Properties.Settings.Default.remotePort = remote;
                        Properties.Settings.Default.delay = delay;
                        Properties.Settings.Default.password = ObfuscatePW.EncryptString(txtPassword.SecurePassword);
                        this.DialogResult = true;
                    }
                    else
                    {
                        lblWarn.Content = "Ports between 1024 and 65535";
                    }
                }
                else
                {
                    lblWarn.Content = "Local, remote, delay only numbers";
                }
            }
        }
    }
}