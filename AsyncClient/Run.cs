using System;
using System.Net;
using LunkerLibrary.common.protocol;
using System.Runtime.InteropServices;
using System.Threading;
using AsyncChat;
using static AsyncChat.CommonHelper;

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

                client.Connect(address, port, ServerType.Login);
                client.ClearDisplay();
                client.SendDisplay(string.Format("Connecting to Login Server [ {0} : {1} ]", address, port), ChatType.System);
            }
            else if (client.runState == RunState.Waiting)
            {
                Thread.Sleep(waitTime);
            }
        }

        private void Login()
        {
            string id;
            string password;
            string newPwd;

            if (client.IsLoginConnected())
            {
                client.RedirectSocket();
                switch (client.runState)
                {
                    case RunState.Waiting:
                        Thread.Sleep(waitTime);
                        break;

                    case RunState.Idle:
                        lock (client.display)
                        {
                            client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                            client.SendDisplay("│          < Login Server Menu >          │", ChatType.Console);
                            client.SendDisplay("│ 1.Signup  2.Signin  3.Modify  4.Delete  │", ChatType.Console);
                            client.SendDisplay("│    5.Restart     6.Close Application    │", ChatType.Console);
                            client.SendDisplay("└-----------------------------------------┘", ChatType.Console);
                        }
                        command = Console.ReadLine();
                        client.ClearDisplay();

                        if (command == "1") client.runState = RunState.Signup;
                        else if (command == "2") client.runState = RunState.Signin;
                        else if (command == "3") client.runState = RunState.Modify;
                        else if (command == "4") client.runState = RunState.Delete;
                        else if (command == "5")
                        {
                            client.InitializeClient();
                        }
                        else if (command == "6") Environment.Exit(0);
                        else client.SendDisplay("Invalid Login Server Command", ChatType.System);
                        break;


                    case RunState.Signup:
                        client.runState = RunState.Waiting;
                        client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                        client.SendDisplay("│               < Sign Up >               │", ChatType.Console);
                        client.SendDisplay("└-----------------------------------------┘", ChatType.Console);

                        UserInfoInput(out id, out password);
                        client.SignupRequest(id, password, false);
                        break;


                    case RunState.Signin:
                        client.runState = RunState.Waiting;
                        client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                        client.SendDisplay("│               < Sign In >               │", ChatType.Console);
                        client.SendDisplay("└-----------------------------------------┘", ChatType.Console);

                        UserInfoInput(out id, out password);
                        client.SigninRequest(id, password, false);
                        client.userInfo = new UserInfo(id.ToCharArray(), password.ToCharArray(), false);
                        break;


                    case RunState.Modify:
                        client.runState = RunState.Waiting;
                        client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                        client.SendDisplay("│         < Modify User Password >        │", ChatType.Console);
                        client.SendDisplay("└-----------------------------------------┘", ChatType.Console);

                        UserInfoInput(out id, out password);
                        client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                        client.SendDisplay("│            Enter New Password           │", ChatType.Console);
                        client.SendDisplay("└-----------------------------------------┘", ChatType.Console);
                        newPwd = Console.ReadLine();
                        client.SendDisplay(string.Format("New Password: {0}", newPwd), ChatType.Console);

                        client.ModifyRequest(id, password, newPwd, false);
                        break;


                    case RunState.Delete:
                        client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                        client.SendDisplay("│             < Delete User >             │", ChatType.Console);
                        client.SendDisplay("└-----------------------------------------┘", ChatType.Console);

                        client.runState = RunState.Waiting;
                        UserInfoInput(out id, out password);
                        client.DeleteRequest(id, password, false);
                        
                        break;


                    default:
                        Console.WriteLine("Invalid Login RunState");
                        break;
                }
            }
            else
            {
                client.SendDisplay("Login Server Disconnected", ChatType.System);
                client.InitializeClient();
            }
        }

        private void Lobby()
        {
            if (client.IsChatConnected())
            {
                client.RedirectSocket();
                switch (client.runState)
                {
                    case RunState.Waiting:
                        Thread.Sleep(waitTime);
                        break;


                    case RunState.Idle:
                        lock (client.display)
                        {
                            client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                            client.SendDisplay("│             < Lobby Menu >              │", ChatType.Console);
                            client.SendDisplay("│     1.Room List       2.Create Room     │", ChatType.Console);
                            client.SendDisplay("│     3.Join Room       4.LogOut          │", ChatType.Console);
                            client.SendDisplay("└-----------------------------------------┘", ChatType.Console);
                        }
                        command = Console.ReadLine();
                        client.ClearDisplay();

                        if (command == "1") client.runState = RunState.List;
                        else if (command == "2") client.runState = RunState.Create;
                        else if (command == "3") client.runState = RunState.Join;
                        else if (command == "4") client.runState = RunState.Logout;
                        else client.SendDisplay("Invalid Lobby Command", ChatType.System);
                        break;


                    case RunState.List:
                        client.runState = RunState.Waiting;
                        client.ListRequest();
                        break;


                    case RunState.Create:
                        client.runState = RunState.Waiting;
                        client.CreateRequest();
                        break;


                    case RunState.Join:
                        client.runState = RunState.Waiting;
                        client.SendDisplay("┌-----------------------------------------┐", ChatType.Console);
                        client.SendDisplay("│            Enter Room Number            │", ChatType.Console);
                        client.SendDisplay("└-----------------------------------------┘", ChatType.Console);

                        if (int.TryParse(Console.ReadLine(), out roomNo))
                        {
                            client.SendDisplay(string.Format("Joining Room {0}...", roomNo), ChatType.System);
                            client.JoinRequest(roomNo);
                        }
                        else client.SendDisplay("Invalid RoomNo", ChatType.System);
                        break;


                    case RunState.Logout:
                        client.runState = RunState.Waiting;
                        client.ChangeCurrentSocket(ServerType.Login);
                        client.LogoutRequest();
                        break;


                    default:
                        Console.WriteLine("Invalid Lobby RunState");
                        break;
                }
            }
            else
            {
                if (client.IsLoginConnected())
                {
                    client.ClearDisplay();
                    client.SendDisplay("Chat Server Disconnected", ChatType.System);
                    client.InitializeClient();
                    //client.BackToLoginServer();
                }
                else
                {
                    client.ClearDisplay();
                    client.SendDisplay("Login Server Disconnected", ChatType.System);
                    client.InitializeClient();
                }
            }
        }

        private void Room()
        {
            if (client.IsChatConnected())
            {
                switch (client.runState)
                {
                    case RunState.Waiting:
                        Thread.Sleep(waitTime);
                        break;

                    case RunState.Idle:
                        client.runState = RunState.Waiting;
                        client.ClearDisplay();
                        lock (client.display)
                        {
                            client.SendDisplay(              "┌-----------------------------------------┐", ChatType.Console);
                            client.SendDisplay(string.Format("     < Room {0} >   (/exit = Leave Room)", client.room.RoomNo), ChatType.Console);
                            client.SendDisplay(              "└-----------------------------------------┘", ChatType.Console);
                        }
                        client.ChatInput();
                        client.LeaveRequest();
                        break;

                    default:
                        Console.WriteLine("Invalid Room RunState");
                        break;
                }
            }
            else
            {
                if (client.IsLoginConnected())
                {
                    client.ClearDisplay();
                    client.SendDisplay("Chat Server Disconnected", ChatType.System);
                    client.InitializeClient();
                    //client.BackToLoginServer();
                }
                else
                {
                    client.ClearDisplay();
                    client.SendDisplay("Login Server Disconnected", ChatType.System);
                    client.InitializeClient();
                }
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
    }
}
