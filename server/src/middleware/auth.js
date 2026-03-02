const pool = require('../db/pool');

async function authMiddleware(req, res, next) {
  const header = req.headers.authorization;
  if (!header || !header.startsWith('Bearer ')) {
    return res.status(401).json({ error: 'Missing auth token' });
  }

  const token = header.slice(7);
  try {
    const result = await pool.query(
      'SELECT uid, friend_code, display_name FROM players WHERE auth_token = $1',
      [token]
    );
    if (result.rows.length === 0) {
      return res.status(401).json({ error: 'Invalid auth token' });
    }
    req.user = result.rows[0];
    // Update last_online (fire and forget)
    pool.query('UPDATE players SET last_online = NOW() WHERE uid = $1', [req.user.uid]);
    next();
  } catch (err) {
    res.status(500).json({ error: 'Auth error' });
  }
}

module.exports = authMiddleware;
