const express = require('express');

const authMiddleware = require('./middleware/auth');
const authRoutes = require('./routes/auth');
const friendRoutes = require('./routes/friends');
const villageRoutes = require('./routes/villages');
const giftRoutes = require('./routes/gifts');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(express.json());

// Routes
app.use('/auth', authRoutes);
app.use('/friends', authMiddleware, friendRoutes);
app.use('/village', authMiddleware, villageRoutes);
app.use('/gifts', authMiddleware, giftRoutes);

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
