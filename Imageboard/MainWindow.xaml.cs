using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
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
using Newtonsoft.Json;
using System.Windows.Markup;
using static System.Net.Mime.MediaTypeNames;
using System.IO.Pipes;
using System.IO;

namespace Imageboard
{
    public class Request
    {
        public int operation { get; set; }
        public string content { get; set; }
        public string author { get; set; }

        public string username { get; set; }
        public string password { get; set; }
        public int id { get; set; }
    }

    
    public partial class MainWindow : Window
    {
        Socket connection = null;
        ObservableCollection<ChatMessage> messages = new ObservableCollection<ChatMessage>();
        ObservableCollection<ServerList> servers = new ObservableCollection<ServerList>();
        string ip = "localhost";
        string username = "[ERROR]";
        string queue = "1";
        string maxQueue = "100";
        public MainWindow()
        {
            InitializeComponent();

            foreach (var line in File.ReadLines("servers.txt"))
            {
                servers.Add(new ServerList { IP = line });
            }

            chat.ItemsSource = messages;
            UserTB.Text = $"Host: {ip} \nLogged in as {username}\nQueue {queue}/{maxQueue}";
            ya_ebal.ScrollChanged += OnScrollChanged;

            server_list.ItemsSource = servers;

        }




        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight)
            {
                //MessageBox.Show("This is the end");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SendBox.Focus();
            string Text = SendBox.Text;
            if (Text != "")
            {
                if (Text[0] == '/')
                {
                    Text = Text.Substring(1);
                    string[] command = Text.Split(' ');
                    switch (command[0])
                    {
                        case "connect":
                            if (command.Length > 1)
                            {
                                messages.Add(new ChatMessage { Id = -1, User = "SYSTEM", Message = $"Connecting to {command[1]}" });
                                string[] host = command[1].Split(':');
                                connection = ExecuteClient(host[0], Convert.ToInt32(host[1]));
                                if (connection != null)
                                {
                                    UserTB.Text = $"Host: {host[0]}:{host[1]} \nLog in using /login\nQueue {queue}/{maxQueue}";
                                    messages.Add(new ChatMessage { Id = -1, User = "SYSTEM", Message = $"Connected to {command[1]}" });
                                    messages.Add(new ChatMessage { Id = -1, User = "SYSTEM", Message = $"Login using /login [USERNAME] [PASSWORD]" });

                                    _ = ReceiveMessagesAsync(connection);
                                    _ = SyncMessages(connection);
                                }
                                else
                                {
                                    messages.Add(new ChatMessage { Id = -1, User = "SYSTEM", Message = $"Failed to connect to {command[1]}" });
                                }
                            }
                            break;

                        case "login":
                            if (command.Length > 2)
                            {
                                messages.Add(new ChatMessage { Id = -1, User = "SYSTEM", Message = $"Trying to log in as {command[1]}" });
                                var request = new Request { operation = 1, username = command[1], password = command[2] };
                                username = command[1];
                                byte[] messageSent = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(request));
                                int byteSent = connection.Send(messageSent);
                            }
                            break;
                        case "register":
                            if (command.Length > 2)
                            {
                                messages.Add(new ChatMessage { Id = -1, User = "SYSTEM", Message = $"Trying to register as {command[1]}" });
                                var request = new Request { operation = 0, username = command[1], password = command[2] };
                                byte[] messageSent = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(request));
                                int byteSent = connection.Send(messageSent);
                            }
                            break;

                        default:
                            Console.WriteLine("Nothing");
                            break;
                    }

                }
                else
                {
                    var request = new Request { operation = 2, content = Text };
                    byte[] messageSent = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(request));
                    int byteSent = connection.Send(messageSent);
                    //messages.Add(new ChatMessage { Id = 500, User = username, Message = SendBox.Text });
                }
            }
            SendBox.Text= string.Empty;
            
        }

        static Socket ExecuteClient(string IP, int PORT)
        {
            try
            {
                IPAddress ipAddr = IPAddress.Parse(IP);
                IPEndPoint localEndPoint = new IPEndPoint(ipAddr, PORT);

                Socket sender = new Socket(ipAddr.AddressFamily,
                           SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    sender.Connect(localEndPoint);


                    return sender;
                }

                catch (ArgumentNullException ane)
                {

                    Console.WriteLine("ArgumentNullException : {0}", ane.ToString());
                }

                catch (SocketException se)
                {

                    Console.WriteLine("SocketException : {0}", se.ToString());
                }

                catch (Exception e)
                {
                    Console.WriteLine("Unexpected exception : {0}", e.ToString());
                }
            }

            catch (Exception e)
            {

                Console.WriteLine(e.ToString());
            }
            return null;
        }

        private async Task ReceiveMessagesAsync(Socket connection)
        {
            var buffer = new byte[2048];
            while (true)
            {
                try
                {
                    int receivedBytes = await Task.Run(() => connection.Receive(buffer));
                    if (receivedBytes > 0)
                    {
                        string message = Encoding.ASCII.GetString(buffer, 0, receivedBytes);
                        if (message != "null")
                        {
                            List<Request> requests = JsonConvert.DeserializeObject<List<Request>>(message);

                            Dispatcher.Invoke(() =>
                            {
                                foreach (var item in requests)
                                {
                                    if(item.id == -1)
                                    {
                                        for(int i = messages.Count - 1; i > 0; i--)
                                        {
                                            if(messages[i].Id > 0)
                                            {
                                                item.id = messages[i].Id;
                                                break;
                                            }
                                        }
                                    }
                                    messages.Add(new ChatMessage
                                    {
                                        Id = item.id,
                                        User = item.author,
                                        Message = item.content
                                    });
                                }
                            });
                        }
                    }
                }
                catch (SocketException ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        messages.Add(new ChatMessage
                        {
                            Id = -1,
                            User = "SYSTEM",
                            Message = $"Socket error: {ex.Message}"
                        });
                    });
                    break;
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        messages.Add(new ChatMessage
                        {
                            Id = -1,
                            User = "SYSTEM",
                            Message = $"Unexpected error: {ex.Message}"
                        });
                    });
                    break;
                }
            }
        }

        private async Task SyncMessages(Socket connection)
        {
            while (true)
            {
                try
                {
                    var request = new Request { operation = 3, id = messages.Last().Id };
                    byte[] messageSent = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(request));
                    int byteSent = connection.Send(messageSent);
                }
                catch (SocketException ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        messages.Add(new ChatMessage
                        {
                            Id = -1,
                            User = "SYSTEM",
                            Message = $"Socket error while sending: {ex.Message}"
                        });
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        messages.Add(new ChatMessage
                        {
                            Id = -1,
                            User = "SYSTEM",
                            Message = $"Unexpected error while sending: {ex.Message}"
                        });
                    });
                }
                await Task.Delay(1000);
            }
        }


        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            connection?.Shutdown(SocketShutdown.Both);
            connection?.Close();
        }

        private void ServerAssign(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ServerList selectedServer)
            {
                string serverContent = selectedServer.IP;


                ip = serverContent;

                SendBox.Text = "/connect " + ip;
            }
        }

        private void AddServer(object sender, RoutedEventArgs e)
        {
            var inputDialog = new InputDialog("Please enter the server address:");
            if (inputDialog.ShowDialog() == true)
            {
                string userInput = inputDialog.ResponseText;
                servers.Add(new ServerList { IP = userInput });
                File.AppendAllText("servers.txt", "\n" + userInput);
            }
            else
            {
                
            }
        }
    }
}


