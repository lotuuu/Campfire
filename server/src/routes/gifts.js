const { Router } = require('express');
const pool = require('../db/pool');

const router = Router();

const MAX_ITEMS_PER_GIFT = 3;
const MAX_GIFTS_PER_DAY = 5;
const GIFT_EXPIRY_DAYS = 7;

// POST /gifts/send
router.post('/send', async (req, res) => {
  const { toUid, items } = req.body;

  if (!toUid) {
    return res.status(400).json({ error: 'toUid is required' });
  }
  if (!Array.isArray(items) || items.length === 0) {
    return res.status(400).json({ error: 'items must be a non-empty array' });
  }
  if (items.length > MAX_ITEMS_PER_GIFT) {
    return res.status(400).json({ error: `Max ${MAX_ITEMS_PER_GIFT} items per gift` });
  }
  if (toUid === req.user.uid) {
    return res.status(400).json({ error: 'Cannot send gift to yourself' });
  }

  try {
    // Verify friendship
    const friendship = await pool.query(
      'SELECT 1 FROM friends WHERE player_uid = $1 AND friend_uid = $2',
      [req.user.uid, toUid]
    );
    if (friendship.rows.length === 0) {
      return res.status(403).json({ error: 'Not friends with this player' });
    }

    // Check daily gift limit
    const todayGifts = await pool.query(
      `SELECT COUNT(*) FROM gifts
       WHERE from_uid = $1 AND to_uid = $2
         AND created_at >= NOW() - INTERVAL '1 day'`,
      [req.user.uid, toUid]
    );
    if (parseInt(todayGifts.rows[0].count) >= MAX_GIFTS_PER_DAY) {
      return res.status(400).json({ error: `Max ${MAX_GIFTS_PER_DAY} gifts per day to same player` });
    }

    const result = await pool.query(
      `INSERT INTO gifts (from_uid, to_uid, items)
       VALUES ($1, $2, $3)
       RETURNING id, created_at`,
      [req.user.uid, toUid, JSON.stringify(items)]
    );

    res.status(201).json({
      giftId: result.rows[0].id,
      createdAt: result.rows[0].created_at
    });
  } catch (err) {
    res.status(500).json({ error: 'Failed to send gift' });
  }
});

// GET /gifts — pending gifts for authenticated user
router.get('/', async (req, res) => {
  try {
    const result = await pool.query(
      `SELECT g.id, g.from_uid, p.display_name AS from_display_name, g.items, g.created_at
       FROM gifts g
       JOIN players p ON p.uid = g.from_uid
       WHERE g.to_uid = $1
         AND g.status = 'pending'
         AND g.created_at >= NOW() - INTERVAL '7 days'
       ORDER BY g.created_at DESC`,
      [req.user.uid]
    );
    res.json({ gifts: result.rows });
  } catch (err) {
    res.status(500).json({ error: 'Failed to fetch gifts' });
  }
});

// POST /gifts/claim/:giftId
router.post('/claim/:giftId', async (req, res) => {
  const { giftId } = req.params;

  try {
    const result = await pool.query(
      `UPDATE gifts SET status = 'claimed', claimed_at = NOW()
       WHERE id = $1 AND to_uid = $2 AND status = 'pending'
       RETURNING items`,
      [giftId, req.user.uid]
    );

    if (result.rows.length === 0) {
      return res.status(404).json({ error: 'Gift not found or already claimed' });
    }

    res.json({ items: result.rows[0].items });
  } catch (err) {
    res.status(500).json({ error: 'Failed to claim gift' });
  }
});

module.exports = router;
