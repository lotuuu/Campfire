using NUnit.Framework;
using Garden;

namespace Garden.Tests
{
    public class TestSocialData
    {
        [Test]
        public void NewSocialData_HasEmptyDefaults()
        {
            var data = new SocialData();
            Assert.IsNull(data.firebaseUid);
            Assert.IsNull(data.friendCode);
            Assert.AreEqual("Camper", data.displayName);
            Assert.IsNotNull(data.cachedFriends);
            Assert.AreEqual(0, data.cachedFriends.Count);
        }

        [Test]
        public void SocialData_SerializesRoundTrip()
        {
            var data = new SocialData
            {
                firebaseUid = "uid123",
                friendCode = "SPARK-7X2K",
                displayName = "My Camp"
            };
            data.cachedFriends.Add(new CachedFriend
            {
                uid = "friend1",
                displayName = "Friend Camp",
                friendCode = "BLAZE-4R1N"
            });

            string json = UnityEngine.JsonUtility.ToJson(data);
            var loaded = UnityEngine.JsonUtility.FromJson<SocialData>(json);

            Assert.AreEqual("uid123", loaded.firebaseUid);
            Assert.AreEqual("SPARK-7X2K", loaded.friendCode);
            Assert.AreEqual("My Camp", loaded.displayName);
            Assert.AreEqual(1, loaded.cachedFriends.Count);
            Assert.AreEqual("friend1", loaded.cachedFriends[0].uid);
        }

        [Test]
        public void SocialData_EmptyFriendList_SerializesCleanly()
        {
            var data = new SocialData { firebaseUid = "test" };
            string json = UnityEngine.JsonUtility.ToJson(data);
            var loaded = UnityEngine.JsonUtility.FromJson<SocialData>(json);
            Assert.IsNotNull(loaded.cachedFriends);
            Assert.AreEqual(0, loaded.cachedFriends.Count);
        }
    }
}
