using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class MallumHouseSave
    {
        public int serverId;
        public int gridX;
        public int gridY;
        public string skinName;
        public List<string> unlockedSkins = new();
    }
}
