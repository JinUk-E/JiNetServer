using System.Net.Sockets;
using JiNet.Preview;
using JiNet.Utils;

namespace JiNet.ClassModule
{
    public class CNetworkService
    {
        SocketAsyncEventArgsPool recevieEventArgsPool;
        SocketAsyncEventArgsPool sendEventArgsPool;
        
        public delegate void SessionHandler(CUserToken token);
        public SessionHandler SessionCreatedCallback { get; set; }
        
        public CLogicMessageEntry LogicEntry { get; private set; }
        public CServerUserManager Usermanager { get; private set; }
        
        public CNetworkService(bool useLogicThread = false)
        {
            SessionCreatedCallback = null;
            Usermanager = new CServerUserManager();

            if (!useLogicThread) return;
            LogicEntry = new CLogicMessageEntry(this);
            LogicEntry.Start();
        }

        public void Initialize() => Initialize(Define.MaxConnections, Define.BufferSize);
        
        public void Initialize(int maxConnections, int bufferSize)
        {
            var preAllocCount = 1;
            var bufferManager = new BufferManager(maxConnections * bufferSize * preAllocCount, bufferSize);
            recevieEventArgsPool = new SocketAsyncEventArgsPool(maxConnections);
            sendEventArgsPool = new SocketAsyncEventArgsPool(maxConnections);
            
            bufferManager.InitBuffer();


            for (var i = 0; i < maxConnections; i++)
            {
                ArgSetting(new SocketAsyncEventArgs(), ReceiveCompleted, bufferManager, recevieEventArgsPool);
                ArgSetting(new SocketAsyncEventArgs(), SendCompleted, bufferManager, sendEventArgsPool);
            }
        }

        private void ArgSetting(SocketAsyncEventArgs arg, Action<object, SocketAsyncEventArgs> innerFunc
            , BufferManager bufferManager, SocketAsyncEventArgsPool pools)
        {
            arg = new();
            arg.Completed += new EventHandler<SocketAsyncEventArgs>(innerFunc);
            arg.UserToken = null;
            
            bufferManager.SetBuffer(arg);
            pools.Push(arg);
        }

        public void Listen(string host, int port, int backlog)
        {
            var clientListener = new CListener();
            clientListener.callbackOnNewclient += OnNewClient;
            clientListener.Start(host,port,backlog);

            byte checkInterval = 10;
            Usermanager.StartHeartbeatChecking(checkInterval, checkInterval);
        }
        public void DisableHeartbeat() => Usermanager.StopHeartbeatChecking();

        public void OnConnectCompleted(Socket socket, CUserToken token)
        {
            token.onSessionClosedCallback += OnSessionClosed;
            Usermanager.Add(token);
            
            // SocketAsyncEventArgsPool에서 빼오지 않고 그때 그때 할당해서 사용한다.
            // 풀은 서버에서 클라이언트와의 통신용으로만 쓰려고 만든것이기 때문이다.
            // 클라이언트 입장에서 서버와 통신을 할 때는 접속한 서버당 두개의 EventArgs만 있으면 되기 때문에 그냥 new해서 쓴다.
            // 서버간 연결에서도 마찬가지이다.
            // 풀링처리를 하려면 c->s로 가는 별도의 풀을 만들어서 써야 한다.
            var receiveEventArg = new SocketAsyncEventArgs();
            receiveEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(ReceiveCompleted);
            receiveEventArg.UserToken = token;
            receiveEventArg.SetBuffer(new byte[Define.BufferSize], 0, Define.BufferSize);
            
            var sendEventArg = new SocketAsyncEventArgs();
            sendEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(SendCompleted);
            sendEventArg.UserToken = token;
            sendEventArg.SetBuffer(null, 0, 0);
            
            BeginReceive(socket, receiveEventArg, sendEventArg);
        }
        
        /// <summary>
        /// 새로운 클라이언트가 접속 성공 했을 때 호출됩니다.
        /// AcceptAsync의 콜백 매소드에서 호출되며 여러 스레드에서 동시에 호출될 수 있기 때문에 공유자원에 접근할 때는 주의해야 합니다.
        /// </summary>
        /// <param name="clientsocket"></param>
        /// <param name="token"></param>
        private void OnNewClient(Socket clientsocket, object token)
        {
            var receiveArgs = recevieEventArgsPool.Pop();
            var sendArgs = sendEventArgsPool.Pop();
            // UserToken은 매번 새로 생성하여 깨끗한 인스턴스로 넣어준다.
            var userToken = new CUserToken(LogicEntry);
            userToken.onSessionClosedCallback += OnSessionClosed;
            receiveArgs.UserToken = userToken;
            sendArgs.UserToken = userToken;
            
            Usermanager.Add(userToken);
            userToken.OnConnected();
            if(SessionCreatedCallback != null) SessionCreatedCallback(userToken);
            
            BeginReceive(clientsocket, receiveArgs, sendArgs);

            var msg = CPacket.Create(CUserToken.SYS_START_HEARBEAT);
            byte sendInterval = 5;
            msg.Push(sendInterval);
            userToken.Send(msg);
        }

        private void BeginReceive(Socket socket, SocketAsyncEventArgs receiveEventArg, SocketAsyncEventArgs sendEventArg)
        {
            var token = receiveEventArg.UserToken as CUserToken;
            token?.SetEventArgs(receiveEventArg, sendEventArg);
            if (token != null) token.Socket = socket;

            if (!socket.ReceiveAsync(receiveEventArg)) ProcessReceive(receiveEventArg);

        }
        
        private void ReceiveCompleted(object? sender, SocketAsyncEventArgs e)
        {
            if (e.LastOperation != SocketAsyncOperation.Receive)
                throw new Exception("The last operation completed on the socket was not a receive.");
            ProcessReceive(e);
        }
        
        private void SendCompleted(object sender, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            try
            {
                var token = socketAsyncEventArgs.UserToken as CUserToken;
                token?.ProcessSend(socketAsyncEventArgs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
        private void ProcessReceive(SocketAsyncEventArgs receiveEventArg)
        {
            var token = receiveEventArg.UserToken as CUserToken;
            if(receiveEventArg.BytesTransferred > 0 && receiveEventArg.SocketError == SocketError.Success)
            {
                token?.OnReceive(receiveEventArg.Buffer, receiveEventArg.Offset, receiveEventArg.BytesTransferred);
                if(!token.Socket.ReceiveAsync(receiveEventArg)) ProcessReceive(receiveEventArg);
                
            }
            else
            {
                try
                {
                    token?.Close();
                }
                catch (Exception)
                {
                    Console.WriteLine("Already closed this socket.");
                }
            }
        }

        private void OnSessionClosed(CUserToken token)
        {
            Usermanager.Remove(token);
            if(recevieEventArgsPool != null) recevieEventArgsPool.Push(token.ReceiveEventArgs);
            if(sendEventArgsPool != null) sendEventArgsPool.Push(token.SendEventArgs);
            token.SetEventArgs(null,null);
        }

        
        
        
    }    
}
