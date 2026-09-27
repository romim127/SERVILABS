const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('AppServicios.Api/wwwroot/app.js', 'utf8');
const code = source.slice(source.indexOf('const NATIVE_PUSH_TOKEN_KEY'), source.indexOf('async function subscribeToPushNotifications'));
const listeners = {};
const calls = [];
const storage = new Map();
const status = { textContent: '' };
let permission = 'prompt';
const push = {
  addListener: async (name, callback) => { listeners[name] = callback; },
  checkPermissions: async () => ({ receive: permission }),
  requestPermissions: async () => { calls.push('permission'); return { receive: permission = 'granted' }; },
  createChannel: async () => calls.push('channel'),
  register: async () => calls.push('register'),
  unregister: async () => calls.push('unregister')
};
const context = vm.createContext({
  window: { Capacitor: { getPlatform: () => 'android', isPluginAvailable: () => true, Plugins: { PushNotifications: push } } },
  document: { getElementById: () => status }, currentSession: { usuarioId: 1 },
  localStorage: { getItem: k => storage.get(k), setItem: (k,v) => storage.set(k,v), removeItem: k => storage.delete(k) },
  fetch: async (url, init) => { calls.push(url); return { ok: true, json: async () => ({ configured: false }) }; },
  loadNotifications: () => {}, AbortSignal
});
vm.runInContext(code, context);
(async () => {
  await vm.runInContext('subscribeToNativePush()', context);
  assert(!calls.includes('permission'));
  assert(!calls.includes('register'));
  await vm.runInContext('subscribeToNativePush(true)', context);
  assert(calls.includes('permission') && calls.includes('channel') && calls.includes('register'));
  await vm.runInContext("saveNativePushToken('test-device-token-1234567890')", context);
  assert(status.textContent.includes('Falta habilitar'));
  assert(calls.includes('/api/Push/native-token'));
  await vm.runInContext('disableNativePush()', context);
  assert(calls.includes('/api/Push/native-unsubscribe') && calls.includes('unregister'));
  assert.equal(storage.size, 0);
  permission = 'denied';
  await vm.runInContext('subscribeToNativePush()', context);
  assert(status.textContent.includes('Ajustes de Android'));
  const before = calls.length;
  vm.runInContext('nativePushSigningOut = true', context);
  await vm.runInContext("saveNativePushToken('late-registration-token')", context);
  assert.equal(calls.length, before);
  console.log('PASS: native permission, registration, missing server configuration, logout and late callback handling');
})().catch(error => { console.error(error); process.exitCode = 1; });
