using UnityEngine;

namespace Olds.Systems.Session
{
    public class SessionManager
    {
        public SessionPlayerInfo playerInfo;
        public SessionManager(byte sessionID = 1)
        {
            Debug.Log($"You are player {sessionID}");
            playerInfo = new SessionPlayerInfo(sessionID);
        }
    }
}