using System.Net;
using System.Net.Sockets;

namespace JiNet.ClassModule
{
    /// <summary>
    /// Endpoint 정보를 받아서 접속을 시도하는 객체
    /// </summary>
    public class CConnector
    {
        public delegate void ConnectedHandler(CUserToken token);
        
        public ConnectedHandler ConnectedCallback { get; set; }

        private Socket _client;
        private CNetworkService _networkService;

        public CConnector(CNetworkService networkService)
        {
            _networkService = networkService;
            ConnectedCallback = null;
        }

        public void Connect(IPEndPoint endPoint)
        {
            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _client.NoDelay = true;
            
            // 비동기 처리
            SocketAsyncEventArgs eventArgs = new();
            eventArgs.Completed += OnConnectCompleted;
            eventArgs.RemoteEndPoint = endPoint;
            var pending = _client.ConnectAsync(eventArgs);
            if(!pending) OnConnectCompleted(null,eventArgs);
        }

        private void OnConnectCompleted(object? sender, SocketAsyncEventArgs eventArgs)
        {
            if (eventArgs.SocketError != SocketError.Success)
            {
                Console.WriteLine(string.Format("Failed to Connected {0}", eventArgs.SocketError));
                return;
            }

            CUserToken token = new(_networkService.LogicEntry);
        }
        
    }    
}

