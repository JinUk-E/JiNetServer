
using System.Net.Sockets;
using JiNet.Interface;
using JiNet.Preview;

namespace JiNet.ClassModule
{
    public class CUserToken(IMessageDispatcher dispatcher)
    {
        // 객체 종료 관련 처리
        private const short SYS_CLOSE_REQ = 0; // 종료 요청
        private const short SYS_CLOSE_ACK = -1; // 종료 응답
        
        public const short SYS_START_HEARBEAT = -2;
        public const short SYS_UPDATE_HEARBEAT = -3;
        
        // 객체 종료 관련 처리, 중복 방지를 위한 변수
        // 0 = 연결된 상태.
        // 1 = 종료된 상태.
        private int IsClosed = 0;
        
        // session객체, 어플리케이션에서 구현하여 사용
        private IPeer peer = null;
        
        private Enums.UserTokenState currentState = Enums.UserTokenState.Idle;
        private CMessageResolver messageResolver = new();
        
        // Buffer 를 리스트로 처리하기 위해 리스트 사용 -> 성능 향상을 위해(참조 : https://github.com/sunduk/FreeNet.git )
        private List<ArraySegment<byte>> sendingList = new();
        private object lockSendingList = new();

        private CHeatbeatChecker heartbeatChecker;
        private bool autoHeartbeat = false;
        
        public Socket Socket { get; set; }
        
        public SocketAsyncEventArgs ReceiveEventArgs { get; private set; }
        public SocketAsyncEventArgs SendEventArgs { get; private set; }
        
        public delegate void CloseHandler(CUserToken token);
        public CloseHandler onSessionClosedCallback;
        
        public long lastHeartbeatTime { get; private set; } = DateTime.Now.Ticks;

        public void OnConnected()
        {
            currentState = Enums.UserTokenState.Connected;
            IsClosed = 0;
            autoHeartbeat = true;
        }
        
        public void SetEventArgs(SocketAsyncEventArgs receiveArgs, SocketAsyncEventArgs sendArgs)
        {
            ReceiveEventArgs = receiveArgs;
            SendEventArgs = sendArgs;
        }
        
        public void OnReceive(byte[] buffer, int offset, int transfered) => 
            messageResolver.OnReceive(buffer, offset, transfered, OnMessgaeCompleted);

        public void SetPeer(IPeer peer) => this.peer = peer;

        private void OnMessgaeCompleted(ArraySegment<byte> buffer)
        {
            if (peer == null) return;
            if (dispatcher!=null) dispatcher.OnMessage(this, buffer);
            else
            {
                CPacket msg = new(buffer, this);
                OnMessage(msg);
            }
            
        }   
        
        public void OnMessage(CPacket msg)
        {
            switch (msg.ProtocolID)
            {
                case SYS_CLOSE_REQ:
                    Disconnect();
                    return;
                case SYS_START_HEARBEAT:
                    {
                        msg.PopProtocolId();
                        var interval = msg.PopByte();
                        heartbeatChecker = new CHeatbeatChecker(this, interval);
                        if(autoHeartbeat) StartHeartbeat();
                    }
                    return;
                case SYS_UPDATE_HEARBEAT:
                    lastHeartbeatTime = DateTime.Now.Ticks;
                    return;
            }

            if (this.peer != null)
            {
                try
                {
                    switch (msg.ProtocolID)
                    {
                        case SYS_CLOSE_ACK:
                            this.peer.OnRemoved();
                            break;

                        default:
                            this.peer.OnMessage(msg);
                            break;
                    }
                }
                catch (Exception)
                {
                    Close();
                }
            }

            if (msg.ProtocolID != SYS_CLOSE_ACK) return;
            if (onSessionClosedCallback != null)
            {
                onSessionClosedCallback(this);
            }
        }
        
        public void Close()
        {
            if(Interlocked.CompareExchange(ref IsClosed, 1, 0).Equals(1)) return;
            if(currentState.Equals(Enums.UserTokenState.Closed)) return;
            currentState = Enums.UserTokenState.Closed;
            Socket.Close();
            Socket = null;
            SendEventArgs.UserToken = null;
            ReceiveEventArgs.UserToken = null;
            
            sendingList.Clear();
            messageResolver.ClearBuffer();
            
            if(peer == null) return;
            var msg = CPacket.Create((short)-1);
            if(dispatcher!=null) dispatcher.OnMessage(this, new ArraySegment<byte>(msg.Buffer, 0, msg.position));
            else OnMessage(msg);
        }
        
        public void Send(CPacket msg)
        {
            msg.RecordSize();
            Send(new ArraySegment<byte>(msg.Buffer, 0, msg.position));
        }

        public void Send(ArraySegment<byte> data)
        {
            lock (lockSendingList)
            {
                sendingList.Add(data);
                if(sendingList.Count > 1) return;
            }

            StartSend();
        }

        private void StartSend()
        {
            try
            {
                // 성능 향상을 위해 SetBuffer에서 BufferList를 사용하는 방식으로 변경함.
                SendEventArgs.BufferList = sendingList;

                // 비동기 전송 시작.
                if (!Socket.SendAsync(SendEventArgs))
                {
                    ProcessSend(SendEventArgs);
                }
            }
            catch (Exception e)
            {
                if (Socket == null)
                {
                    Close();
                    return;
                }

                Console.WriteLine("send error!! close socket. " + e.Message);
                throw new Exception(e.Message, e);
            }
        }
        
        static int sendCount = 0;
        static object csCount = new();
        
        public void ProcessSend(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            // 연결이 끊겨서 이미 소켓이 종료된 경우일 것이다.
            if (socketAsyncEventArgs.BytesTransferred <= 0 || socketAsyncEventArgs.SocketError != SocketError.Success) return;

            lock (lockSendingList)
            {
                // 리스트에 들어있는 데이터의 총 바이트 수.
                var size = sendingList.Sum(obj => obj.Count);

                // 전송이 완료되기 전에 추가 전송 요청을 했다면 sending_list에 무언가 더 들어있을 것이다.
                if (socketAsyncEventArgs.BytesTransferred != size)
                {
                    if (socketAsyncEventArgs.BytesTransferred < sendingList[0].Count)
                    {
                        var error = $"Need to send more! transferred {socketAsyncEventArgs.BytesTransferred},  packet size {size}";
                        Console.WriteLine(error);
                        Close();
                        return;
                    }

                    // 보낸 만큼 빼고 나머지 대기중인 데이터들을 한방에 보내버린다.
                    var sent_index = 0;
                    var sum = 0;
                    for (var i = 0; i < sendingList.Count; i++)
                    {
                        sum += sendingList[i].Count;
                        if (sum <= socketAsyncEventArgs.BytesTransferred)
                        {
                            // 여기 까지는 전송 완료된 데이터 인덱스.
                            sent_index = i;
                            continue;
                        }

                        break;
                    }
                    // 전송 완료된것은 리스트에서 삭제한다.
                    sendingList.RemoveRange(0, sent_index + 1);

                    // 나머지 데이터들을 한방에 보낸다.
                    StartSend();
                    return;
                }

                // 다 보냈고 더이상 보낼것도 없다.
                sendingList.Clear();
 
                // 종료가 예약된 경우, 보낼건 다 보냈으니 진짜 종료 처리를 진행한다.
                if (currentState == Enums.UserTokenState.ReserveClosing) Socket.Shutdown(SocketShutdown.Send);
            }
        }
        
        public void Disconnect()
        {
            // close the socket associated with the client
            try
            {
                if (sendingList.Count <= 0)
                {
                    Socket.Shutdown(SocketShutdown.Send);
                    return;
                }

                currentState = Enums.UserTokenState.ReserveClosing;
            }
            // throws if client process has already closed
            catch (Exception)
            {
                Close();
            }
        }

        public void Ban()
        {
            try
            {
                ByeBye();
            }
            catch (Exception)
            {
                Close();
            }
        }
        private void ByeBye() => Send(CPacket.Create(SYS_CLOSE_REQ));
        
        public bool IsConnected() => currentState == Enums.UserTokenState.Connected;

        public void StartHeartbeat()
        {
            if(heartbeatChecker!=null) heartbeatChecker.Play();
        }
        public void StopHeartbeat()
        {
            if(heartbeatChecker!=null) heartbeatChecker.Stop();
        }
        public void DisableHeartbeat()
        {
            StopHeartbeat();
            autoHeartbeat = false;
        }

        public void UpdateHearbeatManually(float time)
        {
            if(heartbeatChecker != null) heartbeatChecker.Update(time);
        }
    }

}

