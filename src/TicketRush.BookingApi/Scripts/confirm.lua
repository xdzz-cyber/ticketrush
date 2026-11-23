-- Підтвердження броні. Конкурує з Order Worker, який повертає прострочені броні в продаж:
-- виграє той, хто першим зробив ZREM члена з holds:expiring.
--
-- KEYS[1] hold:{reservationId}   KEYS[2] holds:expiring
-- ARGV[1] now (unix ms)          ARGV[2] reservationId (N)
-- ARGV[3] скільки тримати підтверджену бронь (ms) — для ідемпотентних повторів confirm
--
-- Повертає {code, eventId, userId, confirmedAtMs}:
--   1 — підтверджено зараз, 2 — вже було підтверджено раніше,
--   0 — броні немає або вона прострочена.

local h = redis.call('HMGET', KEYS[1], 'eventId', 'userId', 'status', 'confirmedAt')
if not h[1] then
  return {0, '', '', ''}
end
if h[3] == 'confirmed' then
  return {2, h[1], h[2], h[4]}
end

local member = h[1] .. ':' .. ARGV[2]
local score = redis.call('ZSCORE', KEYS[2], member)
if not score or tonumber(score) < tonumber(ARGV[1]) then
  -- Прострочена: місце поверне Order Worker.
  return {0, '', '', ''}
end

redis.call('ZREM', KEYS[2], member)
redis.call('HSET', KEYS[1], 'status', 'confirmed', 'confirmedAt', ARGV[1])
redis.call('PEXPIRE', KEYS[1], ARGV[3])
return {1, h[1], h[2], ARGV[1]}
