// Scaling: однаковий тест для 1 і 3 інстансів Booking API.
// TODO(Астахов): сценарій, що шукає межу пропускної здатності (ramping-arrival-rate до насичення), однаковий для обох запусків; тег/env INSTANCES для підпису звіту; підрахунок розподілу відповідей за заголовком X-Instance — два HTML-звіти (results/scaling-1.html, results/scaling-3.html) і таблиця RPS/p95 для 1 vs 3 у README.
import http from 'k6/http';
import { BASE_URL } from './lib/common.js';

export const options = { vus: 1, iterations: 1 };

export default function () {
  http.get(`${BASE_URL}/events`);
}
