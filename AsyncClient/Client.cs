using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using LunkerLibrary.common.protocol;
using WebSocketSharp;
using static AsyncClient.CommonHelper;

namespace AsyncClient
{
    class Client
    {
        private Socket currentSocket = null;
        private Socket newSocket = null;
        private Socket loginSocket = null;
        private Socket chatSocket = null;
        private int viewCount = 27;
        private int recvSize = 200;

        public WebSocket webSocket = null;
        public UserInfo userInfo;
        public Cookie cookie;
        public ServerInfo serverInfo;
        public ChattingRoom room;
        public int roomNo = 0;
        public List<StringBuilder> display = null;
        public int line;
        public ServerType serverType;
        public RunType runType;
        public RunState runState;
        public bool isConnecting;
        public bool isWebSocket = false;

        public CommonHeader responseHeader;

        public bool doesBodyExist = false;

        public Client()
        {
            runType = RunType.Start;
            runState = RunState.Idle;
            display = new List<StringBuilder>();
            line = 0;
            isConnecting = false;
        }

        public void Connect(string address, int port, ServerType server)
        {
            try
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

                    newSocket = loginSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    newSocket.ConnectAsync(args);
                }
            }
            catch (Exception exc)
            {
                SendDisplay("Connect Error"+ exc.Message, ChatType.System);
                Console.ReadKey();
                Environment.Exit(0);
            }
        }


        private void StartWebClient(string url)
        {
            try
            {
                webSocket = new WebSocket(url);

                webSocket.ConnectAsync();

                webSocket.OnOpen += (sender, e) =>
                {
                    ConnectionSetup();
                };

                webSocket.OnMessage += (sender, e) =>
                {
                    ResponseHandler rh = new ResponseHandler(this);
                    rh.HandleResponse(e.RawData);
                };

                webSocket.OnClose += (sender, e) =>
                {
                    isWebSocket = false;
                };
            }
            catch (Exception exc)
            {
                SendDisplay("WebSocket Error" + exc.Message, ChatType.System);
                Console.ReadKey();
                Environment.Exit(0);
            }
        }
        

        private void Send(byte[] sendData)
        {
            try
            {
                SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();
                sendArgs.SetBuffer(sendData, 0, sendData.Length);
                sendArgs.UserToken = currentSocket;
                sendArgs.Completed += new EventHandler<SocketAsyncEventArgs>(Send_Completed);
                currentSocket.SendAsync(sendArgs);
            }
            catch (Exception exc)
            {
                SendDisplay("Send Error" + exc.Message, ChatType.System);
                Console.ReadKey();
                Environment.Exit(0);
            }
        }

        private void Receive()
        {
            try
            {
                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
                byte[] recvData = new byte[recvSize];
                receiveArgs.UserToken = currentSocket;
                receiveArgs.SetBuffer(recvData, 0, recvData.Length);
                receiveArgs.Completed += new EventHandler<SocketAsyncEventArgs>(Recieve_Completed);
                currentSocket.ReceiveAsync(receiveArgs);
            }
            catch (Exception exc)
            {
                SendDisplay("Receive Error" + exc.Message, ChatType.System);
                Console.ReadKey();
                Environment.Exit(0);
            }
        }

        private void Connect_Completed(object sender, SocketAsyncEventArgs e)
        {
            try
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
                        ConnectionSetup();
                        Receive();
                    }
                }
                else
                {
                    currentSocket = null;
                    SendDisplay("Connection Failed!", ChatType.System);
                    SendDisplay("Press Enter Key to Retry... (/exit)", ChatType.System);
                    string command = Console.ReadLine();
                    if (command == "/exit")
                    {
                        SendDisplay("Exit Application... Bye", ChatType.System);
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                    isConnecting = false;
                    runState = RunState.Idle;
                }
            }
            catch (Exception exc)
            {
                SendDisplay("Connect_Completed Error" + exc.Message, ChatType.System);
                Console.ReadKey();
                Environment.Exit(0);
            }
        }

        private void Send_Completed(object sender, SocketAsyncEventArgs e)
        {
            Socket client = (Socket)sender;
            //SendDisplay(string.Format("Data Sent"), ChatType.Debug);
        }

        private void Recieve_Completed(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                Socket client = (Socket)sender;

                if (client.Connected && e.BytesTransferred > 0)
                {
                    //SendDisplay("Data Received", ChatType.Debug);
                    byte[] szData = new byte[e.BytesTransferred];
                    Array.Copy(e.Buffer, szData, szData.Length);
                    lock(szData)
                    {
                        ResponseHandler rh = new ResponseHandler(this);
                        rh.HandleResponse(szData);
                    }
                    byte[] recvData = new byte[recvSize];
                    e.SetBuffer(recvData, 0, recvSize);

                    client.ReceiveAsync(e);
                }
                else
                {
                    SendDisplay("Recieve Failed!", ChatType.System);
                    CheckSocketConnection();
                }
            }
            catch (Exception exc)
            {
                SendDisplay("Receive_Completed Error " + exc, ChatType.System);
                Console.ReadKey();
                Environment.Exit(0);
            }

        }

        public void ConnectionPassing()
        {
            Connect(serverInfo.GetPureIp(), serverInfo.Port, ServerType.Chat);
        }

        public void ConnectionSetup()
        {
            CommonHeader header = new CommonHeader(MessageType.ConnectionSetup, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void Signup(string id, string pwd, bool isDummy)
        {
            /*
            char[] idtmp = new char[18];
            char[] pwdtmp = new char[18];
            Array.Copy(id.ToCharArray(), idtmp, id.ToCharArray().Length);
            Array.Copy(pwd.ToCharArray(), pwdtmp, pwd.ToCharArray().Length);


            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            UserInfo userInfo = new UserInfo(idtmp, pwdtmp, isDummy);
            */

            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Signup, MessageState.Request, 0, cookie, userInfo);

            SendStructAsBytes<CommonHeader>(header);
        }

        public void Signin(string id, string pwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Signin, MessageState.Request, 0, cookie, userInfo);

            SendStructAsBytes<CommonHeader>(header);
        }
        
        public void Modify(string id, string pwd, string nPwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            int bodyLength = Marshal.SizeOf(typeof(CLModifyRequestBody));
            CommonHeader header = new CommonHeader(MessageType.Modify, MessageState.Request, bodyLength, cookie, userInfo);
            CLModifyRequestBody modifyReqBody = new CLModifyRequestBody(userInfo, nPwd);

            SendStructAsBytes<CommonHeader>(header);
            SendStructAsBytes<CLModifyRequestBody>(modifyReqBody);
        }

        public void Delete(string id, string pwd, bool isDummy)
        {
            UserInfo userInfo = new UserInfo(id, pwd, isDummy);
            Cookie cookie = new Cookie();
            CommonHeader header = new CommonHeader(MessageType.Delete, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void Join(int roomNo)
        {
            int bodyLength = Marshal.SizeOf(typeof(CCJoinRequestBody));
            CommonHeader header = new CommonHeader(MessageType.JoinRoom, MessageState.Request, bodyLength, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);

            room = new ChattingRoom(roomNo);
            CCJoinRequestBody joinReqBody = new CCJoinRequestBody(room);
            SendStructAsBytes<CCJoinRequestBody>(joinReqBody);
        }

        public void Create()
        {
            CommonHeader header = new CommonHeader(MessageType.CreateRoom, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void List()
        {
            CommonHeader header = new CommonHeader(MessageType.ListRoom, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void Logout()
        {
            CommonHeader header = new CommonHeader(MessageType.Logout, MessageState.Request, 0, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
        }

        public void Leave()
        {
            int bodyLength = Marshal.SizeOf(typeof(CCLeaveRequestBody));
            CommonHeader header = new CommonHeader(MessageType.LeaveRoom, MessageState.Request, bodyLength, cookie, userInfo);
            SendStructAsBytes<CommonHeader>(header);
            CCLeaveRequestBody leaveReqbody = new CCLeaveRequestBody(room);
            SendStructAsBytes<CCLeaveRequestBody>(leaveReqbody);
            room.RoomNo = 0;
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

                if (sData.CompareTo("/exit") == 0)
                {
                    break;
                }
                else
                {
                    if (currentSocket != null)
                    {
                        if (!currentSocket.Connected)
                        {
                            CheckSocketConnection();
                            SendDisplay("Chat Connection Failed!", ChatType.System);
                            SendDisplay("Press Any Key...", ChatType.System);
                            Console.ReadKey();
                        }
                        else
                        {
                            byte[] sendBody = Encoding.UTF8.GetBytes(sData.Trim());
                            CommonHeader header = new CommonHeader(MessageType.Chatting, MessageState.Request, sendBody.Length, new Cookie(), userInfo);
                            SendStructAsBytes<CommonHeader>(header);

                            try
                            {
                                if (isWebSocket)
                                    webSocket.Send(sendBody);
                                else
                                    Send(sendBody);
                            }
                            catch (Exception e)
                            {
                                SendDisplay("Chatting Send Error" + e.Message, ChatType.System);
                                Console.ReadKey();
                                Environment.Exit(0);
                            }

                        //SendDisplay(sData, ChatType.Send);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        public void BackToLoginSocket()
        {
            if (chatSocket != null)
            {
                if (loginSocket != null)
                {
                    currentSocket = null;
                    currentSocket = loginSocket;
                    chatSocket.Close();
                    chatSocket = null;
                    runType = RunType.Login;
                }
                else runType = RunType.Start;
            }
            else runType = RunType.Login;
        }

        public void CheckSocketConnection()
        {
            lock (currentSocket)
            {
                currentSocket = null;
                if (chatSocket != null)
                {
                    if (chatSocket.Connected)
                    {
                        currentSocket = chatSocket;
                        runType = RunType.Lobby;
                    }
                    else
                    {
                        chatSocket = null;
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
                                isConnecting = false;
                            }
                        }
                    }
                }
                else
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
                            isConnecting = false;
                        }
                    }
                }
                runState = RunState.Idle;
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
                Environment.Exit(0);
            }

            if (typeof(T) == typeof(CommonHeader))
            {
                CommonHeader header = (CommonHeader)structure;
                //SendDisplay(DebugHeader(header), ChatType.Send);
            }
            else
            {
                //SendDisplay(string.Format("{0}", typeof(T).ToString()), ChatType.Send);
            }
        }

        public string DebugHeader(CommonHeader header)
        {
            string s = string.Format("Type: {0}, State: {1}, BodyLength: {2}, Cookie: {3}, Id: {4}, Pwd: {5}, isDummy: {6}",
                    header.Type.ToString(), header.State.ToString(), header.BodyLength, header.Cookie.Value,
                    header.UserInfo.GetPureId(), header.UserInfo.GetPurePwd(), header.UserInfo.IsDummy);
            return s;
        }

        public bool IsConnected()
        {
            if (currentSocket != null)
            {
                if (!currentSocket.Connected)
                    return false;
                else
                    return true;
            }
            else return false;
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
            if (chatSocket != null)
            {
                if (!chatSocket.Connected)
                    return false;
                else
                    return true;
            }
            else return false;
        }
    }   
}