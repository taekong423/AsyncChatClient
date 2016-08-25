using System;
using System.Text;
using System.Runtime.InteropServices;
using LunkerLibrary.common.protocol;
using AsyncChat;
using static AsyncChat.CommonHelper;


namespace AsyncClient
{
    class ResponseHandler
    {
        Client client;
        

        public ResponseHandler(Client client)
        {
            this.client = client;
        }

        public void HandleResponse(byte[] data)
        {
            if (IsHeader(data) && client.doesBodyExist == false)
            {
                client.responseHeader = BytesToStruct<CommonHeader>(data);
                client.DebugDisplay("[Received] " + client.DebugHeader(client.responseHeader));

                if (client.responseHeader.BodyLength == 0)
                {
                    client.doesBodyExist = false;
                    if (client.responseHeader.State == MessageState.Success)
                    {
                        switch (client.responseHeader.Type)
                        {
                            // Move to Lobby State when ConnectionSetup Success Received
                            case MessageType.ConnectionSetup:
                                ResponseState();
                                if (client.isPassing)
                                {
                                    client.isPassing = false;
                                    client.JoinRequest(client.room.RoomNo);
                                }
                                else
                                {
                                    client.runType = RunType.Lobby;
                                    client.runState = RunState.Idle;
                                }
                                break;

                            case MessageType.ConnectionPassing:
                                ConnectionPassing();
                                break;

                            // Move to Room State
                            case MessageType.JoinRoom:
                                ResponseState();
                                client.runType = RunType.Room;
                                client.runState = RunState.Idle;
                                break;

                            // Leave Room and Move to Lobby State
                            case MessageType.LeaveRoom:
                                ResponseState();
                                client.room.RoomNo = 0;
                                client.runType = RunType.Lobby;
                                client.runState = RunState.Idle;
                                break;

                            // Disconnect from Chat server... Move to Login State
                            case MessageType.Logout:
                                ResponseState();
                                client.BackToLoginServer();
                                client.InitializeLogin();
                                client.runState = RunState.Idle;
                                break;

                            default:
                                ResponseState();
                                client.runState = RunState.Idle;
                                break;
                        }
                    }
                    else
                    {
                        ResponseState();
                        client.runState = RunState.Idle;
                    }
                }
                else if (client.responseHeader.BodyLength > 0)
                    client.doesBodyExist = true;
                else
                {
                    client.doesBodyExist = false;
                    client.SendDisplay("Invalid BodyLength", ChatType.System);
                    client.runState = RunState.Idle;
                }
            }
            else if (client.doesBodyExist == true)
            {
                client.doesBodyExist = false;

                // Response Body
                if (client.responseHeader.State == MessageState.Success)
                {
                    switch (client.responseHeader.Type)
                    {
                        // Chatting Message (byte[])
                        case MessageType.Chatting:
                            Chatting(data);
                            break;

                        // Signin Body (ServerInfo, Cookie)
                        case MessageType.Signin:
                            Signin(data);
                            break;

                        // ListRoom Body (ChattingRoom[])
                        case MessageType.ListRoom:
                            List(data);
                            client.runState = RunState.Idle;
                            break;

                        // CreateRoom Body (
                        case MessageType.CreateRoom:
                            Create(data);
                            client.runState = RunState.Idle;
                            break;

                        default:
                            client.SendDisplay("Invalid Type", ChatType.System);
                            client.runState = RunState.Idle;
                            break;
                    }
                }
                else if (client.responseHeader.State == MessageState.Fail)
                {
                    switch (client.responseHeader.Type)
                    {
                        // Connection Pass to Join Room in other Server
                        case MessageType.JoinRoom:
                            JoinFail(data);
                            break;
                        default:
                            ResponseState();
                            client.runState = RunState.Idle;
                            break;
                    }
                }
                else
                {
                    ResponseState();
                    client.runState = RunState.Idle;
                }
            }
            else
            {
                client.SendDisplay("Wrong Data", ChatType.System);
                client.runState = RunState.Idle;
            }
        }

        public bool IsHeader(byte[] data)
        {
            int dataSize = data.Length;
            CommonHeader headerStruct = BytesToStruct<CommonHeader>(data);
            bool isCorrect;

            switch (headerStruct.State)
            {
                case MessageState.Request:
                case MessageState.Response:
                case MessageState.Success:
                case MessageState.Fail:
                case MessageState.Error:
                    isCorrect = true;
                    break;
                default:
                    isCorrect = false;
                    break;
            }
            if (dataSize == Marshal.SizeOf(typeof(CommonHeader)) && isCorrect)
            {
                return true;
            }
            else return false;
        }

        private void ResponseState()
        {
            client.SendDisplay(string.Format("{0} {1}", client.responseHeader.Type.ToString(), client.responseHeader.State.ToString()), ChatType.System);
        }

        private void ConnectionPassing()
        {
            client.ConnectChatServer();
        }

        private void Signin(byte[] data)
        {
            CLSigninResponseBody signinBody = BytesToStruct<CLSigninResponseBody>(data);
            client.serverInfo = signinBody.ServerInfo;
            client.DebugDisplay(string.Format("{0}:{1}", signinBody.ServerInfo.GetPureIp(), signinBody.ServerInfo.Port));
            client.userInfo = client.responseHeader.UserInfo;
            client.cookie = client.responseHeader.Cookie;
            client.ConnectChatServer();
        }

        private void List(byte[] data)
        {
            object[] objArr = ByteToStructureArray(data, typeof(ChattingRoom));
            ChattingRoom[] roomList = Array.ConvertAll(objArr, element => (ChattingRoom)element);

            client.SendDisplay(                  "┌------< LIST >------┐", ChatType.Console);
            for (int i = 0; i < roomList.Length; i++)
                client.SendDisplay(string.Format("        Room [{0}]", roomList[i].RoomNo), ChatType.Console);
            client.SendDisplay(                  "└--------------------┘", ChatType.Console);
        }

        private void JoinFail(byte[] data)
        {
            CCJoinResponseBody joinBody = BytesToStruct<CCJoinResponseBody>(data);
            client.serverInfo = joinBody.ServerInfo;

            client.ConnectionPassing();
        }

        private void Create(byte[] data)
        {
            CCCreateRoomResponseBody createBody = BytesToStruct<CCCreateRoomResponseBody>(data);
            client.SendDisplay(string.Format("Room {0} Created", createBody.ChattingRoom.RoomNo), ChatType.Receive);
        }

        private void Chatting(byte[] data)
        {
            string id = client.responseHeader.UserInfo.GetPureId();

            string message = "[" + id + "] " + Encoding.UTF8.GetString(data);
            client.SendDisplay(message, ChatType.Receive);
        }
    }
}
