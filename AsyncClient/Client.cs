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
        private Socket loginSocket = null;
        private Socket chatSocket = null;
        
        private int line;
        private int viewCount = 27;
        private int recvBufferSize = 200;

        private bool debug = true;

        public List<StringBuilder> display = null;
        public WebSocket webSocket = null;
        public UserInfo userInfo;
        public Cookie cookie;
        public ServerInfo serverInfo;
        public ChattingRoom room;
        public int roomNo = 0;
        
        public ServerType serverType;
        public RunType runType;
        public RunState runState;
        public bool isWebSocket = false;

        public CommonHeader responseHeader;

        public bool doesBodyExist = false;

        public Client()
        {
            InitializeClient();
            display = new List<StringBuilder>();
            line = 0;
        }

        public void InitializeClient()
        {
            currentSocket = null;
            newSocket = null;

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

            runType = RunType.Start;
            runState = RunState.Idle;
        }

        public void ChangeCurrentSocket(ServerType server)
        {
            currentSocket = null;
            if (server == ServerType.Login)
                currentSocket = loginSocket;
            else if (server == ServerType.Chat)
                currentSocket = chatSocket;
        }

        public void BackToLoginServer()
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
                    if (webSocket.IsAlive)
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

        private void StartWebClient(string url)
        {
            webSocket = new WebSocket(url);
            webSocket.WaitTime = TimeSpan.MaxValue;
            webSocket.ConnectAsync();
            
            webSocket.OnOpen += (sender, e) =>
            {
                ConnectionSetupRequest();
            };

            webSocket.OnMessage += (sender, e) =>
            {
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

        public void Connect(string address, int port, ServerType server)
        {
            serverType = server;
            if (port == 80)
            {
                isWebSocket = true;
                string url = "ws://" + address + ":" + port.ToString() + "/";
                StartWebClient(url);
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
            }
        }

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
                RedirectSocket();
                runState = RunState.Idle;
            }
        }

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
                RedirectSocket();
                runState = RunState.Idle;
            }
        }

        private void Connect_Completed(object sender, SocketAsyncEventArgs e)
        {
            currentSocket = null;

            if (serverType == ServerType.Login)
            {
                loginSocket = (Socket)sender;
                currentSocket = loginSocket;
            }
            else if (serverType == ServerType.Chat)
            {
                chatSocket = (Socket)sender;
                currentSocket = chatSocket;
            }

            if (currentSocket.Connected)
            {
                if (serverType == ServerType.Login)
                {
                    SendDisplay("Connected to Login Server!", ChatType.System);
                    runType = RunType.Login;
                    runState = RunState.Idle;
                    Receive();
                }
                else if (serverType == ServerType.Chat)
                {
                    ConnectionSetupRequest();
                    Receive();
                }
            }
            else
            {
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

        private void Send_Completed(object sender, SocketAsyncEventArgs e)
        {
            Socket client = (Socket)sender;
            DebugDisplay("Data Sent");
        }

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
                RedirectSocket();
                runState = RunState.Idle;
            }
        }

        public void ConnectionPassing()
        {
            Connect(serverInfo.GetPureIp(), serverInfo.Port, ServerType.Chat);
        }

        public void ConnectionSetupRequest()
        {
            CommonHeader header = new CommonHeader(MessageType.ConnectionSetup, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void SignupRequest(string id, string pwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Signup, MessageState.Request, 0, cookie, userInfo);

            SendStructAsBytes<CommonHeader>(header);
        }

        public void SigninRequest(string id, string pwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Signin, MessageState.Request, 0, cookie, userInfo);

            SendStructAsBytes<CommonHeader>(header);
        }
        
        public void ModifyRequest(string id, string pwd, string nPwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            int bodyLength = Marshal.SizeOf(typeof(CLModifyRequestBody));
            CommonHeader header = new CommonHeader(MessageType.Modify, MessageState.Request, bodyLength, cookie, userInfo);
            CLModifyRequestBody modifyReqBody = new CLModifyRequestBody(userInfo, nPwd);

            SendStructAsPacket(header, modifyReqBody);

            //SendStructAsBytes<CommonHeader>(header);
            //SendStructAsBytes<CLModifyRequestBody>(modifyReqBody);
        }

        public void DeleteRequest(string id, string pwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Delete, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void JoinRequest(int roomNo)
        {
            int bodyLength = Marshal.SizeOf(typeof(CCJoinRequestBody));
            CommonHeader header = new CommonHeader(MessageType.JoinRoom, MessageState.Request, bodyLength, cookie, userInfo);

            room = new ChattingRoom(roomNo);
            CCJoinRequestBody joinReqBody = new CCJoinRequestBody(room);

            SendStructAsPacket(header, joinReqBody);

            //SendStructAsBytes<CommonHeader>(header);
            //SendStructAsBytes<CCJoinRequestBody>(joinReqBody);
        }

        public void CreateRequest()
        {
            CommonHeader header = new CommonHeader(MessageType.CreateRoom, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void ListRequest()
        {
            CommonHeader header = new CommonHeader(MessageType.ListRoom, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void LogoutRequest()
        {
            CommonHeader header = new CommonHeader(MessageType.Logout, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void LeaveRequest()
        {
            int bodyLength = Marshal.SizeOf(typeof(CCLeaveRequestBody));
            CommonHeader header = new CommonHeader(MessageType.LeaveRoom, MessageState.Request, bodyLength, cookie, userInfo);
            CCLeaveRequestBody leaveReqbody = new CCLeaveRequestBody(room);

            SendStructAsPacket(header, leaveReqbody);

            //SendStructAsBytes<CommonHeader>(header);
            //SendStructAsBytes<CCLeaveRequestBody>(leaveReqbody);
        }

        public void Heartbeat()
        {

        }

        public void ChatInput()
        {
            string sData;
            while (true)
            {
                sData = Console.ReadLine();

                byte[] sendBody = Encoding.UTF8.GetBytes(sData.Trim());
                CommonHeader header = new CommonHeader(MessageType.Chatting, MessageState.Request, sendBody.Length, new Cookie(), userInfo);
                SendStructAsBytes<CommonHeader>(header);

                byte[] packet = new byte[Marshal.SizeOf(typeof(CommonHeader)) + Marshal.SizeOf(typeof(CCLeaveRequestBody))];
                Array.Copy(StructToBytes(header), 0, packet, 0, Marshal.SizeOf(typeof(CommonHeader)));
                

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
                                RedirectSocket();
                                runState = RunState.Idle;
                            }
                            else
                            {
                                Array.Copy(sendBody, 0, packet, Marshal.SizeOf(typeof(CommonHeader)), sendBody.Length);
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
                                isWebSocket = false;
                                ReconnectLoginServer();
                                runState = RunState.Idle;
                                break;
                            }
                            else
                            {
                                Array.Copy(sendBody, 0, packet, Marshal.SizeOf(typeof(CommonHeader)), sendBody.Length);
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
                RedirectSocket();
                runState = RunState.Idle;
                //Environment.Exit(0);
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

        public string DebugHeader(CommonHeader header)
        {
            string s = string.Format("[Type: {0}] [State: {1}] [BodyLength: {2}] [Cookie: {3}] [Id, Pwd: {4}, {5}] [isDummy: {6}]",
                    header.Type.ToString(), header.State.ToString(), header.BodyLength, header.Cookie.Value,
                    header.UserInfo.GetPureId(), header.UserInfo.GetPurePwd(), header.UserInfo.IsDummy);
            return s;
        }

        public void DebugDisplay(string s)
        {
            if (debug)
                SendDisplay(s, ChatType.Debug);
        }

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