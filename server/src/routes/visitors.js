const { Router } = require('express');
const pool = require('../db/pool');

const router = Router();

// --- Helper functions ---

function weightedRandom(templates) {
  const totalWeight = templates.reduce((sum, t) => sum + t.weight, 0);
  let roll = Math.random() * totalWeight;
  for (const t of templates) {
    roll -= t.weight;
    if (roll <= 0) return t;
  }
  return templates[templates.length - 1];
}

function rollDialogue(dialoguePool) {
  if (!Array.isArray(dialoguePool) || dialoguePool.length === 0) return [];
  // Each entry can be a string or an array of lines; pick one at random
  const pick = dialoguePool[Math.floor(Math.random() * dialoguePool.length)];
  return Array.isArray(pick) ? pick : [pick];
}

function rollOffers(offerPool) {
  if (!Array.isArray(offerPool) || offerPool.length === 0) return [];
  // Return a random subset or the whole pool depending on design; for now return all
  return offerPool;
}

function rollGift(giftPool) {
  if (!Array.isArray(giftPool) || giftPool.length === 0) return null;
  return giftPool[Math.floor(Math.random() * giftPool.length)];
}

function rollQuest(questPool) {
  if (!Array.isArray(questPool) || questPool.length === 0) return null;
  return questPool[Math.floor(Math.random() * questPool.length)];
}

function buildVisitorPayload(template, overrides) {
  const payload = {
    visitor_type: template.type,
    visitor_id: template.visitor_id,
    name: template.name,
    portrait_id: template.portrait_id,
    dialogue: rollDialogue(template.dialogue_pool)
  };

  if (template.type === 'merchant') {
    payload.offers = rollOffers(template.offer_pool);
  } else if (template.type === 'gifter') {
    payload.gift = rollGift(template.gift_pool);
  } else if (template.type === 'quester') {
    payload.quest = rollQuest(template.quest_pool);
  }

  return { ...payload, ...overrides };
}

// GET /visitors/tonight
router.get('/tonight', async (req, res) => {
  const uid = req.user.uid;
  const today = new Date().toISOString().slice(0, 10); // YYYY-MM-DD

  try {
    // Increment visit count (deduplicated by date)
    const visitResult = await pool.query(
      `INSERT INTO player_visit_counts (player_uid, count, last_visit_date)
       VALUES ($1, 1, $2)
       ON CONFLICT (player_uid) DO UPDATE
         SET count = CASE
           WHEN player_visit_counts.last_visit_date < $2
             THEN player_visit_counts.count + 1
           ELSE player_visit_counts.count
         END,
         last_visit_date = $2
       RETURNING count`,
      [uid, today]
    );
    const visitNumber = visitResult.rows[0].count;

    // Read flame level from village snapshot
    const villageResult = await pool.query(
      `SELECT snapshot FROM villages WHERE player_uid = $1`,
      [uid]
    );
    const flameLevel = villageResult.rows.length > 0
      ? (villageResult.rows[0].snapshot?.flameLevel ?? 1)
      : 1;

    // Priority 1: Scheduled date visitors
    const dateVisitor = await pool.query(
      `SELECT vt.* FROM visitor_schedule vs
       JOIN visitor_templates vt ON vt.visitor_id = vs.visitor_id
       WHERE vs.date = $1
       ORDER BY vs.priority DESC
       LIMIT 1`,
      [today]
    );
    if (dateVisitor.rows.length > 0) {
      return res.json(buildVisitorPayload(dateVisitor.rows[0]));
    }

    // Priority 2: Visit number milestones
    const milestoneVisitor = await pool.query(
      `SELECT vt.* FROM visitor_schedule vs
       JOIN visitor_templates vt ON vt.visitor_id = vs.visitor_id
       WHERE vs.visit_number = $1
       ORDER BY vs.priority DESC
       LIMIT 1`,
      [visitNumber]
    );
    if (milestoneVisitor.rows.length > 0) {
      return res.json(buildVisitorPayload(milestoneVisitor.rows[0]));
    }

    // Priority 3: Weather-triggered visitors
    const weatherVisitor = await pool.query(
      `SELECT vt.* FROM visitor_schedule vs
       JOIN visitor_templates vt ON vt.visitor_id = vs.visitor_id
       WHERE vs.weather_condition IS NOT NULL
       ORDER BY vs.priority DESC`
    );
    // TODO: Check actual weather conditions when weather data is available on the server.
    // For now, weather triggers are a no-op placeholder until we pipe weather into the server.

    // Priority 4: Quest returns
    const questReturn = await pool.query(
      `SELECT * FROM visitor_quests
       WHERE player_uid = $1 AND return_date_utc <= $2
       ORDER BY return_date_utc ASC
       LIMIT 1`,
      [uid, today]
    );
    if (questReturn.rows.length > 0) {
      const quest = questReturn.rows[0];
      // Load the visitor template for display info
      const templateResult = await pool.query(
        `SELECT * FROM visitor_templates WHERE visitor_id = $1`,
        [quest.visitor_id]
      );
      const template = templateResult.rows.length > 0
        ? templateResult.rows[0]
        : { type: 'quester', visitor_id: quest.visitor_id, name: quest.visitor_id, portrait_id: null, dialogue_pool: [] };

      return res.json(buildVisitorPayload(template, {
        visitor_type: 'quester',
        dialogue: quest.return_dialogue || [],
        quest: {
          quest_id: quest.id,
          is_return: true,
          reward: quest.reward
        }
      }));
    }

    // Priority 5: Random pool (weighted, gated by flame level)
    const randomPool = await pool.query(
      `SELECT * FROM visitor_templates WHERE flame_level_min <= $1`,
      [flameLevel]
    );
    if (randomPool.rows.length === 0) {
      return res.json({ visitor_type: null, message: 'No visitors available tonight' });
    }

    const chosen = weightedRandom(randomPool.rows);
    return res.json(buildVisitorPayload(chosen));
  } catch (err) {
    console.error('Error in GET /visitors/tonight:', err);
    res.status(500).json({ error: 'Failed to determine tonight\'s visitor' });
  }
});

// POST /visitors/quest/accept
router.post('/quest/accept', async (req, res) => {
  const uid = req.user.uid;
  const { visitor_id, request_item, request_count, return_days, reward, return_dialogue } = req.body;

  if (!visitor_id || !request_item || !request_count || !return_days) {
    return res.status(400).json({ error: 'visitor_id, request_item, request_count, and return_days are required' });
  }

  try {
    const returnDate = new Date();
    returnDate.setUTCDate(returnDate.getUTCDate() + return_days);
    const returnDateStr = returnDate.toISOString().slice(0, 10);

    const result = await pool.query(
      `INSERT INTO visitor_quests (player_uid, visitor_id, request_item, request_count, return_date_utc, reward, return_dialogue)
       VALUES ($1, $2, $3, $4, $5, $6, $7)
       RETURNING id`,
      [uid, visitor_id, request_item, request_count, returnDateStr,
       JSON.stringify(reward || {}), JSON.stringify(return_dialogue || [])]
    );

    res.status(201).json({
      quest_id: result.rows[0].id,
      return_date: returnDateStr
    });
  } catch (err) {
    console.error('Error in POST /visitors/quest/accept:', err);
    res.status(500).json({ error: 'Failed to accept quest' });
  }
});

// POST /visitors/quest/complete
router.post('/quest/complete', async (req, res) => {
  const uid = req.user.uid;
  const { quest_id } = req.body;

  if (!quest_id) {
    return res.status(400).json({ error: 'quest_id is required' });
  }

  try {
    const today = new Date().toISOString().slice(0, 10);
    const result = await pool.query(
      `DELETE FROM visitor_quests
       WHERE id = $1 AND player_uid = $2 AND return_date_utc <= $3
       RETURNING reward`,
      [quest_id, uid, today]
    );

    if (result.rows.length === 0) {
      return res.status(404).json({ error: 'Quest not found' });
    }

    res.json({ reward: result.rows[0].reward });
  } catch (err) {
    console.error('Error in POST /visitors/quest/complete:', err);
    res.status(500).json({ error: 'Failed to complete quest' });
  }
});

module.exports = router;
