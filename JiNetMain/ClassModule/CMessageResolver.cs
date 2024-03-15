using System.Diagnostics;
using JiNet.Preview;

namespace JiNet.ClassModule
{
    public delegate void CompletedMessageCallback(ArraySegment<byte> buffer);
    
    public class CMessageResolver
    {
        private int messageSize = 0;
        private byte[] messageBuffer = new byte[Define.BufferSize];
        private int currentPosition = 0;
        private int positionToRead = 0;
        private int remainBytes = 0;
        
        private bool ReadUntil(byte[] buffer, ref int srcPosition)
        {
            var copySize = positionToRead - currentPosition;
            
            if (remainBytes < copySize) copySize = remainBytes;

            Array.Copy(buffer, srcPosition, messageBuffer, currentPosition, copySize);

            srcPosition += copySize;
            currentPosition += copySize;
            remainBytes -= copySize;
            
            return currentPosition >= positionToRead;
        }
        
        public void OnReceive(byte[] buffer, int offset, int transfered, Action<ArraySegment<byte>> callback)
        {
            remainBytes = transfered;
            
            var srcPosition = offset;
            while (remainBytes > 0)
            {
                var completed = false;

                if (currentPosition < Define.Headersize)
                {
                    positionToRead = Define.Headersize;
                    completed = ReadUntil(buffer, ref srcPosition);
                    if(!completed) return;
                    
                    messageSize = GetTotalMessageSize();
                    if (messageSize <= 0)
                    {
                        ClearBuffer();
                        return;
                    }
                    
                    positionToRead = messageSize;
                    if(remainBytes <= 0) return;
                }
                
                completed = ReadUntil(buffer, ref srcPosition);

                if (!completed) continue;
                var clone = new byte[positionToRead];
                Array.Copy(messageBuffer, clone, positionToRead);
                ClearBuffer();
                callback(new ArraySegment<byte>(clone, 0, positionToRead));
            }
        }

        public void ClearBuffer()
        {
            Array.Clear(messageBuffer,0, messageBuffer.Length);
            currentPosition = 0;
            positionToRead = 0;
        }

        private int GetTotalMessageSize()
        {
            return Define.Headersize switch
            {
                2 => BitConverter.ToInt16(messageBuffer, 0),
                4 => BitConverter.ToInt32(messageBuffer, 0),
                _ => 0
            };
        }

    }    
}
