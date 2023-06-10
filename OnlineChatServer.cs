using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;

namespace OnlineChatServer
{
    class ChatServer
    {
        private DatabaseManager databaseManager = new DatabaseManager();

        private TcpListener listener;
        private ConcurrentDictionary<string, TcpClient> clients =
            new ConcurrentDictionary<string, TcpClient>();
        private ConcurrentDictionary<TcpClient, byte[]> buffers =
            new ConcurrentDictionary<TcpClient, byte[]>();
        private ConcurrentDictionary<TcpClient, string> bufferStrings =
            new ConcurrentDictionary<TcpClient, string>();
        private Dictionary<string, List<Message>> waitingMessage =
            new Dictionary<string, List<Message>>();
        private volatile bool isRunning = false;

        public delegate void SignEventHandler(object sender, string name);
        public event SignEventHandler SignInEvent;
        public event SignEventHandler SignOutEvent;
        public delegate void StartServiceEventHandler(
            object sender,
            bool isSucceed,
            string errorMessage
        );
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
            Console.WriteLine("Server started, listening on " + listener.LocalEndpoint + "...");
            ;
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
                Task.Run(() => ReceiveAndProcessRequest(client));
            }
        }

        private void ReceiveAndProcessRequest(TcpClient client)
        {
            if (buffers.ContainsKey(client))
            {
                Console.WriteLine("错误，已经存在该客户端的缓冲区，但是SignIn再次被调用");
                return;
            }
            buffers[client] = new byte[1024];
            bufferStrings[client] = "";
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


        private string ReceiveJsonString(TcpClient client)
        {
            string? json = null;
            while (true)
            {
                int cnt = 0;
                for (int i = 0; i < bufferStrings[client].Length; i++)
                {
                    if (bufferStrings[client][i] == '{')
                    {
                        cnt++;
                    }
                    else if (bufferStrings[client][i] == '}')
                    {
                        if (cnt > 0)
                        {
                            cnt--;
                            if (cnt == 0)
                            {
                                json = bufferStrings[client].Substring(0, i + 1);
                                bufferStrings[client] = bufferStrings[client].Substring(i + 1);
                                break;
                            }
                        }
                        else
                        {
                            throw new Exception("algorithm error");
                        }
                    }
                }
                if (json != null)
                {
                    return json;
                }
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
                bufferStrings[client] += Encoding.UTF8.GetString(buffer, 0, byteReceived);
            }
            return json;
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
            Console.WriteLine("Execute:" + json);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement requeseIDElement;
                if (!doc.RootElement.TryGetProperty("RequestID", out requeseIDElement))
                {
                    return null;
                }
                int requestID = requeseIDElement.GetInt32();
                JsonElement methodNameElement;
                if (!doc.RootElement.TryGetProperty("MethodName", out methodNameElement))
                {
                    return null;
                }
                string methodName = methodNameElement.ToString();
                MethodInfo? method = GetType()
                    .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                {
                    Console.WriteLine("method == null");
                    return null;
                }
                JsonElement methodParamsElement;
                if (!doc.RootElement.TryGetProperty("MethodParams", out methodParamsElement))
                {
                    return null;
                }
                List<object> methodParams = new List<object>();
                methodParams.Add(client);
                methodParams.Add(requestID);
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
                object? result = null;
                try
                {
                    result = method?.Invoke(this, methodParamsArray);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
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

        private string GetFriendList(TcpClient client,int requestID, string id)
        {
            if (client != clients[id])
            {
                return JsonSerializer.Serialize(
                    new { RequestID=requestID, Type = "response", Value = "client not match" }
                );
            }
            return JsonSerializer.Serialize(
                new { RequestID=requestID,Type = "response", Value = databaseManager.GetFriendList(id) }
            );
        }

        private string SignIn(TcpClient client,int requestID, string id, string password)
        {
            string correctPassword = databaseManager.GetPassword(id);
            if (correctPassword == null)
            {
                return JsonSerializer.Serialize(new { RequestID=requestID,Type = "response", Value = 0 });
            }
            else if (correctPassword == password)
            {
                clients[id] = client;
                if (waitingMessage.ContainsKey(id))
                {
                }
                return JsonSerializer.Serialize(new { RequestID=requestID,Type = "response", Value = 1 });
            }
            else
            {
                return JsonSerializer.Serialize(new { RequestID=requestID,Type = "response", Value = 0 });
            }
        }

        private string SignUp(
            TcpClient client,
            int requestID,
            string id,
            string password,
            string name,
            string phone_number,
            string email
        )
        {
            if (databaseManager.AddNewUser(id, password, name, phone_number, email))
            {
                return JsonSerializer.Serialize(new { RequestID=requestID,Type = "response", Value = 1 });
            }
            else
            {
                return JsonSerializer.Serialize(new { RequestID=requestID,Type = "response", Value = 0 });
            }
        }

        private string SendMessage(TcpClient client,int requestID, string id, string friend_id, string message)
        {
            if (client != clients[id])
            {
                return JsonSerializer.Serialize(
                    new { Type = "message", Value = "Client Not Math" }
                );
            }
            if (IsOnline(friend_id))
            {
                SendJson(
                    clients[friend_id],
                    JsonSerializer.Serialize(
                        new
                        {
                            RequestID=requestID,
                            Type = "message",
                            SenderID=id,
                            Message=message,
                            ReveiverID=friend_id
                        }
                    )
                );
                return JsonSerializer.Serialize(
                    new { RequestID=requestID,Type = "response", Value = 1 }
                );
            }
            else
            {
                if (!waitingMessage.ContainsKey(friend_id))
                {
                    waitingMessage[friend_id] = new List<Message>();
                }
                waitingMessage[friend_id].Add(new Message(id,friend_id, message));
                return JsonSerializer.Serialize(
                    new { RequestID=requestID,Type = "response", Value = "SendOfflineMessage Successful" }
                );
            }
        }

        private string AddFriend(TcpClient client,int requestID, string id, string friend_id)
        {
            if (databaseManager.AddFriend(id, friend_id))
            {
                return JsonSerializer.Serialize(new { RequestID=requestID,Type = "AddFriend", Value = 1 });
            }
            else
            {
                return JsonSerializer.Serialize(new { RequestID=requestID,Type = "AddFriend", Value = 0 });
            }
        }
    }
}
