import * as functions from "firebase-functions";
import * as admin from "firebase-admin";

admin.initializeApp();
const db = admin.firestore();

// Generate a unique friend code like "SPARK-7X2K"
function generateFriendCode(): string {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No I,O,0,1 to avoid confusion
  const prefixes = ["SPARK", "BLAZE", "EMBER", "FLAME", "TORCH", "FLARE"];
  const prefix = prefixes[Math.floor(Math.random() * prefixes.length)];
  let suffix = "";
  for (let i = 0; i < 4; i++) {
    suffix += chars[Math.floor(Math.random() * chars.length)];
  }
  return `${prefix}-${suffix}`;
}

// On new user creation, generate friend code and create player profile
export const onUserCreated = functions.auth.user().onCreate(async (user) => {
  let friendCode: string;
  let attempts = 0;
  do {
    friendCode = generateFriendCode();
    const existing = await db.collection("players")
      .where("friendCode", "==", friendCode)
      .limit(1)
      .get();
    if (existing.empty) break;
    attempts++;
  } while (attempts < 10);

  await db.collection("players").doc(user.uid).set({
    friendCode,
    displayName: `Camper #${user.uid.substring(0, 4).toUpperCase()}`,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
    lastOnline: admin.firestore.FieldValue.serverTimestamp(),
  });
});

// When a friend request is accepted, add to both friend lists
export const onFriendRequestAccepted = functions.firestore
  .document("friendRequests/{requestId}")
  .onUpdate(async (change) => {
    const before = change.before.data();
    const after = change.after.data();

    if (before.status === "pending" && after.status === "accepted") {
      const fromUid = after.fromUid;
      const toUid = after.toUid;

      const [fromDoc, toDoc] = await Promise.all([
        db.collection("players").doc(fromUid).get(),
        db.collection("players").doc(toUid).get(),
      ]);

      const fromData = fromDoc.data();
      const toData = toDoc.data();

      // Check friend count limits (20 max)
      const [fromFriends, toFriends] = await Promise.all([
        db.collection("friends").doc(fromUid).collection("list").count().get(),
        db.collection("friends").doc(toUid).collection("list").count().get(),
      ]);

      if (fromFriends.data().count >= 20 || toFriends.data().count >= 20) {
        await change.after.ref.update({ status: "pending" });
        return;
      }

      const batch = db.batch();
      batch.set(
        db.collection("friends").doc(fromUid).collection("list").doc(toUid),
        {
          displayName: toData?.displayName || "Camper",
          friendCode: toData?.friendCode || "",
          addedAt: admin.firestore.FieldValue.serverTimestamp(),
        }
      );
      batch.set(
        db.collection("friends").doc(toUid).collection("list").doc(fromUid),
        {
          displayName: fromData?.displayName || "Camper",
          friendCode: fromData?.friendCode || "",
          addedAt: admin.firestore.FieldValue.serverTimestamp(),
        }
      );

      await batch.commit();
    }
  });

// Scheduled cleanup of expired gifts (older than 7 days)
export const cleanupExpiredGifts = functions.pubsub
  .schedule("every 24 hours")
  .onRun(async () => {
    const sevenDaysAgo = new Date();
    sevenDaysAgo.setDate(sevenDaysAgo.getDate() - 7);

    const expired = await db.collection("gifts")
      .where("status", "==", "pending")
      .where("createdAt", "<", admin.firestore.Timestamp.fromDate(sevenDaysAgo))
      .get();

    const batch = db.batch();
    expired.docs.forEach((doc) => batch.delete(doc.ref));
    await batch.commit();

    console.log(`Cleaned up ${expired.size} expired gifts`);
  });
