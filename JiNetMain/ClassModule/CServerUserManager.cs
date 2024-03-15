namespace JiNet.ClassModule
{
    public class CServerUserManager
    {
        private object csUser = new();
        private List<CUserToken> users = new();
        private Timer timerHeartbeat;
        private long heartbeatDuration;
        
        public void StartHeartbeatChecking(uint checkInterval, uint allowDuration)
        {
            heartbeatDuration = allowDuration * 10000000;
            timerHeartbeat = new Timer(CheckHeartbeat, null, 1000 * checkInterval, 1000 * checkInterval);
        }
        
        public void StopHeartbeatChecking() => timerHeartbeat.Dispose();
        
        public void Add(CUserToken token)
        {
            lock (csUser) users.Add(token);
        }
        
        public void Remove(CUserToken token)
        {
            lock(csUser) users.Remove(token);
        }

        public bool isExist(CUserToken ower)
        {
            lock (csUser) return users.Exists(obj => obj == ower);
        }
        
        public int GetTotalCount() => users.Count;
        
        private void CheckHeartbeat(object state)
        {
            var allowedTime = DateTime.Now.Ticks - heartbeatDuration;
            lock (csUser)
            {
                for (var i = 0; i < users.Count; i++)
                {
                    var heartBeatTime = users[i].lastHeartbeatTime;
                    if (heartBeatTime >= allowedTime) continue;
                    users[i].Disconnect(); 
                }
            }
        }

    }    
}

