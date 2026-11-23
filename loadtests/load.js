// Load: робота під типовим навантаженням.
// TODO(Астахов): сценарій constant-arrival-rate (або constant-vus) на кілька хвилин: суміш GET /events, POST /events/{id}/reservations і confirm; userId у форматі API (1–64 символи: літери, цифри, . _ @ -, напр. `vu-${__VU}-${__ITER}` як у spike.js, інакше 400) і новий Idempotency-Key на кожне бронювання; thresholds з CLAUDE.md (p95<300 мс, http_req_failed<1%) — `docker compose run --rm --service-ports k6 run /scripts/load.js` проходить thresholds при ≥ 500 запитів/с, HTML-звіт у results/.
// TODO(Астахов): у setup() скинути подію через POST /admin/events/{id}/reset з capacity, більшою за кількість бронювань за тест (500 запитів/с × тривалість, напр. 1 000 000; див. spike.js) — інакше подія розпродається за ~100 с і далі тест міряє лише відповіді 409; у підсумку reservations_sold_out = 0.
// TODO(Астахов): внести результати load-тесту (RPS, p95, частка помилок) у таблицю "Результати" в README — рядки load заповнені з results/load.html.
import http from 'k6/http';
import { BASE_URL } from './lib/common.js';

export const options = { vus: 1, iterations: 1 };

export default function () {
  http.get(`${BASE_URL}/events`);
}
