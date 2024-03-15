using System.Net.Sockets;

namespace JiNet.Utils
{
    /// <summary>
    /// 이 클래스는 SocketAsyncEventArgs 개체로 분할하여 할당할 수 있는 하나의 큰 버퍼를 생성합니다
    /// 각 소켓 I/O 작업을 통해 버퍼를 쉽게 재사용할 수 있고 힙 메모리 조각을 보호할 수 있습니다.
    ///
    /// BufferManager 클래스에 노출된 작업은 스레드 안전하지 않습니다.
    /// </summary>
    internal class BufferManager(int totalBytes, int bufferSize)
    {
        byte[] mBuffer;
        Stack<int> mFreeIndexPool = new();
        int mCurrentIndex = 0;

        /// <summary>
        /// 버퍼 풀에서 사용하는 버퍼 공간 할당
        /// </summary>
        public void InitBuffer()
        {
            //하나의 큰 버퍼를 생성하고 각 SocketAsyncEventArg 개체로 나눕니다
            mBuffer = new byte[totalBytes];
        }
        
        /// <summary>
        /// 버퍼 풀의 버퍼를 지정된 SocketAsyncEventArgs 개체에 할당합니다
        /// </summary>
        /// <returns>버퍼가 성공적으로 설정되었으면 true, 그렇지 않으면 false</returns>
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if (mFreeIndexPool.Count > 0)
                args.SetBuffer(mBuffer, mFreeIndexPool.Pop(), bufferSize);
            else
            {
                if ((totalBytes - bufferSize) < mCurrentIndex) return false;
                
                args.SetBuffer(mBuffer, mCurrentIndex, bufferSize);
                mCurrentIndex += bufferSize;
            }
            return true;
        }
        
        /// <summary>
        /// SocketAsyncEventArg 개체에서 버퍼를 제거합니다. 그러면 버퍼가 다시 버퍼 풀로 이동할 수 있습니다
        /// </summary>
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            mFreeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }    
}

