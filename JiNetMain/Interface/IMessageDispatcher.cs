using JiNet.ClassModule;

namespace JiNet.Interface
{
    public interface IMessageDispatcher
    {
        void OnMessage(CUserToken userToken, ArraySegment<byte> buffer);
    }    
}

