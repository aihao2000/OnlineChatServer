using System;
using System.Reflection;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.Generic;
namespace OnlineChatServer
{
    class MyTest
    {
        public static void TestJsonExecute()
        {
            Console.WriteLine("TestJsonExecute");
            string json = @"{
            ""methodName"": ""TmpFunc"",
            ""methodParams"": [""p1"",""p2""]
            }";
            ChatServer server=new ChatServer();
            server.Start("127.0.0.1",21101);
            Console.WriteLine(server.ExecuteFuncByJsonAndReturnJsonResult(json));
        }
    }
}

