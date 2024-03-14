
using System.Net.Sockets;
using JiNet.Define;
using JiNet.Interface;

namespace JiNet.ClassModule
{
    public class CUserToken
    {
#region Private Variables
        
        // 객체 종료 관련 처리
        private const short SYS_CLOSE_REQ = 0; // 종료 요청
        private const short SYS_CLOSE_ACK = -1; // 종료 응답
        
        // 객체 종료 관련 처리, 중복 방지를 위한 변수
        private bool IsClosed = true;
        
        // session객체, 어플리케이션에서 구현하여 사용
        private IPeer peer;
        
        private Enums.UserTokenState currentState;
        private CMessageResolver messageResolver;
        
        // Buffer 를 리스트로 처리하기 위해 리스트 사용 -> 성능 향상을 위해(참조 : https://github.com/sunduk/FreeNet.git )
        private List<ArraySegment<byte>> sendingList;
        private object lockSendingList;
        
        private IMessageDispatcher messageDispatcher;
        
        private CHeatbeatChecker heartbeatChecker;
        private bool autoHeartbeat = false;
        
#endregion

#region Public Variables


        public const short SYS_START_HEARBEAT = -2;
        public const short SYS_UPDATE_HEARBEAT = -3;
        
        public Socket Socket { get; set; }
        
        public SocketAsyncEventArgs ReceiveEventArgs { get; private set; }
        public SocketAsyncEventArgs SendEventArgs { get; private set; }
        
        public delegate void CloseHandler(CUserToken token);
        public CloseHandler onSessionClosedCallback;
        
        public long lastHeartbeatTime { get; private set; }
        
#endregion

#region Public Methods

        public CUserToken(IMessageDispatcher dispatcher)
        {
            messageDispatcher = dispatcher;
            lockSendingList = new();
            messageResolver = new();
            peer = null;
            sendingList = new();
            lastHeartbeatTime = DateTime.Now.Ticks;
            
            currentState = Enums.UserTokenState.Idle;
        }

        public void OnConnected()
        {
            currentState = Enums.UserTokenState.Connected;
            IsClosed = false;
            autoHeartbeat = true;
        }
        
        public void SetEventArgs(SocketAsyncEventArgs receiveArgs, SocketAsyncEventArgs sendArgs)
        {
            ReceiveEventArgs = receiveArgs;
            SendEventArgs = sendArgs;
        }
        
        public void OnReceive(byte[] buffer, int offset, int transfered)
        {
            messageResolver.OnReceive(buffer, offset, transfered, OnMessgaeCompleted);
        }
        
        public void SetPeer(IPeer peer) => this.peer = peer;

        public void OnMessage(CPacket msg)
        {
            
        }

#endregion


#region Private Methods

        private void OnMessgaeCompleted(ArraySegment<byte> buffer)
        {
            if (peer == null) return;
            if (messageDispatcher!=null) messageDispatcher.OnMessage(this, buffer);
            else
            {
                CPacket msg = new(buffer, this);
                OnMessage(msg);
            }
            
        }   

#endregion
    }

}

