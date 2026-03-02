const { Router } = require('express');
const pool = require('../db/pool');

const router = Router();

const MAX_FRIENDS = 20;

// GET /friends — returns friend list
router.get('/', async (req, res) => {
  try {
    const result = await pool.query(
      `SELECT p.uid, p.display_name, p.friend_code, p.last_online
       FROM friends f
       JOIN players p ON p.uid = f.friend_uid
       WHERE f.player_uid = $1
       ORDER BY p.display_name`,
      [req.user.uid]
    );
    res.json({ friends: result.rows });
  } catch (err) {
    res.status(500).json({ error: 'Failed to fetch friends' });
  }
});

// POST /friends/request — send friend request by friend code
router.post('/request', async (req, res) => {
  const { friendCode } = req.body;
  if (!friendCode) {
    return res.status(400).json({ error: 'friendCode is required' });
  }

  try {
    // Look up target player
    const target = await pool.query(
      'SELECT uid FROM players WHERE friend_code = $1',
      [friendCode]
    );
    if (target.rows.length === 0) {
      return res.status(404).json({ error: 'Player not found' });
    }

    const toUid = target.rows[0].uid;

    if (toUid === req.user.uid) {
      return res.status(400).json({ error: 'Cannot send friend request to yourself' });
    }

    // Check if already friends
    const existing = await pool.query(
      'SELECT 1 FROM friends WHERE player_uid = $1 AND friend_uid = $2',
      [req.user.uid, toUid]
    );
    if (existing.rows.length > 0) {
      return res.status(400).json({ error: 'Already friends' });
    }

    // Check for existing pending request in either direction
    const pendingRequest = await pool.query(
      `SELECT id FROM friend_requests
       WHERE status = 'pending'
         AND ((from_uid = $1 AND to_uid = $2) OR (from_uid = $2 AND to_uid = $1))`,
      [req.user.uid, toUid]
    );
    if (pendingRequest.rows.length > 0) {
      return res.status(400).json({ error: 'Friend request already pending' });
    }

    await pool.query(
      'INSERT INTO friend_requests (from_uid, to_uid) VALUES ($1, $2)',
      [req.user.uid, toUid]
    );

    res.status(201).json({ message: 'Friend request sent' });
  } catch (err) {
    res.status(500).json({ error: 'Failed to send friend request' });
  }
});

// GET /friends/requests — pending incoming requests
router.get('/requests', async (req, res) => {
  try {
    const result = await pool.query(
      `SELECT fr.id, fr.from_uid, p.display_name AS from_name, fr.status, fr.created_at
       FROM friend_requests fr
       JOIN players p ON p.uid = fr.from_uid
       WHERE fr.to_uid = $1 AND fr.status = 'pending'
       ORDER BY fr.created_at DESC`,
      [req.user.uid]
    );
    res.json({ requests: result.rows });
  } catch (err) {
    res.status(500).json({ error: 'Failed to fetch friend requests' });
  }
});

// POST /friends/accept/:requestId
router.post('/accept/:requestId', async (req, res) => {
  const { requestId } = req.params;

  const client = await pool.connect();
  try {
    await client.query('BEGIN');

    // Verify request exists and is pending
    const reqResult = await client.query(
      `SELECT id, from_uid, to_uid FROM friend_requests
       WHERE id = $1 AND status = 'pending' AND to_uid = $2`,
      [requestId, req.user.uid]
    );
    if (reqResult.rows.length === 0) {
      await client.query('ROLLBACK');
      return res.status(404).json({ error: 'Friend request not found' });
    }

    const friendRequest = reqResult.rows[0];

    // Check both users have < MAX_FRIENDS
    const countA = await client.query(
      'SELECT COUNT(*) FROM friends WHERE player_uid = $1',
      [friendRequest.from_uid]
    );
    const countB = await client.query(
      'SELECT COUNT(*) FROM friends WHERE player_uid = $1',
      [friendRequest.to_uid]
    );

    if (parseInt(countA.rows[0].count) >= MAX_FRIENDS) {
      await client.query('ROLLBACK');
      return res.status(400).json({ error: 'Sender has reached max friends' });
    }
    if (parseInt(countB.rows[0].count) >= MAX_FRIENDS) {
      await client.query('ROLLBACK');
      return res.status(400).json({ error: 'You have reached max friends' });
    }

    // Update request status
    await client.query(
      `UPDATE friend_requests SET status = 'accepted' WHERE id = $1`,
      [requestId]
    );

    // Insert symmetric friend rows
    await client.query(
      `INSERT INTO friends (player_uid, friend_uid) VALUES ($1, $2), ($2, $1)
       ON CONFLICT DO NOTHING`,
      [friendRequest.from_uid, friendRequest.to_uid]
    );

    await client.query('COMMIT');

    // Return updated friend list
    const friendList = await pool.query(
      `SELECT p.uid, p.display_name, p.friend_code, p.last_online
       FROM friends f
       JOIN players p ON p.uid = f.friend_uid
       WHERE f.player_uid = $1
       ORDER BY p.display_name`,
      [req.user.uid]
    );

    res.json({ friends: friendList.rows });
  } catch (err) {
    await client.query('ROLLBACK');
    res.status(500).json({ error: 'Failed to accept friend request' });
  } finally {
    client.release();
  }
});

// POST /friends/decline/:requestId
router.post('/decline/:requestId', async (req, res) => {
  const { requestId } = req.params;

  try {
    const result = await pool.query(
      `UPDATE friend_requests SET status = 'declined'
       WHERE id = $1 AND to_uid = $2 AND status = 'pending'
       RETURNING id`,
      [requestId, req.user.uid]
    );

    if (result.rows.length === 0) {
      return res.status(404).json({ error: 'Friend request not found' });
    }

    res.json({ message: 'Friend request declined' });
  } catch (err) {
    res.status(500).json({ error: 'Failed to decline friend request' });
  }
});

// DELETE /friends/:friendUid
router.delete('/:friendUid', async (req, res) => {
  const { friendUid } = req.params;

  try {
    const result = await pool.query(
      `DELETE FROM friends
       WHERE (player_uid = $1 AND friend_uid = $2)
          OR (player_uid = $2 AND friend_uid = $1)
       RETURNING player_uid`,
      [req.user.uid, friendUid]
    );

    if (result.rows.length === 0) {
      return res.status(404).json({ error: 'Friend not found' });
    }

    res.json({ message: 'Friend removed' });
  } catch (err) {
    res.status(500).json({ error: 'Failed to remove friend' });
  }
});

module.exports = router;
