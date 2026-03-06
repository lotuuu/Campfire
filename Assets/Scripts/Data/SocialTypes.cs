using System;
using System.Collections.Generic;

namespace Garden
{
    [Serializable]
    public class FriendRequest
    {
        public string id;
        public string fromUid;
        public string fromName;
        public string status; // "pending", "accepted", "declined"
    }

    [Serializable]
    public class GiftMessage
    {
        public string id;
        public string fromUid;
        public string fromName;
        public List<GiftItem> items = new();
    }

    [Serializable]
    public class GiftItem
    {
        public string type; // "seed" or "item"
        public string name;
        public int count;
    }

    [Serializable]
    public class VillageSnapshot
    {
        public int flameLevel;
        public List<SnapshotPlot> plots = new();
        public List<SnapshotVase> vases = new();
        public List<SnapshotGarden> gardens = new();

        public static VillageSnapshot FromSaveData(SaveData data, int flameLevel)
        {
            var snapshot = new VillageSnapshot { flameLevel = flameLevel };

            foreach (var p in data.plots)
            {
                snapshot.plots.Add(new SnapshotPlot
                {
                    seedName = p.seedName,
                    state = p.state.ToString(),
                    gridX = p.gridX,
                    gridY = p.gridY
                });
            }

            foreach (var v in data.vases)
            {
                snapshot.vases.Add(new SnapshotVase
                {
                    currentWater = v.currentWater,
                    capacity = v.capacity,
                    state = v.state.ToString(),
                    gridX = v.gridX,
                    gridY = v.gridY
                });
            }

            foreach (var g in data.gardens)
            {
                snapshot.gardens.Add(new SnapshotGarden
                {
                    plantName = g.plantName,
                    mature = g.mature,
                    gridX = g.gridX,
                    gridY = g.gridY
                });
            }

            return snapshot;
        }

    }

    [Serializable]
    public class SnapshotPlot
    {
        public string seedName;
        public string state;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class SnapshotVase
    {
        public int currentWater;
        public int capacity;
        public string state;
        public int gridX;
        public int gridY;
    }

    [Serializable]
    public class SnapshotGarden
    {
        public string plantName;
        public bool mature;
        public int gridX;
        public int gridY;
    }
}
