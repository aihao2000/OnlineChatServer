
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;

namespace OnlineChatServer
{
    class ChatServer
    {
        private TcpListener listener;
        private ConcurrentDictionary<string, TcpClient> clients = new ConcurrentDictionary<string, TcpClient>();
        private bool isRunning = false;
        private Thread serviceThread;

        public delegate void SignEventHandler(object sender, string name);
        public event SignEventHandler SignInEvent;
        public event SignEventHandler SignOutEvent;
        public delegate void StartServiceEventHandler(object sender, bool isSucceed, string errorMessage);
        public event StartServiceEventHandler StartServiceEvent;
        private SqliteConnection databaseConn=new SqliteConnection("Data Source=hello.db");
        public void Start(string ip, int port)
        {
            if (isRunning)
            {
                return;
            }
            try
            {
                listener = new TcpListener(IPAddress.Parse(ip), port);
                listener.Start();
                Console.WriteLine("Server started, listening on " + listener.LocalEndpoint + "..."); ;
                isRunning = true;
                serviceThread = new Thread(AcceptRequest);
                serviceThread.IsBackground = true;
                serviceThread.Start();
                Task.Run(Maintain);
                if (StartServiceEvent != null)
                {
                    StartServiceEvent(this, true, "");
                }
            }
            catch (Exception ex)
            {
                if (StartServiceEvent != null)
                {
                    StartServiceEvent(this, false, ex.Message);
                }
            }
        }
        public void End()
        {
            isRunning = false;
            serviceThread.Abort();
        }
        public void AcceptRequest()
        {
            while (isRunning)
            {
                TcpClient client = listener.AcceptTcpClient();
                Task.Run(() => SignIn(client));
            }
        }
        private void SignIn(TcpClient client)
        {
            clients[GetName(client)] = client;
            Console.WriteLine(GetName(client) + "sign in");
            SignInEvent(this, GetName(client));
            SendMessage(client);
        }
        private string GetName(TcpClient client)
        {
            string name = "";
            if (client.Client != null)
            {
                name = client.Client.RemoteEndPoint.ToString();
            }
            return name;
        }
        private void SendMessage(TcpClient client)
        {
            NetworkStream clientStream = client.GetStream();
            byte[] buffer = new byte[1024];
            string name = GetName(client);
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            while (clientStream.CanRead)
            {
                int byteReceived;
                try
                {
                    byteReceived = clientStream.Read(buffer, 0, buffer.Length);
                }
                catch
                {
                    break;
                }
                string s1 = Encoding.UTF8.GetString(nameBytes);
                string s2 = Encoding.UTF8.GetString(buffer, 0, byteReceived);
                Console.WriteLine(s1 + ":" + s2);
                foreach (TcpClient otherClient in clients.Values)
                {
                    if (otherClient != client)
                    {
                        NetworkStream otherClientStream = otherClient.GetStream();
                        if (otherClientStream.CanWrite)
                        {
                            try
                            {
                                otherClientStream.Write(nameBytes, 0, nameBytes.Length);
                                otherClientStream.Write(buffer, 0, byteReceived);
                                Console.WriteLine("write to" + GetName(otherClient));
                            }
                            catch
                            {
                                SignOut(client);
                            }
                        }
                    }
                }
            }
            SignOut(client);
        }
        private void SignOut(TcpClient client)
        {
            string name = GetName(client);
            client.Close();
            try
            {
                clients.TryRemove(name, out _);
            }
            catch { }
            SignOutEvent(this, name);
            Console.WriteLine(name + "sign out");
        }
        void RemoveInvalidClient()
        {
            List<string> invalidClients = new List<string>();
            foreach (KeyValuePair<string, TcpClient> kvp in clients)
            {
                if (!kvp.Value.Connected)
                {
                    invalidClients.Add(kvp.Key);
                }
            }
            foreach (string invalidClientName in invalidClients)
            {
                try
                {
                    clients.TryRemove(invalidClientName, out _);
                    SignOutEvent(this, invalidClientName);

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

        }
        void Maintain()
        {
            while (isRunning)
            {
                RemoveInvalidClient();
            }
        }
    }
}
