namespace Olds.Systems.Session
{
    public readonly struct SessionPlayerInfo
    {
        public SessionPlayerInfo(byte sessionID)
        {
            sid = sessionID;
        }
        public readonly byte sid;
        
    }
}