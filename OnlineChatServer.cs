
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Reflection;

namespace OnlineChatServer
{
    class ChatServer
    {
        private DatabaseManager databaseManager = new DatabaseManager();

        private TcpListener listener;
        private ConcurrentDictionary<string, TcpClient> clients = new ConcurrentDictionary<string, TcpClient>();
        private Dictionary<TcpClient, byte[]> buffers = new Dictionary<TcpClient, byte[]>();
        private volatile bool isRunning = false;

        public delegate void SignEventHandler(object sender, string name);
        public event SignEventHandler SignInEvent;
        public event SignEventHandler SignOutEvent;
        public delegate void StartServiceEventHandler(object sender, bool isSucceed, string errorMessage);
        public event StartServiceEventHandler StartServiceEvent;

        public void Start(string ip, int port)
        {
            if (Volatile.Read(ref isRunning))
            {
                return;
            }
            listener = new TcpListener(IPAddress.Parse(ip), port);
            try
            {
                listener.Start();
            }
            catch (SocketException ex)
            {
                StartServiceEvent?.Invoke(this, false, ex.Message);
                return;
            }
            Console.WriteLine("Server started, listening on " + listener.LocalEndpoint + "..."); ;
            Volatile.Write(ref isRunning, true);
            databaseManager.Start();
            Task.Run(AcceptConnectionRequest);
            Task.Run(Maintain);
            StartServiceEvent?.Invoke(this, true, "");
        }
        public void End()
        {
            Volatile.Write(ref isRunning, false);
        }
        public void AcceptConnectionRequest()
        {
            while (Volatile.Read(ref isRunning))
            {
                TcpClient client = listener.AcceptTcpClient();
                Task.Run(() => ReceiveAndProcessMessage(client));
            }
        }
        private void ReceiveAndProcessMessage(TcpClient client)
        {
            if (buffers.ContainsKey(client))
            {
                Console.WriteLine("错误，已经存在该客户端的缓冲区，但是SignIn再次被调用");
                return;
            }
            buffers[client] = new byte[1024];
            string? name = null;
            while (IsConnected(client))
            {
                string? json = ReceiveJsonString(client);
                if (json == null)
                {
                    return;
                }
                string? result = ExecuteFuncByJsonAndReturnJsonResult(json);
                if (result != null)
                {
                    SendJson(client, result);
                }
            }
            if (name == null)
            {
                return;
            }
            Console.WriteLine(name + "sign in");
        }
        private bool SendJson(TcpClient client, string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            NetworkStream clientStream = client.GetStream();
            try
            {
                clientStream.Write(bytes, 0, bytes.Length);
            }
            catch
            {
                return false;
            }
            return true;
        }
        private string? ReceiveJsonString(TcpClient client)
        {
            byte[] buffer = buffers[client];
            NetworkStream clientStream = client.GetStream();
            int byteReceived;
            try
            {
                byteReceived = clientStream.Read(buffer, 0, buffer.Length);
            }
            catch
            {
                return null;
            }
            string s = Encoding.UTF8.GetString(buffer, 0, byteReceived);
            return s;
        }
        public bool IsConnected(string id)
        {
            TcpClient client;
            if (clients.TryGetValue(id, out client))
            {
                return IsConnected(client);
            }
            else
            {
                return false;
            }
        }
        public bool IsConnected(TcpClient client)
        {
            byte[] buffer = new byte[0];
            if (client.Client != null)
            {
                client.Client.Send(buffer, SocketFlags.OutOfBand);
                return client.Client.Connected;
            }
            else
            {
                return false;
            }
        }
        void RemoveInvalidClient()
        {
            List<string> invalidClients = new List<string>();
            foreach (KeyValuePair<string, TcpClient> kvp in clients)
            {
                if (!IsConnected(kvp.Key))
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
            while (Volatile.Read(ref isRunning))
            {
                RemoveInvalidClient();
            }
        }
        public string? ExecuteFuncByJsonAndReturnJsonResult(string json)
        {
            Console.WriteLine("Execute:"+json);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement methodNameElement;
                if (!doc.RootElement.TryGetProperty("methodName", out methodNameElement))
                {
                    return null;
                }
                string methodName = methodNameElement.ToString();
                MethodInfo? method = GetType().GetMethod(methodName,BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                {
                    Console.WriteLine("method == null");
                    return null;
                }
                JsonElement methodParamsElement;
                if (!doc.RootElement.TryGetProperty("methodParams", out methodParamsElement))
                {
                    return null;
                }
                List<object> methodParams = new List<object>();
                foreach (JsonElement param in methodParamsElement.EnumerateArray())
                {
                    switch (param.ValueKind)
                    {
                        case JsonValueKind.Number:
                            methodParams.Add(param.GetInt32());
                            break;
                        case JsonValueKind.String:
                            methodParams.Add(param.GetString());
                            break;
                    }
                }
                object[] methodParamsArray = methodParams.ToArray();
                object? result = method?.Invoke(this, methodParamsArray);
                if (result == null)
                {
                    return null;
                }
                else
                {
                    return result.ToString();
                }
            }

        }
        private string GetFriendList(string id)
        {
            return databaseManager.GetFriendList(id);
        }
        private string SignIn(string name, string password)
        {
            return "";
        }
        private string SignUp(string id, string password, string name, string phone_number)
        {
            return databaseManager.AddNewUser(id, password, name, phone_number);
        }
        private string SendMessage(string id, string friend_id)
        {
            return "";
        }
        private string TmpFunc(string a, string b)
        {
            return a + ":" + b;
        }
    }
}
