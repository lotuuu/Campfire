using System;
using System.Collections.Generic;

namespace Garden
{
    public enum MallumState
    {
        Idle,
        FetchingWater,
        OnQuest,
        QuestComplete
    }

    [Serializable]
    public class MallumSave
    {
        public int serverId;
        public MallumState state = MallumState.Idle;
        public int assignedVaseIndex = -1;
        public string assignedQuestName;
        public string startTimeUtc;
        public List<RewardEntry> pendingRewards = new();
    }

    [Serializable]
    public class RewardEntry
    {
        public string itemKey;
        public int count;
    }
}
