using OnlineChatServer;
var server=new ChatServer();
server.Start("127.0.0.1",21101);
while(true)
{
    string str=Console.ReadLine();
    if(str=="exit")
    {
        server.End();
        break;
    }
}