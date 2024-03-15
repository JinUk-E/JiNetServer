using JiNet.Interface;

namespace JiNet.ClassModule
{
    /// <summary>
    /// 두개의 큐를 교체해가며 활용한다.
    /// IO스레드에서 입력큐에 막 쌓아놓고,
    /// 로직스레드에서 큐를 뒤바꾼뒤(swap) 쌓아놓은 패킷을 가져가 처리한다.
    /// 참고 : http://roadster.egloos.com/m/4199854
    /// </summary>
    public class CDoubleBufferingQueue : ILogicQueue
    {
        // 실제 데이터가 들어갈 큐.
        private Queue<CPacket> queue1;
        private Queue<CPacket> queue2;
        // 각각의 큐에 대한 참조.
        private Queue<CPacket> refInput;
        private Queue<CPacket> refOutput;
        
        private object? cs_write;

        public CDoubleBufferingQueue()
        {
            // 초기에는 큐와 참조가 1:1로 매칭되게 설정한다.
            // ref_input - queue1
            // ref_output - queue2
            queue1 = new();
            queue2 = new();
            refInput = queue1;
            refOutput = queue2;
        }
        
        /// <summary>
        /// IO스레드에서 전달한 패킷을 보관한다.
        /// </summary>
        public void enqueue(CPacket msg)
        {
            lock(cs_write) refInput.Enqueue(msg);
        }

        public Queue<CPacket> PacketQueue()
        {
            Swap();
            return refOutput;
        }

        private void Swap()
        {
            lock (cs_write) (refInput, refOutput) = (refOutput, refInput);
        }
    }    
}

