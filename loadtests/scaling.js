// Scaling: однаковий тест для 1 і 3 інстансів Booking API.
// TODO(Астахов): у setup() скинути подію через POST /admin/events/{id}/reset з capacity, більшою за кількість бронювань за тест (як у load.js); userId у форматі API (`vu-${__VU}-${__ITER}`, див. spike.js) — під час обох запусків немає відповідей 409 і 400, тобто міряється саме бронювання.
// TODO(Астахов): сценарій, що шукає межу пропускної здатності (ramping-arrival-rate до насичення), однаковий для обох запусків; тег/env INSTANCES для підпису звіту; підрахунок розподілу відповідей за заголовком X-Instance — два HTML-звіти (results/scaling-1.html, results/scaling-3.html) і таблиця RPS/p95 для 1 vs 3 у README.
import http from 'k6/http';
import { BASE_URL } from './lib/common.js';

export const options = { vus: 1, iterations: 1 };

export default function () {
  http.get(`${BASE_URL}/events`);
}
