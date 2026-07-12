// Service Worker para notificaciones push
self.addEventListener('push', function(event) {
  let data = {};
  try {
    data = event.data.json();
  } catch {
    data = { title: 'Notificación', body: event.data && event.data.text() };
  }
  const title = data.title || 'Notificación';
  const options = {
    body: data.body || '',
    icon: data.icon || '/ia-avatar.png',
    badge: data.badge || '/ia-avatar.png',
    data: data.url ? { url: data.url } : {}
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', function(event) {
  event.notification.close();
  const url = event.notification.data && event.notification.data.url;
  if (url) {
    event.waitUntil(clients.openWindow(url));
  }
});
