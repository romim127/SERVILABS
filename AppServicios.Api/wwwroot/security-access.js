// A fresh, single-use challenge is obtained for every protected request.
window.securityHeaders = async function (action) {
  const response = await fetch('/api/Seguridad/publica', { cache: 'no-store' });
  if (!response.ok) throw new Error('No se pudo comprobar la seguridad del acceso. Reintentá.');
  const { turnstile: settings } = await response.json();
  if (!settings.enabled) return {};
  if (!settings.ready) throw new Error('La verificación de acceso se está configurando. Reintentá más tarde.');
  if (!window.turnstile) {
    await new Promise((resolve, reject) => {
      const script = document.createElement('script');
      const timeout = setTimeout(() => { script.remove(); reject(new Error('No se pudo cargar la verificación. Revisá tu conexión.')); }, 20000);
      script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
      script.onload = () => { clearTimeout(timeout); resolve(); };
      script.onerror = () => { clearTimeout(timeout); script.remove(); reject(new Error('No se pudo cargar la verificación de seguridad.')); };
      document.head.append(script);
    });
  }
  const token = await new Promise((resolve, reject) => {
    const dialog = document.createElement('dialog');
    dialog.className = 'security-challenge';
    dialog.setAttribute('aria-label', 'Verificación de seguridad');
    const title = document.createElement('h3'); title.textContent = 'Verificación de seguridad';
    const target = document.createElement('div');
    const cancel = document.createElement('button'); cancel.type = 'button'; cancel.textContent = 'Cancelar';
    dialog.append(title, target, cancel); document.body.append(dialog); dialog.showModal();
    let widget; let done = false;
    const finish = (value, error) => {
      if (done) return; done = true; clearTimeout(timer);
      if (widget !== undefined) window.turnstile.remove(widget);
      dialog.close(); dialog.remove(); error ? reject(new Error(error)) : resolve(value);
    };
    const timer = setTimeout(() => finish(null, 'La verificación venció. Reintentá.'), 120000);
    cancel.onclick = () => finish(null, 'Verificación cancelada.');
    dialog.oncancel = (event) => { event.preventDefault(); finish(null, 'Verificación cancelada.'); };
    try {
      widget = window.turnstile.render(target, { sitekey: settings.siteKey, action, theme: 'auto', size: 'flexible',
        callback: value => finish(value), 'error-callback': () => finish(null, 'No se pudo verificar el acceso. Reintentá.'),
        'expired-callback': () => finish(null, 'La verificación venció. Reintentá.') });
    } catch { finish(null, 'No se pudo iniciar la verificación.'); }
  });
  return { 'X-Turnstile-Token': token };
};
