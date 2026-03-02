using System;
using System.Collections;
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

        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                ["flameLevel"] = flameLevel
            };

            var plotList = new List<Dictionary<string, object>>();
            foreach (var p in plots)
            {
                plotList.Add(new Dictionary<string, object>
                {
                    ["seedName"] = p.seedName,
                    ["state"] = p.state,
                    ["gridX"] = p.gridX,
                    ["gridY"] = p.gridY
                });
            }
            dict["plots"] = plotList;

            var vaseList = new List<Dictionary<string, object>>();
            foreach (var v in vases)
            {
                vaseList.Add(new Dictionary<string, object>
                {
                    ["currentWater"] = v.currentWater,
                    ["capacity"] = v.capacity,
                    ["state"] = v.state,
                    ["gridX"] = v.gridX,
                    ["gridY"] = v.gridY
                });
            }
            dict["vases"] = vaseList;

            var gardenList = new List<Dictionary<string, object>>();
            foreach (var g in gardens)
            {
                gardenList.Add(new Dictionary<string, object>
                {
                    ["plantName"] = g.plantName,
                    ["mature"] = g.mature,
                    ["gridX"] = g.gridX,
                    ["gridY"] = g.gridY
                });
            }
            dict["gardens"] = gardenList;

            return dict;
        }

        public static VillageSnapshot FromDictionary(Dictionary<string, object> dict)
        {
            var snapshot = new VillageSnapshot
            {
                flameLevel = Convert.ToInt32(dict["flameLevel"])
            };

            if (dict.TryGetValue("plots", out var plotsObj) && plotsObj is IList plotList)
            {
                foreach (var item in plotList)
                {
                    if (item is Dictionary<string, object> d)
                    {
                        snapshot.plots.Add(new SnapshotPlot
                        {
                            seedName = d["seedName"] as string,
                            state = d["state"] as string,
                            gridX = Convert.ToInt32(d["gridX"]),
                            gridY = Convert.ToInt32(d["gridY"])
                        });
                    }
                }
            }

            if (dict.TryGetValue("vases", out var vasesObj) && vasesObj is IList vaseList)
            {
                foreach (var item in vaseList)
                {
                    if (item is Dictionary<string, object> d)
                    {
                        snapshot.vases.Add(new SnapshotVase
                        {
                            currentWater = Convert.ToInt32(d["currentWater"]),
                            capacity = Convert.ToInt32(d["capacity"]),
                            state = d["state"] as string,
                            gridX = Convert.ToInt32(d["gridX"]),
                            gridY = Convert.ToInt32(d["gridY"])
                        });
                    }
                }
            }

            if (dict.TryGetValue("gardens", out var gardensObj) && gardensObj is IList gardenList)
            {
                foreach (var item in gardenList)
                {
                    if (item is Dictionary<string, object> d)
                    {
                        snapshot.gardens.Add(new SnapshotGarden
                        {
                            plantName = d["plantName"] as string,
                            mature = Convert.ToBoolean(d["mature"]),
                            gridX = Convert.ToInt32(d["gridX"]),
                            gridY = Convert.ToInt32(d["gridY"])
                        });
                    }
                }
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
