const { Router } = require('express');
const crypto = require('crypto');
const pool = require('../db/pool');

const router = Router();

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

module.exports = router;
