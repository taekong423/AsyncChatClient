using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace AsyncClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Run run = new Run();
            
            //TestConnect(); 
        }

        static void TestConnect()
        {
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EndPoint ep = new IPEndPoint(IPAddress.Parse("10.100.58.3"), 43320);
            s.Connect(ep);
            Console.WriteLine("sss");
        }
    }
}
