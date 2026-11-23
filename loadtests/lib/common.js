// Спільне для всіх сценаріїв k6.
export const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
export const jsonHeaders = { 'Content-Type': 'application/json' };
