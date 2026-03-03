const { Router } = require('express');
const pool = require('../db/pool');

const router = Router();

// PUT /village — upsert own village snapshot
router.put('/', async (req, res) => {
  const { snapshot } = req.body;
  if (snapshot === undefined) {
    return res.status(400).json({ error: 'snapshot is required' });
  }

  if (typeof snapshot !== 'object' || snapshot === null || Array.isArray(snapshot)) {
    return res.status(400).json({ error: 'snapshot must be a JSON object' });
  }

  const snapshotStr = JSON.stringify(snapshot);
  if (snapshotStr.length > 102400) {
    return res.status(413).json({ error: 'Village snapshot too large (max 100KB)' });
  }

  try {
    await pool.query(
      `INSERT INTO villages (player_uid, snapshot, updated_at)
       VALUES ($1, $2, NOW())
       ON CONFLICT (player_uid)
       DO UPDATE SET snapshot = $2, updated_at = NOW()`,
      [req.user.uid, snapshotStr]
    );
    res.json({ message: 'Village updated' });
  } catch (err) {
    res.status(500).json({ error: 'Failed to update village' });
  }
});

// GET /village/:uid — get friend's village snapshot
router.get('/:uid', async (req, res) => {
  const { uid } = req.params;

  try {
    // Verify friendship (or self)
    if (uid !== req.user.uid) {
      const friendship = await pool.query(
        'SELECT 1 FROM friends WHERE player_uid = $1 AND friend_uid = $2',
        [req.user.uid, uid]
      );
      if (friendship.rows.length === 0) {
        return res.status(403).json({ error: 'Not friends with this player' });
      }
    }

    const result = await pool.query(
      'SELECT snapshot, updated_at FROM villages WHERE player_uid = $1',
      [uid]
    );

    if (result.rows.length === 0) {
      return res.json({ snapshot: {}, updatedAt: null });
    }

    res.json({
      snapshot: result.rows[0].snapshot,
      updatedAt: result.rows[0].updated_at
    });
  } catch (err) {
    res.status(500).json({ error: 'Failed to fetch village' });
  }
});

module.exports = router;
