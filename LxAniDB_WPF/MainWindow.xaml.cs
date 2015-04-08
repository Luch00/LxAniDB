using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
using System.Windows.Threading;
using System.Xml.Serialization;

namespace LxAniDB_WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private BindingList<string> files = new BindingList<string>();
        private BindingList<string> history = new BindingList<string>();
        //private bool running = false;
        private Action cancelWork;
        private string sessionKey = string.Empty;
        private string currentPacket = string.Empty;
        private TimeSpan lastSent = DateTime.Now.TimeOfDay;
        private DispatcherTimer logoutTimer;
        private TaskScheduler context = TaskScheduler.FromCurrentSynchronizationContext();

        UdpClient udpClient;

        public MainWindow()
        {
            this.udpClient = new UdpClient(Properties.Settings.Default.localPort);
            logoutTimer = new DispatcherTimer();
            logoutTimer.Tick += new EventHandler(logoutTimer_Tick);
            logoutTimer.Interval = new TimeSpan(0, 20, 0);
            InitializeComponent();
            listBox.ItemsSource = files;
            ReadHistory();
        }

        private void logoutTimer_Tick(object sender, EventArgs e)
        {
            if (this.sessionKey != string.Empty)
            {
                SendPacket("LOGOUT s=" + this.sessionKey);
            }
        }

        private void HasUser()
        {
            if (Properties.Settings.Default.username == "" ||Properties.Settings.Default.password == "")
            {
                Login loginDialog = new Login();
                loginDialog.Owner = this;
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
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Multiselect = true;
            dlg.Filter = "Video Files (*.mkv, *.mp4, *.avi) | *.mkv; *.mp4; *.avi";
            dlg.Title = "Select files...";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                foreach (string file in dlg.FileNames)
                {
                    if (!CheckHistory(System.IO.Path.GetFileName(file)))
                    {
                        files.Add(System.IO.Path.GetFullPath(file));
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
            Settings settingsDialog = new Settings();
            settingsDialog.Owner = this;
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

                var progress = new Progress<int>((i) => this.progressBar.Value = i);
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
            this.btnHash.Content = "Start Hashing";
            this.cancelWork = null;
        }

        private void DoWork(CancellationToken token, IProgress<int> progress, string viewed, string state)
        {
            foreach (string file in files)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    double size = fs.Length;
                    StringBuilder sb = new StringBuilder();
                    if (size <= 9728000)
                    {
                        byte[] data = new byte[fs.Length];
                        fs.Read(data, 0, (int)size);
                        byte[] hash = MD4Context.GetDigest(data).ToArray();
                    }
                    else
                    {
                        // MD4 hash of 9500KB chunk
                        double totalLength = 0;
                        double readBytes = 0;
                        double bufferSize = 9728000;
                        List<byte[]> hashList = new List<byte[]>();

                        while (readBytes < size)
                        {
                            if (token.IsCancellationRequested)
                            {
                                return;
                            }
                            if (readBytes + bufferSize > size)
                            {
                                bufferSize = size - readBytes;
                            }
                            byte[] data = new byte[(int)bufferSize];
                            fs.Read(data, 0, (int)bufferSize);

                            byte[] hash = MD4Context.GetDigest(data).ToArray();
                            hashList.Add(hash);
                            totalLength += hash.Length;
                            readBytes += bufferSize;
                            double p = (readBytes / size) * 100;
                            progress.Report((int)Math.Truncate(p));

                        }
                        byte[] total = new byte[(int)totalLength];
                        int offset = 0;
                        foreach (byte[] b in hashList)
                        {
                            Buffer.BlockCopy(b, 0, total, offset, b.Length);
                            offset += b.Length;
                        }

                        byte[] totalHash = MD4Context.GetDigest(total).ToArray();
                        
                        foreach (byte b in totalHash)
                        {
                            sb.Append(b.ToString("x2"));
                        }
                        hashList.Clear();
                    }
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    BuildPacketString("MYLISTADD size=" + size + "&ed2k=" + sb.ToString() + "&state=" + state + "&viewed=" + viewed);
                    Task.Factory.StartNew(() =>
                        {
                            if (history.Count >= 20)
                            {
                                history.RemoveAt(0);
                                history.Add(System.IO.Path.GetFileName(file));
                            }
                            else
                            {
                                history.Add(System.IO.Path.GetFileName(file));
                            }
                        },CancellationToken.None, TaskCreationOptions.None, context);
                }
            }
        }

        private void BuildPacketString(string v)
        {
            if (sessionKey == string.Empty)
            {
                // Login if not logged in yet
                WriteLog("Login...");
                string s = "AUTH user=" + Properties.Settings.Default.username + "&pass=" + ObfuscatePW.ToInsecureString(ObfuscatePW.DecryptString(Properties.Settings.Default.password)) + "&protover=3&client=lxanidb&clientver=1";
                //WriteLog("Packet: " + s);
                SendPacket(s);
                logoutTimer.Start();
            }
            SendPacket(v + "&s=" + sessionKey);
            // Send packet with session key
            // Receive reply
            // if no reply, cancel hashing
        }

        private void SendPacket(string packet)
        {
            logoutTimer.Stop();
            logoutTimer.Start();
            if ((DateTime.Now.TimeOfDay.TotalSeconds - lastSent.TotalSeconds) < Properties.Settings.Default.delay)
            {
                WriteLog("Delay");
                Thread.Sleep(Properties.Settings.Default.delay * 1000);
            }
            this.currentPacket = packet;
            this.udpClient.Connect(Properties.Settings.Default.remoteServer, Properties.Settings.Default.remotePort);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, Properties.Settings.Default.localPort);

            byte[] content = Encoding.ASCII.GetBytes(packet);
            this.lastSent = DateTime.Now.TimeOfDay;
            this.udpClient.Send(content, content.Length);
            byte[] response = udpClient.Receive(ref endPoint);
            if (response.Length > 0)
            {
                string reply = Encoding.ASCII.GetString(response);
                CheckMessage(reply);
                //WriteLog("Reply: " + reply);
            }
            else
            {
                WriteLog("Empty reply :(");
            }
            // udpclient send & receive
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

        private string MessageBuilder(string[] parts, int index)
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
            }
            else
            {
                this.msgLog.AppendText("<" + time + ">" + msg + "\n");
            }
        }

        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            History historyDialog = new History(ref history);
            historyDialog.Owner = this;
            historyDialog.ShowDialog();
            historyDialog = null;
        }

        private void ReadHistory()
        {
            if (File.Exists(System.IO.Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB\History.xml")))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(BindingList<string>));
                StreamReader reader = new StreamReader(System.IO.Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB\History.xml"));
                history = (BindingList<string>)serializer.Deserialize(reader);
                reader.Close();
            }
        }

        private void SaveHistory()
        {
            if (!Directory.Exists(System.IO.Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB")))
            {
                Directory.CreateDirectory(System.IO.Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB"));
            }
            XmlSerializer serializer = new XmlSerializer(typeof(BindingList<string>));
            TextWriter writer = new StreamWriter(System.IO.Path.Combine((Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), @"Luch\LxAniDB\History.xml"));
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
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                string ext = System.IO.Path.GetExtension(file);
                if(ext == ".mkv" || ext == ".avi" || ext == ".mp4")
                {
                    if (!CheckHistory(System.IO.Path.GetFileName(file)))
                    {
                        this.files.Add(file);
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
