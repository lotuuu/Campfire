const { Router } = require('express');
const crypto = require('crypto');
const pool = require('../db/pool');
const authMiddleware = require('../middleware/auth');

const router = Router();

const DISPLAY_NAME_MAX = 20;
const DISPLAY_NAME_REGEX = /^[a-zA-Z0-9 ]+$/;

const PREFIXES = ['SPARK', 'BLAZE', 'EMBER', 'FLAME', 'TORCH', 'FLARE'];
const CODE_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

function generateFriendCode() {
  const prefix = PREFIXES[Math.floor(Math.random() * PREFIXES.length)];
  let suffix = '';
  for (let i = 0; i < 4; i++) {
    suffix += CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)];
  }
  return `${prefix}-${suffix}`;
}

router.post('/register', async (req, res) => {
  const uid = crypto.randomUUID();
  const authToken = crypto.randomBytes(32).toString('hex');

  for (let attempt = 0; attempt < 10; attempt++) {
    const friendCode = generateFriendCode();
    try {
      const result = await pool.query(
        `INSERT INTO players (uid, auth_token, friend_code)
         VALUES ($1, $2, $3)
         RETURNING uid, friend_code, display_name`,
        [uid, authToken, friendCode]
      );
      const player = result.rows[0];
      return res.status(201).json({
        uid: player.uid,
        authToken,
        friendCode: player.friend_code,
        displayName: player.display_name
      });
    } catch (err) {
      // 23505 = unique_violation — retry on friend_code collision
      if (err.code === '23505' && err.constraint && err.constraint.includes('friend_code')) {
        continue;
      }
      return res.status(500).json({ error: 'Registration failed' });
    }
  }

  res.status(500).json({ error: 'Could not generate unique friend code' });
});

router.put('/display-name', authMiddleware, async (req, res) => {
  const { displayName } = req.body;

  if (!displayName || typeof displayName !== 'string') {
    return res.status(400).json({ error: 'displayName is required' });
  }

  const trimmed = displayName.trim();
  if (trimmed.length === 0 || trimmed.length > DISPLAY_NAME_MAX) {
    return res.status(400).json({ error: `Name must be 1-${DISPLAY_NAME_MAX} characters` });
  }
  if (!DISPLAY_NAME_REGEX.test(trimmed)) {
    return res.status(400).json({ error: 'Name can only contain letters, numbers, and spaces' });
  }

  try {
    await pool.query(
      'UPDATE players SET display_name = $1 WHERE uid = $2',
      [trimmed, req.user.uid]
    );
    res.json({ displayName: trimmed });
  } catch (err) {
    res.status(500).json({ error: 'Failed to update display name' });
  }
});

module.exports = router;
