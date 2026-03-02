using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class SocialData
    {
        public string firebaseUid;
        public string friendCode;
        public string displayName = "Camper";
        public List<CachedFriend> cachedFriends = new();
        public int pendingGiftCount;
        public int pendingRequestCount;
    }

    [Serializable]
    public class CachedFriend
    {
        public string uid;
        public string displayName;
        public string friendCode;
    }
}
