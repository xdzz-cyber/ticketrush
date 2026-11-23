// Spike (flash sale): різкий сплеск бронювань однієї події з обмеженою кількістю місць.
// Перевіряє: p95 бронювання, частку помилок і відсутність oversell.
//
// Параметри (env): BASE_URL, EVENT_ID=1, SEATS=100, PEAK_RPS=1000, CONFIRM_RATIO=0.5
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';
import { BASE_URL, jsonHeaders } from './lib/common.js';

const EVENT_ID = Number(__ENV.EVENT_ID || 1);
const SEATS = Number(__ENV.SEATS || 100);
const PEAK_RPS = Number(__ENV.PEAK_RPS || 1000);
const CONFIRM_RATIO = Number(__ENV.CONFIRM_RATIO || 0.5);

const reservationsOk = new Counter('reservations_ok');
const reservationsSoldOut = new Counter('reservations_sold_out');
const confirmationsOk = new Counter('confirmations_ok');

// 409 "місць немає" — очікувана бізнес-відповідь, а не помилка.
http.setResponseCallback(http.expectedStatuses({ min: 200, max: 299 }, 409));

export const options = {
  scenarios: {
    flash_sale: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 200,
      maxVUs: 2000,
      stages: [
        { target: 10, duration: '10s' },       // до старту продажу
        { target: PEAK_RPS, duration: '5s' },  // старт продажу — сплеск
        { target: PEAK_RPS, duration: '30s' }, // пік
        { target: 10, duration: '10s' },       // спад
      ],
    },
  },
  thresholds: {
    'http_req_duration{name:reserve}': ['p(95)<300'],
    http_req_failed: ['rate<0.01'],
    reservations_ok: [`count<=${SEATS}`], // oversell: успішних броней не більше, ніж місць
    checks: ['rate>0.99'],
  },
};

export function setup() {
  const res = http.post(`${BASE_URL}/admin/events/${EVENT_ID}/reset`, JSON.stringify({ capacity: SEATS }), {
    headers: jsonHeaders,
  });
  if (res.status !== 204) {
    throw new Error(`Reset failed: ${res.status} ${res.body}`);
  }
}

export default function () {
  const res = http.post(
    `${BASE_URL}/events/${EVENT_ID}/reservations`,
    JSON.stringify({ userId: `vu-${__VU}-${__ITER}` }),
    { headers: { ...jsonHeaders, 'Idempotency-Key': uuidv4() }, tags: { name: 'reserve' } },
  );
  check(res, { 'reserve: 201 or 409': (r) => r.status === 201 || r.status === 409 });

  if (res.status === 409) {
    reservationsSoldOut.add(1);
    return;
  }
  if (res.status !== 201) {
    return;
  }
  reservationsOk.add(1);

  if (Math.random() < CONFIRM_RATIO) {
    const confirm = http.post(`${BASE_URL}/reservations/${res.json('reservationId')}/confirm`, null, {
      tags: { name: 'confirm' },
    });
    if (check(confirm, { 'confirm: 202': (r) => r.status === 202 })) {
      confirmationsOk.add(1);
    }
  }
}

export function teardown() {
  const res = http.get(`${BASE_URL}/events/${EVENT_ID}`);
  if (!check(res, { 'teardown: event loaded': (r) => r.status === 200 })) {
    console.error(`Cannot verify oversell: GET /events/${EVENT_ID} returned ${res.status}`);
    return;
  }
  const event = res.json();
  const sold = event.capacity - event.available;
  console.log(`Event ${EVENT_ID}: capacity=${event.capacity}, available=${event.available}, sold=${sold}`);
  check(event, {
    'no oversell: available >= 0': (e) => e.available >= 0,
    'all seats sold out': (e) => e.available === 0,
  });
}
