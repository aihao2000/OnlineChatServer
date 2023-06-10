# OnlineChatServer

基于socket TCP 的在线聊天室。

数据库使用Sqlite。

使用腾讯云轻量应用服务器部署。

客户端使用socket连接服务器程序。

以json为基本单位进行通讯，通过括号匹配处理粘包问题。

使用c#反射机制实现了json rpc。可远程调用OnlineChatServer.下ChatServer类中，权限满足的所有函数即服务，并以json的形式返回函数的返回值到客户端，以RequestID对应。

并且可以方便使用监听者模式扩展开发，订阅相关事件即可。

基础功能包含：

- 账号注册
- 登录
- 添加好友
- 查看好友列表
- 向好友发送信息

## 扩展开发

在后端实现任意函数，第一个参数必须是TcpClient，可通过以下json形式调用

````json
{ RequestID = requestID, MethodName = methodName, MethodParams = new[] { para1, para2,... } }
````

函数的返回值将封装为json发送向客户端，具体结构请参考函数返回值的具体编写，通常结构为

```json
{
    "Type" = "response",
    "Value" = return_value
}
```

## 使用客户端SDK使服务应用于任意客户端

简单示例见https://github.com/AisingioroHao0/OnlineChatConsoleClient

复制示例中的OnlineChatClientSDK，直接引入,创捷实例，调用连接，然后执行符合直觉的方法即可，将自动开启一个子线程用于与服务器通信，并使用线程安全的中间件保留服务器发送的数据，暴露方法为阻塞方法，将会直到接收到响应为止。

登录后的信息主要存于userInfo

好友列表自动存于friendList

收到的消息自动存于messages
