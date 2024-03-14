using System.Collections.Generic;
using JiNet.ClassModule;

namespace JiNet.Interface
{
    public interface ILogicQueue
    {
        void enqueue(CPacket msg);
        Queue<CPacket> PacketQueue();
    }
}