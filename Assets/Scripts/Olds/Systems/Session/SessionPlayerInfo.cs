namespace Intents
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