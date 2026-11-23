-- Атомарне бронювання одного місця. Redis виконує скрипт цілком, без перемежування
-- з іншими командами, тому перевірка залишку і декремент не можуть "розійтися" — oversell неможливий.
--
-- KEYS[1] event:{id}:stock      KEYS[2] hold:{reservationId}
-- KEYS[3] holds:expiring        KEYS[4] idem:reserve:{eventId}:{key}
-- ARGV[1] reservationId (N)     ARGV[2] eventId        ARGV[3] userId
-- ARGV[4] expiresAt (unix ms)   ARGV[5] TTL ключа hold (ms)
-- ARGV[6] TTL ключа ідемпотентності (s)                ARGV[7] member для holds:expiring
--
-- Повертає {code, "reservationId|expiresAtMs"}:
--   1 — заброньовано, 2 — повтор запиту з тим самим Idempotency-Key,
--   0 — місць немає, -1 — подію не знайдено.

local cached = redis.call('GET', KEYS[4])
if cached then
  return {2, cached}
end

local stock = redis.call('GET', KEYS[1])
if not stock then
  return {-1, ''}
end
if tonumber(stock) <= 0 then
  return {0, ''}
end

redis.call('DECR', KEYS[1])
redis.call('HSET', KEYS[2], 'eventId', ARGV[2], 'userId', ARGV[3], 'expiresAt', ARGV[4], 'status', 'held')
redis.call('PEXPIRE', KEYS[2], ARGV[5])
redis.call('ZADD', KEYS[3], ARGV[4], ARGV[7])

local result = ARGV[1] .. '|' .. ARGV[4]
redis.call('SET', KEYS[4], result, 'EX', ARGV[6])
return {1, result}
