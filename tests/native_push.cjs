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
  assert.equal(calls.filter(c => c === 'permission').length, 1);
  await vm.runInContext('subscribeToNativePush()', context);
  assert.equal(calls.filter(c => c === 'permission').length, 1);
  assert(calls.includes('permission') && calls.includes('channel') && calls.includes('register'));
  await vm.runInContext("saveNativePushToken('test-device-token-1234567890')", context);
  assert(status.textContent.includes('Falta habilitar'));
  assert(calls.includes('/api/Push/native-token'));
  await vm.runInContext('disableNativePush()', context);
  assert(calls.includes('/api/Push/native-unsubscribe') && calls.includes('unregister'));
  assert(!storage.has('servilabs-native-push-token'));
  assert(storage.has('servilabs-notification-permission-requested'));
  permission = 'denied';
  await vm.runInContext('subscribeToNativePush()', context);
  assert(status.textContent.includes('Ajustes de Android'));
  assert.equal(calls.filter(c => c === 'permission').length, 1);
  permission = 'prompt';
  await vm.runInContext('subscribeToNativePush()', context);
  assert.equal(calls.filter(c => c === 'permission').length, 1);
  const before = calls.length;
  vm.runInContext('nativePushSigningOut = true', context);
  await vm.runInContext("saveNativePushToken('late-registration-token')", context);
  assert.equal(calls.length, before);
  console.log('PASS: native permission, registration, missing server configuration, logout and late callback handling');
})().catch(error => { console.error(error); process.exitCode = 1; });

const exitCode = source.slice(source.indexOf("const leaveAppButton ="), source.indexOf("document.getElementById('enablePushButton')?.addEventListener"));
let exitHandler;
let exited = false;
const exitButton = { hidden: true, addEventListener: (event, callback) => { exitHandler = callback; } };
const hint = { hidden: true };
vm.runInNewContext(exitCode, {
  document: { getElementById: id => id === 'leaveAppButton' ? exitButton : hint },
  window: { Capacitor: { getPlatform: () => 'android', isPluginAvailable: () => true, Plugins: { App: { exitApp: async () => { exited = true; } } } } }
});
(async () => {
  assert.equal(exitButton.hidden, false);
  await exitHandler();
  assert(exited);
  console.log('PASS: Close application invokes Android exitApp, not minimizeApp');
})().catch(error => { console.error(error); process.exitCode = 1; });
