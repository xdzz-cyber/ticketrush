-- Скидання події перед навантажувальним тестом (лише при Admin:Enabled=true).
-- Видаляє активні броні події з holds:expiring, щоб їх пізніше не "повернули" понад новий ліміт.
--
-- KEYS[1] event:{id}:stock   KEYS[2] event:{id}   KEYS[3] holds:expiring
-- ARGV[1] capacity           ARGV[2] префікс member: "{eventId}:"
-- Повертає кількість видалених броней або -1, якщо подію не знайдено.

if redis.call('EXISTS', KEYS[2]) == 0 then
  return -1
end

local removed = 0
local members = redis.call('ZRANGE', KEYS[3], 0, -1)
for _, m in ipairs(members) do
  if string.sub(m, 1, #ARGV[2]) == ARGV[2] then
    redis.call('ZREM', KEYS[3], m)
    removed = removed + 1
  end
end

redis.call('SET', KEYS[1], ARGV[1])
redis.call('HSET', KEYS[2], 'capacity', ARGV[1])
return removed
