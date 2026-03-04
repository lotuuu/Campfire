const express = require('express');
const rateLimit = require('express-rate-limit');

const authMiddleware = require('./middleware/auth');
const authRoutes = require('./routes/auth');
const friendRoutes = require('./routes/friends');
const villageRoutes = require('./routes/villages');
const giftRoutes = require('./routes/gifts');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(express.json());

const globalLimiter = rateLimit({
  windowMs: 60 * 1000,
  max: 100,
  standardHeaders: true,
  legacyHeaders: false,
  message: { error: 'Too many requests, please try again later' }
});

const registerLimiter = rateLimit({
  windowMs: 60 * 1000,
  max: 5,
  standardHeaders: true,
  legacyHeaders: false,
  message: { error: 'Too many registration attempts, please try again later' }
});

app.use(globalLimiter);

// Routes
app.use('/auth', registerLimiter, authRoutes);
app.use('/friends', authMiddleware, friendRoutes);
app.use('/village', authMiddleware, villageRoutes);
app.use('/gifts', authMiddleware, giftRoutes);
app.use('/visitors', authMiddleware, require('./routes/visitors'));

// Health check
app.get('/health', (req, res) => {
  res.json({ status: 'ok' });
});

// Error handling
app.use((err, req, res, next) => {
  console.error('Unhandled error:', err);
  res.status(500).json({ error: 'Internal server error' });
});

app.listen(PORT, () => {
  console.log(`Camp Fire server listening on port ${PORT}`);
});
