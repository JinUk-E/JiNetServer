using JiNet.ClassModule;

namespace JiNet.Interface
{
    /// <summary>
    /// 서버와 클라이언트에서 공통으로 사용하는 세션객체
    /// 서버
    ///     클라이언트 객체를 나타냄, 풀링 여부는 사용자의 지정에 따라 달라짐.
    ///     SessionCreatedCallback 함수를 호출하면 생성한다.
    ///
    /// 클라이언트
    ///     접속한 서버의 객체를 나타냄 
    /// </summary>
    public interface IPeer
    {
        void OnMessage(CPacket msg);

        void OnRemoved();

        void Send(CPacket msg);

        void Disconnect();
    }    
}

