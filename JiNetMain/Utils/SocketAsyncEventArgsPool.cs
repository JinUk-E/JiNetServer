using System.Net.Sockets;

namespace JiNet.Utils
{
    public class SocketAsyncEventArgsPool(int capacity)
    {
        private Stack<SocketAsyncEventArgs> socketPool = new(capacity);
        public void Push(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (socketAsyncEventArgs == null)
                throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null");
            lock (socketPool)
            {
                if (socketPool.Contains(socketAsyncEventArgs)) throw new Exception("Already exist item.");
                socketPool.Push(socketAsyncEventArgs);
            }
        }

        public SocketAsyncEventArgs Pop()
        {
            lock (socketPool)
            {
                return socketPool.Pop();
            }
        }
        
        public int Count => socketPool.Count;
    }    
}

