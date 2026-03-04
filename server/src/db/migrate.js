const pool = require('./pool');

const migration = `
CREATE TABLE IF NOT EXISTS players (
  id SERIAL PRIMARY KEY,
  uid TEXT UNIQUE NOT NULL,
  auth_token TEXT UNIQUE NOT NULL,
  friend_code TEXT UNIQUE NOT NULL,
  display_name TEXT NOT NULL DEFAULT 'Camper',
  created_at TIMESTAMPTZ DEFAULT NOW(),
  last_online TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS friend_requests (
  id SERIAL PRIMARY KEY,
  from_uid TEXT NOT NULL REFERENCES players(uid),
  to_uid TEXT NOT NULL REFERENCES players(uid),
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'accepted', 'declined')),
  created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS friends (
  player_uid TEXT NOT NULL REFERENCES players(uid),
  friend_uid TEXT NOT NULL REFERENCES players(uid),
  added_at TIMESTAMPTZ DEFAULT NOW(),
  PRIMARY KEY (player_uid, friend_uid)
);

CREATE TABLE IF NOT EXISTS villages (
  player_uid TEXT UNIQUE NOT NULL REFERENCES players(uid),
  snapshot JSONB NOT NULL DEFAULT '{}',
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS gifts (
  id SERIAL PRIMARY KEY,
  from_uid TEXT NOT NULL REFERENCES players(uid),
  to_uid TEXT NOT NULL REFERENCES players(uid),
  items JSONB NOT NULL DEFAULT '[]',
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'claimed')),
  created_at TIMESTAMPTZ DEFAULT NOW(),
  claimed_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_friend_requests_to ON friend_requests(to_uid, status);
CREATE INDEX IF NOT EXISTS idx_gifts_to ON gifts(to_uid, status);
CREATE INDEX IF NOT EXISTS idx_players_friend_code ON players(friend_code);

CREATE TABLE IF NOT EXISTS visitor_templates (
  id SERIAL PRIMARY KEY,
  visitor_id TEXT UNIQUE NOT NULL,
  name TEXT NOT NULL,
  portrait_id TEXT,
  type TEXT NOT NULL CHECK (type IN ('merchant', 'gifter', 'quester')),
  flame_level_min INTEGER NOT NULL DEFAULT 1,
  dialogue_pool JSONB NOT NULL DEFAULT '[]',
  offer_pool JSONB NOT NULL DEFAULT '[]',
  gift_pool JSONB NOT NULL DEFAULT '[]',
  quest_pool JSONB NOT NULL DEFAULT '[]',
  weight REAL NOT NULL DEFAULT 1.0
);

CREATE TABLE IF NOT EXISTS visitor_schedule (
  id SERIAL PRIMARY KEY,
  visitor_id TEXT NOT NULL REFERENCES visitor_templates(visitor_id),
  date DATE,
  visit_number INTEGER,
  weather_condition TEXT,
  priority INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_visitor_schedule_date ON visitor_schedule(date);
CREATE INDEX IF NOT EXISTS idx_visitor_schedule_visit ON visitor_schedule(visit_number);

CREATE TABLE IF NOT EXISTS visitor_quests (
  id SERIAL PRIMARY KEY,
  player_uid TEXT NOT NULL REFERENCES players(uid),
  visitor_id TEXT NOT NULL,
  request_item TEXT NOT NULL,
  request_count INTEGER NOT NULL,
  return_date_utc DATE NOT NULL,
  reward JSONB NOT NULL DEFAULT '{}',
  return_dialogue JSONB NOT NULL DEFAULT '[]',
  created_at TIMESTAMPTZ DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_visitor_quests_player ON visitor_quests(player_uid);
CREATE INDEX IF NOT EXISTS idx_visitor_quests_return ON visitor_quests(return_date_utc);

CREATE TABLE IF NOT EXISTS player_visit_counts (
  player_uid TEXT UNIQUE NOT NULL REFERENCES players(uid),
  count INTEGER NOT NULL DEFAULT 0,
  last_visit_date DATE
);
`;

async function migrate() {
  try {
    console.log('Running migrations...');
    await pool.query(migration);
    console.log('Migrations complete.');
  } catch (err) {
    console.error('Migration failed:', err.message);
    process.exit(1);
  } finally {
    await pool.end();
  }
}

migrate();
