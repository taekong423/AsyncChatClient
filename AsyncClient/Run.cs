using System;
using System.Net;
using LunkerLibrary.common.protocol;
using System.Runtime.InteropServices;
using System.Threading;

namespace AsyncClient
{
    class Run
    {
        Client client;
        string command = null;
        int waitTime = 200;
        int roomNo;

        public Run()
        {
            client = new Client();
            //client.SendDisplay(Marshal.SizeOf(typeof(CommonHeader)).ToString(), ChatType.Debug);
            RunClient();
        }

        public void RunClient()
        {
            while (true)
            {
                switch (client.runType)
                {
                    case RunType.Start:
                        Start();
                        break;
                    case RunType.Login:
                        Login();
                        break;
                    case RunType.Lobby:
                        Lobby();
                        break;
                    case RunType.Room:
                        Room();
                        break;
                    default:
                        Console.WriteLine("Invalid RunType");
                        break;
                }
            }
        }

        private void Start()
        {
            if (client.runState == RunState.Idle)
            {
                client.runState = RunState.Waiting;
                ServerInfo info = GetEndPoint();
                string address = info.GetPureIp();
                int port = info.Port;

                if (client.isConnecting == false)
                {
                    client.isConnecting = true;
                    client.Connect(address, port, ServerType.Login);
                    client.SendDisplay(string.Format("Connecting to Login Server [ {0} : {1} ]", address, port), ChatType.System);
                }
            }
            else if (client.runState == RunState.Waiting)
            {
                Thread.Sleep(waitTime);
            }
        }

        private void UserInfoInput(out string id, out string password)
        {
            client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
            client.SendDisplay("│                Enter ID                 │", ChatType.Console);
            client.SendDisplay("└-----------------------------------------┘", ChatType.Console);
            id = Console.ReadLine();
            client.SendDisplay(string.Format("ID: {0}", id), ChatType.Console);

            client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
            client.SendDisplay("│              Enter Password             │", ChatType.Console);
            client.SendDisplay("└-----------------------------------------┘", ChatType.Console);
            password = Console.ReadLine();
            client.SendDisplay(string.Format("Password: {0}", password), ChatType.Console);
        }

        private void Login()
        {
            string id;
            string password;
            string newPwd;

            if (client.IsConnected())
            {
                switch (client.runState)
                {
                    case RunState.Waiting:
                        Thread.Sleep(waitTime);
                        break;

                    case RunState.Idle:
                        lock (client.display)
                        {
                            //client.display.Clear();
                            client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                            client.SendDisplay("│          < Login Server Menu >          │", ChatType.Console);
                            client.SendDisplay("│ 1.Signup  2.Signin  3.Modify  4.Delete  │", ChatType.Console);
                            client.SendDisplay("│    5.Restart     6.Close Application    │", ChatType.Console);
                            client.SendDisplay("└-----------------------------------------┘", ChatType.Console);
                        }
                        command = Console.ReadLine();
                        if (command == "1") client.runState = RunState.Signup;
                        else if (command == "2") client.runState = RunState.Signin;
                        else if (command == "3") client.runState = RunState.Modify;
                        else if (command == "4") client.runState = RunState.Delete;
                        else if (command == "5")
                        {
                            client.isConnecting = false;
                            client.runType = RunType.Start;
                        }
                        else if (command == "6") Environment.Exit(0);
                        else client.SendDisplay("Invalid Login Server Command", ChatType.System);
                        break;


                    case RunState.Signup:
                        client.runState = RunState.Waiting;
                        UserInfoInput(out id, out password);
                        client.Signup(id, password, false);
                        break;


                    case RunState.Signin:
                        client.runState = RunState.Waiting;
                        UserInfoInput(out id, out password);
                        client.Signin(id, password, false);
                        client.userInfo = new UserInfo(id.ToCharArray(), password.ToCharArray(), false);
                        break;


                    case RunState.Modify:
                        client.runState = RunState.Waiting;

                        UserInfoInput(out id, out password);

                        client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                        client.SendDisplay("│            Enter New Password           │", ChatType.Console);
                        client.SendDisplay("└-----------------------------------------┘", ChatType.Console);
                        newPwd = Console.ReadLine();
                        client.SendDisplay(string.Format("New Password: {0}", newPwd), ChatType.Console);

                        client.Modify(id, password, newPwd, false);
                        break;


                    case RunState.Delete:
                        client.runState = RunState.Waiting;
                        UserInfoInput(out id, out password);
                        client.Delete(id, password, false);
                        
                        break;


                    default:
                        Console.WriteLine("Invalid Login RunState");
                        break;
                }
            }
            else
            {
                client.SendDisplay("Disconnected Login", ChatType.System);
                client.isConnecting = true;
                client.runType = RunType.Start;
                client.runState = RunState.Idle;
            }
        }

        private void Lobby()
        {
            
            if (client.IsConnected())
            {
                switch (client.runState)
                {
                    case RunState.Waiting:
                        Thread.Sleep(waitTime);
                        break;


                    case RunState.Idle:
                        lock (client.display)
                        {
                            //client.display.Clear();
                            client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                            client.SendDisplay("│             < Lobby Menu >              │", ChatType.Console);
                            client.SendDisplay("│     1.Room List       2.Create Room     │", ChatType.Console);
                            client.SendDisplay("│     3.Join Room       4.LogOut          │", ChatType.Console);
                            client.SendDisplay("└-----------------------------------------┘", ChatType.Console);
                        }
                        command = Console.ReadLine();
                        if (command == "1") client.runState = RunState.List;
                        else if (command == "2") client.runState = RunState.Create;
                        else if (command == "3") client.runState = RunState.Join;
                        else if (command == "4") client.runState = RunState.Logout;
                        else client.SendDisplay("Invalid Lobby Command", ChatType.System);
                        break;


                    case RunState.List:
                        client.runState = RunState.Waiting;
                        client.List();
                        break;


                    case RunState.Create:
                        client.runState = RunState.Waiting;
                        client.Create();
                        break;


                    case RunState.Join:
                        client.runState = RunState.Waiting;
                        client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                        client.SendDisplay("│            Enter Room Number            │", ChatType.Console);
                        client.SendDisplay("└-----------------------------------------┘", ChatType.Console);

                        if (int.TryParse(Console.ReadLine(), out roomNo))
                        {
                            client.SendDisplay(string.Format("Joining Room {0}...", roomNo), ChatType.System);
                            client.Join(roomNo);
                        }
                        else client.SendDisplay("Invalid RoomNo", ChatType.System);
                        break;


                    case RunState.Logout:
                        client.runState = RunState.Waiting;
                        client.Logout();
                        client.BackToLoginSocket();
                        client.runType = RunType.Login;
                        client.runState = RunState.Idle;
                        break;


                    default:
                        Console.WriteLine("Invalid Lobby RunState");
                        break;
                }
            }
            else
            {
                client.SendDisplay("Disconnected Lobby", ChatType.System);
                client.runType = RunType.Login;
                client.runState = RunState.Idle;
            }
        }

        private void Room()
        {
            if (client.IsConnected())
            {
                switch (client.runState)
                {
                    case RunState.Waiting:
                        Thread.Sleep(waitTime);
                        break;

                    case RunState.Idle:
                        lock (client.display)
                        {
                            client.SendDisplay(              "┌-----------------------------------------┐", ChatType.Console);
                            client.SendDisplay(string.Format("     < Room {0} >   (/exit = Leave Room)", client.room.RoomNo), ChatType.Console);
                            client.SendDisplay(              "└-----------------------------------------┘", ChatType.Console);
                        }
                        client.ChatInput();
                        client.Leave();
                        client.runState = RunState.Waiting;
                        break;

                    default:
                        Console.WriteLine("Invalid Room RunState");
                        break;
                }
            }
            else
            {
                client.SendDisplay("Disconnected Room", ChatType.System);
                client.runType = RunType.Lobby;
                client.runState = RunState.Idle;
            }
        }

        public ServerInfo GetEndPoint()
        {
            char[] ip = new char[15];
            IPAddress address;
            int port = 0;
            ServerInfo info = new ServerInfo(ip, port);
            string[] str = System.IO.File.ReadAllLines("LoginServer.conf");
            str[0] = str[0].Trim();
            string[] serverInfo = str[0].Split(':');

            
            if (IPAddress.TryParse(serverInfo[0], out address) && int.TryParse(serverInfo[1], out port))
            {
                Array.Copy(serverInfo[0].ToCharArray(), info.Ip, serverInfo[0].ToCharArray().Length);
                info.Port = port;
                return info;
            }
            else
            {
                Console.WriteLine("Invalid LoginServer.conf Format ---> 10.100.58.7:43310");
                Environment.Exit(0);
                return default(ServerInfo);
            }
        }
    }
}
