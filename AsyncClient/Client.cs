using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using LunkerLibrary.common.protocol;
using WebSocketSharp;
using AsyncChat;
using static AsyncChat.CommonHelper;

namespace AsyncClient
{
    class Client
    {
        private Socket currentSocket;
        private Socket newSocket;
        public Socket loginSocket = null;
        public Socket chatSocket = null;
        public WebSocket webSocket = null;

        private int line;
        private int viewCount = 27;
        private int recvBufferSize = 200;

        public bool debug = true;

        public List<StringBuilder> display = null;
        
        public UserInfo userInfo;
        public Cookie cookie;
        public ServerInfo serverInfo;
        public ChattingRoom room;
        public int roomNo = 0;
        
        public ConnectionType serverType;
        public RunType runType;
        public RunState runState;
        public bool isWebSocket = false;
        public bool isPassing = false;
        public CommonHeader responseHeader;

        public bool doesBodyExist = false;

        public Client()
        {
            InitializeClient();
            display = new List<StringBuilder>();
            line = 0;
        }

        // Initialize All members. Set RunState as Start
        public void InitializeClient()
        {
            Console.Title = "Starting...";
            currentSocket = null;
            newSocket = null;
            try
            {
                if (chatSocket != null)
                {
                    if (chatSocket.Connected)
                        chatSocket.Close();

                    chatSocket = null;
                }

                if (loginSocket != null)
                {
                    if (loginSocket.Connected)
                        loginSocket.Close();

                    loginSocket = null;
                }

                if (webSocket != null)
                {
                    if (webSocket.ReadyState == WebSocketState.Open)
                        webSocket.Close();

                    webSocket = null;
                }
            }
            catch (Exception exc)
            {
                SendDisplay("Restarting Application..." + exc.GetType().ToString(), ChatType.System);
                SendDisplay("Press Any Key...", ChatType.System);
                Console.ReadKey();
                InitializeClient();
                //Environment.Exit(0);
            }
            

            userInfo = new UserInfo();
            cookie = new Cookie();
            serverInfo = new ServerInfo();
            room = new ChattingRoom();
            roomNo = 0;

            isWebSocket = false;
            isPassing = false;

            runType = RunType.Start;
            runState = RunState.Idle;
        }

        // Initialize and Set RunState as Login
        public void InitializeLogin()
        {
            try
            {
                if (chatSocket != null)
                {
                    if (chatSocket.Connected)
                        chatSocket.Close();

                    chatSocket = null;
                }

                if (webSocket != null)
                {
                    if (webSocket.ReadyState == WebSocketState.Open)
                        webSocket.Close();

                    webSocket = null;
                }
            }
            catch (Exception exc)
            {
                SendDisplay("Restarting Application..." + exc.GetType().ToString(), ChatType.System);
                SendDisplay("Press Any Key...", ChatType.System);
                Console.ReadKey();
                InitializeClient();
                //Environment.Exit(0);
            }

            isWebSocket = false;
            isPassing = false;

            runType = RunType.Login;
            runState = RunState.Idle;
        }

        // Close Chat Server Socket and Set current Socket as Login Server Socket
        public void BackToLoginServer()
        {
            try
            {
                currentSocket = null;
                newSocket = null;
                if (!isWebSocket)
                {
                    if (chatSocket != null)
                    {
                        if (chatSocket.Connected)
                            chatSocket.Close();
                        chatSocket = null;
                    }
                }
                else
                {
                    if (webSocket != null)
                    {
                        if (webSocket.ReadyState == WebSocketState.Open)
                            webSocket.Close();
                        webSocket = null;
                        isWebSocket = false;
                    }
                }

                if (loginSocket != null)
                {
                    if (loginSocket.Connected)
                    {
                        currentSocket = loginSocket;
                        runType = RunType.Login;
                    }
                    else
                    {
                        loginSocket = null;
                        runType = RunType.Start;
                    }
                }
                else runType = RunType.Start;
            }
            catch (Exception exc)
            {
                SendDisplay("Restarting Application..." + exc.GetType().ToString(), ChatType.System);
                SendDisplay("Press Any Key...", ChatType.System);
                Console.ReadKey();
                InitializeClient();
                //Environment.Exit(0);
            } 
        }

        // Start Websocket Client
        private void StartWebClient(string url)
        {
            try
            {
                webSocket = new WebSocket(url);
                webSocket.WaitTime = TimeSpan.MaxValue;
                webSocket.ConnectAsync();

                webSocket.OnOpen += (sender, e) =>
                {
                    Console.Title = "In WebSocket Chat Server!!!";
                    ConnectionSetupRequest();
                };

                webSocket.OnMessage += (sender, e) =>
                {
                    DebugDisplay("Data Received");
                    ResponseHandler rh = new ResponseHandler(this);
                    rh.HandleResponse(e.RawData);
                };

                webSocket.OnClose += (sender, e) =>
                {
                    SendDisplay("WebSocket Closing", ChatType.System);
                    BackToLoginServer();
                    runState = RunState.Idle;
                };

                webSocket.OnError += (sender, e) =>
                {
                    SendDisplay("WebSocket Error", ChatType.System);
                    isWebSocket = false;
                    Console.ReadKey();
                    RedirectSocket();
                    runState = RunState.Idle;
                };
            }
            catch (Exception exc)
            {
                SendDisplay("Closing Application..." + exc.GetType().ToString(), ChatType.System);
                SendDisplay("Press Any Key...", ChatType.System);
                Console.ReadKey();
                Environment.Exit(0);
            }
        }

        // Async Connect to Socket
        public void Connect(string address, int port, ConnectionType server)
        {
            serverType = server;
            if (port == 80)
            {
                isWebSocket = true;
                string url = "ws://" + address + ":" + port.ToString() + "/";
                StartWebClient(url);
                Console.Title = "Connecting WebSocket...";
            }
            else
            {
                isWebSocket = false;
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.RemoteEndPoint = ipEndPoint;
                args.Completed += new EventHandler<SocketAsyncEventArgs>(Connect_Completed);
                newSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                newSocket.ConnectAsync(args);
                Console.Title = "Connecting Socket...";
            }
        }

        // Async Send
        private void Send(byte[] sendData)
        {
            try
            {
                if (currentSocket != null)
                {
                    if (currentSocket.Connected)
                    {
                        SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();
                        sendArgs.SetBuffer(sendData, 0, sendData.Length);
                        sendArgs.UserToken = currentSocket;
                        sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(Send_Completed);
                        currentSocket.SendAsync(sendArgs);
                    }
                    else SendDisplay("Send Socket Not Connected", ChatType.System);    
                }
                else
                {
                    SendDisplay("Send Fail", ChatType.System);
                }
            }
            catch (Exception exc)
            {
                SendDisplay("Send Error" + exc.Message, ChatType.System);
                Console.ReadKey();
                InitializeClient();
                //RedirectSocket();
                runState = RunState.Idle;
            }
        }

        // Async Receive (loop)
        private void Receive()
        {
            try
            {
                if (currentSocket != null)
                {
                    if (currentSocket.Connected)
                    {
                        SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
                        byte[] recvData = new byte[recvBufferSize];
                        receiveArgs.UserToken = currentSocket;
                        receiveArgs.SetBuffer(recvData, 0, recvData.Length);
                        receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(Recieve_Completed);
                        currentSocket.ReceiveAsync(receiveArgs);
                    }
                    else SendDisplay("Receive Socket Not Connected", ChatType.System);
                }
                else
                {
                    SendDisplay("Receive Fail", ChatType.System);
                    SendDisplay("Press Any Key...", ChatType.System);
                }
            }
            catch (Exception exc)
            {
                SendDisplay("Receive Error" + exc.Message, ChatType.System);
                Console.ReadKey();
                InitializeClient();
                //RedirectSocket();
                runState = RunState.Idle;
            }
        }

        // Called when ConnectAsync is completed
        private void Connect_Completed(object sender, SocketAsyncEventArgs e)
        {
            currentSocket = null;

            if (serverType == ConnectionType.Login)
            {
                loginSocket = (Socket)sender;
                currentSocket = loginSocket;
                Console.Title = "In Login Server!!!";
            }
            else if (serverType == ConnectionType.Chat)
            {
                chatSocket = (Socket)sender;
                currentSocket = chatSocket;
                Console.Title = "In Socket Chat Server!!!";
            }

            if (currentSocket.Connected)
            {
                if (serverType == ConnectionType.Login)
                {
                    SendDisplay("Connected to Login Server!", ChatType.System);
                    runType = RunType.Login;
                    runState = RunState.Idle;
                    Receive();
                }
                else if (serverType == ConnectionType.Chat)
                {
                    ConnectionSetupRequest();
                    Receive();
                }
            }
            else
            {
                Console.Title = "Connection Failed...";
                SendDisplay("Connection Failed!", ChatType.System);
                SendDisplay("Enter Anything to Retry... or Enter \"/exit\" to Close Application", ChatType.System);
                string command = Console.ReadLine();
                if (command == "/exit")
                {
                    SendDisplay("Closing Application...", ChatType.System);
                    SendDisplay("Press Any Key...", ChatType.System);
                    Console.ReadKey();
                    Environment.Exit(0);
                }
                InitializeClient();
            }
        }

        // Called when SendAsync is completed
        private void Send_Completed(object sender, SocketAsyncEventArgs e)
        {
            Socket client = (Socket)sender;
            DebugDisplay("Data Sent");
        }

        // Called when ReceiveAsync is completed
        private void Recieve_Completed(object sender, SocketAsyncEventArgs e)
        {
            Socket client = (Socket)sender;

            if (client.Connected)
            {
                if (e.BytesTransferred > 0)
                {
                    DebugDisplay("Data Received");
                    byte[] szData = new byte[e.BytesTransferred];
                    Array.Copy(e.Buffer, szData, szData.Length);
                    lock (szData)
                    {
                        ResponseHandler rh = new ResponseHandler(this);
                        rh.HandleResponse(szData);
                    }
                }
                else if (e.BytesTransferred < 0)
                {
                    SendDisplay("Invalid Bytes Transferred (less than 0)...", ChatType.System);
                }

                byte[] recvData = new byte[recvBufferSize];
                e.SetBuffer(recvData, 0, recvBufferSize);
                client.ReceiveAsync(e);
            }
            else
            {
                SendDisplay("Recieve Failed!", ChatType.System);
                SendDisplay("Press Any Key...", ChatType.System);
                InitializeClient();
                //RedirectSocket();
                runState = RunState.Idle;
            }
        }

        // Connect to ChatServer with ServerInfo given from Login Server
        public void ConnectChatServer()
        {
            Connect(serverInfo.GetPureIp(), serverInfo.Port, ConnectionType.Chat);
        }

        // Send ConnectionPassing Request to Login Server
        public void ConnectionPassing()
        {
            isPassing = true;
            ChangeCurrentSocket(loginSocket);
            CommonHeader header = new CommonHeader(MessageType.ConnectionPassing, MessageState.Request, 0, cookie, userInfo);
            CLConnectionPassingRequestBody passingReqBody = new CLConnectionPassingRequestBody(serverInfo);
            SendStructAsPacket(header, passingReqBody);
        }

        // Send ConnectionSetup Request to Chat Server (to check cookie)
        public void ConnectionSetupRequest()
        {
            CommonHeader header = new CommonHeader(MessageType.ConnectionSetup, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        // Send Signup Request to create new user in DB
        public void SignupRequest(string id, string pwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Signup, MessageState.Request, 0, cookie, userInfo);

            SendStructAsBytes<CommonHeader>(header);
        }

        // Send Signin Request to connect to chat server lobby
        public void SigninRequest(string id, string pwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Signin, MessageState.Request, 0, cookie, userInfo);

            SendStructAsBytes<CommonHeader>(header);
        }
        
        // Send Modify Request to change the password of user
        public void ModifyRequest(string id, string pwd, string nPwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            int bodyLength = Marshal.SizeOf(typeof(CLModifyRequestBody));
            CommonHeader header = new CommonHeader(MessageType.Modify, MessageState.Request, bodyLength, cookie, userInfo);
            CLModifyRequestBody modifyReqBody = new CLModifyRequestBody(userInfo, nPwd);

            SendStructAsPacket(header, modifyReqBody);
        }

        // Send Delete Request to delete user from DB
        public void DeleteRequest(string id, string pwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Delete, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        // Send Join Request to join a room
        public void JoinRequest(int roomNo)
        {
            int bodyLength = Marshal.SizeOf(typeof(CCJoinRequestBody));
            CommonHeader header = new CommonHeader(MessageType.JoinRoom, MessageState.Request, bodyLength, cookie, userInfo);

            room = new ChattingRoom(roomNo);
            CCJoinRequestBody joinReqBody = new CCJoinRequestBody(room);

            SendStructAsPacket(header, joinReqBody);
        }

        // Send Create Request to create a new chat room
        public void CreateRequest()
        {
            CommonHeader header = new CommonHeader(MessageType.CreateRoom, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        // Send List Request to get list of rooms in chat servers
        public void ListRequest()
        {
            CommonHeader header = new CommonHeader(MessageType.ListRoom, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        // Send Logout Request to leave chat server and network with login server again
        public void LogoutRequest()
        {
            CommonHeader header = new CommonHeader(MessageType.Logout, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        // Send Leave Request to leave the room and go to lobby
        public void LeaveRequest()
        {
            int bodyLength = Marshal.SizeOf(typeof(CCLeaveRequestBody));
            CommonHeader header = new CommonHeader(MessageType.LeaveRoom, MessageState.Request, bodyLength, cookie, userInfo);
            CCLeaveRequestBody leaveReqbody = new CCLeaveRequestBody(room);

            SendStructAsPacket(header, leaveReqbody);
        }

        // Start Chatting
        public void ChatInput()
        {
            string sData;
            while (true)
            {
                sData = Console.ReadLine();

                byte[] sendBody = Encoding.UTF8.GetBytes(sData);
                CommonHeader header = new CommonHeader(MessageType.Chatting, MessageState.Request, sendBody.Length, new Cookie(), userInfo);

                byte[] packet = new byte[Marshal.SizeOf(typeof(CommonHeader)) + sendBody.Length];
                Array.Copy(StructToBytes(header), 0, packet, 0, Marshal.SizeOf(typeof(CommonHeader)));
                Array.Copy(sendBody, 0, packet, Marshal.SizeOf(typeof(CommonHeader)), sendBody.Length);

                if (sData.CompareTo("/exit") == 0)
                {
                    break;
                }
                else
                {
                    if (!isWebSocket)
                    {
                        if (currentSocket != null)
                        {
                            if (!currentSocket.Connected)
                            {
                                SendDisplay("Chat Connection Failed!", ChatType.System);
                                SendDisplay("Press Any Key...", ChatType.System);
                                Console.ReadKey();
                                InitializeClient();
                                //RedirectSocket();
                                runState = RunState.Idle;
                            }
                            else
                            {
                                Send(packet);
                                DebugDisplay(sData);
                            }
                        }
                        else break;
                    }
                    else
                    {
                        if (webSocket != null)
                        {
                            if (webSocket.ReadyState != WebSocketState.Open)
                            {
                                SendDisplay("Chat Connection Failed!", ChatType.System);
                                SendDisplay("Press Any Key...", ChatType.System);
                                Console.ReadKey();
                                InitializeClient();
                                //isWebSocket = false;
                                //ReconnectLoginServer();
                                runState = RunState.Idle;
                                break;
                            }
                            else
                            {
                                webSocket.Send(packet);
                                DebugDisplay(sData);
                            }
                        }
                        else
                        {
                            isWebSocket = false;
                            break;
                        }
                    }
                }
            }
        }

        // Change the socket that is currently networking (receiveing, sending data)
        public void ChangeCurrentSocket(Socket newSocket)
        {
            currentSocket = null;

            if (newSocket != null)
            {
                if (newSocket.Connected)
                    currentSocket = newSocket;
                else
                {
                    InitializeClient();
                }
            }
            else InitializeClient();
        }

        // Change back to Login state
        public void ReconnectLoginServer()
        {
            if (loginSocket != null)
            {
                if (loginSocket.Connected)
                {
                    currentSocket = loginSocket;
                    runType = RunType.Login;
                }
                else
                {
                    loginSocket = null;
                    runType = RunType.Start;
                }
            }
            else runType = RunType.Start;
        }

        // if chat socket is disconnected, connect to login socket
        // and if login socket is disconnected, restart program
        public void RedirectSocket()
        {
            currentSocket = null;

            if (!isWebSocket)
            {
                if (chatSocket != null)
                {
                    if (chatSocket.Connected)
                    {
                        currentSocket = chatSocket;
                        //runType = RunType.Lobby;
                    }
                    else
                    {
                        chatSocket = null;
                        ReconnectLoginServer();
                    }
                }
                else ReconnectLoginServer();
            }
            else
            {
                if (webSocket != null)
                {
                    if (webSocket.ReadyState != WebSocketState.Open)
                    {
                        webSocket = null;
                        ReconnectLoginServer();
                    }
                    //else runType = RunType.Lobby;
                }
                else ReconnectLoginServer();
            }
        }

        // Control Console Display
        public void SendDisplay(string nMessage, ChatType nType)
        {
            StringBuilder buffer = new StringBuilder();
            switch (nType)
            {
                case ChatType.Send:
                    buffer.Append("Send : ");
                    break;
                case ChatType.Receive:
                    buffer.Append("Recv : ");
                    break;
                case ChatType.System:
                    buffer.Append("System: ");
                    break;
                case ChatType.Debug:
                    buffer.Append("DEBUG: ");
                    break;
            }
            buffer.Append(nMessage);

            lock (display)
            {
                if (line < viewCount)
                {
                    line++;
                    display.Add(buffer);
                }
                else
                {
                    display.RemoveAt(0);
                    display.Add(buffer);
                }
                //line++;

                Console.Clear();
                for (int i = 0; i < viewCount; i++)
                {
                    if (i < display.Count)
                    {
                        Console.WriteLine(display[i].ToString());
                    }
                    else
                    {
                        Console.WriteLine();
                    }
                }
            }

            Console.Write("\nInput Command: ");
        }

        // convert struct type data into byte array
        // and combine header byte array and body byte array into one byte array
        // and send combined byte array
        private void SendStructAsPacket<T>(CommonHeader header, T body)
        {
            byte[] packet = new byte[Marshal.SizeOf(typeof(CommonHeader)) + Marshal.SizeOf(typeof(T))];
            Array.Copy(StructToBytes(header), 0, packet, 0, Marshal.SizeOf(typeof(CommonHeader)));
            Array.Copy(StructToBytes(body), 0, packet, Marshal.SizeOf(typeof(CommonHeader)), Marshal.SizeOf(typeof(T)));

            if (isWebSocket)
                webSocket.Send(packet);
            else
                Send(packet);
        }

        // convert struct type data into byte array
        // and send the byte array
        private void SendStructAsBytes<T>(object structure)
        {
            byte[] sendBytes = new byte[Marshal.SizeOf(typeof(T))];
            sendBytes = StructToBytes((T)structure);
            try
            {
                if (!isWebSocket)
                    Send(sendBytes);
                else
                    webSocket.Send(sendBytes);
            }
            catch (Exception e)
            {
                SendDisplay("Struct Send Error" + e.Message, ChatType.System);
                Console.ReadKey();
                InitializeClient();
                //RedirectSocket();
                runState = RunState.Idle;
            }

            if (typeof(T) == typeof(CommonHeader))
            {
                CommonHeader header = (CommonHeader)structure;
                DebugDisplay("[Sending] " + DebugHeader(header));
            }
            else
            {
                DebugDisplay(typeof(T).ToString());
            }
        }

        // return header struct data as string on console for debug purpose
        public string DebugHeader(CommonHeader header)
        {
            string s = string.Format("[Type: {0}] [State: {1}] [BodyLength: {2}] [Cookie: {3}] [Id, Pwd: {4}, {5}] [isDummy: {6}]",
                    header.Type.ToString(), header.State.ToString(), header.BodyLength, header.Cookie.Value,
                    header.UserInfo.GetPureId(), header.UserInfo.GetPurePwd(), header.UserInfo.IsDummy);
            return s;
        }

        // Display the string argument if debug is true
        public void DebugDisplay(string s)
        {
            if (debug)
                SendDisplay(s, ChatType.Debug);
        }

        // Clear Console Display
        public void ClearDisplay()
        {
            if (!debug)
            {
                lock (display)
                {
                    if (display != null) display.Clear();
                    else display = new List<StringBuilder>();
                }
                line = 0;
            }
        }

        // Check if Login socket is connected
        public bool IsLoginConnected()
        {
            if (loginSocket != null)
            {
                if (!loginSocket.Connected)
                    return false;
                else
                    return true;
            }
            else return false;
        }

        // Check if Chat socket is connected
        public bool IsChatConnected()
        {
            if (!isWebSocket)
            {
                if (chatSocket != null)
                {
                    if (!chatSocket.Connected)
                        return false;
                    else
                        return true;
                }
                else return false;
            }
            else
            {
                if (webSocket != null)
                {
                    if (webSocket.ReadyState != WebSocketState.Open)
                        return false;
                    else
                        return true;
                }
                else return false;
            }
        }
    }   
}