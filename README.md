# TicketRush

Розподілена система продажу квитків з піковим навантаженням (flash sale). Іспитовий проєкт:
Габрук Богдан, Астахов Богдан. Архітектура і рішення — у [docs/architecture.md](docs/architecture.md).

## Структура

```
src/TicketRush.Shared/        контракти між сервісами (змінювати лише за згодою обох)
src/TicketRush.BookingApi/    Booking API — Габрук
src/TicketRush.OrderWorker/   Order Worker — Астахов
tests/                        тести Booking API на справжньому Redis (Testcontainers)
loadtests/                    сценарії k6: spike.js (Габрук), load.js і scaling.js (Астахов)
nginx/, docker-compose.yml    інфраструктура — Астахов
results/                      HTML-звіти k6
```

Відкриті задачі: `grep -rn "TODO(" .`

## Вимоги

- Docker Desktop (Compose v2);
- .NET SDK 8+ — лише для локальної збірки й тестів (у Docker усе збирається само).

## Запуск системи

```powershell
docker compose up -d --build
```

| Що | Адреса |
| --- | --- |
| API (через Nginx) | http://localhost:8080 |
| RabbitMQ UI | http://localhost:15672 (ticketrush / ticketrush) |
| PostgreSQL | localhost:5432 (ticketrush / ticketrush) |
| Redis | localhost:6379 |

Паролі за замовчуванням — лише для локального запуску; змінити можна у `.env` (див. `.env.example`).

Масштабування Booking API до 3 інстансів:

```powershell
docker compose up -d --scale booking-api=3
```

Зупинка з видаленням даних: `docker compose down -v`.

### Локальний запуск сервісу без Docker (для розробки)

Інфраструктура — у Docker, сервіс — через `dotnet run`. `appsettings.json` уже налаштовані на `localhost`
і на ті самі логіни, що й compose:

```powershell
docker compose up -d redis rabbitmq postgres
dotnet run --project src/TicketRush.BookingApi --urls http://localhost:5099
```

Якщо у `.env` змінено паролі, передайте їх змінними середовища, наприклад `$env:RabbitMq__Password = "..."`.
Ендпоінт `/admin/...` локально вимкнений; увімкнути — `$env:Admin__Enabled = "true"`.

## API

| Метод | Шлях | Відповіді |
| --- | --- | --- |
| GET | `/events` | 200 — список подій із залишком місць |
| GET | `/events/{id}` | 200 / 404 |
| POST | `/events/{id}/reservations` + заголовок `Idempotency-Key`, тіло `{"userId":"u1"}` | 201 — бронь на 5 хв; 200 — повтор з тим самим ключем; 409 — місць немає; 404 |
| POST | `/reservations/{id}/confirm` | 202 — підтверджено, замовлення в черзі; 410 — бронь прострочена/не існує; 503 — черга недоступна, повторіть |
| POST | `/admin/events/{id}/reset`, тіло `{"capacity":100}` | 204 — скинути подію перед тестом (лише при `Admin__Enabled=true`) |
| GET | `/health` | 200 |

Кожна відповідь містить заголовок `X-Instance` — ім'я контейнера, що обробив запит.

```powershell
curl.exe -s http://localhost:8080/events
curl.exe -s -X POST http://localhost:8080/events/1/reservations -H "Content-Type: application/json" -H "Idempotency-Key: demo-1" -d '{\"userId\":\"u1\"}'
curl.exe -s -X POST http://localhost:8080/reservations/<reservationId>/confirm
```

## Тести

Потрібен запущений Docker (Testcontainers піднімає Redis):

```powershell
dotnet test
```

Ключовий тест — `ParallelReservations_NeverSellMoreThanCapacity`: 1000 паралельних бронювань на 100 місць → рівно 100 успішних.

## Навантажувальне тестування (k6)

k6 запускається в контейнері (профіль `k6` у compose), локально встановлювати не потрібно.
Під час тесту живий дашборд — http://localhost:5665, після завершення HTML-звіт у `results/`.

**Spike (flash sale)** — Габрук:

```powershell
docker compose run --rm --service-ports -e K6_WEB_DASHBOARD_EXPORT=/results/spike.html k6 run /scripts/spike.js
```

Параметри: `-e SEATS=100 -e PEAK_RPS=1000 -e CONFIRM_RATIO=0.5 -e EVENT_ID=1`.
Thresholds: p95 бронювання < 300 мс, помилки < 1%, `reservations_ok <= SEATS` (немає oversell);
у teardown перевіряється, що `available >= 0`, і друкується `capacity / available / sold`.

**Load** — Астахов:

```powershell
docker compose run --rm --service-ports -e K6_WEB_DASHBOARD_EXPORT=/results/load.html k6 run /scripts/load.js
```

**Scaling (1 vs 3 інстанси)** — Астахов:

```powershell
docker compose up -d --scale booking-api=1
docker compose run --rm --service-ports -e K6_WEB_DASHBOARD_EXPORT=/results/scaling-1.html k6 run /scripts/scaling.js
docker compose up -d --scale booking-api=3
docker compose run --rm --service-ports -e K6_WEB_DASHBOARD_EXPORT=/results/scaling-3.html k6 run /scripts/scaling.js
```

## Результати

Звіти лежать у `results/`, висновки — у звіті до іспиту.

| Метрика | Ціль | Spike, 1 інстанс ([spike.html](results/spike.html)) | Load | Scaling 1 → 3 |
| --- | --- | --- | --- | --- |
| Пропускна здатність бронювання | ≥ 500 запитів/с | 686 запитів/с у середньому за тест, пік сценарію 1000 запитів/с | — | — |
| Затримка бронювання | p95 ≤ 300 мс | p95 = 2.2 мс | — | — |
| Oversell | 0 | 0 (100 броней на 100 місць, `available = 0`) | — | — |
| Помилки 5xx | < 1% | 0% | — | — |
| Масштабування | приріст RPS | — | — | — |

Spike: 37 728 запитів за 55 с, з них 100 успішних броней, решта — очікувані `409` («місць немає»).
Середовище: Docker Desktop на одній машині, k6 у тій самій Docker-мережі.

### Знайдені вузькі місця

| Проблема | Симптом | Рішення | Ефект |
| --- | --- | --- | --- |
| Nginx без keepalive до upstream | 16% помилок, p95 1.85 с; у логах nginx `connect() failed (99: Address not available)` — вичерпано ефемерні порти | `keepalive 128` в upstream | 0% помилок, p95 2.4 мс |
| Redis без персистентності | після рестарту Redis і API залишок повертався до початкового — продані місця йшли в продаж удруге | AOF (`appendfsync everysec`) | залишок переживає рестарт |
