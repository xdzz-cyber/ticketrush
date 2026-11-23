# Архітектура TicketRush

```
Клієнти / k6 → Nginx → Booking API ×N ─┬→ Redis      (каталог, залишок місць, броні з TTL)
                                       └→ RabbitMQ   (черга booking.confirmed)
                                              ↓
                                         Order Worker ─┬→ PostgreSQL (події, замовлення)
                                                       └→ Redis      (повернення прострочених броней)
```

| Компонент | Відповідальний | Роль |
| --- | --- | --- |
| Booking API | Габрук | Синхронна частина: каталог, атомарне бронювання, підтвердження → RabbitMQ |
| Order Worker | Астахов | Асинхронна частина: оплата, запис замовлень, звільнення прострочених броней |
| Nginx, Docker Compose | Астахов | Балансування, запуск і масштабування |
| TicketRush.Shared | спільно | Контракт повідомлення, схема ключів Redis, seed-дані |

## Де живе каталог

Каталог (метадані подій і залишок місць) **зберігається в Redis**, а не кешується з PostgreSQL на кожен запит.
Початкові дані беруться з `TicketRush.Shared.SeedData`: Booking API на старті заливає їх у Redis
(лише якщо ключів ще немає), Order Worker — ті самі події в PostgreSQL. Таким чином Redis виконує роль
швидкого сховища на критичному шляху (каталог + залишок), а PostgreSQL — довготривалого сховища подій і замовлень.
Залишок місць змінюється лише в Redis, тому для нього Redis — джерело правди і працює з AOF (див. нижче).

## Дані в Redis

| Ключ | Тип | Що зберігає |
| --- | --- | --- |
| `events:ids` | SET | id подій каталогу |
| `event:{id}` | HASH | name, venue, startsAt, capacity |
| `event:{id}:stock` | STRING | залишок місць |
| `hold:{reservationId}` | HASH, TTL | eventId, userId, expiresAt, status (`held`/`confirmed`) |
| `holds:expiring` | ZSET | member `{eventId}:{reservationId}`, score = expiresAt (unix ms) |
| `idem:reserve:{eventId}:{key}` | STRING, TTL 24 год | результат бронювання для повторів з тим самим `Idempotency-Key` |

## Потоки

**Бронювання** `POST /events/{id}/reservations` (`Idempotency-Key`). Один Lua-скрипт (`reserve.lua`):
перевірка ключа ідемпотентності → перевірка `stock > 0` → `DECR` → запис `hold` з TTL → `ZADD holds:expiring`.
Redis виконує скрипт атомарно, тому два запити не можуть забрати одне й те саме останнє місце.

**Підтвердження** `POST /reservations/{id}/confirm`. `confirm.lua` перевіряє, що бронь існує і не прострочена,
робить `ZREM` з `holds:expiring` і позначає бронь `confirmed`. Після цього API публікує `BookingConfirmed`
у RabbitMQ з publisher confirms і відповідає `202 Accepted`. Повторний confirm повертає той самий результат
і ще раз публікує повідомлення (на випадок, якщо минула публікація не вдалась), тому Order Worker ідемпотентний.

**Звільнення прострочених броней** (Order Worker). Періодично бере з `holds:expiring` члени зі score ≤ now
і для кожного, якщо `ZREM` повернув 1, робить `INCR stock`.

**Правило розв'язання гонки confirm vs прострочення:** хто перший зробив `ZREM` члена `holds:expiring`,
той і вирішив долю броні. Тому підтверджене місце ніколи не повернеться в продаж, а прострочене — не буде підтверджене.

**Замовлення** (Order Worker). Споживає `booking.confirmed` (manual ack), імітує оплату, пише `Order` у PostgreSQL.
Унікальний індекс на `ReservationId` робить повторну доставку безпечною.

## Чому так

- **Redis на критичному шляху, PostgreSQL — поза ним.** Пік запитів б'є в Redis (in-memory, однопотокове
  виконання скриптів), а запис у БД іде асинхронно через чергу зі своєю швидкістю.
- **Stateless API.** Увесь стан — у Redis/RabbitMQ, тому кількість інстансів змінюється `--scale`.
- **Ідемпотентність з обох боків.** Клієнт може повторити бронювання (`Idempotency-Key`) і підтвердження
  (ідемпотентне за `reservationId`); RabbitMQ доставляє at-least-once, воркер дедуплікує за `ReservationId`.
- **Redis з AOF (`appendfsync everysec`).** Без нього рестарт Redis стирав залишок, Booking API заново
  заливав початкову кількість місць — і вже продані місця йшли в продаж удруге (перевірено на прототипі).
  З AOF залишок і броні переживають рестарт.

## Відомі обмеження прототипу

- Один інстанс Redis: при краху машини можна втратити до ~1 с записів (AOF everysec); у продакшені — репліки/Sentinel.
- Lua-скрипти працюють з кількома ключами, тому для Redis Cluster знадобились би hash tags.
- `POST /admin/events/{id}/reset` не має авторизації і увімкнений у compose лише для навантажувальних тестів.
- Автотести покривають логіку в Redis (Lua-скрипти); HTTP-рівень перевіряється k6-сценаріями.
