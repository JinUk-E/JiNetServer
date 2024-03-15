using JiNet.Interface;

namespace JiNet.ClassModule
{
    public class CLogicMessageEntry(CNetworkService service) : IMessageDispatcher
    {
        CNetworkService service = service;
        ILogicQueue messageQueue = new CDoubleBufferingQueue();
        AutoResetEvent logicEvent = new(false);


        public void Start()
        {
            var logic = new Thread(DoLogic);
            logic.Start();
        }
        
        public void OnMessage(CUserToken userToken, ArraySegment<byte> buffer)
        {
            // IO스레드에서 호출된다.
            var msg = new CPacket(buffer, userToken);
            messageQueue.enqueue(msg);
            logicEvent.Set();
        }

        private void DoLogic()
        {
            while(true)
            {
                logicEvent.WaitOne();
                DispatchAll(messageQueue.PacketQueue());
            }
        }

        private void DispatchAll(Queue<CPacket> queue)
        {
            while (queue.Count > 0)
            {
                var msg = queue.Dequeue();
                if(!service.Usermanager.isExist(msg.Ower)) continue;
                msg.Ower.OnMessage(msg);
            }
        }
    }    
}

