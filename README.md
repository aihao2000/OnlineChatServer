# OnlineChatServer

基于socket的在线聊天室

客户端使用socket连接服务器程序,可远程调用OnlineChatServer.下ChatServer类中，权限满足的所有函数，使用json为单位进行通讯

### 服务

使用以下格式进行rpc，忽略第一个参数TcpClient

````json
{
    "methodName":method_name,
    "methodParams":[
        para1,
        para2
    ]
}
````

函数的返回值将封装为json发送向客户端，具体结构请参考函数返回值的具体编写，通常结构为

```json
{
    "methodName" = method_name_called,
    "result"=result
}
```

简单示例程序如

```c#
// See https://aka.ms/new-console-template for more information
using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var server = new TcpClient();
server.Connect("120.53.14.170", 21101);
void ListenAndPrint()
{
    byte[] buffer = new byte[1024];
    while (true)
    {
        int len = server.GetStream().Read(buffer, 0, buffer.Length);
        string str = Encoding.UTF8.GetString(buffer, 0, len);
        Console.WriteLine(str);
    }
}
Task.Run(ListenAndPrint);
void SignUp(string id, string password, string name, string phone_number)
{
    string str = JsonSerializer.Serialize(new
    {
        methodName = "SignUp",
        methodParams = new[] { id, password, name, phone_number }
    });
    byte[] bytes = Encoding.UTF8.GetBytes(str);
    server.GetStream().Write(bytes, 0, bytes.Length);
}
void SignIn(string id, string password)
{
    string str = JsonSerializer.Serialize(new
    {
        methodName = "SignIn",
        methodParams = new[] { id, password }
    });
    byte[] bytes = Encoding.UTF8.GetBytes(str);
    server.GetStream().Write(bytes, 0, bytes.Length);
}
void AddFriend(string id, string friend_id)
{
    string str = JsonSerializer.Serialize(new
    {
        methodName = "AddFriend",
        methodParams = new[] { id, friend_id }
    });
    byte[] bytes = Encoding.UTF8.GetBytes(str);
    server.GetStream().Write(bytes, 0, bytes.Length);
}
void GetFriendList(string id)
{
    string str = JsonSerializer.Serialize(new
    {
        methodName = "GetFriendList",
        methodParams = new[] { id }
    });
    byte[] bytes = Encoding.UTF8.GetBytes(str);
    server.GetStream().Write(bytes, 0, bytes.Length);
}
void SendMessage(string id,string friend_id,string message)
{
    string str = JsonSerializer.Serialize(new
    {
        methodName = "GetFriendList",
        methodParams = new[] { id ,friend_id,message}
    });
    byte[] bytes = Encoding.UTF8.GetBytes(str);
    server.GetStream().Write(bytes, 0, bytes.Length);
}
SignUp("id1", "password", "name", "phone_number");
SignUp("id2", "password", "name", "phone_number");
SignIn("id", "password");
AddFriend("id1", "id2");
GetFriendList("id1");
SendMessage("id1", "id2", "message");
while(true)
{

}

```

