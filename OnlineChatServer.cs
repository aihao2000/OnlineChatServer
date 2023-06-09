
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
        private Dictionary<string, List<Tuple<string, string>>> waitingMessage = new Dictionary<string, List<Tuple<string, string>>>();
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
                string? result = ExecuteFuncByJsonAndReturnJsonResult(client, json);
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
            return Encoding.UTF8.GetString(buffer, 0, byteReceived);
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
        private bool IsOnline(string name)
        {
            return clients.ContainsKey(name) && IsConnected(clients[name]);
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

        public string? ExecuteFuncByJsonAndReturnJsonResult(TcpClient client, string json)
        {
            Console.WriteLine("Execute:" + json);//TODO：删除或改成线程安全的日志写入
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement methodNameElement;
                if (!doc.RootElement.TryGetProperty("methodName", out methodNameElement))
                {
                    return null;
                }
                string methodName = methodNameElement.ToString();
                MethodInfo? method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
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
                methodParams.Add(client);
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
        private string GetFriendList(TcpClient client, string id)
        {
            if (client != clients[id])
            {
                return JsonSerializer.Serialize(new
                {
                    methodName = "GetFriendList",
                    result = "客户端不匹配"
                });
            }
            return JsonSerializer.Serialize(new
            {
                methodName = "GetFriendList",
                result = databaseManager.GetFriendList(id)
            });
        }
        private string SignIn(TcpClient client, string id, string password)
        {
            string correctPassword = databaseManager.GetPassword(id);
            if (correctPassword == null)
            {
                return JsonSerializer.Serialize(new
                {
                    methodName = "SignIn",
                    result = "账号不存在"
                });
            }
            else if (correctPassword == password)
            {
                clients[id] = client;
                if (waitingMessage.ContainsKey(id))
                {
                    //TODO:客户端注意验证
                    SendJson(client, JsonSerializer.Serialize(waitingMessage[id]));
                    waitingMessage.Remove(id);
                }
                return JsonSerializer.Serialize(new
                {
                    methodName = "SignIn",
                    result = "登录成功"
                });
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    methodName = "SignIn",
                    result = "密码错误"
                });
            }
        }
        private string SignUp(TcpClient client, string id, string password, string name, string phone_number)
        {
            if (databaseManager.AddNewUser(id, password, name, phone_number))
            {
                return JsonSerializer.Serialize(new
                {
                    methodName = "SignUp",
                    result = "注册成功"
                });
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    methodName = "SignUp",
                    result = "注册成功"
                });
            }
        }

        private string SendMessage(TcpClient client, string id, string friend_id, string message)
        {
            if (client != clients[id])
            {
                return JsonSerializer.Serialize(new
                {
                    methodName = "SendMessage",
                    result = "客户端不匹配"
                });
            }
            if (IsOnline(friend_id))
            {
                SendJson(clients[friend_id], JsonSerializer.Serialize(new
                {
                    methodName = "ReceiveMessage",
                    senderName = id,
                    message = message
                }));
                return JsonSerializer.Serialize(new
                {
                    methodName = "SendMessage",
                    result = "发送成功"
                });
            }
            else
            {
                if (!waitingMessage.ContainsKey(friend_id))
                {
                    waitingMessage[friend_id] = new List<Tuple<string, string>>();
                }
                waitingMessage[friend_id].Add(Tuple.Create(id, message));
                return JsonSerializer.Serialize(new
                {

                    methodName = "SendMessage",
                    result = "发送成功但是对方不在线"
                });
            }
        }
        private string? AddFriend(TcpClient client, string id, string friend_id)
        {
            if (databaseManager.AddFriend(id, friend_id))
            {
                return JsonSerializer.Serialize(new
                {
                    methodName = "AddFriend",
                    result = "添加成功"
                });
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    methodName = "AddFriend",
                    result = "添加失败"
                });
            }
        }
    }
}
