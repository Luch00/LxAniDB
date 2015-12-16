using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using RHash;

namespace LxAniDB_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {

        private readonly BindingList<string> files = new BindingList<string>();
        private BindingList<string> history = new BindingList<string>();
        private Action cancelWork;
        private string sessionKey = string.Empty;
        private string currentPacket = string.Empty;
        private DateTime lastSent = DateTime.UtcNow;
        private readonly DispatcherTimer logoutTimer;
        private readonly TaskScheduler context = TaskScheduler.FromCurrentSynchronizationContext();
        private bool working = false;

        public event PropertyChangedEventHandler PropertyChanged;

        private readonly UdpClient udpClient;

        public MainWindow()
        {
            this.udpClient = new UdpClient(Properties.Settings.Default.localPort);
            // 10 second timeouts for sending and receiving
            this.udpClient.Client.SendTimeout = 10000;
            this.udpClient.Client.ReceiveTimeout = 10000;
            logoutTimer = new DispatcherTimer();
            logoutTimer.Tick += logoutTimer_Tick;
            logoutTimer.Interval = new TimeSpan(0, 20, 0);
            InitializeComponent();
            ReadHistory();
        }

        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public BindingList<string> Files
        {
            get { return files; }
        }

        private void logoutTimer_Tick(object sender, EventArgs e)
        {
            if (this.sessionKey != string.Empty)
            {
                SendPacket("LOGOUT s=" + this.sessionKey);
                logoutTimer.Stop();
            }
        }

        private void HasUser()
        {
            if (Properties.Settings.Default.username == "" || Properties.Settings.Default.password == "")
            {
                Login loginDialog = new Login {Owner = this};
                loginDialog.ShowDialog();
                if (loginDialog.DialogResult.HasValue && loginDialog.DialogResult.Value)
                {
                    Properties.Settings.Default.Save();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }
        }

        private void btnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Video Files (*.mkv, *.mp4, *.avi) | *.mkv; *.mp4; *.avi",
                Title = "Select files..."
            };

            bool? result = dlg.ShowDialog();

            if (result == true)
            {
                foreach (string file in dlg.FileNames)
                {
                    if (!CheckHistory(Path.GetFileName(file)))
                    {
                        files.Add(Path.GetFullPath(file));
                        RaisePropertyChanged("Files");
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            HasUser();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsDialog = new Settings {Owner = this};
            settingsDialog.ShowDialog();
            if (settingsDialog.DialogResult.HasValue && settingsDialog.DialogResult.Value)
            {
                Properties.Settings.Default.Save();
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            this.files.Clear();
        }

        private void btnHash_Click(object sender, RoutedEventArgs e)
        {
            if (files.Count > 0 && this.cancelWork == null)
            {
                StartWork();
            }
            else if(this.cancelWork != null)
            {
                this.cancelWork();
            }
        }

        private async void  StartWork()
        {
            this.btnAddFiles.IsEnabled = false;
            this.btnClear.IsEnabled = false;
            this.checkWatched.IsEnabled = false;
            this.comboBox.IsEnabled = false;
            this.btnSettings.IsEnabled = false;
            this.btnHistory.IsEnabled = false;
            this.working = true;
            this.btnHash.Content = "Stop Hashing";

            try
            {
                var cancellationTokenSource = new CancellationTokenSource();

                this.cancelWork = () =>
                {
                    cancellationTokenSource.Cancel();
                };

                var token = cancellationTokenSource.Token;

                string viewed = "0";
                if (checkWatched.IsChecked == true)
                {
                    viewed = "1";
                }
                string state = "0";
                int index = comboBox.SelectedIndex;
                if (index == 0)
                {
                    state = "1";
                }
                else if (index == 1)
                {
                    state = "2";
                }
                else if (index == 2)
                {
                    state = "3";
                }

                var progress = new Progress<int>(i => this.progressBar.Value = i);
                await Task.Run(() => DoWork(token, progress, viewed, state), token);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            this.files.Clear();
            this.btnAddFiles.IsEnabled = true;
            this.btnClear.IsEnabled = true;
            this.checkWatched.IsEnabled = true;
            this.comboBox.IsEnabled = true;
            this.btnHistory.IsEnabled = true;
            this.btnSettings.IsEnabled = true;
            this.working = false;
            this.btnHash.Content = "Start Hashing";
            this.cancelWork = null;
        }

        private void DoWork(CancellationToken token, IProgress<int> progress, string viewed, string state)
        {
            foreach (string file in files)
            {
                if (token.IsCancellationRequested)
                {
                    WriteLog("CANCELLED");
                    return;
                }
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    double size = fs.Length;
                    StringBuilder sb = new StringBuilder();
                    string finalHash = string.Empty;
                    if (size <= 9728000)
                    {
                        byte[] data = new byte[fs.Length];
                        fs.Read(data, 0, (int)size);
                        finalHash = Hasher.GetHashForMsg(data, HashType.MD4);
                    }
                    else
                    {
                        // MD4 hash of 9500KB chunk
                        double readBytes = 0;
                        double bufferSize = 9728000;
                        while (readBytes < size)
                        {
                            if (token.IsCancellationRequested)
                            {
                                WriteLog("CANCELLED");
                                return;
                            }
                            if (readBytes + bufferSize > size)
                            {
                                bufferSize = size - readBytes;
                            }
                            byte[] data = new byte[(int)bufferSize];
                            fs.Read(data, 0, (int)bufferSize);
                            string hash = Hasher.GetHashForMsg(data, HashType.MD4);
                            sb.Append(hash);
                            data = null;

                            // Calculate progress % and report to progressbar
                            readBytes += bufferSize;
                            double p = (readBytes / size) * 100;
                            progress.Report((int)Math.Truncate(p));
                        }
                        finalHash = Hasher.GetHashForMsg(StringToByteArray(sb.ToString()), HashType.MD4);
                        //WriteLog(finalHash);
                    }
                    if (token.IsCancellationRequested)
                    {
                        WriteLog("CANCELLED");
                        return;
                    }
                    LoginSendPacket("MYLISTADD size=" + size + "&ed2k=" + finalHash + "&state=" + state + "&viewed=" + viewed);
                    Task.Factory.StartNew(() =>
                        {
                            if (history.Count >= 100)
                            {
                                history.RemoveAt(0);
                                history.Add(Path.GetFileName(file));
                            }
                            else
                            {
                                history.Add(Path.GetFileName(file));
                            }
                        },CancellationToken.None, TaskCreationOptions.None, context);
                }
            }
        }

        public static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        private void LoginSendPacket(string packet)
        {
            if (sessionKey == string.Empty)
            {
                // Login if not logged in yet
                WriteLog("LOGIN...");
                string s = "AUTH user=" + Properties.Settings.Default.username + "&pass=" + ObfuscatePW.ToInsecureString(ObfuscatePW.DecryptString(Properties.Settings.Default.password)) + "&protover=3&client=lxanidb&clientver=1";
                if(!SendPacket(s))
                {
                    return;
                }
                logoutTimer.Start();
            }
            SendPacket(packet + "&s=" + sessionKey);
        }

        private bool SendPacket(string packet)
        {   
            if (logoutTimer.IsEnabled)
            {
                logoutTimer.Stop();
                logoutTimer.Start();
            }
            TimeSpan elapsed = DateTime.UtcNow - this.lastSent;
            if (elapsed.TotalSeconds < Properties.Settings.Default.delay)
            {
                Thread.Sleep(TimeSpan.FromSeconds(Properties.Settings.Default.delay - elapsed.TotalSeconds));
            }
            this.currentPacket = packet;
            try
            {
                this.udpClient.Connect(Properties.Settings.Default.remoteServer, Properties.Settings.Default.remotePort);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, Properties.Settings.Default.localPort);

                byte[] content = Encoding.ASCII.GetBytes(packet);
                this.lastSent = DateTime.UtcNow;
                this.udpClient.Send(content, content.Length);
                byte[] response = udpClient.Receive(ref endPoint);
                if (response.Length > 0)
                {
                    string reply = Encoding.ASCII.GetString(response);
                    CheckMessage(reply);
                }
                return true;
            }
            catch (Exception ex)
            {
                this.cancelWork();
                WriteLog("SOMETHING WENT WRONG, TRY AGAIN LATER");
                WriteLog("MESSAGE: " + ex.Message);
                return false;
            }
        }

        private void CheckMessage(string s)
        {
            if (s.Length > 0)
            {
                string[] split = s.Split(' ');
                switch (split[0])
                {
                    case "200":
                    case "201":
                        this.sessionKey = split[1];
                        WriteLog(MessageBuilder(split, 2));
                        break;
                    case "203":
                        this.sessionKey = string.Empty;
                        WriteLog(MessageBuilder(split, 1));
                        //this.cancelWork();
                        break;
                    case "210":
                    case "310":
                    case "311":
                    case "320":
                    case "411":
                        WriteLog(MessageBuilder(split, 1));
                        break;
                    case "322":
                    case "330":
                    case "350":
                    case "403":
                    case "500":
                    case "502":
                    case "503":
                    case "504":
                    case "600":
                    case "601":
                        WriteLog(MessageBuilder(split, 1));
                        this.cancelWork();
                        break;
                    case "501":
                    case "506":
                        this.sessionKey = string.Empty;
                        WriteLog(MessageBuilder(split, 1));
                        SendPacket(currentPacket);
                        break;
                    default:
                        WriteLog(MessageBuilder(split, 0));
                        this.cancelWork();
                        break;
                }
            }
        }

        private static string MessageBuilder(string[] parts, int index)
        {
            string message = string.Empty;
            for (int i = index; i < parts.Length; i++)
            {
                message += parts[i];
                if ((i + 1) < parts.Length)
                {
                    message += " ";
                }
            }
            return message;
        }

        private void WriteLog(string msg)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            msg = msg.Replace("\n", " ");
            if (!msgLog.Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => this.msgLog.AppendText("<" + time + ">" + msg + "\n"));
                Dispatcher.Invoke(() => this.msgLog.ScrollToEnd());
            }
            else
            {
                this.msgLog.AppendText("<" + time + ">" + msg + "\n");
                this.msgLog.ScrollToEnd();
            }
        }

        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            History historyDialog = new History(ref history) {Owner = this};
            historyDialog.ShowDialog();
            historyDialog = null;
        }

        private void ReadHistory()
        {
            if (!File.Exists(Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB\History.xml")))
                return;

            XmlSerializer serializer = new XmlSerializer(typeof(BindingList<string>));
            StreamReader reader = new StreamReader(Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB\History.xml"));
            history = (BindingList<string>)serializer.Deserialize(reader);
            reader.Close();
        }

        private void SaveHistory()
        {
            if (!Directory.Exists(Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB")))
            {
                Directory.CreateDirectory(Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB"));
            }
            XmlSerializer serializer = new XmlSerializer(typeof(BindingList<string>));
            TextWriter writer = new StreamWriter(Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB\History.xml"));
            serializer.Serialize(writer, history);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveHistory();
        }

        private void listBox_Drop(object sender, DragEventArgs e)
        {
            HandleDrop(e);
        }

        private void HandleDrop(DragEventArgs e)
        {
            if (!working && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] dropFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in dropFiles)
                {
                    string ext = Path.GetExtension(file);
                    if (ext == ".mkv" || ext == ".avi" || ext == ".mp4")
                    {
                        if (!CheckHistory(Path.GetFileName(file)))
                        {
                            this.files.Add(file);
                            RaisePropertyChanged("Files");
                        }
                    }
                } 
            }
        }

        private bool CheckHistory(string name)
        {
            return history.Contains(name);
        }
    }
}
