using System.Net;
using System.Net.Sockets;
using JiNet.Preview;

namespace JiNet.ClassModule
{
    public class CListener
    {
        // 비동기 Accept를 위한 EventArgs.
        SocketAsyncEventArgs acceptArgs;
        Socket listenSocket;
        // Accept처리의 순서를 제어하기 위한 이벤트 변수.
        AutoResetEvent flowControlEvent;
        
        // 새로운 클라이언트가 접속했을 때 호출되는 콜백.
        public delegate void NewclientHandler(Socket clientSocket, object token);
        public NewclientHandler callbackOnNewclient = null;

        public void Start(string host, int port, int backlog)
        {
            listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var address = (host.Equals(Define.LocalHost)) ? IPAddress.Any : IPAddress.Parse(host);
            var endPoint = new IPEndPoint(address, port);

            try
            {
                listenSocket.Bind(endPoint);
                listenSocket.Listen(backlog);

                acceptArgs = new SocketAsyncEventArgs();
                acceptArgs.Completed += OnAcceptCompleted;

                var listenThread = new Thread(DoListen);
                listenThread.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        /// <summary>
        /// 클라이언트 목록을 순회하며 순차적으로 처리하도록 로직을 정리해둠.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        private void DoListen()
        {
            flowControlEvent = new(false);

            while (true)
            {
                acceptArgs.AcceptSocket = null;
                var pending = true;
                try
                {
                    pending = listenSocket.AcceptAsync(acceptArgs);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                // 즉시 완료 되면 이벤트가 발생하지 않으므로 리턴값이 false일 경우 콜백 매소드를 직접 호출해 줍니다.
                // pending상태라면 비동기 요청이 들어간 상태이므로 콜백 매소드를 기다리면 됩니다.
                // http://msdn.microsoft.com/ko-kr/library/system.net.sockets.socket.acceptasync%28v=vs.110%29.aspx
                if (!pending) OnAcceptCompleted(null, acceptArgs);
                
                // 접속처리가 완료되면 다음 Accept를 받아들일 수 있도록 통보합니다.
                flowControlEvent.WaitOne();
            }
        }

        private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError.Equals(SocketError.Success))
            {
                var clientSocket = e.AcceptSocket;
                clientSocket.NoDelay = true;
                
                // 이 클래스에서는 accept까지의 역할만 수행하고 클라이언트의 접속 이후의 처리는
                // 외부로 넘기기 위해서 콜백 매소드를 호출해 주도록 합니다.
                // 이유는 소켓 처리부와 컨텐츠 구현부를 분리하기 위함입니다.
                // 컨텐츠 구현부분은 자주 바뀔 가능성이 있지만, 소켓 Accept부분은 상대적으로 변경이 적은 부분이기 때문에
                // 양쪽을 분리시켜주는것이 좋습니다.
                // 또한 클래스 설계 방침에 따라 Listen에 관련된 코드만 존재하도록 하기 위한 이유도 있습니다.
                if (e.UserToken != null) callbackOnNewclient(clientSocket, e.UserToken);
                
                flowControlEvent.Set();
                return;
            }
            flowControlEvent.Set();
        }
    }    
}

