const { Pool } = require('pg');

const pool = new Pool({
  connectionString: process.env.DATABASE_URL || 'postgresql://campfire:campfire@localhost:5432/campfire'
});

module.exports = pool;
