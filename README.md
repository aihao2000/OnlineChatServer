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
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.WriteLine("Hello, World!");
var client = new TcpClient();
client.Connect("127.0.0.1", 21101);
while(true)
{
    string id=Console.ReadLine();
    string password=Console.ReadLine();
    object jsonObject = new
    {
        methodName = "SignIn",
        methodParams = new[] { id, password }
    };
    string str=JsonSerializer.Serialize(jsonObject);
    Console.WriteLine(str);
    byte[] buffer=Encoding.UTF8.GetBytes(str);
    client.GetStream().Write(buffer, 0, buffer.Length);
    byte[] buffer2=new byte[1024];
    int len=client.GetStream().Read(buffer2, 0, buffer2.Length);
    string str2=Encoding.UTF8.GetString(buffer2, 0, len);
    Console.WriteLine(str2);
}
```

