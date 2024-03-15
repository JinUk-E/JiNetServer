namespace JiNet.ClassModule
{
    public class CHeatbeatChecker
    {
        private CUserToken server;
        private Timer timerHeartbeat;
        private uint interval;
        private float elapsedTime;
    
        public CHeatbeatChecker(CUserToken server, uint interval)
        {
            this.server = server;
            this.interval = interval;
            timerHeartbeat = new(OnTimer, null, Timeout.Infinite, interval * 1000);
        }
    
        private void OnTimer(object state) => Send();

        private void Send()
        {
            var msg = CPacket.Create((short)CUserToken.SYS_UPDATE_HEARBEAT);
            server.Send(msg);
        }
    
        public void Update(float time)
        {
            elapsedTime += time;
            if (elapsedTime < interval) return;
            elapsedTime = 0.0f;
            Send();
        }
    
        public void Stop()
        {
            elapsedTime = 0;
            timerHeartbeat.Change(Timeout.Infinite, Timeout.Infinite);
        }
    
        public void Play()
        {
            elapsedTime = 0;
            timerHeartbeat.Change(0, interval * 1000);
        }
    }    
}
