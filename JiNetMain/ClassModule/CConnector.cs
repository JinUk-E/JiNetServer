using System.Net;
using System.Net.Sockets;

namespace JiNet.ClassModule
{
    /// <summary>
    /// Endpoint 정보를 받아서 접속을 시도하는 객체
    /// </summary>
    public class CConnector(CNetworkService networkService)
    {
        public delegate void ConnectedHandler(CUserToken token);
        
        public ConnectedHandler ConnectedCallback { get; set; } = null;

        private Socket _client;

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
            Console.WriteLine("Connected!");
            // token은 현재 접속한 원격지 서버를 의미.
            CUserToken token = new(networkService.LogicEntry);
            
            // 1) 어플리케이션 코드로 '접속 완료' 콜백을 전달한다.
            // 반드시 아래 on_connect_completed함수가 수행되기 전에 호출되어야 한다.
            // 네트웍 코드로부터 패킷 수신 처리가 수행되기 전에 어플리케이션 코드에서 모든 준비를 마쳐놓고 기다려야 하기 때문이다.
            // 만약 2)번이 먼저 수행되고 그 다음 1)번이 수행된다면 네트웍 코드에서 수신한 패킷을 어플리케이션에서 받아가지 못할 상황이 발생할 수 있다.
            ConnectedCallback?.Invoke(token);
            
            // 2) 데이터 수신 준비.
            // 아래 함수가 호출된 직후부터 패킷 수신이 가능하다.
            // 딜레이 없이 즉시 패킷 수신 처리가 이루어 질 수 있으므로
            // 어플리케이션쪽 코드에서는 네트웍 코드가 넘겨준 패킷을 처리할 수 있는 상태여야 한다.
            networkService.OnConnectCompleted(_client,token);
        }
        
    }    
}

