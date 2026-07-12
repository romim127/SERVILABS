// --- MULTI-MONEDA: Configuración y lógica de moneda ---
const CURRENCY_KEY = 'appservicios-currency';
const currencySelect = document.getElementById('currencySelect');
let currentCurrency = localStorage.getItem(CURRENCY_KEY) || 'ARS';

function setCurrency(currency) {
  currentCurrency = currency;
  localStorage.setItem(CURRENCY_KEY, currency);
  if (currencySelect) currencySelect.value = currency;
  updateAllCurrencyDisplays();
}

function updateAllCurrencyDisplays() {
  // Actualiza todos los elementos que muestran montos
  // (esto puede ser más granular según la app, aquí solo fuerza updatePreview y payment)
  updatePreview && updatePreview();
  updatePaymentCurrency && updatePaymentCurrency();
  // Puedes agregar más hooks aquí si hay más lugares con montos
}

if (currencySelect) {
  currencySelect.value = currentCurrency;
  currencySelect.addEventListener('change', (e) => {
    setCurrency(e.target.value);
  });
}
// --- FIN MULTI-MONEDA ---
// --- INTEGRACIÓN FLUJO ONBOARDING: mostrar app tras login/registro exitoso ---
// --- ASISTENTE IA FLOTANTE ---
// --- NOTIFICACIONES PUSH ---
async function subscribeToPushNotifications() {
  if (!('serviceWorker' in navigator) || !('PushManager' in window)) return;
  const reg = await navigator.serviceWorker.ready;
  let permission = Notification.permission;
  if (permission !== 'granted') {
    permission = await Notification.requestPermission();
  }
  if (permission !== 'granted') return;
  let subscription = await reg.pushManager.getSubscription();
  if (!subscription) {
    const keyResponse = await fetch('/api/Push/public-key');
    if (!keyResponse.ok) return;
    const keyPayload = await keyResponse.json();
    const vapidPublicKey = keyPayload.publicKey;
    if (!vapidPublicKey) return;
    const convertedVapidKey = urlBase64ToUint8Array(vapidPublicKey);
    subscription = await reg.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey: convertedVapidKey
    });
  }
  // Enviar la suscripción al backend
  if (currentSession && currentSession.usuarioId) {
    const subJson = subscription.toJSON();
    await fetch('/api/Push/subscribe', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        usuarioId: currentSession.usuarioId,
        endpoint: subJson.endpoint,
        p256dh: subJson.keys.p256dh,
        auth: subJson.keys.auth
      })
    });
  }
  return subscription;
}

function urlBase64ToUint8Array(base64String) {
  const padding = '='.repeat((4 - base64String.length % 4) % 4);
  const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
  const rawData = window.atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; ++i) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

// Llama a subscribeToPushNotifications() después de login/registro exitoso
window.subscribeToPushNotifications = subscribeToPushNotifications;
const aiVoicePrefVoice = document.getElementById('aiVoicePrefVoice');
const aiVoicePrefMsg = document.getElementById('aiVoicePrefMsg');
let aiVoiceMode = 'voz';

function setAiVoiceMode(mode) {
  aiVoiceMode = mode;
  localStorage.setItem('ai-voice-mode', mode);
}
if (aiVoicePrefVoice) aiVoicePrefVoice.addEventListener('change', () => setAiVoiceMode('voz'));
if (aiVoicePrefMsg) aiVoicePrefMsg.addEventListener('change', () => setAiVoiceMode('mensaje'));
// Cargar preferencia guardada
const savedVoiceMode = localStorage.getItem('ai-voice-mode');
if (savedVoiceMode === 'mensaje') {
  aiVoiceMode = 'mensaje';
  if (aiVoicePrefMsg) aiVoicePrefMsg.checked = true;
}

function speakAi(text) {
  if ('speechSynthesis' in window && aiVoiceMode === 'voz') {
    const utter = new SpeechSynthesisUtterance(text.replace(/<[^>]+>/g, ''));
    utter.lang = 'es-AR';
    window.speechSynthesis.speak(utter);
  }
}
const aiAssistant = document.getElementById('aiAssistant');
const aiAssistantBtn = document.getElementById('aiAssistantBtn');
const aiAssistantPanel = document.getElementById('aiAssistantPanel');
const aiAssistantClose = document.getElementById('aiAssistantClose');
const aiAssistantMessages = document.getElementById('aiAssistantMessages');
const aiAssistantForm = document.getElementById('aiAssistantForm');
const aiAssistantInput = document.getElementById('aiAssistantInput');

function showAiAssistantPanel() {
  if (aiAssistantPanel) aiAssistantPanel.style.display = 'block';
}
function hideAiAssistantPanel() {
  if (aiAssistantPanel) aiAssistantPanel.style.display = 'none';
}
if (aiAssistantBtn) aiAssistantBtn.addEventListener('click', showAiAssistantPanel);
if (aiAssistantClose) aiAssistantClose.addEventListener('click', hideAiAssistantPanel);

function appendAiMessage(text, from = 'bot') {
  if (!aiAssistantMessages) return;
  const div = document.createElement('div');
  div.className = 'ai-message ai-message-' + from;
  div.textContent = text;
  aiAssistantMessages.appendChild(div);
  aiAssistantMessages.scrollTop = aiAssistantMessages.scrollHeight;
  if (from === 'bot' && aiVoiceMode === 'voz') speakAi(text);
}

function buildLocalAiResponse(userText) {
  const normalized = String(userText || '').toLowerCase();
  if (/funer|sepelio|cremaci|duelo|velatorio/.test(normalized)) {
    return 'Podemos ayudarte con servicios funerarios de forma respetuosa: traslado, sala velatoria, sepelio, cremación, gestiones y acompañamiento. Indica ciudad y urgencia para orientar la solicitud.';
  }
  if (/inscrib|registr|crear cuenta|alta|ofrecer|trabaj/.test(normalized)) {
    return 'Para sumarte, toca Registrarme y crea tu perfil como Profesional o Empresa/Cuadrilla. El Plan Fundadores Pro tiene alta de 2500 ARS y mensualidad de 2500 ARS por 3 meses para los primeros 100.';
  }
  if (/pagar|pago|tarifa|precio|comisi/.test(normalized)) {
    return 'El alta del Plan Fundadores Pro es de 2500 ARS, con mensualidad de 2500 ARS por 3 meses. El pago protegido por servicios tendrá comisión del 2%.';
  }
  return 'Estoy para ayudarte a encontrar servicios, registrarte, publicar una solicitud o armar tu perfil profesional. Cuéntame qué necesitas y te guío paso a paso.';
}

async function requestAiAssistantResponse(userText) {
  try {
    const response = await fetch('/api/Ai/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: userText, context: 'onboarding' })
    });

    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    const payload = await response.json();
    return payload.message || buildLocalAiResponse(userText);
  } catch (error) {
    console.warn('Asistente IA en modo local.', error);
    return buildLocalAiResponse(userText);
  }
}

if (aiAssistantForm) {
  aiAssistantForm.addEventListener('submit', async function(e) {
    e.preventDefault();
    const userText = aiAssistantInput.value.trim();
    if (!userText) return;
    appendAiMessage(userText, 'user');
    aiAssistantInput.value = '';
    appendAiMessage('Estoy pensando...', 'bot');
    const thinkingMessage = aiAssistantMessages?.lastElementChild;
    const response = await requestAiAssistantResponse(userText);
    if (thinkingMessage) {
      thinkingMessage.textContent = response;
      if (aiVoiceMode === 'voz') speakAi(response);
    } else {
      appendAiMessage(response, 'bot');
    }
  });
}

// Mensaje proactivo al abrir mapa (sin redeclarar mapStatus)
if (typeof mapStatus !== 'undefined' && mapStatus) {
  mapStatus.addEventListener('DOMSubtreeModified', function() {
    if (aiAssistantMessages && aiAssistantPanel && aiAssistantPanel.style.display !== 'block') {
      appendAiMessage('¿Quieres ver profesionales o clientes cerca tuyo en el mapa? Usa los filtros o pregúntame aquí.', 'bot');
    }
  });
}

// --- FIN ASISTENTE IA FLOTANTE ---
// Ejemplo de recordatorio proactivo al iniciar sesión
// Mensaje IA proactivo si el usuario no ha interactuado hoy
document.addEventListener('DOMContentLoaded', function() {
    // --- AVISOS IA PROACTIVOS ---
    // Simulación de datos de usuario y actividad
    const userRole = (currentSession && currentSession.rol) || '';
    // --- AVISOS IA REALES ---
    // userId ya declarado arriba
    if (userRole === 'profesional' && userId) {
      // Citas próximas (solicitudes aceptadas con fecha hoy o futura)
      fetch(`/api/SolicitudesTrabajo?userId=${userId}`)
        .then(r => r.ok ? r.json() : [])
        .then(data => {
          const hoy = new Date().toISOString().slice(0, 10);
          const citas = (data || []).filter(s => s.estado === 'Aceptado' && s.fechaRequerida && s.fechaRequerida.slice(0,10) >= hoy);
          if (citas.length > 0) {
            setTimeout(() => {
              appendAiMessage(`Recuerda: tienes una cita hoy a las ${citas[0].fechaRequerida.slice(11,16)} con ${citas[0].clienteNombre} (${citas[0].servicioNombre}). ¿Quieres que te avise 10 minutos antes?`, 'bot');
            }, 8000);
          }
          // Objetivo mensual (simulado: objetivo 10 trabajos/mes)
          const mes = hoy.slice(0,7);
          const trabajosMes = (data || []).filter(s => s.estado === 'Completado' && s.fechaCompletacion && s.fechaCompletacion.slice(0,7) === mes);
          if (trabajosMes.length < 10) {
            setTimeout(() => {
              appendAiMessage(`¡Vas al ${Math.round(trabajosMes.length / 10 * 100)}% de tu objetivo mensual! ¿Quieres ver oportunidades para sumar más trabajos?`, 'bot');
            }, 10000);
          }
          // Mensajes sin responder (simulado: solicitudes aceptadas sin fecha completada)
          const sinResponder = (data || []).filter(s => s.estado === 'Aceptado' && !s.fechaCompletacion);
          if (sinResponder.length > 0) {
            setTimeout(() => {
              appendAiMessage(`Tienes trabajos aceptados sin marcar como completados. ¿Quieres gestionarlos ahora?`, 'bot');
            }, 12000);
          }
        });
    }
    if (userRole === 'cliente' && userId) {
      // Seguimiento de última solicitud
      fetch(`/api/SolicitudesTrabajo?userId=${userId}`)
        .then(r => r.ok ? r.json() : [])
        .then(data => {
          const ultima = (data || []).find(s => s.estado === 'Pendiente' || s.estado === 'Aceptado');
          if (ultima) {
            setTimeout(() => {
              appendAiMessage('¿Quieres hacer seguimiento a tu última solicitud o necesitas ayuda para elegir profesional?', 'bot');
            }, 9000);
          }
        });
    }
    // --- FIN AVISOS IA PROACTIVOS ---
  // Notificación IA si hay solicitud relevante para el perfil (para todos los perfiles, usando datos reales)
  // userProfile y userName ya declarados arriba
  // userId ya declarado arriba
  if (userProfile && userName && userId) {
    fetch(`/api/SolicitudesTrabajo?userId=${userId}`)
      .then(r => r.ok ? r.json() : [])
      .then(data => {
        // Buscar solicitudes pendientes que coincidan con el perfil/rubro/servicio del usuario
        const match = (data || []).find(s => {
          // Coincidencia flexible: servicioNombre, rubroNombre, descripcion
          const fields = [s.servicioNombre, s.rubroNombre, s.descripcion].join(' ').toLowerCase();
          return userProfile && fields.includes(userProfile.toLowerCase()) && s.estado === 'Pendiente';
        });
        if (match) {
          setTimeout(() => {
            appendAiMessage(`${userName}, ha salido una solicitud real que coincide con tu perfil: "${match.servicioNombre}" en ${match.ubicacion || 'zona a definir'}: "${match.descripcion || 'Sin descripción'}"`, 'bot');
          }, 6000);
        }
      });
  }
  const today = new Date().toISOString().slice(0, 10);
  const lastActive = localStorage.getItem('ai-last-active') || '';
  // userName y userProfile ya declarados arriba
  let proactiveMsg = '';
  // Aviso si no interactuó hoy
  if (lastActive !== today) {
    if (userName && /maria/i.test(userName)) {
      proactiveMsg = 'María, ¿vas a seguir en la búsqueda del plomero o vas a dejar que la casa se inunde? 😅';
    } else if (userName && /juan/i.test(userName)) {
      proactiveMsg = 'Juan, ¿vas a trabajar hoy o prefieres buscar nuevas oportunidades?';
    } else {
      proactiveMsg = '¡Ey! ¿Vas a trabajar hoy, buscar algo, o necesitas ayuda para avanzar? ¡Estoy lista para ayudarte!';
    }
    setTimeout(() => {
      appendAiMessage(proactiveMsg, 'bot');
      localStorage.setItem('ai-last-active', today);
    }, 3500);
  }
  // Aviso de solicitud relevante para el perfil (siempre, aunque esté activo)
  const solicitudes = [
    { servicio: 'Pintor', mensaje: 'Se busca pintor para trabajo en zona centro.' },
    { servicio: 'Gasista', mensaje: 'Urgente: instalación de gas en departamento.' },
    { servicio: 'Electricista', mensaje: 'Reparación eléctrica en casa particular.' },
    { servicio: 'Cuida niños', mensaje: 'Familia busca niñera para tardes.' }
  ];
  const match = solicitudes.find(s => new RegExp(s.servicio, 'i').test(userProfile));
  if (userName && match) {
    setTimeout(() => {
      appendAiMessage(`${userName}, ha salido una solicitud de trabajo que podría dar con tu perfil de ${match.servicio.toLowerCase()}: "${match.mensaje}"`, 'bot');
    }, 6000);
  }
});
function showAppAfterAuth() {
  const wizard = document.getElementById('wizardEntry');
  const header = document.getElementById('mainAppHeader');
  const main = document.getElementById('inicio');
  const footer = document.getElementById('mainAppFooter');
  if (wizard) wizard.style.display = 'none';
  if (header) header.style.display = '';
  if (main) main.style.display = '';
  if (footer) footer.style.display = '';
}

// Hook para login exitoso
async function handleLoginAndShowApp(e) {
  e.preventDefault();
  // Copiar valores del wizard a los inputs reales de login
  const wizEmail = document.getElementById('wizardEmail');
  const wizPass = document.getElementById('wizardPassword');
  if (wizEmail && wizEmail.value && loginEmailInput) {
    loginEmailInput.value = wizEmail.value;
  }
  if (wizPass && wizPass.value && loginPasswordInput) {
    loginPasswordInput.value = wizPass.value;
  }
  await handleLogin();
  if (currentSession) showAppAfterAuth();
}

// Hook para registro exitoso
async function handleRegisterAndShowApp(e) {
  e.preventDefault();
  await submitRegistration();
  if (currentSession) showAppAfterAuth();
}

document.addEventListener('DOMContentLoaded', function() {
  // Login
  const loginForm = document.getElementById('loginForm');
  if (loginForm) {
    loginForm.addEventListener('submit', handleLoginAndShowApp);
  }
  // Registro
  const registerForm = document.getElementById('registerForm');
  if (registerForm) {
    registerForm.addEventListener('submit', handleRegisterAndShowApp);
  }
});
// --- FIN INTEGRACIÓN FLUJO ONBOARDING ---
const THEME_KEY = 'appservicios-theme';
const SESSION_KEY = 'appservicios-session';
const root = document.documentElement;
const themeButtons = Array.from(document.querySelectorAll('[data-theme-choice]'));
const themeLabel = document.getElementById('themeLabel');
const healthStatus = document.getElementById('healthStatus');
const apiState = document.getElementById('apiState');
const rubroCount = document.getElementById('rubroCount');
const serviceCount = document.getElementById('serviceCount');
const proCount = document.getElementById('proCount');
const rubrosContainer = document.getElementById('rubrosContainer');
const currentYear = document.getElementById('currentYear');
const experienceState = document.getElementById('experienceState');
const featuredServiceName = document.getElementById('featuredServiceName');
const servicePicker = document.getElementById('servicePicker');
const urgencyPicker = document.getElementById('urgencyPicker');
const budgetRange = document.getElementById('budgetRange');
const budgetValue = document.getElementById('budgetValue');
const messageInput = document.getElementById('messageInput');
const previewService = document.getElementById('previewService');
const previewMessage = document.getElementById('previewMessage');
const previewUrgency = document.getElementById('previewUrgency');
const previewBudget = document.getElementById('previewBudget');
const roleTabs = Array.from(document.querySelectorAll('[data-role-tab]'));
const roleViews = Array.from(document.querySelectorAll('[data-role-view]'));
const authTabs = Array.from(document.querySelectorAll('[data-auth-tab]'));
const authViews = Array.from(document.querySelectorAll('[data-auth-view]'));
const loginEmailInput = document.getElementById('loginEmailInput');
const loginPasswordInput = document.getElementById('loginPasswordInput');
const loginButton = document.getElementById('loginButton');
const loginFeedback = document.getElementById('loginFeedback');
const installAppButton = document.getElementById('installAppButton');
const accountRoleButtons = Array.from(document.querySelectorAll('[data-account-role]'));
const accessState = document.getElementById('accessState');
const welcomeCaption = document.getElementById('welcomeCaption');
const welcomeTitle = document.getElementById('welcomeTitle');
const welcomeText = document.getElementById('welcomeText');
const rolePreviewInput = document.getElementById('rolePreviewInput');
const registerNameInput = document.getElementById('registerNameInput');
const registerEmailInput = document.getElementById('registerEmailInput');
const registerPhoneInput = document.getElementById('registerPhoneInput');
const registerDniInput = document.getElementById('registerDniInput');
const registerBirthDateInput = document.getElementById('registerBirthDateInput');
const registerLocationInput = document.getElementById('registerLocationInput');
const registerPasswordInput = document.getElementById('registerPasswordInput');
const clientRegisterFields = document.getElementById('clientRegisterFields');
const clientPreferencesInput = document.getElementById('clientPreferencesInput');
const proOnboarding = document.getElementById('proOnboarding');
const selectedSectorInput = document.getElementById('selectedSectorInput');
const sectorButtons = Array.from(document.querySelectorAll('[data-sector]'));
const proExperienceInput = document.getElementById('proExperienceInput');
const proRateInput = document.getElementById('proRateInput');
const proReachInput = document.getElementById('proReachInput');
const proGoalInput = document.getElementById('proGoalInput');
const proFarJobsInput = document.getElementById('proFarJobsInput');
const proDistanceBonusInput = document.getElementById('proDistanceBonusInput');
const proDescriptionInput = document.getElementById('proDescriptionInput');
const aiRubroDescription = document.getElementById('aiRubroDescription');
const aiRubroButton = document.getElementById('aiRubroButton');
const aiRubroResult = document.getElementById('aiRubroResult');
const termsCheckbox = document.getElementById('termsCheckbox');
const registerContinueButton = document.getElementById('registerContinueButton');
const registerNote = document.getElementById('registerNote');
const registerFeedback = document.getElementById('registerFeedback');
const sessionSummary = document.getElementById('sessionSummary');
const logoutButton = document.getElementById('logoutButton');
const notificationsList = document.getElementById('notificationsList');
const notificationsRefreshButton = document.getElementById('notificationsRefreshButton');
const notificationsCounter = document.getElementById('notificationsCounter');
const notificationToastHost = document.getElementById('notificationToastHost');
const paymentState = document.getElementById('paymentState');
const startPaymentButton = document.getElementById('startPaymentButton');
const mercadoPagoLinkButton = document.getElementById('mercadoPagoLinkButton');
const verifyPaymentButton = document.getElementById('verifyPaymentButton');
const confirmPaymentButton = document.getElementById('confirmPaymentButton');
const paymentFeedback = document.getElementById('paymentFeedback');
const requestState = document.getElementById('requestState');
const requestClientSelect = document.getElementById('requestClientSelect');
const requestServiceSelect = document.getElementById('requestServiceSelect');
const requestDateInput = document.getElementById('requestDateInput');
const requestBudgetInput = document.getElementById('requestBudgetInput');
const requestLocationInput = document.getElementById('requestLocationInput');
const requestDescriptionInput = document.getElementById('requestDescriptionInput');
const requestSubmitButton = document.getElementById('requestSubmitButton');
const requestRefreshButton = document.getElementById('requestRefreshButton');
const requestFeedback = document.getElementById('requestFeedback');
const requestListContainer = document.getElementById('requestListContainer');
const proState = document.getElementById('proState');
const proSelect = document.getElementById('proSelect');
const proRefreshButton = document.getElementById('proRefreshButton');
const proFeedback = document.getElementById('proFeedback');
const proPendingList = document.getElementById('proPendingList');
const proAssignedList = document.getElementById('proAssignedList');
const chatState = document.getElementById('chatState');
const chatRequestSelect = document.getElementById('chatRequestSelect');
const chatInfoText = document.getElementById('chatInfoText');
const chatMessagesList = document.getElementById('chatMessagesList');
const chatMessageInput = document.getElementById('chatMessageInput');
const chatSendButton = document.getElementById('chatSendButton');
const chatFeedback = document.getElementById('chatFeedback');
const coordinationSync = document.getElementById('coordinationSync');
const coordDateRange = document.getElementById('coordDateRange');
const coordCityFilter = document.getElementById('coordCityFilter');
const coordRubroFilter = document.getElementById('coordRubroFilter');
const coordRefreshButton = document.getElementById('coordRefreshButton');
const coordPdfButton = document.getElementById('coordPdfButton');
const coordExportButton = document.getElementById('coordExportButton');
const coordUsersMetric = document.getElementById('coordUsersMetric');
const coordUsersDetail = document.getElementById('coordUsersDetail');
const coordFlowMetric = document.getElementById('coordFlowMetric');
const coordFlowDetail = document.getElementById('coordFlowDetail');
const coordRevenueMetric = document.getElementById('coordRevenueMetric');
const coordRevenueDetail = document.getElementById('coordRevenueDetail');
const coordEngagementMetric = document.getElementById('coordEngagementMetric');
const coordEngagementDetail = document.getElementById('coordEngagementDetail');
const coordTargetStatus = document.getElementById('coordTargetStatus');
const coordAlertsList = document.getElementById('coordAlertsList');
const coordOpsDonut = document.getElementById('coordOpsDonut');
let deferredInstallPrompt = null;
const coordOpsText = document.getElementById('coordOpsText');
const coordActivationDonut = document.getElementById('coordActivationDonut');
const coordActivationText = document.getElementById('coordActivationText');
const coordAcceptanceDonut = document.getElementById('coordAcceptanceDonut');
const coordAcceptanceText = document.getElementById('coordAcceptanceText');
const mapStatus = document.getElementById('mapStatus');
const mapSummary = document.getElementById('mapSummary');
const mapLegendList = document.getElementById('mapLegendList');
const mapAudienceFilter = document.getElementById('mapAudienceFilter');
const mapRadiusFilter = document.getElementById('mapRadiusFilter');
const mapDistanceHint = document.getElementById('mapDistanceHint');
const useCurrentLocationButton = document.getElementById('useCurrentLocationButton');
const routeStatus = document.getElementById('routeStatus');
const routeSummary = document.getElementById('routeSummary');
const routeNearestButton = document.getElementById('routeNearestButton');
const openGoogleMapsRouteButton = document.getElementById('openGoogleMapsRouteButton');
const openWazeRouteButton = document.getElementById('openWazeRouteButton');
const mapRefreshButton = document.getElementById('mapRefreshButton');
const coordRubrosBody = document.getElementById('coordRubrosBody');
const coordProsBody = document.getElementById('coordProsBody');
const coordMovementsList = document.getElementById('coordMovementsList');
const coordAuditList = document.getElementById('coordAuditList');
const coordAdminModeHint = document.getElementById('coordAdminModeHint');
const coordAdminUsersBody = document.getElementById('coordAdminUsersBody');
const coordAdminFeedback = document.getElementById('coordAdminFeedback');
const coordMiniBacklog = document.getElementById('coordMiniBacklog');
const coordMiniHotRubro = document.getElementById('coordMiniHotRubro');
const coordMiniHealth = document.getElementById('coordMiniHealth');
const coordMiniDemand = document.getElementById('coordMiniDemand');
const coordMiniAlerts = document.getElementById('coordMiniAlerts');
const colorSchemeMedia = window.matchMedia('(prefers-color-scheme: dark)');

let cachedRubros = [];
let cachedServicios = [];
let cachedClientes = [];
let cachedProfesionales = [];
let cachedRequests = [];
let currentSession = null;
let currentSector = '';
let lastRegisteredClientId = 0;
let lastRegisteredProfessionalId = 0;
let pendingProfessionalUserId = 0;
let pendingPaymentId = 0;
let mercadoPagoCheckoutUrl = '';
let mercadoPagoPreferenceId = '';
let paymentApproved = false;
let selectedChatRequestId = 0;
let notificationPollTimer = 0;
let coordinationPollTimer = 0;
let cachedCoordinationDashboard = null;
let notificationsInitialized = false;
let serviceMap = null;
let serviceMapLayer = null;
let serviceRouteLayer = null;
let currentDeviceLocation = null;
let currentVisibleMapItems = [];
let selectedRouteItem = null;
let mapLocationWatchId = null;
const MAP_DEVICE_LOCATION_KEY = 'appservicios-current-location';
const mapGeoCache = new Map();
const shownNotificationIds = new Set();
const shownCoordinationGoalKeys = new Set();
const nativeFetch = window.fetch.bind(window);

function getStoredAccessToken() {
  if (currentSession?.accessToken) {
    return currentSession.accessToken;
  }

  try {
    const raw = localStorage.getItem(SESSION_KEY);
    if (!raw) return '';
    const saved = JSON.parse(raw);
    return saved?.accessToken || '';
  } catch {
    return '';
  }
}

window.fetch = (input, init = {}) => {
  const url = typeof input === 'string' ? input : input?.url || '';
  const token = getStoredAccessToken();
  const headers = new Headers(init?.headers || {});
  const isApiCall = typeof url === 'string' && (url.startsWith('/api/') || url.startsWith('/health'));

  if (token && isApiCall && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  return nativeFetch(input, { ...init, headers });
};

function persistCurrentDeviceLocation() {
  try {
    if (currentDeviceLocation && isValidMapCoordinate(currentDeviceLocation.lat, currentDeviceLocation.lng)) {
      localStorage.setItem(MAP_DEVICE_LOCATION_KEY, JSON.stringify({
        lat: Number(currentDeviceLocation.lat),
        lng: Number(currentDeviceLocation.lng),
        label: currentDeviceLocation.label || '',
        fullLabel: currentDeviceLocation.fullLabel || '',
        source: currentDeviceLocation.source || 'device',
        updatedAt: currentDeviceLocation.updatedAt || new Date().toISOString()
      }));
      return;
    }

    localStorage.removeItem(MAP_DEVICE_LOCATION_KEY);
  } catch {
    // Ignorar persistencia si el entorno la bloquea.
  }
}

function hydrateCurrentDeviceLocation() {
  try {
    const raw = localStorage.getItem(MAP_DEVICE_LOCATION_KEY);
    if (!raw) return;

    const parsed = JSON.parse(raw);
    const lat = Number(parsed?.lat);
    const lng = Number(parsed?.lng);
    if (!isValidMapCoordinate(lat, lng)) return;

    currentDeviceLocation = {
      lat,
      lng,
      label: parsed?.label || `Lat ${lat.toFixed(4)}, Lon ${lng.toFixed(4)}`,
      fullLabel: parsed?.fullLabel || parsed?.label || 'Última ubicación conocida',
      source: parsed?.source || 'cache',
      updatedAt: parsed?.updatedAt || ''
    };
  } catch {
    currentDeviceLocation = null;
  }
}

function describeLocationSource(source) {
  switch (source) {
    case 'capacitor':
    case 'capacitor-watch':
      return 'GPS del dispositivo';
    case 'browser':
    case 'browser-watch':
      return 'navegador';
    case 'cache':
      return 'última ubicación guardada';
    default:
      return 'ubicación detectada';
  }
}

function updateCurrentLocationHint(location, labelOverride = '') {
  if (!mapDistanceHint || !location || !isValidMapCoordinate(location.lat, location.lng)) {
    return;
  }

  const label = labelOverride || location.label || `Lat ${Number(location.lat).toFixed(4)}, Lon ${Number(location.lng).toFixed(4)}`;
  const sourceLabel = describeLocationSource(location.source);
  mapDistanceHint.textContent = `Ubicación actual detectada: ${label} (${sourceLabel}, ${Number(location.lat).toFixed(4)}, ${Number(location.lng).toFixed(4)}).`;
}

currentYear.textContent = new Date().getFullYear();
hydrateMapGeoCache();
hydrateCurrentDeviceLocation();

function resolveTheme(theme) {
  if (theme === 'system') {
    return colorSchemeMedia.matches ? 'dark' : 'light';
  }

  return theme;
}

function toLabel(theme) {
  switch (theme) {
    case 'light': return 'Claro';
    case 'dark': return 'Oscuro';
    default: return 'Sistema';
  }
}

function applyTheme(theme) {
  const resolved = resolveTheme(theme);
  root.dataset.theme = resolved;
  themeLabel.textContent = `${toLabel(theme)} · ${resolved === 'dark' ? 'Neón elegante' : 'Luz nítida'}`;

  themeButtons.forEach((button) => {
    const isActive = button.dataset.themeChoice === theme;
    button.classList.toggle('is-active', isActive);
    button.setAttribute('aria-pressed', String(isActive));
  });
}

function setTheme(theme) {
  localStorage.setItem(THEME_KEY, theme);
  applyTheme(theme);
}

themeButtons.forEach((button) => {
  button.addEventListener('click', () => setTheme(button.dataset.themeChoice || 'system'));
});

const savedTheme = localStorage.getItem(THEME_KEY) || 'system';
applyTheme(savedTheme);

if (typeof colorSchemeMedia.addEventListener === 'function') {
  colorSchemeMedia.addEventListener('change', () => {
    if ((localStorage.getItem(THEME_KEY) || 'system') === 'system') {
      applyTheme('system');
    }
  });
}

function setOfflineState() {
  healthStatus.textContent = 'Sin conexión';
  apiState.textContent = 'Offline';
  rubroCount.textContent = '0';
  serviceCount.textContent = '0';
  proCount.textContent = '0';
  rubrosContainer.innerHTML = '<span class="chip">No se pudo conectar con la API</span>';

  if (servicePicker) {
    servicePicker.innerHTML = '<option value="">Sin datos disponibles</option>';
  }

  if (requestServiceSelect) {
    requestServiceSelect.innerHTML = '<option value="">Sin servicios disponibles</option>';
  }

  if (requestClientSelect) {
    requestClientSelect.innerHTML = '<option value="">Sin clientes disponibles</option>';
  }

  if (requestState) {
    requestState.textContent = 'Sin conexión';
  }

  if (requestFeedback) {
    requestFeedback.textContent = 'No se pudo conectar con la API.';
  }

  if (mapStatus) {
    mapStatus.textContent = 'Sin conexión';
  }

  if (mapSummary) {
    mapSummary.textContent = 'No se pudieron cargar las ubicaciones del mapa.';
  }

  if (mapLegendList) {
    mapLegendList.innerHTML = `
      <div class="request-item">
        <strong>Mapa fuera de línea</strong>
        <small>No pudimos obtener las ubicaciones desde la API.</small>
      </div>`;
  }
}

function formatCurrency(value) {
  let currency = currentCurrency || 'ARS';
  let locales = 'es-AR';
  if (currency === 'USD') locales = 'en-US';
  if (currency === 'EUR') locales = 'es-ES';
  return new Intl.NumberFormat(locales, {
    style: 'currency',
    currency,
    maximumFractionDigits: 0
  }).format(value);
}

// Hook para actualizar la UI de pagos (placeholder, puedes expandirlo)
function updatePaymentCurrency() {
  // Ejemplo: actualizar textos de pago profesional
  const paymentLabels = document.querySelectorAll('.payment-header strong, .payment-summary strong');
  paymentLabels.forEach(el => {
    if (el.textContent && el.textContent.match(/ARS|USD|EUR/)) {
      el.textContent = formatCurrency(2500);
    }
  });
  // También podrías actualizar otros montos fijos aquí
}

function formatCompactNumber(value) {
  return new Intl.NumberFormat('es-AR', {
    notation: 'compact',
    maximumFractionDigits: 1
  }).format(Number(value || 0));
}

function formatPercent(value) {
  return `${Number(value || 0).toFixed(1)}%`;
}

function setDonutValue(element, value, label) {
  if (!element) return;

  const safeValue = Math.max(0, Math.min(100, Number(value || 0)));
  element.style.setProperty('--value', safeValue.toFixed(1));

  const text = element.querySelector('span');
  if (text) {
    text.textContent = label || formatPercent(safeValue);
  }
}

function normalizeText(value) {
  return String(value || '')
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim();
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

async function extractApiError(response) {
  try {
    const contentType = response.headers.get('content-type') || '';
    hydrateMapGeoCache();
    hydrateCurrentDeviceLocation();

  // --- INICIO: Geolocalización automática y fallback por IP ---
  async function detectAndApplyInitialLocation() {
    // 1. Intentar obtener ubicación por navegador/Capacitor
    try {
      const position = await getCurrentMapPosition();
      await applyDetectedPosition(position, { render: true, resolveLabel: true });
      await startWatchingCurrentLocationForMap();
      return;
    } catch (geoError) {
      // 2. Si falla, intentar obtener por IP
      try {
        if (mapStatus) mapStatus.textContent = 'Buscando ubicación por IP...';
        const resp = await fetch('https://ipapi.co/json');
        if (!resp.ok) throw new Error('IP geolocation failed');
        const data = await resp.json();
        if (isValidMapCoordinate(data.latitude, data.longitude)) {
          await applyDetectedPosition({ lat: data.latitude, lng: data.longitude, source: 'ip' }, { render: true, resolveLabel: true });
          if (mapStatus) mapStatus.textContent = 'Ubicación aproximada por IP';
          return;
        }
      } catch (ipError) {
        // 3. Si ambos fallan, centrar mapa global
        if (mapStatus) mapStatus.textContent = 'No se pudo detectar ubicación, mostrando mapa global.';
        const mapInstance = ensureServiceMap();
        if (mapInstance) {
          mapInstance.setView([20, 0], 2); // Zoom global
          window.setTimeout(() => mapInstance.invalidateSize(), 120);
        }
      }
    }
  }
  // --- FIN: Geolocalización automática y fallback por IP ---

  // Lanzar la detección automática al cargar la app (después de hidratar cachés)
  window.addEventListener('DOMContentLoaded', () => {
    detectAndApplyInitialLocation();
  });
    if (contentType.includes('application/json')) {
      const body = await response.json();

      if (body?.errors) {
        return Object.values(body.errors).flat().join(' ');
      }

      return body?.title || body?.message || JSON.stringify(body);
    }

    const text = await response.text();
    return text || `Error ${response.status}`;
  } catch {
    return `Error ${response.status}`;
  }
}

function hydrateMapGeoCache() {
  try {
    const raw = localStorage.getItem('appservicios-map-geocache');
    if (!raw) return;

    const parsed = JSON.parse(raw);
    Object.entries(parsed).forEach(([key, value]) => {
      if (value && Number.isFinite(Number(value.lat)) && Number.isFinite(Number(value.lng))) {
        mapGeoCache.set(key, { lat: Number(value.lat), lng: Number(value.lng) });
      }
    });
  } catch {
    // Se ignora caché corrupta o inaccesible.
  }
}

function persistMapGeoCache() {
  try {
    const snapshot = {};
    mapGeoCache.forEach((value, key) => {
      if (value) {
        snapshot[key] = value;
      }
    });
    localStorage.setItem('appservicios-map-geocache', JSON.stringify(snapshot));
  } catch {
    // Puede fallar en modo privado o por cuota.
  }
}

function isValidMapCoordinate(lat, lng) {
  const parsedLat = Number(lat);
  const parsedLng = Number(lng);

  return Number.isFinite(parsedLat)
    && Number.isFinite(parsedLng)
    && Math.abs(parsedLat) > 0.001
    && Math.abs(parsedLng) > 0.001
    && parsedLat >= -90
    && parsedLat <= 90
    && parsedLng >= -180
    && parsedLng <= 180;
}

function normalizeLocationForMap(location) {
  const cleaned = String(location || '').trim();
  if (!cleaned) return '';
  return /argentina/i.test(cleaned) ? cleaned : `${cleaned}, Argentina`;
}

function formatDistanceKm(distanceKm) {
  const value = Number(distanceKm);
  if (!Number.isFinite(value)) return 'distancia no disponible';
  if (value < 1) return `${Math.round(value * 1000)} m`;
  return `${value.toFixed(value < 10 ? 1 : 0)} km`;
}

function buildDistanceFinanceSummary(item) {
  const parts = [];
  const distance = Number(item?.distanciaKm);
  const traslado = Number(item?.costoTraslado);
  const incentivo = Number(item?.incentivo);

  if (Number.isFinite(distance) && distance > 0) {
    parts.push(`Distancia: ${formatDistanceKm(distance)}`);
  }

  if (Number.isFinite(traslado) && traslado > 0) {
    parts.push(`Traslado: ${formatCurrency(traslado)}`);
  }

  if (Number.isFinite(incentivo) && incentivo > 0) {
    parts.push(`Bono extra: ${formatCurrency(incentivo)}`);
  }

  return parts.join(' · ');
}

function calculateDistanceKm(lat1, lng1, lat2, lng2) {
  if (!isValidMapCoordinate(lat1, lng1) || !isValidMapCoordinate(lat2, lng2)) {
    return Number.NaN;
  }

  const toRad = (value) => (Number(value) * Math.PI) / 180;
  const earthRadiusKm = 6371;
  const dLat = toRad(Number(lat2) - Number(lat1));
  const dLng = toRad(Number(lng2) - Number(lng1));
  const originLat = toRad(lat1);
  const targetLat = toRad(lat2);

  const a = Math.sin(dLat / 2) ** 2
    + Math.cos(originLat) * Math.cos(targetLat) * Math.sin(dLng / 2) ** 2;

  return earthRadiusKm * (2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a)));
}

async function reverseGeocodeCoordinates(lat, lng) {
  try {
    const endpoint = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${encodeURIComponent(lat)}&lon=${encodeURIComponent(lng)}`;
    const response = await nativeFetch(endpoint, {
      headers: {
        'Accept': 'application/json',
        'Accept-Language': 'es-AR'
      }
    });

    if (!response.ok) {
      throw new Error(`Reverse geocoding ${response.status}`);
    }

    const result = await response.json();
    const address = result?.address || {};
    const shortLabel = [
      address.suburb || address.neighbourhood || address.city_district,
      address.city || address.town || address.village || address.state
    ].filter(Boolean).join(', ') || result?.display_name || `Lat ${Number(lat).toFixed(4)}, Lon ${Number(lng).toFixed(4)}`;

    mapGeoCache.set(normalizeLocationForMap(shortLabel), { lat: Number(lat), lng: Number(lng) });
    persistMapGeoCache();

    return {
      shortLabel,
      fullLabel: result?.display_name || shortLabel
    };
  } catch (error) {
    console.warn('No se pudo resolver el nombre de la ubicación actual.', error);
    return {
      shortLabel: `Lat ${Number(lat).toFixed(4)}, Lon ${Number(lng).toFixed(4)}`,
      fullLabel: 'Tu ubicación actual'
    };
  }
}

function applyCurrentLocationToInputs(label) {
  const safeLabel = String(label || 'Tu ubicación actual').trim();

  if (requestLocationInput) {
    const currentValue = requestLocationInput.value.trim();
    if (!currentValue || /buenos aires/i.test(currentValue)) {
      requestLocationInput.value = safeLabel;
    }
  }

  if (registerLocationInput) {
    const currentValue = registerLocationInput.value.trim();
    if (!currentValue || /caba|buenos aires/i.test(currentValue)) {
      registerLocationInput.value = safeLabel;
      registerLocationInput.dispatchEvent(new Event('input', { bubbles: true }));
    }
  }
}

async function getCurrentMapPosition() {
  const capacitorBridge = window.Capacitor;
  const capacitorGeolocation = capacitorBridge?.Plugins?.Geolocation;
  const isNativePlatform = typeof capacitorBridge?.isNativePlatform === 'function' && capacitorBridge.isNativePlatform();

  if (isNativePlatform && capacitorGeolocation) {
    const currentPermissions = await capacitorGeolocation.checkPermissions().catch(() => null);
    const isGranted = currentPermissions?.location === 'granted' || currentPermissions?.coarseLocation === 'granted';

    if (!isGranted) {
      const requestedPermissions = await capacitorGeolocation.requestPermissions();
      const requestedGranted = requestedPermissions?.location === 'granted' || requestedPermissions?.coarseLocation === 'granted';

      if (!requestedGranted) {
        throw new Error('La app no tiene permiso de ubicación en Android.');
      }
    }

    const position = await capacitorGeolocation.getCurrentPosition({
      enableHighAccuracy: true,
      timeout: 15000,
      maximumAge: 0
    });

    return {
      lat: Number(position.coords.latitude),
      lng: Number(position.coords.longitude),
      source: 'capacitor'
    };
  }

  if (!('geolocation' in navigator)) {
    throw new Error('Este dispositivo no soporta ubicación automática.');
  }

  const position = await new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(resolve, reject, {
      enableHighAccuracy: true,
      timeout: 15000,
      maximumAge: 0
    });
  });

  return {
    lat: Number(position.coords.latitude),
    lng: Number(position.coords.longitude),
    source: 'browser'
  };
}

async function applyDetectedPosition(position, options = {}) {
  const { render = true, resolveLabel = true } = options;
  const lat = Number(position?.lat);
  const lng = Number(position?.lng);

  if (!isValidMapCoordinate(lat, lng)) {
    throw new Error('La ubicación recibida no es válida.');
  }

  currentDeviceLocation = {
    lat,
    lng,
    label: currentDeviceLocation?.label || `Lat ${lat.toFixed(4)}, Lon ${lng.toFixed(4)}`,
    fullLabel: currentDeviceLocation?.fullLabel || 'Tu ubicación actual',
    source: position?.source || currentDeviceLocation?.source || 'device',
    updatedAt: new Date().toISOString()
  };

  persistCurrentDeviceLocation();
  applyCurrentLocationToInputs(currentDeviceLocation.label);
  updateCurrentLocationHint(currentDeviceLocation);

  if (mapStatus) {
    mapStatus.textContent = 'Ubicación detectada';
  }

  const mapInstance = ensureServiceMap();
  if (mapInstance) {
    mapInstance.setView([lat, lng], 13);
    window.setTimeout(() => mapInstance.invalidateSize(), 80);
  }

  if (render) {
    await renderServiceMap();
  }

  if (!resolveLabel) {
    return;
  }

  const reverse = await reverseGeocodeCoordinates(lat, lng);
  currentDeviceLocation = {
    ...currentDeviceLocation,
    label: reverse.shortLabel,
    fullLabel: reverse.fullLabel,
    updatedAt: new Date().toISOString()
  };

  persistCurrentDeviceLocation();
  applyCurrentLocationToInputs(reverse.shortLabel);
  updateCurrentLocationHint(currentDeviceLocation, reverse.shortLabel);

  if (render) {
    await renderServiceMap();
  }
}

async function startWatchingCurrentLocationForMap() {
  if (mapLocationWatchId) {
    return mapLocationWatchId;
  }

  const onLocationUpdate = (coords, source) => {
    const lat = Number(coords?.latitude ?? coords?.lat);
    const lng = Number(coords?.longitude ?? coords?.lng);
    if (!isValidMapCoordinate(lat, lng)) {
      return;
    }

    const previous = currentDeviceLocation;
    const movedEnough = !previous
      || Math.abs(Number(previous.lat) - lat) > 0.0005
      || Math.abs(Number(previous.lng) - lng) > 0.0005;

    if (!movedEnough) {
      return;
    }

    applyDetectedPosition({ lat, lng, source }, { render: true, resolveLabel: false }).catch((error) => {
      console.warn('No se pudo refrescar la ubicación en vivo.', error);
    });
  };

  const capacitorBridge = window.Capacitor;
  const capacitorGeolocation = capacitorBridge?.Plugins?.Geolocation;
  const isNativePlatform = typeof capacitorBridge?.isNativePlatform === 'function' && capacitorBridge.isNativePlatform();

  if (isNativePlatform && capacitorGeolocation?.watchPosition) {
    mapLocationWatchId = await capacitorGeolocation.watchPosition({
      enableHighAccuracy: true,
      timeout: 10000,
      maximumAge: 0
    }, (position, err) => {
      if (err) {
        console.warn('Seguimiento GPS no disponible temporalmente.', err);
        return;
      }

      if (position?.coords) {
        onLocationUpdate(position.coords, 'capacitor-watch');
      }
    });

    return mapLocationWatchId;
  }

  if ('geolocation' in navigator) {
    mapLocationWatchId = navigator.geolocation.watchPosition((position) => {
      onLocationUpdate(position.coords, 'browser-watch');
    }, (error) => {
      console.warn('Seguimiento del navegador no disponible.', error);
    }, {
      enableHighAccuracy: true,
      timeout: 10000,
      maximumAge: 0
    });
  }

  return mapLocationWatchId;
}

async function requestCurrentLocationForMap(options = {}) {
  const { silent = false, autoWatch = true } = options;
  const originalButtonText = useCurrentLocationButton?.textContent || 'Usar mi ubicación actual';
  if (useCurrentLocationButton) {
    useCurrentLocationButton.disabled = true;
    useCurrentLocationButton.classList.add('is-disabled');
    useCurrentLocationButton.textContent = 'Ubicando...';
  }

  if (mapStatus) {
    mapStatus.textContent = 'Buscando tu ubicación...';
  }

  try {
    const position = await getCurrentMapPosition();
    await applyDetectedPosition(position, { render: true, resolveLabel: true });

    if (autoWatch) {
      await startWatchingCurrentLocationForMap();
    }
  } catch (error) {
    if (mapStatus) {
      mapStatus.textContent = currentDeviceLocation ? 'Usando última ubicación conocida' : 'Ubicación no disponible';
    }

    if (mapDistanceHint) {
      mapDistanceHint.textContent = currentDeviceLocation
        ? `No pudimos refrescar el GPS ahora. Seguimos mostrando ${currentDeviceLocation.label || 'la última ubicación conocida'}.`
        : 'No pudimos obtener tu ubicación actual. Revisa los permisos del navegador o del teléfono.';
    }

    if (!silent) {
      throw error;
    }
  } finally {
    if (useCurrentLocationButton) {
      useCurrentLocationButton.disabled = false;
      useCurrentLocationButton.classList.remove('is-disabled');
      useCurrentLocationButton.textContent = originalButtonText;
    }
  }
}

async function geocodeLocation(location) {
  const query = normalizeLocationForMap(location);
  if (!query) return null;

  if (mapGeoCache.has(query)) {
    return mapGeoCache.get(query);
  }

  try {
    const endpoint = `https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&countrycodes=ar&q=${encodeURIComponent(query)}`;
    const response = await nativeFetch(endpoint, {
      headers: {
        'Accept': 'application/json',
        'Accept-Language': 'es-AR'
      }
    });

    if (!response.ok) {
      throw new Error(`Geocoding ${response.status}`);
    }

    const results = await response.json();
    const first = Array.isArray(results) ? results[0] : null;

    if (!first) {
      return null;
    }

    const coords = {
      lat: Number(first.lat),
      lng: Number(first.lon)
    };

    if (isValidMapCoordinate(coords.lat, coords.lng)) {
      mapGeoCache.set(query, coords);
      persistMapGeoCache();
      return coords;
    }
  } catch (error) {
    console.warn('No se pudo geocodificar la ubicación para el mapa.', error);
  }

  return null;
}

async function resolveCoordinatesForPayload(location, lat = 0, lng = 0) {
  if (isValidMapCoordinate(lat, lng)) {
    return { lat: Number(lat), lng: Number(lng), source: 'direct' };
  }

  const geocoded = await geocodeLocation(location);
  if (geocoded && isValidMapCoordinate(geocoded.lat, geocoded.lng)) {
    return { ...geocoded, source: 'geocoded' };
  }

  return { lat: 0, lng: 0, source: 'none' };
}

function getMapMarkerMeta(type) {
  switch (type) {
    case 'cliente':
      return { color: '#b86bff', label: 'Cliente', emoji: '🙋' };
    case 'solicitud':
      return { color: '#9eff8a', label: 'Solicitud', emoji: '🧰' };
    default:
      return { color: '#6ef2ff', label: 'Profesional', emoji: '👷' };
  }
}

function buildMapItems() {
  const filter = mapAudienceFilter?.value || 'todos';
  const items = [];

  if (filter === 'todos' || filter === 'profesionales') {
    cachedProfesionales.slice(0, 18).forEach((item) => {
      items.push({
        type: 'profesional',
        id: Number(item.id || 0),
        title: item.usuarioNombre || `Profesional #${item.id || '?'}`,
        subtitle: Array.isArray(item.rubros) && item.rubros.length > 0
          ? item.rubros.join(', ')
          : 'Perfil profesional activo',
        detail: `${formatCurrency(item.tarifaBase || 0)} · ${item.ubicacion || 'Sin ubicación'}`,
        location: item.ubicacion || '',
        latitud: Number(item.latitud || 0),
        longitud: Number(item.longitud || 0)
      });
    });
  }

  if (filter === 'todos' || filter === 'clientes') {
    cachedClientes.slice(0, 18).forEach((item) => {
      items.push({
        type: 'cliente',
        id: Number(item.id || 0),
        title: item.usuarioNombre || `Cliente #${item.id || '?'}`,
        subtitle: item.preferencias || 'Cliente buscando atención rápida',
        detail: item.ubicacion || 'Sin ubicación',
        location: item.ubicacion || '',
        latitud: Number(item.latitud || 0),
        longitud: Number(item.longitud || 0)
      });
    });
  }

  if (filter === 'todos' || filter === 'solicitudes') {
    cachedRequests.slice(0, 18).forEach((item) => {
      items.push({
        type: 'solicitud',
        id: Number(item.id || 0),
        title: formatRequestTitle(item),
        subtitle: `${item.clienteNombre || 'Cliente'} · ${item.estado || 'Pendiente'}`,
        detail: `${formatCurrency(item.presupuestoEstimado || 0)} · ${item.ubicacion || 'Sin ubicación'}`,
        location: item.ubicacion || '',
        latitud: Number(item.latitud || 0),
        longitud: Number(item.longitud || 0)
      });
    });
  }

  return items.slice(0, 36);
}

function setRouteLinkState(anchor, enabled, href = '#') {
  if (!anchor) return;

  anchor.href = enabled ? href : '#';
  anchor.classList.toggle('is-disabled', !enabled);
  anchor.classList.toggle('payment-link-disabled', !enabled);
  anchor.setAttribute('aria-disabled', String(!enabled));
}

function syncRouteActions(target) {
  const hasUsableTarget = !!(currentDeviceLocation && target && isValidMapCoordinate(target.lat, target.lng));

  if (routeNearestButton) {
    routeNearestButton.disabled = !hasUsableTarget;
    routeNearestButton.classList.toggle('is-disabled', !hasUsableTarget);
  }

  const googleHref = hasUsableTarget
    ? `https://www.google.com/maps/dir/?api=1&origin=${encodeURIComponent(`${currentDeviceLocation.lat},${currentDeviceLocation.lng}`)}&destination=${encodeURIComponent(`${target.lat},${target.lng}`)}&travelmode=driving`
    : '#';

  const wazeHref = hasUsableTarget
    ? `https://waze.com/ul?ll=${encodeURIComponent(`${target.lat},${target.lng}`)}&navigate=yes`
    : '#';

  setRouteLinkState(openGoogleMapsRouteButton, hasUsableTarget, googleHref);
  setRouteLinkState(openWazeRouteButton, hasUsableTarget, wazeHref);
}

function renderRouteSummary(target, routeData = null) {
  if (!routeSummary) return;

  if (!currentDeviceLocation) {
    routeSummary.innerHTML = `
      <div class="request-item">
        <strong>Ubicación actual pendiente</strong>
        <small>Activa tu ubicación para calcular distancia, tiempo estimado y abrir la navegación.</small>
      </div>`;
    if (routeStatus) {
      routeStatus.textContent = 'Necesita tu ubicación';
    }
    syncRouteActions(null);
    return;
  }

  if (!target) {
    routeSummary.innerHTML = `
      <div class="request-item">
        <strong>Sin destino cercano</strong>
        <small>No hay puntos visibles dentro del filtro actual para trazar una ruta.</small>
      </div>`;
    if (routeStatus) {
      routeStatus.textContent = 'Sin destino';
    }
    syncRouteActions(null);
    return;
  }

  const directDistance = Number.isFinite(target.distanceKm)
    ? formatDistanceKm(target.distanceKm)
    : 'distancia no disponible';
  const routeDistance = Number.isFinite(routeData?.distanceKm)
    ? formatDistanceKm(routeData.distanceKm)
    : directDistance;
  const eta = Number.isFinite(routeData?.durationMin)
    ? `${Math.max(1, Math.round(routeData.durationMin))} min aprox.`
    : 'ETA pendiente';

  routeSummary.innerHTML = `
    <div class="request-item">
      <strong>${escapeHtml(target.title)}</strong>
      <small>${escapeHtml(target.subtitle)}</small>
      <small>${escapeHtml(target.detail)}</small>
      <small>Distancia: ${routeDistance}</small>
      <small>Tiempo estimado: ${eta}</small>
    </div>`;

  if (routeStatus) {
    routeStatus.textContent = routeData ? 'Ruta calculada' : 'Destino listo';
  }

  syncRouteActions(target);
}

async function fetchRouteData(origin, target) {
  const osrmUrl = `https://router.project-osrm.org/route/v1/driving/${origin.lng},${origin.lat};${target.lng},${target.lat}?overview=full&geometries=geojson`;

  try {
    const response = await nativeFetch(osrmUrl, {
      headers: { 'Accept': 'application/json' }
    });

    if (!response.ok) {
      throw new Error(`Routing ${response.status}`);
    }

    const payload = await response.json();
    const route = Array.isArray(payload?.routes) ? payload.routes[0] : null;

    if (!route?.geometry?.coordinates?.length) {
      throw new Error('Ruta vacía');
    }

    return {
      coordinates: route.geometry.coordinates.map(([lng, lat]) => [lat, lng]),
      distanceKm: Number(route.distance || 0) / 1000,
      durationMin: Number(route.duration || 0) / 60,
      source: 'osrm'
    };
  } catch (error) {
    console.warn('No se pudo calcular la ruta real, se usará una línea directa.', error);
    return {
      coordinates: [[origin.lat, origin.lng], [target.lat, target.lng]],
      distanceKm: calculateDistanceKm(origin.lat, origin.lng, target.lat, target.lng),
      durationMin: calculateDistanceKm(origin.lat, origin.lng, target.lat, target.lng) / 35 * 60,
      source: 'fallback'
    };
  }
}

async function previewRouteToItem(target) {
  if (!currentDeviceLocation || !target) {
    renderRouteSummary(target, null);
    return;
  }

  const mapInstance = ensureServiceMap();
  if (!mapInstance) return;

  if (!serviceRouteLayer) {
    serviceRouteLayer = window.L.layerGroup().addTo(mapInstance);
  }

  serviceRouteLayer.clearLayers();
  selectedRouteItem = target;

  if (routeStatus) {
    routeStatus.textContent = 'Calculando ruta...';
  }

  const routeData = await fetchRouteData(currentDeviceLocation, target);
  const polyline = window.L.polyline(routeData.coordinates, {
    color: '#f59e0b',
    weight: 4,
    opacity: 0.9,
    dashArray: routeData.source === 'fallback' ? '8 8' : undefined
  });

  polyline.addTo(serviceRouteLayer);
  mapInstance.fitBounds(polyline.getBounds(), { padding: [28, 28] });
  renderRouteSummary(target, routeData);
}

function getNearestVisibleItem() {
  if (!Array.isArray(currentVisibleMapItems) || currentVisibleMapItems.length === 0) {
    return null;
  }

  const withDistance = currentVisibleMapItems.filter((item) => Number.isFinite(item.distanceKm));
  return withDistance[0] || currentVisibleMapItems[0] || null;
}

function renderMapLegend(items) {
  if (!mapLegendList) return;

  if (!Array.isArray(items) || items.length === 0) {
    mapLegendList.innerHTML = `
      <div class="request-item">
        <strong>Sin ubicaciones listas</strong>
        <small>Carga clientes, profesionales o solicitudes con ubicación para verlos aquí.</small>
      </div>`;
    return;
  }

  mapLegendList.innerHTML = '';
  const visibleItems = items.slice(0, 12);

  visibleItems.forEach((item) => {
    const meta = getMapMarkerMeta(item.type);
    const distanceLine = Number.isFinite(item.distanceKm)
      ? `<small>A ${formatDistanceKm(item.distanceKm)} de tu ubicación</small>`
      : '';

    const card = document.createElement('div');
    card.className = 'map-item';
    card.innerHTML = `
      <strong>${meta.emoji} ${escapeHtml(item.title)}</strong>
      <small>${escapeHtml(item.subtitle)}</small>
      <small>${escapeHtml(item.detail)}</small>
      ${distanceLine}
      <small>${item.source === 'geocoded' ? 'Ubicación aproximada por dirección cargada' : 'Ubicación registrada'}</small>`;
    mapLegendList.appendChild(card);
  });

  if (items.length > visibleItems.length) {
    const extra = document.createElement('small');
    extra.className = 'mini-note';
    extra.textContent = `Mostrando ${visibleItems.length} de ${items.length} puntos disponibles.`;
    mapLegendList.appendChild(extra);
  }
}

function ensureServiceMap() {
  const mapElement = document.getElementById('serviceMap');
  if (!mapElement || !window.L) {
    return null;
  }

  if (!serviceMap) {
    serviceMap = window.L.map(mapElement, {
      scrollWheelZoom: false,
      attributionControl: true
    }).setView([-34.6037, -58.3816], 10);

    window.L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(serviceMap);

    serviceMapLayer = window.L.layerGroup().addTo(serviceMap);
    serviceRouteLayer = window.L.layerGroup().addTo(serviceMap);
  }

  return serviceMap;
}

async function renderServiceMap() {
  if (!mapStatus) return;

  const mapInstance = ensureServiceMap();
  if (!mapInstance) {
    mapStatus.textContent = 'Mapa no disponible';
    if (mapSummary) {
      mapSummary.textContent = 'No se pudo inicializar el mapa en este navegador.';
    }
    return;
  }

  const radiusKm = Number(mapRadiusFilter?.value || 0);
  const items = buildMapItems();
  if (items.length === 0) {
    if (serviceMapLayer) {
      serviceMapLayer.clearLayers();
    }
    mapStatus.textContent = 'Sin datos';
    if (mapSummary) {
      mapSummary.textContent = 'Todavía no hay suficientes ubicaciones cargadas para mostrar.';
    }
    renderMapLegend([]);
    return;
  }

  mapStatus.textContent = 'Ubicando puntos...';

  const resolvedItems = [];
  for (const item of items) {
    const coords = await resolveCoordinatesForPayload(item.location, item.latitud, item.longitud);
    if (isValidMapCoordinate(coords.lat, coords.lng)) {
      const distanceKm = currentDeviceLocation
        ? calculateDistanceKm(currentDeviceLocation.lat, currentDeviceLocation.lng, coords.lat, coords.lng)
        : Number.NaN;

      resolvedItems.push({
        ...item,
        lat: coords.lat,
        lng: coords.lng,
        source: coords.source,
        distanceKm
      });
    }
  }

  let visibleItems = [...resolvedItems];

  if (currentDeviceLocation) {
    visibleItems.sort((left, right) => {
      const leftDistance = Number.isFinite(left.distanceKm) ? left.distanceKm : Number.MAX_SAFE_INTEGER;
      const rightDistance = Number.isFinite(right.distanceKm) ? right.distanceKm : Number.MAX_SAFE_INTEGER;
      return leftDistance - rightDistance;
    });
  }

  if (radiusKm > 0 && currentDeviceLocation) {
    visibleItems = visibleItems.filter((item) => Number.isFinite(item.distanceKm) && item.distanceKm <= radiusKm);
  }

  if (serviceMapLayer) {
    serviceMapLayer.clearLayers();
  }

  if (serviceRouteLayer) {
    serviceRouteLayer.clearLayers();
  }

  const bounds = [];

  if (currentDeviceLocation) {
    const userMarker = window.L.circleMarker([currentDeviceLocation.lat, currentDeviceLocation.lng], {
      radius: 10,
      color: '#f59e0b',
      weight: 2,
      fillColor: '#fbbf24',
      fillOpacity: 0.95
    });

    userMarker.bindPopup(`
      <strong>Tu ubicación actual</strong><br>
      <small>${escapeHtml(currentDeviceLocation.label || 'Ubicación detectada')}</small>`);
    userMarker.addTo(serviceMapLayer);
    userMarker.openPopup();
    bounds.push([currentDeviceLocation.lat, currentDeviceLocation.lng]);
  }

  if (visibleItems.length === 0) {
    mapStatus.textContent = radiusKm > 0 ? 'Sin resultados cercanos' : 'Sin coordenadas válidas';

    if (mapSummary) {
      mapSummary.textContent = radiusKm > 0 && currentDeviceLocation
        ? `No encontramos puntos dentro de ${radiusKm} km de tu ubicación actual.`
        : 'Carga una ubicación más precisa para ver puntos reales en el mapa.';
    }

    if (mapDistanceHint) {
      mapDistanceHint.textContent = radiusKm > 0 && !currentDeviceLocation
        ? 'Para aplicar cercanía real primero debes activar tu ubicación actual.'
        : (mapDistanceHint.textContent || 'Activa tu ubicación para ordenar perfiles y solicitudes por cercanía real.');
    }

    renderMapLegend([]);

    if (bounds.length === 1) {
      mapInstance.setView(bounds[0], 12);
    }

    window.setTimeout(() => mapInstance.invalidateSize(), 120);
    return;
  }

  visibleItems.forEach((item) => {
    const meta = getMapMarkerMeta(item.type);
    const distanceLine = Number.isFinite(item.distanceKm)
      ? `<br><small>A ${formatDistanceKm(item.distanceKm)} de tu ubicación</small>`
      : '';

    const popupHtml = `
      <strong>${escapeHtml(item.title)}</strong><br>
      <small>${escapeHtml(item.subtitle)}</small><br>
      <small>${escapeHtml(item.detail)}</small>${distanceLine}<br>
      <small>${item.source === 'geocoded' ? 'Ubicación aproximada por dirección cargada' : 'Ubicación registrada en la app'}</small>`;

    const marker = window.L.circleMarker([item.lat, item.lng], {
      radius: 8,
      color: meta.color,
      weight: 2,
      fillColor: meta.color,
      fillOpacity: 0.8
    });

    marker.bindPopup(popupHtml);
    marker.on('click', () => {
      previewRouteToItem(item).catch((error) => console.error('No se pudo trazar la ruta al punto seleccionado.', error));
    });
    marker.addTo(serviceMapLayer);
    bounds.push([item.lat, item.lng]);
  });

  const nearestDistanceKm = Number.isFinite(visibleItems[0]?.distanceKm)
    ? Number(visibleItems[0].distanceKm)
    : Number.NaN;

  if (currentDeviceLocation) {
    const viewportRadiusKm = radiusKm > 0 ? radiusKm : 25;
    const focusItems = visibleItems
      .filter((item) => Number.isFinite(item.distanceKm) && item.distanceKm <= viewportRadiusKm)
      .slice(0, 8);

    if (focusItems.length === 0 && Number.isFinite(nearestDistanceKm) && nearestDistanceKm <= 120) {
      focusItems.push(visibleItems[0]);
    }

    const focusBounds = [
      [currentDeviceLocation.lat, currentDeviceLocation.lng],
      ...focusItems.map((item) => [item.lat, item.lng])
    ];

    if (focusBounds.length === 1) {
      mapInstance.setView(focusBounds[0], 13);
    } else {
      mapInstance.fitBounds(focusBounds, { padding: [24, 24], maxZoom: 13 });
    }
  } else if (bounds.length === 1) {
    mapInstance.setView(bounds[0], 12);
  } else {
    mapInstance.fitBounds(bounds, { padding: [24, 24] });
  }

  const counts = visibleItems.reduce((acc, item) => {
    acc[item.type] = (acc[item.type] || 0) + 1;
    return acc;
  }, { profesional: 0, cliente: 0, solicitud: 0 });

  currentVisibleMapItems = visibleItems;
  const nearestItem = getNearestVisibleItem();
  if (!selectedRouteItem || !visibleItems.some((item) => item.type === selectedRouteItem.type && item.id === selectedRouteItem.id)) {
    selectedRouteItem = nearestItem;
  }

  mapStatus.textContent = currentDeviceLocation ? 'Mapa por cercanía' : 'Mapa en vivo';
  if (mapSummary) {
    const nearestLine = currentDeviceLocation && Number.isFinite(visibleItems[0]?.distanceKm)
      ? ` · más cercano a ${formatDistanceKm(visibleItems[0].distanceKm)}`
      : '';

    mapSummary.textContent = `${counts.profesional} profesionales · ${counts.cliente} clientes · ${counts.solicitud} solicitudes ubicadas${nearestLine}`;
  }

  if (mapDistanceHint) {
    if (currentDeviceLocation) {
      const locationLabel = currentDeviceLocation.label || `Lat ${Number(currentDeviceLocation.lat).toFixed(4)}, Lon ${Number(currentDeviceLocation.lng).toFixed(4)}`;
      mapDistanceHint.textContent = `Tu ubicación actual (${locationLabel}) está centrada en el mapa${radiusKm > 0 ? ` y el filtro quedó aplicado hasta ${radiusKm} km.` : '.'}`;
    } else if (radiusKm > 0) {
      mapDistanceHint.textContent = 'Seleccionaste un radio, pero necesitas activar tu ubicación actual para filtrar por cercanía.';
    }
  }

  renderMapLegend(visibleItems);
  renderRouteSummary(selectedRouteItem, null);
  window.setTimeout(() => mapInstance.invalidateSize(), 120);
}

function getCurrentAccountRole() {
  const active = accountRoleButtons.find((button) => button.classList.contains('is-active'));
  return active?.dataset.accountRole || 'cliente';
}

async function fetchSessionContext(userId) {
  const response = await fetch(`/api/Auth/usuarios/${userId}/context`);
  if (!response.ok) {
    throw new Error(await extractApiError(response));
  }

  return response.json();
}

function applySessionUI() {
  if (!sessionSummary) return;

  if (!currentSession) {
    sessionSummary.innerHTML = `
      <strong>Sin sesión activa</strong>
      <small>Inicia sesión para ver tu panel personalizado y trabajar con tu cuenta real.</small>`;

    if (logoutButton) {
      logoutButton.classList.add('is-hidden');
    }

    if (loginFeedback) {
      loginFeedback.classList.remove('is-error', 'is-success');
      loginFeedback.textContent = 'Inicia sesión con una cuenta ya registrada para abrir tu panel personalizado.';
    }

    return;
  }

  const rubros = Array.isArray(currentSession.rubros) && currentSession.rubros.length > 0
    ? currentSession.rubros.join(', ')
    : 'Sin rubros asociados todavía';

  const paymentText = currentSession.rol === 'Profesional'
    ? (currentSession.estadoPago || 'Sin pago registrado')
    : 'No aplica';

  const sessionDetail = currentSession.rol === 'Profesional'
    ? `Pago: ${paymentText} · Rubros: ${rubros}`
    : currentSession.rol === 'Administrador'
      ? 'Panel ejecutivo, auditoría operativa y reportes financieros habilitados.'
      : 'Sesión lista para publicar y seguir solicitudes.';

  sessionSummary.innerHTML = `
    <strong>${currentSession.nombre}</strong>
    <small>${currentSession.email} · Rol: ${currentSession.rol}</small>
    <small>Ubicación: ${currentSession.ubicacion || 'Sin ubicación cargada'}</small>
    <small>${sessionDetail}</small>`;

  if (logoutButton) {
    logoutButton.classList.remove('is-hidden');
  }

  if (loginFeedback) {
    loginFeedback.classList.remove('is-error');
    loginFeedback.classList.add('is-success');
    loginFeedback.textContent = `Sesión iniciada como ${currentSession.nombre} (${currentSession.rol}) con acceso seguro.`;
  }

  if (currentSession.rol === 'Administrador') {
    setActiveRole('coordinacion');
  }

  if (currentSession.rol === 'Profesional' && paymentState) {
    const approved = !!currentSession.tienePagoAprobado;
    pendingProfessionalUserId = Number(currentSession.usuarioId || 0);
    paymentApproved = approved;
    paymentState.textContent = approved ? 'Pago aprobado' : (currentSession.estadoPago || 'Pendiente de pago');
    paymentState.classList.toggle('is-paid', approved);
  }
}

function saveSession(session) {
  currentSession = session;
  notificationsInitialized = false;
  shownNotificationIds.clear();
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  applySessionUI();
  startNotificationPolling();
  loadNotifications();
  subscribeToPushNotifications().catch((error) => {
    console.warn('No se pudo activar push en este dispositivo:', error);
  });
}

function clearSession() {
  currentSession = null;
  notificationsInitialized = false;
  shownNotificationIds.clear();
  stopNotificationPolling();
  localStorage.removeItem(SESSION_KEY);
  applySessionUI();
  loadNotifications();
  fillRequestSelectors();
  fillProfessionalSelector();
  loadRequests();
  loadProfessionalDashboard();
  loadCoordinationDashboard();
}

async function restoreSavedSession() {
  const raw = localStorage.getItem(SESSION_KEY);
  if (!raw) {
    applySessionUI();
    return;
  }

  try {
    const saved = JSON.parse(raw);
    if (!saved?.usuarioId) {
      clearSession();
      return;
    }

    const fresh = await fetchSessionContext(saved.usuarioId);
    currentSession = fresh;
    notificationsInitialized = false;
    shownNotificationIds.clear();
    localStorage.setItem(SESSION_KEY, JSON.stringify(fresh));
    applySessionUI();
    startNotificationPolling();
    subscribeToPushNotifications().catch((error) => {
      console.warn('No se pudo activar push en este dispositivo:', error);
    });
    fillRequestSelectors();
    fillProfessionalSelector();
    await Promise.all([loadRequests(), loadProfessionalDashboard(), loadNotifications(), loadCoordinationDashboard()]);
  } catch (error) {
    console.error(error);
    clearSession();
  }
}

async function handleLogin() {
  const email = loginEmailInput?.value.trim() || '';
  const password = loginPasswordInput?.value || '';

  if (!email || password.length < 6) {
    if (loginFeedback) {
      loginFeedback.classList.remove('is-success');
      loginFeedback.classList.add('is-error');
      loginFeedback.textContent = 'Ingresa un email válido y una contraseña de al menos 6 caracteres.';
    }
    return;
  }

  if (loginButton) {
    loginButton.disabled = true;
    loginButton.classList.add('is-disabled');
  }

  try {
    const response = await fetch('/api/Auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    const session = await response.json();
    saveSession(session);

    if (session.rol === 'Profesional') {
      setAccountRole('profesional');
      setActiveRole('profesional');
      lastRegisteredProfessionalId = Number(session.profesionalId || 0);
      document.getElementById('dashboard-profesional')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else if (session.rol === 'Administrador') {
      setActiveRole('coordinacion');
      document.getElementById('dashboard-coordinacion-real')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    } else {
      setAccountRole('cliente');
      setActiveRole('cliente');
      lastRegisteredClientId = Number(session.clienteId || 0);
      document.getElementById('dashboard-cliente')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    fillRequestSelectors();
    fillProfessionalSelector();
    await Promise.all([loadRequests(), loadProfessionalDashboard(), loadNotifications(), loadCoordinationDashboard()]);
  } catch (error) {
    console.error(error);
    if (loginFeedback) {
      loginFeedback.classList.remove('is-success');
      loginFeedback.classList.add('is-error');
      loginFeedback.textContent = `No se pudo iniciar sesión: ${error.message || 'revisa tus credenciales.'}`;
    }
  } finally {
    if (loginButton) {
      loginButton.disabled = false;
      loginButton.classList.remove('is-disabled');
    }
  }
}

function resetProfessionalPaymentState(message = 'Primero acepta los términos y genera la orden de pago.') {
  pendingPaymentId = 0;
  mercadoPagoCheckoutUrl = '';
  mercadoPagoPreferenceId = '';
  paymentApproved = false;

  if (paymentState) {
    paymentState.textContent = 'Pendiente de pago';
    paymentState.classList.remove('is-paid');
  }

  if (mercadoPagoLinkButton) {
    mercadoPagoLinkButton.href = '#';
    mercadoPagoLinkButton.classList.add('is-disabled', 'payment-link-disabled');
    mercadoPagoLinkButton.setAttribute('aria-disabled', 'true');
  }

  if (verifyPaymentButton) {
    verifyPaymentButton.disabled = true;
    verifyPaymentButton.classList.add('is-disabled');
  }

  if (confirmPaymentButton) {
    confirmPaymentButton.disabled = true;
    confirmPaymentButton.classList.add('is-disabled');
  }

  if (paymentFeedback) {
    paymentFeedback.classList.remove('is-error', 'is-success');
    paymentFeedback.textContent = message;
  }

  updateRegisterGate();
}

function invalidateProfessionalPaymentIfNeeded(message = 'Modificaste los datos del alta profesional. Genera nuevamente la orden de pago.') {
  if (pendingPaymentId || paymentApproved) {
    resetProfessionalPaymentState(message);
  }
}

function getRegistrationBaseData() {
  const role = getCurrentAccountRole();
  const isProfessional = role === 'profesional';
  const nombre = registerNameInput?.value.trim() || '';
  const email = registerEmailInput?.value.trim() || '';
  const telefono = registerPhoneInput?.value.trim() || '';
  const dni = registerDniInput?.value.trim() || '';
  const fechaNacimiento = registerBirthDateInput?.value || '';
  const ubicacion = registerLocationInput?.value.trim() || '';
  const password = registerPasswordInput?.value || '';

  if (!nombre || !email || !telefono || !dni || !fechaNacimiento || !ubicacion || password.length < 6) {
    return {
      isProfessional,
      error: 'Completa nombre, email, teléfono, DNI, fecha, ubicación y una contraseña de al menos 6 caracteres.'
    };
  }

  return {
    isProfessional,
    nombre,
    ubicacion,
    userPayload: {
      nombre,
      email,
      telefono,
      dni,
      fechaNacimiento: new Date(`${fechaNacimiento}T12:00:00`).toISOString(),
      rol: isProfessional ? 'Profesional' : 'Cliente',
      passwordHash: password,
      activo: true,
      verificadoRenaper: false,
      recibeNotificaciones: true
    }
  };
}

async function createOrUpdateProfessionalUser(userPayload) {
  const sendUserRequest = async (method, endpoint) => {
    const response = await fetch(endpoint, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(userPayload)
    });

    return response;
  };

  const isUpdate = pendingProfessionalUserId > 0;
  const endpoint = isUpdate ? `/api/Usuarios/${pendingProfessionalUserId}` : '/api/Usuarios';
  const method = isUpdate ? 'PUT' : 'POST';

  let response = await sendUserRequest(method, endpoint);

  if (!response.ok && !isUpdate) {
    const apiError = await extractApiError(response);
    const duplicateDetected = /Ya existe un usuario con ese email|Ya existe un usuario con ese DNI/i.test(apiError);

    if (duplicateDetected) {
      const loginResponse = await fetch('/api/Auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: userPayload.email,
          password: userPayload.passwordHash
        })
      });

      if (loginResponse.ok) {
        const session = await loginResponse.json();

        if (String(session.rol || '').toLowerCase() === 'profesional' && Number(session.usuarioId || 0) > 0) {
          pendingProfessionalUserId = Number(session.usuarioId || 0);
          response = await sendUserRequest('PUT', `/api/Usuarios/${pendingProfessionalUserId}`);

          if (!response.ok) {
            throw new Error(await extractApiError(response));
          }

          const reusedUser = await response.json();

          if (paymentFeedback) {
            paymentFeedback.classList.remove('is-error');
            paymentFeedback.textContent = 'Recuperamos tu cuenta profesional existente y continuaremos con la orden de pago.';
          }

          return reusedUser;
        }
      }

      throw new Error('Ese email o DNI ya está registrado. Inicia sesión con esa cuenta o cambia los datos si quieres crear una nueva.');
    }

    throw new Error(apiError);
  }

  if (!response.ok) {
    throw new Error(await extractApiError(response));
  }

  const user = await response.json();
  pendingProfessionalUserId = Number(user.id || 0);
  return user;
}

async function startProfessionalPayment() {
  const data = getRegistrationBaseData();
  if (!data.isProfessional) return;

  if (!termsCheckbox?.checked) {
    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-success');
      paymentFeedback.classList.add('is-error');
      paymentFeedback.textContent = 'Debes aceptar términos antes de generar la orden de pago.';
    }
    return;
  }

  if (data.error) {
    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-success');
      paymentFeedback.classList.add('is-error');
      paymentFeedback.textContent = data.error;
    }
    return;
  }

  if (!currentSector) {
    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-success');
      paymentFeedback.classList.add('is-error');
      paymentFeedback.textContent = 'Selecciona o identifica tu rubro antes de generar la orden.';
    }
    return;
  }

  if (startPaymentButton) {
    startPaymentButton.disabled = true;
    startPaymentButton.classList.add('is-disabled');
  }

  try {
    const user = await createOrUpdateProfessionalUser(data.userPayload);

    const paymentPayload = {
      usuarioId: user.id,
      moneda: 'ARS',
      concepto: 'Plan Fundadores Pro',
      proveedor: 'Mercado Pago',
      detalle: `Alta profesional en ${currentSector} · ${data.ubicacion}`,
      planCodigo: 'FUNDADORES_PRO'
    };

    const response = await fetch('/api/PagosProfesionales', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(paymentPayload)
    });

    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    const payment = await response.json();
    pendingPaymentId = Number(payment.id || 0);
    paymentApproved = false;

    if (paymentState) {
      paymentState.textContent = `Orden #${payment.id} pendiente`;
      paymentState.classList.remove('is-paid');
    }

    if (confirmPaymentButton) {
      confirmPaymentButton.disabled = false;
      confirmPaymentButton.classList.remove('is-disabled');
    }

    try {
      const mpResponse = await fetch(`/api/PagosProfesionales/${payment.id}/mercadopago/preference`, {
        method: 'POST'
      });

      if (!mpResponse.ok) {
        throw new Error(await extractApiError(mpResponse));
      }

      const preference = await mpResponse.json();
      mercadoPagoPreferenceId = preference.preferenceId || '';
      mercadoPagoCheckoutUrl = preference.sandboxInitPoint || preference.initPoint || '';

      if (mercadoPagoLinkButton && mercadoPagoCheckoutUrl) {
        mercadoPagoLinkButton.href = mercadoPagoCheckoutUrl;
        mercadoPagoLinkButton.classList.remove('is-disabled', 'payment-link-disabled');
        mercadoPagoLinkButton.setAttribute('aria-disabled', 'false');
      }

      if (verifyPaymentButton) {
        verifyPaymentButton.disabled = false;
        verifyPaymentButton.classList.remove('is-disabled');
      }

      if (paymentFeedback) {
        paymentFeedback.classList.remove('is-error');
        paymentFeedback.classList.add('is-success');
        const paymentAmountLabel = `${Number(payment.monto || 2500).toLocaleString('es-AR')} ${payment.moneda || 'ARS'}`;
        paymentFeedback.textContent = preference.message || `Orden generada por ${paymentAmountLabel}. Se abrió Mercado Pago en una pestaña nueva.`;
      }

      if (mercadoPagoCheckoutUrl) {
        window.open(mercadoPagoCheckoutUrl, '_blank', 'noopener');
      }
    } catch (mpError) {
      console.error(mpError);
      if (paymentFeedback) {
        paymentFeedback.classList.remove('is-success');
        paymentFeedback.classList.add('is-error');
        paymentFeedback.textContent = `Orden generada por ${formatCurrency(payment.monto || 2500)}, pero Mercado Pago aún no está configurado: ${mpError.message || 'usa por ahora el botón de aprobación demo.'}`;
      }
    }
  } catch (error) {
    console.error(error);
    if (paymentState) {
      paymentState.textContent = 'Error de pago';
      paymentState.classList.remove('is-paid');
    }

    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-success');
      paymentFeedback.classList.add('is-error');
      paymentFeedback.textContent = `No se pudo generar la orden: ${error.message || 'revisa los datos e intenta nuevamente.'}`;
    }
  } finally {
    updateRegisterGate();
  }
}

async function verifyMercadoPagoPayment() {
  if (!pendingPaymentId) {
    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-success');
      paymentFeedback.classList.add('is-error');
      paymentFeedback.textContent = 'Primero genera la orden de pago.';
    }
    return;
  }

  if (verifyPaymentButton) {
    verifyPaymentButton.disabled = true;
    verifyPaymentButton.classList.add('is-disabled');
  }

  try {
    const response = await fetch(`/api/PagosProfesionales/${pendingPaymentId}/mercadopago/verificar`, {
      method: 'POST'
    });

    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    const verification = await response.json();
    paymentApproved = !!verification.aprobado;

    if (paymentState) {
      paymentState.textContent = paymentApproved ? 'Pago aprobado' : (verification.estado || 'Pendiente');
      paymentState.classList.toggle('is-paid', paymentApproved);
    }

    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-error');
      paymentFeedback.classList.toggle('is-success', paymentApproved);
      paymentFeedback.textContent = verification.message || (paymentApproved
        ? 'Mercado Pago informó el pago como aprobado.'
        : 'Mercado Pago todavía no confirma el pago.');
    }
  } catch (error) {
    console.error(error);
    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-success');
      paymentFeedback.classList.add('is-error');
      paymentFeedback.textContent = `No se pudo verificar el pago con Mercado Pago: ${error.message || 'intenta nuevamente.'}`;
    }
  } finally {
    updateRegisterGate();
  }
}

async function confirmProfessionalPayment() {
  if (!pendingPaymentId) {
    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-success');
      paymentFeedback.classList.add('is-error');
      paymentFeedback.textContent = 'Primero genera la orden de pago.';
    }
    return;
  }

  if (confirmPaymentButton) {
    confirmPaymentButton.disabled = true;
    confirmPaymentButton.classList.add('is-disabled');
  }

  try {
    const response = await fetch(`/api/PagosProfesionales/${pendingPaymentId}/confirmar`, {
      method: 'POST'
    });

    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    const payment = await response.json();
    paymentApproved = String(payment.estado || '').toLowerCase() === 'aprobado';

    if (paymentState) {
      paymentState.textContent = paymentApproved ? 'Pago aprobado' : (payment.estado || 'Pago procesado');
      paymentState.classList.toggle('is-paid', paymentApproved);
    }

    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-error');
      paymentFeedback.classList.toggle('is-success', paymentApproved);
      paymentFeedback.textContent = paymentApproved
        ? `Pago demo acreditado correctamente. Referencia ${payment.referenciaExterna}. Ya puedes activar tu perfil profesional.`
        : 'El pago demo fue procesado, revisa el estado antes de continuar.';
    }
  } catch (error) {
    console.error(error);
    if (paymentState) {
      paymentState.textContent = 'Error de pago';
      paymentState.classList.remove('is-paid');
    }

    if (paymentFeedback) {
      paymentFeedback.classList.remove('is-success');
      paymentFeedback.classList.add('is-error');
      paymentFeedback.textContent = `No se pudo confirmar el pago demo: ${error.message || 'intenta nuevamente.'}`;
    }
  } finally {
    updateRegisterGate();
  }
}

function setActiveRole(role) {
  roleTabs.forEach((tab) => {
    const isActive = tab.dataset.roleTab === role;
    tab.classList.toggle('is-active', isActive);
    tab.setAttribute('aria-pressed', String(isActive));
  });

  roleViews.forEach((view) => {
    view.classList.toggle('is-active', view.dataset.roleView === role);
  });

  if (experienceState) {
    const labels = {
      cliente: 'Modo cliente',
      profesional: 'Modo profesional',
      coordinacion: 'Modo coordinación'
    };

    experienceState.textContent = labels[role] || 'Modo cliente';
  }
}

function setActiveAuthTab(mode) {
  authTabs.forEach((tab) => {
    const isActive = tab.dataset.authTab === mode;
    tab.classList.toggle('is-active', isActive);
    tab.setAttribute('aria-pressed', String(isActive));
  });

  authViews.forEach((view) => {
    view.classList.toggle('is-active', view.dataset.authView === mode);
  });
}

function setAccountRole(role) {
  accountRoleButtons.forEach((button) => {
    const isActive = button.dataset.accountRole === role;
    button.classList.toggle('is-active', isActive);
    button.setAttribute('aria-pressed', String(isActive));
  });

  const config = {
    cliente: {
      state: 'Inicio cliente',
      caption: 'Bienvenida cliente',
      title: 'Publica una necesidad y recibe opciones con contexto claro',
      text: 'La experiencia prioriza ubicación, urgencia, presupuesto y perfiles confiables para que elegir sea rápido y simple.',
      role: 'Cliente',
      note: 'Onboarding corto con foco en confianza y claridad'
    },
    profesional: {
      state: 'Inicio profesional',
      caption: 'Bienvenida profesional',
      title: 'Activa tu perfil, destaca tu experiencia y encuentra oportunidades cercanas',
      text: 'Separaremos tu perfil por rubros principales, con ayuda de IA para orientarte hacia el sector laboral más adecuado.',
      role: 'Profesional',
      note: 'Debes seleccionar rubro y aceptar el alta profesional de ARS 2.500.'
    }
  };

  const current = config[role] || config.cliente;
  const isProfessional = role === 'profesional';

  if (accessState) accessState.textContent = current.state;
  if (welcomeCaption) welcomeCaption.textContent = current.caption;
  if (welcomeTitle) welcomeTitle.textContent = current.title;
  if (welcomeText) welcomeText.textContent = current.text;
  if (rolePreviewInput) rolePreviewInput.value = current.role;
  if (registerNote) registerNote.textContent = current.note;
  if (clientRegisterFields) clientRegisterFields.classList.toggle('is-hidden', isProfessional);
  if (proOnboarding) proOnboarding.classList.toggle('is-hidden', !isProfessional);

  updateRegisterGate();
}

function setSelectedSector(sector) {
  currentSector = sector;

  sectorButtons.forEach((button) => {
    const isActive = button.dataset.sector === sector;
    button.classList.toggle('is-active', isActive);
    button.setAttribute('aria-pressed', String(isActive));
  });

  if (selectedSectorInput) {
    selectedSectorInput.value = sector || 'Selecciona o pide ayuda a la IA';
  }
}

function suggestSectorFromDescription(text) {
  const normalized = normalizeText(text);

  const rules = [
    { sector: 'TECNOLOGIA Y SISTEMAS', keywords: ['software', 'programacion', 'programador', 'pc', 'computadora', 'redes', 'sistemas', 'soporte', 'datos', 'tecnico it', 'ciberseguridad', 'web'] },
    { sector: 'SALUD Y ENFERMERIA', keywords: ['enfermer', 'salud', 'paciente', 'clinica', 'medic', 'farmacia', 'cuidados', 'hospital'] },
    { sector: 'PRODUCCION, MANUFACTURA Y OPERARIOS', keywords: ['produccion', 'operario', 'fabrica', 'planta', 'maquina', 'ensamblado', 'manufactura', 'linea'] },
    { sector: 'COMERCIO VENTAS Y LOGISTICA', keywords: ['venta', 'vendedor', 'comercio', 'logistica', 'reparto', 'delivery', 'stock', 'deposito', 'chofer', 'atencion al cliente'] },
    { sector: 'ADMINISTRACION CONTABILIDAD Y FINANZAS', keywords: ['administracion', 'contabilidad', 'finanzas', 'caja', 'facturacion', 'excel', 'rrhh', 'tesoreria', 'auditoria'] },
    { sector: 'HOSTELERIA TURISMO Y GASTRONOMIA', keywords: ['gastronomia', 'cocina', 'chef', 'mozo', 'hotel', 'turismo', 'barista', 'recepcion', 'hosteleria'] },
    { sector: 'CONSTRUCCION Y SERVICIOS GENERALES', keywords: ['albanil', 'construccion', 'plomeria', 'electricidad', 'mantenimiento', 'limpieza', 'soldadura', 'mecanica', 'servicios generales'] }
  ];

  let bestSector = 'CONSTRUCCION Y SERVICIOS GENERALES';
  let bestScore = 0;

  rules.forEach((rule) => {
    const score = rule.keywords.reduce((total, keyword) => total + (normalized.includes(keyword) ? 1 : 0), 0);
    if (score > bestScore) {
      bestScore = score;
      bestSector = rule.sector;
    }
  });

  return { sector: bestSector, confident: bestScore > 0 };
}

function updateRegisterGate() {
  const isProfessional = getCurrentAccountRole() === 'profesional';
  if (!registerContinueButton) return;

  if (isProfessional) {
    const accepted = !!termsCheckbox?.checked;
    const hasOrder = pendingPaymentId > 0;
    const canCreate = accepted && paymentApproved;

    registerContinueButton.disabled = !canCreate;
    registerContinueButton.classList.toggle('is-disabled', !canCreate);

    if (startPaymentButton) {
      startPaymentButton.disabled = !accepted;
      startPaymentButton.classList.toggle('is-disabled', !accepted);
    }

    if (verifyPaymentButton) {
      verifyPaymentButton.disabled = !hasOrder;
      verifyPaymentButton.classList.toggle('is-disabled', !hasOrder);
    }

    if (confirmPaymentButton) {
      confirmPaymentButton.disabled = !hasOrder;
      confirmPaymentButton.classList.toggle('is-disabled', !hasOrder);
    }

    if (!accepted) {
      registerContinueButton.textContent = 'Acepta términos para continuar';
    } else if (!paymentApproved) {
      registerContinueButton.textContent = 'Completa el pago de ARS 2.500';
    } else {
      registerContinueButton.textContent = 'Activar perfil profesional';
    }
  } else {
    registerContinueButton.disabled = false;
    registerContinueButton.classList.remove('is-disabled');
    registerContinueButton.textContent = 'Crear cuenta cliente';
  }
}

function syncDistanceBonusInputState() {
  if (!proDistanceBonusInput) return;

  const acceptsFarJobs = !!proFarJobsInput?.checked;
  proDistanceBonusInput.disabled = !acceptsFarJobs;
  proDistanceBonusInput.classList.toggle('is-disabled', !acceptsFarJobs);

  if (!acceptsFarJobs) {
    proDistanceBonusInput.value = '0';
  }
}

function resolveSelectedRubroIds() {
  const normalizedSector = normalizeText(currentSector);
  if (!normalizedSector) return [];

  const exactMatches = cachedRubros.filter((rubro) => normalizeText(rubro.nombre) === normalizedSector);
  if (exactMatches.length > 0) {
    return exactMatches.map((rubro) => rubro.id);
  }

  const fallbackAliases = {
    'tecnologia y sistemas': ['Técnica'],
    'salud y enfermeria': ['Enfermería'],
    'produccion, manufactura y operarios': ['Metalurgia', 'Soldadura'],
    'comercio ventas y logistica': ['Técnica'],
    'administracion contabilidad y finanzas': ['Técnica'],
    'hosteleria turismo y gastronomia': ['Cuidados de Niños'],
    'construccion y servicios generales': ['Electricidad', 'Plomería', 'Mecánica']
  };

  const aliases = fallbackAliases[normalizedSector] || [];
  const fallbackMatches = cachedRubros.filter((rubro) => aliases.some((alias) => normalizeText(alias) === normalizeText(rubro.nombre)));
  return fallbackMatches.map((rubro) => rubro.id);
}

async function submitRegistration() {
  const data = getRegistrationBaseData();
  const isProfessional = data.isProfessional;

  if (registerFeedback) {
    registerFeedback.classList.remove('is-error', 'is-success');
    registerFeedback.textContent = 'Validando datos del alta...';
  }

  if (data.error) {
    if (registerFeedback) {
      registerFeedback.classList.add('is-error');
      registerFeedback.textContent = data.error;
    }
    return;
  }

  if (registerContinueButton) {
    registerContinueButton.disabled = true;
    registerContinueButton.classList.add('is-disabled');
  }

  try {
    if (isProfessional) {
      if (!termsCheckbox?.checked) {
        throw new Error('Debes aceptar los términos y el abono profesional antes de continuar.');
      }

      if (!paymentApproved || !pendingPaymentId) {
        throw new Error('Debes completar el pago profesional de ARS 2.500 antes de activar el perfil.');
      }

      const rubroIds = resolveSelectedRubroIds();
      if (rubroIds.length === 0) {
        throw new Error('No se pudo resolver el rubro seleccionado contra los rubros cargados en backend.');
      }

      const experienceYears = Number(proExperienceInput?.value || 0);
      const tariff = Number(proRateInput?.value || 0);
      const reach = Number(proReachInput?.value || 10);
      const goal = Number(proGoalInput?.value || 0);
      const acceptsFarJobs = !!proFarJobsInput?.checked;
      const distanceBonus = Math.max(0, Number(proDistanceBonusInput?.value || 0));
      const description = proDescriptionInput?.value.trim() || '';

      if (description.length < 10) {
        throw new Error('La descripción profesional debe tener al menos 10 caracteres.');
      }

      if (!Number.isFinite(distanceBonus) || distanceBonus < 0) {
        throw new Error('El bono por kilómetro extra debe ser un número válido mayor o igual a 0.');
      }

      const ensuredUser = await createOrUpdateProfessionalUser(data.userPayload);
      const existingProfessionalSession = await fetchSessionContext(ensuredUser.id);
      const existingProfessionalId = Number(existingProfessionalSession?.profesionalId || 0);

      let existingProfessionalData = null;
      if (existingProfessionalId > 0) {
        const existingProfessionalResponse = await fetch(`/api/Profesionales/${existingProfessionalId}`);
        if (existingProfessionalResponse.ok) {
          existingProfessionalData = await existingProfessionalResponse.json();
        }
      }

      const professionalCoords = await resolveCoordinatesForPayload(data.ubicacion);

      const professionalPayload = {
        usuarioId: ensuredUser.id,
        latitud: professionalCoords.lat,
        longitud: professionalCoords.lng,
        ubicacion: data.ubicacion,
        'añosExperiencia': experienceYears,
        descripcion: description,
        tarifaBase: tariff,
        radioAlcanceKm: reach,
        gananciaMensualObjetivo: goal,
        gananciaMensualActual: Number(existingProfessionalData?.gananciaMensualActual || 0),
        aceptaTrabajoLejano: acceptsFarJobs,
        bonoPorDistancia: acceptsFarJobs ? distanceBonus : 0,
        rubroIds
      };

      const professionalEndpoint = existingProfessionalId > 0
        ? `/api/Profesionales/${existingProfessionalId}`
        : '/api/Profesionales';
      const professionalMethod = existingProfessionalId > 0 ? 'PUT' : 'POST';

      const professionalResponse = await fetch(professionalEndpoint, {
        method: professionalMethod,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(professionalPayload)
      });

      if (!professionalResponse.ok) {
        throw new Error(await extractApiError(professionalResponse));
      }

      const createdProfessional = await professionalResponse.json();
      lastRegisteredProfessionalId = Number(createdProfessional.id || 0);

      const professionalSession = await fetchSessionContext(ensuredUser.id);
      saveSession(professionalSession);

      if (registerFeedback) {
        registerFeedback.classList.add('is-success');
        registerFeedback.textContent = `Alta profesional completa: ${createdProfessional.usuarioNombre || data.nombre} quedó registrada en ${currentSector}.`;
      }
    } else {
      const userResponse = await fetch('/api/Usuarios', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data.userPayload)
      });

      if (!userResponse.ok) {
        throw new Error(await extractApiError(userResponse));
      }

      const createdUser = await userResponse.json();

      const clientCoords = await resolveCoordinatesForPayload(data.ubicacion);

      const clientPayload = {
        usuarioId: createdUser.id,
        latitud: clientCoords.lat,
        longitud: clientCoords.lng,
        ubicacion: data.ubicacion,
        preferencias: clientPreferencesInput?.value.trim() || 'Atención clara y seguimiento rápido.',
        recibeNotificaciones: true
      };

      const clientResponse = await fetch('/api/Clientes', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(clientPayload)
      });

      if (!clientResponse.ok) {
        throw new Error(await extractApiError(clientResponse));
      }

      const createdClient = await clientResponse.json();
      lastRegisteredClientId = Number(createdClient.id || 0);

      const clientSession = await fetchSessionContext(createdUser.id);
      saveSession(clientSession);

      if (registerFeedback) {
        registerFeedback.classList.add('is-success');
        registerFeedback.textContent = `Cuenta cliente creada: ${createdClient.usuarioNombre || data.nombre} ya puede publicar solicitudes.`;
      }
    }

    await loadHomeData();
    const targetId = isProfessional ? 'dashboard-profesional' : 'dashboard-cliente';
    document.getElementById(targetId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  } catch (error) {
    console.error(error);
    if (registerFeedback) {
      registerFeedback.classList.remove('is-success');
      registerFeedback.classList.add('is-error');
      registerFeedback.textContent = `No se pudo crear la cuenta: ${error.message || 'revisa los datos e intenta nuevamente.'}`;
    }
  } finally {
    updateRegisterGate();
  }
}

function updatePreview() {
  const selectedText = servicePicker?.selectedOptions?.[0]?.textContent?.trim() || 'Servicio en preparación';
  const urgency = urgencyPicker?.value || 'Hoy';
  const amount = Number(budgetRange?.value || 0);
  const message = (messageInput?.value || '').trim() || 'Describe aquí la necesidad principal.';

  if (budgetValue) budgetValue.textContent = formatCurrency(amount);
  if (previewService) previewService.textContent = selectedText;
  if (previewMessage) previewMessage.textContent = message;
  if (previewUrgency) previewUrgency.textContent = urgency;
  if (previewBudget) previewBudget.textContent = formatCurrency(amount);
    // Inicializar valores de presupuesto con la moneda correcta
    updatePreview();
  if (featuredServiceName) featuredServiceName.textContent = selectedText;
}

function formatDateLabel(isoDate) {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) {
    return isoDate || 'Sin fecha';
  }

  return new Intl.DateTimeFormat('es-AR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(date);
}

function formatDateTimeLabel(isoDate) {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) {
    return isoDate || 'Ahora';
  }

  return new Intl.DateTimeFormat('es-AR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

function stopNotificationPolling() {
  if (notificationPollTimer) {
    window.clearInterval(notificationPollTimer);
    notificationPollTimer = 0;
  }
}

function startNotificationPolling() {
  stopNotificationPolling();

  if (!currentSession?.usuarioId) {
    return;
  }

  notificationPollTimer = window.setInterval(() => {
    loadNotifications();
  }, 20000);
}

function showNotificationToasts(items) {
  if (!notificationToastHost || !Array.isArray(items)) return;

  items
    .filter((item) => !item.leida && !shownNotificationIds.has(item.id))
    .slice(0, 3)
    .forEach((item) => {
      shownNotificationIds.add(item.id);

      const toast = document.createElement('div');
      toast.className = 'toast-card';
      toast.innerHTML = `
        <strong>${item.titulo || 'Nueva novedad'}</strong>
        <small>${item.mensaje || 'Tienes una actualización en AppServicios.'}</small>`;

      notificationToastHost.appendChild(toast);
      window.setTimeout(() => {
        toast.remove();
      }, 5000);
    });
}

function renderNotifications(items) {
  if (!notificationsList) return;

  const unreadCount = Array.isArray(items) ? items.filter((item) => !item.leida).length : 0;
  if (notificationsCounter) {
    notificationsCounter.textContent = String(unreadCount);
    notificationsCounter.classList.toggle('is-empty', unreadCount === 0);
  }

  if (!currentSession?.usuarioId) {
    notificationsList.innerHTML = `
      <div class="request-item">
        <strong>Sin sesión activa</strong>
        <small>Inicia sesión para ver tus alertas de trabajo.</small>
      </div>`;
    return;
  }

  if (!Array.isArray(items) || items.length === 0) {
    notificationsList.innerHTML = `
      <div class="request-item">
        <strong>Sin novedades</strong>
        <small>No hay alertas nuevas para tu cuenta por ahora.</small>
      </div>`;
    return;
  }

  notificationsList.innerHTML = '';
  items.slice(0, 10).forEach((item) => {
    const card = document.createElement('div');
    card.className = `request-item notification-item ${item.leida ? 'is-read' : 'is-unread'}`;
    card.innerHTML = `
      <strong>${item.titulo || 'Notificación'}</strong>
      <small>${item.mensaje || 'Tienes una novedad en tu panel.'}</small>
      <div class="notification-meta">
        <small>${formatDateTimeLabel(item.fechaCreacion)}</small>
        ${item.leida
          ? '<span class="pill">Leída</span>'
          : `<button type="button" class="action-btn accept" data-notification-read="${item.id}">Marcar leída</button>`}
      </div>`;
    notificationsList.appendChild(card);
  });
}

async function loadNotifications() {
  if (!notificationsList) return;

  if (!currentSession?.usuarioId) {
    renderNotifications([]);
    return;
  }

  notificationsList.innerHTML = `
    <div class="request-item">
      <strong>Cargando alertas</strong>
      <small>Buscando notificaciones relevantes para tu perfil...</small>
    </div>`;

  try {
    const response = await fetch(`/api/Notificaciones/usuario/${currentSession.usuarioId}`);
    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    const items = await response.json();

    if (notificationsInitialized) {
      showNotificationToasts(items);
    } else {
      items.filter((item) => !item.leida).forEach((item) => shownNotificationIds.add(item.id));
      notificationsInitialized = true;
    }

    renderNotifications(items);
  } catch (error) {
    console.error(error);
    notificationsList.innerHTML = `
      <div class="request-item">
        <strong>Error al cargar</strong>
        <small>No fue posible recuperar tus notificaciones en este momento.</small>
      </div>`;
  }
}

async function markNotificationAsRead(notificationId) {
  try {
    shownNotificationIds.add(notificationId);

    const response = await fetch(`/api/Notificaciones/${notificationId}/marcar-leida`, {
      method: 'POST'
    });

    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    await loadNotifications();
  } catch (error) {
    console.error(error);
  }
}

function startCoordinationPolling() {
  if (coordinationPollTimer) {
    window.clearInterval(coordinationPollTimer);
  }

  coordinationPollTimer = window.setInterval(() => {
    loadCoordinationDashboard();
  }, 25000);
}

function showCoordinationGoalToasts(objetivos) {
  if (!notificationToastHost || !Array.isArray(objetivos)) return;

  objetivos
    .filter((item) => item.estado === 'Cumplido' && !shownCoordinationGoalKeys.has(item.clave))
    .slice(0, 2)
    .forEach((item) => {
      shownCoordinationGoalKeys.add(item.clave);

      const toast = document.createElement('div');
      toast.className = 'toast-card';
      toast.innerHTML = `
        <strong>🎯 ${item.titulo}</strong>
        <small>${item.mensaje}</small>`;

      notificationToastHost.appendChild(toast);
      window.setTimeout(() => toast.remove(), 5500);
    });
}

function renderCoordinationAlerts(items) {
  if (!coordAlertsList) return;

  if (!Array.isArray(items) || items.length === 0) {
    coordAlertsList.innerHTML = `
      <div class="request-item">
        <strong>Sin alertas activas</strong>
        <small>La operación está estable por ahora.</small>
      </div>`;
    return;
  }

  coordAlertsList.innerHTML = '';
  items.forEach((item) => {
    const card = document.createElement('div');
    card.className = `coord-alert-item is-${item.tipo || 'info'}`;
    card.innerHTML = `
      <strong>${item.titulo || 'Alerta'}</strong>
      <small>${item.mensaje || ''}</small>`;
    coordAlertsList.appendChild(card);
  });
}

function renderCoordinationTableRows(container, rows, emptyMessage, mapper) {
  if (!container) return;

  if (!Array.isArray(rows) || rows.length === 0) {
    container.innerHTML = `<tr><td colspan="4">${emptyMessage}</td></tr>`;
    return;
  }

  container.innerHTML = rows.map(mapper).join('');
}

function renderCoordinationMovements(items) {
  if (!coordMovementsList) return;

  if (!Array.isArray(items) || items.length === 0) {
    coordMovementsList.innerHTML = `
      <div class="request-item">
        <strong>Sin movimientos recientes</strong>
        <small>Cuando haya nuevas operaciones aparecerán aquí.</small>
      </div>`;
    return;
  }

  coordMovementsList.innerHTML = '';
  items.forEach((item) => {
    const card = document.createElement('div');
    card.className = 'movement-item';
    card.innerHTML = `
      <strong>${item.titulo || 'Movimiento'}</strong>
      <small>${item.descripcion || ''}</small>
      <small>${formatDateTimeLabel(item.fecha)} · ${item.tipo || 'Operación'} · ${item.resumen || ''}</small>`;
    coordMovementsList.appendChild(card);
  });
}

function setAdminFeedback(message) {
  if (!coordAdminFeedback) return;
  coordAdminFeedback.textContent = message;
}

function renderCoordinationAudit(items) {
  if (!coordAuditList) return;

  if (!Array.isArray(items) || items.length === 0) {
    coordAuditList.innerHTML = `
      <div class="request-item">
        <strong>Sin auditoría reciente</strong>
        <small>Los accesos y operaciones críticas aparecerán aquí.</small>
      </div>`;
    return;
  }

  coordAuditList.innerHTML = '';
  items.forEach((item) => {
    const card = document.createElement('div');
    card.className = 'movement-item';
    card.innerHTML = `
      <strong>${item.tipo || 'Evento'} · ${item.accion || 'Acción'}</strong>
      <small>${item.descripcion || ''}</small>
      <small>${item.usuario || 'Sistema'} · ${item.entidad || 'General'}${item.entidadId ? ` #${item.entidadId}` : ''} · ${formatDateTimeLabel(item.fecha)}</small>`;
    coordAuditList.appendChild(card);
  });
}

function renderAdminUsers(items, adminMode) {
  if (!coordAdminUsersBody) return;

  if (coordAdminModeHint) {
    coordAdminModeHint.textContent = adminMode
      ? 'Acciones reales habilitadas para administración operativa.'
      : 'Inicia sesión como administrador para habilitar sanciones, verificaciones y aprobaciones.';
  }

  if (!adminMode) {
    coordAdminUsersBody.innerHTML = '<tr><td colspan="5">Inicia sesión como administrador para gestionar usuarios reales.</td></tr>';
    setAdminFeedback('Solo el rol Administrador puede ejecutar acciones reales desde este tablero.');
    return;
  }

  if (!Array.isArray(items) || items.length === 0) {
    coordAdminUsersBody.innerHTML = '<tr><td colspan="5">Todavía no hay usuarios disponibles para gestionar.</td></tr>';
    setAdminFeedback('Cuando haya cuentas recientes aparecerán aquí para su revisión.');
    return;
  }

  coordAdminUsersBody.innerHTML = items.map((item) => {
    const isCurrentAdmin = Number(currentSession?.usuarioId || 0) === Number(item.id || 0) && item.rol === 'Administrador';
    const paymentState = item.tienePagoAprobado ? 'Aprobado' : (item.estadoPago || 'No aplica');
    const suspendButton = isCurrentAdmin
      ? '<span class="mini-note">Admin actual</span>'
      : `<button type="button" class="action-btn ${item.activo ? 'reject' : 'accept'}" data-admin-action="toggle-active" data-user-id="${item.id}" data-current-active="${item.activo}">${item.activo ? 'Suspender' : 'Reactivar'}</button>`;
    const paymentButton = item.rol === 'Profesional' && item.pagoId && !item.tienePagoAprobado
      ? `<button type="button" class="action-btn complete" data-admin-action="approve-payment" data-payment-id="${item.pagoId}" data-user-id="${item.id}">Aprobar alta</button>`
      : '';

    return `
      <tr>
        <td>
          <strong>${item.nombre || 'Usuario'}</strong><br />
          <small>${item.email || ''}</small><br />
          <small>Alta ${formatDateTimeLabel(item.fechaRegistro)} · ${item.ubicacion || 'Sin ubicación'}</small>
        </td>
        <td>${item.rol || 'Sin rol'}</td>
        <td>${item.activo ? 'Activo' : 'Suspendido'} · ${item.verificadoRenaper ? 'Verificado' : 'Sin verificar'} · ${item.recibeNotificaciones ? 'Notif. ON' : 'Notif. OFF'}</td>
        <td>${paymentState}</td>
        <td>
          <div class="inline-actions">
            ${suspendButton}
            <button type="button" class="action-btn ${item.verificadoRenaper ? '' : 'accept'}" data-admin-action="toggle-verify" data-user-id="${item.id}" data-current-verified="${item.verificadoRenaper}">${item.verificadoRenaper ? 'Quitar verif.' : 'Verificar'}</button>
            <button type="button" class="action-btn" data-admin-action="toggle-notifications" data-user-id="${item.id}" data-current-notify="${item.recibeNotificaciones}">${item.recibeNotificaciones ? 'Silenciar' : 'Habilitar avisos'}</button>
            ${paymentButton}
          </div>
        </td>
      </tr>`;
  }).join('');

  setAdminFeedback('Selecciona una acción para ejecutar sanciones, verificaciones o aprobaciones en vivo.');
}

async function runAdminUserAction(userId, payload, successMessage) {
  if (!currentSession || currentSession.rol !== 'Administrador') {
    setAdminFeedback('Inicia sesión como administrador para usar estas acciones.');
    return;
  }

  setAdminFeedback('Aplicando cambio admin...');

  const response = await fetch(`/api/Coordinacion/admin/usuarios/${userId}/accion`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      adminUserId: currentSession.usuarioId,
      ...payload
    })
  });

  if (!response.ok) {
    throw new Error(await extractApiError(response));
  }

  const updated = await response.json();
  setAdminFeedback(`${successMessage}: ${updated.nombre || 'usuario'} (${updated.rol || 'rol'}).`);
  await Promise.all([loadCoordinationDashboard(), loadRequests(), loadProfessionalDashboard()]);
}

async function runAdminPaymentApproval(paymentId) {
  if (!currentSession || currentSession.rol !== 'Administrador') {
    setAdminFeedback('Inicia sesión como administrador para aprobar altas profesionales.');
    return;
  }

  setAdminFeedback('Aprobando alta profesional...');

  const response = await fetch(`/api/PagosProfesionales/${paymentId}/confirmar`, {
    method: 'POST'
  });

  if (!response.ok) {
    throw new Error(await extractApiError(response));
  }

  const payment = await response.json();
  setAdminFeedback(`Alta profesional aprobada. Pago #${payment.id} en estado ${payment.estado || 'Procesado'}.`);
  await Promise.all([loadCoordinationDashboard(), loadProfessionalDashboard()]);
}

function getCoordinationFilters() {
  const days = Number(coordDateRange?.value || 30);
  const city = coordCityFilter?.value.trim() || '';
  const rubroId = coordRubroFilter?.value || '';
  return { days, city, rubroId };
}

function buildCoordinationQueryString() {
  const filters = getCoordinationFilters();
  const params = new URLSearchParams();
  params.set('days', String(filters.days || 30));

  if (filters.city) {
    params.set('city', filters.city);
  }

  if (filters.rubroId) {
    params.set('rubroId', filters.rubroId);
  }

  if (currentSession?.rol === 'Administrador' && Number(currentSession?.usuarioId || 0) > 0) {
    params.set('adminUserId', String(currentSession.usuarioId));
  }

  return params.toString();
}

function fillCoordinationRubroFilter() {
  if (!coordRubroFilter) return;

  const currentValue = coordRubroFilter.value;
  coordRubroFilter.innerHTML = '<option value="">Todos los rubros</option>';

  cachedRubros.forEach((rubro) => {
    const option = document.createElement('option');
    option.value = String(rubro.id);
    option.textContent = rubro.nombre;
    coordRubroFilter.appendChild(option);
  });

  if (currentValue && cachedRubros.some((rubro) => String(rubro.id) === currentValue)) {
    coordRubroFilter.value = currentValue;
  }
}

function exportCoordinationReport() {
  const query = buildCoordinationQueryString();
  window.open(`/api/Coordinacion/reporte.csv?${query}`, '_blank', 'noopener');
}

function buildCoordinationPdfHtml(data) {
  const alertas = Array.isArray(data?.alertas) && data.alertas.length > 0
    ? data.alertas
    : [{ titulo: 'Sin alertas activas', mensaje: 'La operación se mantiene estable por ahora.' }];
  const rubros = Array.isArray(data?.rubrosTop) ? data.rubrosTop : [];
  const pros = Array.isArray(data?.profesionalesTop) ? data.profesionalesTop : [];
  const auditoria = Array.isArray(data?.auditoriaReciente) ? data.auditoriaReciente : [];

  const rubrosRows = rubros.length > 0
    ? rubros.map((item) => `
      <tr>
        <td>${escapeHtml(item.rubro || 'Sin rubro')}</td>
        <td>${item.solicitudes || 0}</td>
        <td>${escapeHtml(formatCurrency(item.ticketPromedio || 0))}</td>
        <td>${escapeHtml(formatPercent(item.tasaCierre || 0))}</td>
      </tr>`).join('')
    : '<tr><td colspan="4">Sin datos disponibles</td></tr>';

  const prosRows = pros.length > 0
    ? pros.map((item) => `
      <tr>
        <td>${escapeHtml(item.profesional || 'Profesional')}</td>
        <td>${escapeHtml(item.rubros || 'Sin rubros')}</td>
        <td>${item.trabajosCompletados || 0}</td>
        <td>${escapeHtml(formatCurrency(item.volumenMovido || 0))}</td>
      </tr>`).join('')
    : '<tr><td colspan="4">Sin profesionales destacados aún</td></tr>';

  const auditRows = auditoria.length > 0
    ? auditoria.slice(0, 10).map((item) => `
      <tr>
        <td>${escapeHtml(formatDateTimeLabel(item.fecha))}</td>
        <td>${escapeHtml(item.tipo || 'Evento')}</td>
        <td>${escapeHtml(item.accion || 'Acción')}</td>
        <td>${escapeHtml(item.usuario || 'Sistema')}</td>
        <td>${escapeHtml(item.descripcion || '')}</td>
      </tr>`).join('')
    : '<tr><td colspan="5">Sin auditoría reciente</td></tr>';

  const alertRows = alertas.map((item) => `
    <li>
      <strong>${escapeHtml(item.titulo || 'Alerta')}</strong>
      <span>${escapeHtml(item.mensaje || '')}</span>
    </li>`).join('');

  return `<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <title>Reporte ejecutivo AppServicios</title>
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #111827; }
    h1,h2,h3 { margin: 0 0 10px; }
    p { margin: 4px 0; }
    .header { border-bottom: 2px solid #111827; padding-bottom: 12px; margin-bottom: 18px; }
    .muted { color: #4b5563; }
    .grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin: 18px 0; }
    .card { border: 1px solid #d1d5db; border-radius: 12px; padding: 12px; }
    .card strong { display: block; font-size: 18px; margin-top: 6px; }
    table { width: 100%; border-collapse: collapse; margin-top: 10px; }
    th, td { border: 1px solid #d1d5db; padding: 8px; text-align: left; vertical-align: top; }
    th { background: #f3f4f6; }
    ul { padding-left: 18px; }
    li { margin-bottom: 8px; }
    li strong, li span { display: block; }
    .section { margin-top: 22px; }
    @media print { body { margin: 12px; } }
  </style>
</head>
<body>
  <div class="header">
    <h1>AppServicios · Reporte ejecutivo</h1>
    <p class="muted">Actualizado ${escapeHtml(formatDateTimeLabel(data?.fechaActualizacion))}</p>
    <p class="muted">Período: ${escapeHtml(String(data?.periodoDias || 30))} días · Ciudad: ${escapeHtml(data?.filtroCiudad || 'Todas')} · Rubro: ${escapeHtml(data?.filtroRubro || 'Todos')}</p>
  </div>

  <div class="grid">
    <div class="card"><small>Usuarios</small><strong>${escapeHtml(formatCompactNumber(data?.totalUsuarios || 0))}</strong><span class="muted">${data?.totalClientes || 0} clientes · ${data?.totalProfesionales || 0} profesionales</span></div>
    <div class="card"><small>Solicitudes</small><strong>${data?.totalSolicitudes || 0}</strong><span class="muted">${data?.solicitudesPendientes || 0} pendientes · ${data?.solicitudesCompletadas || 0} completadas</span></div>
    <div class="card"><small>Volumen</small><strong>${escapeHtml(formatCurrency(data?.volumenGestionado || 0))}</strong><span class="muted">Pipeline ${escapeHtml(formatCurrency(data?.pipelinePendiente || 0))}</span></div>
    <div class="card"><small>Conversión</small><strong>${escapeHtml(formatPercent(data?.cumplimientoOperativo || 0))}</strong><span class="muted">Aceptación ${escapeHtml(formatPercent(data?.tasaAceptacion || 0))}</span></div>
  </div>

  <div class="section">
    <h2>Alertas y objetivos</h2>
    <ul>${alertRows}</ul>
  </div>

  <div class="section">
    <h2>Rubros con más movimiento</h2>
    <table>
      <thead><tr><th>Rubro</th><th>Solicitudes</th><th>Ticket</th><th>Cierre</th></tr></thead>
      <tbody>${rubrosRows}</tbody>
    </table>
  </div>

  <div class="section">
    <h2>Profesionales destacados</h2>
    <table>
      <thead><tr><th>Profesional</th><th>Rubros</th><th>Completados</th><th>Volumen</th></tr></thead>
      <tbody>${prosRows}</tbody>
    </table>
  </div>

  <div class="section">
    <h2>Auditoría ejecutiva</h2>
    <table>
      <thead><tr><th>Fecha</th><th>Tipo</th><th>Acción</th><th>Usuario</th><th>Descripción</th></tr></thead>
      <tbody>${auditRows}</tbody>
    </table>
  </div>

  <script>
    window.addEventListener('load', () => setTimeout(() => window.print(), 300));
  </script>
</body>
</html>`;
}

async function exportCoordinationPdf() {
  try {
    const query = buildCoordinationQueryString();
    const response = await fetch(`/api/Coordinacion/dashboard?${query}`);
    if (!response.ok) {
      throw new Error('No se pudo preparar el reporte PDF.');
    }

    const data = await response.json();
    cachedCoordinationDashboard = data;

    const reportWindow = window.open('', '_blank');
    if (!reportWindow) {
      throw new Error('El navegador bloqueó la ventana emergente del PDF.');
    }

    reportWindow.document.open();
    reportWindow.document.write(buildCoordinationPdfHtml(data));
    reportWindow.document.close();
    setAdminFeedback('Se abrió la vista ejecutiva lista para guardar como PDF.');
  } catch (error) {
    console.error(error);
    setAdminFeedback(`No se pudo exportar el PDF: ${error.message || 'intenta nuevamente.'}`);
  }
}

function renderCoordinationDashboard(data) {
  if (!data) return;
  cachedCoordinationDashboard = data;

  if (coordinationSync) {
    coordinationSync.textContent = `Actualizado ${formatDateTimeLabel(data.fechaActualizacion)} · ${data.periodoDias || 30} días`;
  }

  if (coordUsersMetric) {
    coordUsersMetric.textContent = formatCompactNumber(data.totalUsuarios || 0);
  }
  if (coordUsersDetail) {
    coordUsersDetail.textContent = `${data.totalClientes || 0} clientes · ${data.totalProfesionales || 0} profesionales · ${data.filtroCiudad || 'Todas las ciudades'}`;
  }

  if (coordFlowMetric) {
    coordFlowMetric.textContent = `${data.totalSolicitudes || 0} solicitudes`;
  }
  if (coordFlowDetail) {
    coordFlowDetail.textContent = `${data.solicitudesPendientes || 0} pendientes · ${data.solicitudesCompletadas || 0} completadas · rubro ${data.filtroRubro || 'global'}`;
  }

  if (coordRevenueMetric) {
    coordRevenueMetric.textContent = formatCurrency(data.volumenGestionado || 0);
  }
  if (coordRevenueDetail) {
    coordRevenueDetail.textContent = `Pipeline ${formatCurrency(data.pipelinePendiente || 0)} · Ticket ${formatCurrency(data.ticketPromedio || 0)}`;
  }

  if (coordEngagementMetric) {
    coordEngagementMetric.textContent = `${data.chatsActivos || 0} chats`;
  }
  if (coordEngagementDetail) {
    coordEngagementDetail.textContent = `SLA ${Number(data.slaRespuestaHoras || 0).toFixed(1)} h · ${data.pagosAprobados || 0} pagos aprobados`;
  }

  if (coordTargetStatus) {
    const goals = Array.isArray(data.objetivos) ? data.objetivos : [];
    const reached = goals.filter((item) => item.estado === 'Cumplido').length;
    coordTargetStatus.textContent = `${reached}/${goals.length || 0} metas cumplidas · ${data.filtroRubro || 'Todos los rubros'}`;
  }

  if (coordMiniBacklog) {
    coordMiniBacklog.textContent = `${data.solicitudesPendientes || 0} / ${data.solicitudesCompletadas || 0}`;
  }
  if (coordMiniHotRubro) {
    coordMiniHotRubro.textContent = data.rubrosTop?.[0]?.rubro || 'Sin datos';
  }
  if (coordMiniHealth) {
    coordMiniHealth.textContent = `${formatPercent(data.cumplimientoOperativo || 0)} · SLA ${Number(data.slaRespuestaHoras || 0).toFixed(1)}h`;
  }
  if (coordMiniDemand) {
    coordMiniDemand.textContent = `${data.totalSolicitudes || 0} solicitudes en ${data.periodoDias || 30} días · pipeline ${formatCurrency(data.pipelinePendiente || 0)}.`;
  }
  if (coordMiniAlerts) {
    coordMiniAlerts.textContent = data.alertas?.[0]?.mensaje || 'Las alertas estratégicas aparecerán aquí.';
  }

  renderCoordinationAlerts(data.alertas);
  showCoordinationGoalToasts(data.objetivos);

  setDonutValue(coordOpsDonut, data.cumplimientoOperativo || 0, formatPercent(data.cumplimientoOperativo || 0));
  setDonutValue(coordActivationDonut, data.activacionProfesional || 0, formatPercent(data.activacionProfesional || 0));
  setDonutValue(coordAcceptanceDonut, data.tasaAceptacion || 0, formatPercent(data.tasaAceptacion || 0));

  if (coordOpsText) {
    coordOpsText.textContent = `${data.solicitudesCompletadas || 0} completadas de ${data.totalSolicitudes || 0} solicitudes.`;
  }
  if (coordActivationText) {
    coordActivationText.textContent = `${data.pagosAprobados || 0} altas pagas sobre ${data.totalProfesionales || 0} profesionales.`;
  }
  if (coordAcceptanceText) {
    coordAcceptanceText.textContent = `${data.solicitudesAceptadas || 0} aceptadas y ${data.solicitudesRechazadas || 0} rechazadas.`;
  }

  renderCoordinationTableRows(
    coordRubrosBody,
    data.rubrosTop,
    'Todavía no hay rubros con datos suficientes.',
    (item) => `
      <tr>
        <td>${item.rubro || 'Sin rubro'}</td>
        <td>${item.solicitudes || 0}</td>
        <td>${formatCurrency(item.ticketPromedio || 0)}</td>
        <td>${formatPercent(item.tasaCierre || 0)}</td>
      </tr>`);

  renderCoordinationTableRows(
    coordProsBody,
    data.profesionalesTop,
    'Todavía no hay profesionales destacados con actividad.',
    (item) => `
      <tr>
        <td>${item.profesional || 'Profesional'}</td>
        <td>${item.rubros || 'Sin rubros'}</td>
        <td>${item.trabajosCompletados || 0}</td>
        <td>${formatCurrency(item.volumenMovido || 0)}</td>
      </tr>`);

  renderCoordinationMovements(data.movimientosRecientes);
  renderCoordinationAudit(data.auditoriaReciente);
  renderAdminUsers(data.usuariosGestion, !!data.adminMode);
}

async function loadCoordinationDashboard() {
  try {
    const query = buildCoordinationQueryString();
    const response = await fetch(`/api/Coordinacion/dashboard?${query}`);
    if (!response.ok) {
      throw new Error('No se pudo cargar el panel de coordinación.');
    }

    const dashboard = await response.json();
    renderCoordinationDashboard(dashboard);
  } catch (error) {
    console.error(error);

    if (coordinationSync) {
      coordinationSync.textContent = 'Error al sincronizar';
    }

    if (coordAlertsList) {
      coordAlertsList.innerHTML = `
        <div class="request-item">
          <strong>Error de carga</strong>
          <small>No fue posible recuperar las métricas ejecutivas.</small>
        </div>`;
    }

    renderAdminUsers([], false);
  }
}

function getChatEligibleRequests() {
  if (currentSession?.clienteId) {
    return cachedRequests.filter((item) => Number(item.clienteId || 0) === Number(currentSession.clienteId));
  }

  if (currentSession?.profesionalId) {
    return cachedRequests.filter((item) => Number(item.profesionalId || 0) === Number(currentSession.profesionalId));
  }

  return [];
}

function updateChatContextInfo() {
  const selected = getChatEligibleRequests().find((item) => Number(item.id) === Number(selectedChatRequestId));

  if (chatState) {
    chatState.textContent = selected ? `Solicitud ${selected.estado || 'activa'}` : 'Selecciona una solicitud';
  }

  if (chatInfoText) {
    chatInfoText.textContent = selected
      ? `${formatRequestTitle(selected)} · ${selected.ubicacion || 'Sin ubicación'} · Estado: ${selected.estado || 'Pendiente'}`
      : 'Elige una solicitud para ver la conversación entre cliente y profesional.';
  }

  if (chatSendButton) {
    const enabled = !!currentSession?.usuarioId && !!selected;
    chatSendButton.disabled = !enabled;
    chatSendButton.classList.toggle('is-disabled', !enabled);

    if (chatMessageInput) {
      chatMessageInput.disabled = !enabled;
    }
  }
}

function renderChatMessages(items) {
  if (!chatMessagesList) return;

  if (!currentSession?.usuarioId) {
    chatMessagesList.innerHTML = `
      <div class="request-item">
        <strong>Sin sesión activa</strong>
        <small>Inicia sesión para habilitar una conversación real.</small>
      </div>`;
    return;
  }

  if (!selectedChatRequestId) {
    chatMessagesList.innerHTML = `
      <div class="request-item">
        <strong>Sin conversación activa</strong>
        <small>Selecciona una solicitud para ver o enviar mensajes.</small>
      </div>`;
    return;
  }

  if (!Array.isArray(items) || items.length === 0) {
    chatMessagesList.innerHTML = `
      <div class="request-item">
        <strong>Aún no hay mensajes</strong>
        <small>Envía el primero para coordinar horario, dirección o detalles del servicio.</small>
      </div>`;
    return;
  }

  chatMessagesList.innerHTML = '';
  items.forEach((item) => {
    const bubble = document.createElement('div');
    const own = Number(item.usuarioId || 0) === Number(currentSession?.usuarioId || 0);
    bubble.className = `chat-bubble ${own ? 'is-own' : ''}`;
    bubble.innerHTML = `
      <strong>${own ? 'Tú' : (item.remitenteNombre || 'Usuario')}</strong>
      <small>${item.contenido || ''}</small>
      <small>${formatDateTimeLabel(item.fechaEnvio)}</small>`;
    chatMessagesList.appendChild(bubble);
  });

  chatMessagesList.scrollTop = chatMessagesList.scrollHeight;
}

function fillChatRequestSelect() {
  if (!chatRequestSelect) return;

  const items = getChatEligibleRequests();
  chatRequestSelect.innerHTML = '';

  if (!currentSession?.usuarioId) {
    selectedChatRequestId = 0;
    chatRequestSelect.disabled = true;
    chatRequestSelect.innerHTML = '<option value="">Inicia sesión para habilitar el chat</option>';
    updateChatContextInfo();
    renderChatMessages([]);
    return;
  }

  if (items.length === 0) {
    selectedChatRequestId = 0;
    chatRequestSelect.disabled = true;
    chatRequestSelect.innerHTML = '<option value="">Aún no tienes solicitudes para conversar</option>';
    updateChatContextInfo();
    renderChatMessages([]);
    return;
  }

  chatRequestSelect.disabled = false;
  items.forEach((item) => {
    const option = document.createElement('option');
    option.value = String(item.id);
    option.textContent = `${formatRequestTitle(item)} · ${item.estado || 'Pendiente'}`;
    chatRequestSelect.appendChild(option);
  });

  const preferredId = items.some((item) => Number(item.id) === Number(selectedChatRequestId))
    ? Number(selectedChatRequestId)
    : Number(items[0].id || 0);

  selectedChatRequestId = preferredId;
  chatRequestSelect.value = String(preferredId);
  updateChatContextInfo();
  loadChatMessages();
}

async function loadChatMessages() {
  if (!chatMessagesList) return;

  updateChatContextInfo();

  if (!currentSession?.usuarioId || !selectedChatRequestId) {
    renderChatMessages([]);
    return;
  }

  chatMessagesList.innerHTML = `
    <div class="request-item">
      <strong>Cargando conversación</strong>
      <small>Buscando mensajes de esta solicitud...</small>
    </div>`;

  try {
    const response = await fetch(`/api/MensajesSolicitud/solicitud/${selectedChatRequestId}?userId=${currentSession.usuarioId}`);
    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    const items = await response.json();
    renderChatMessages(items);
  } catch (error) {
    console.error(error);
    chatMessagesList.innerHTML = `
      <div class="request-item">
        <strong>Error al cargar</strong>
        <small>No fue posible recuperar la conversación.</small>
      </div>`;
  }
}

function openChatForRequest(requestId) {
  selectedChatRequestId = Number(requestId || 0);
  fillChatRequestSelect();
  if (chatRequestSelect && selectedChatRequestId) {
    chatRequestSelect.value = String(selectedChatRequestId);
  }
  loadChatMessages();
  document.getElementById('chat-solicitud')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

async function sendChatMessage() {
  const content = chatMessageInput?.value.trim() || '';

  if (!currentSession?.usuarioId) {
    if (chatFeedback) {
      chatFeedback.textContent = 'Inicia sesión para enviar mensajes.';
    }
    return;
  }

  if (!selectedChatRequestId) {
    if (chatFeedback) {
      chatFeedback.textContent = 'Selecciona una solicitud antes de escribir.';
    }
    return;
  }

  if (!content) {
    if (chatFeedback) {
      chatFeedback.textContent = 'Escribe un mensaje antes de enviarlo.';
    }
    return;
  }

  if (chatSendButton) {
    chatSendButton.disabled = true;
    chatSendButton.classList.add('is-disabled');
  }

  try {
    const response = await fetch('/api/MensajesSolicitud', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        solicitudTrabajoId: selectedChatRequestId,
        usuarioId: currentSession.usuarioId,
        contenido: content
      })
    });

    if (!response.ok) {
      throw new Error(await extractApiError(response));
    }

    if (chatMessageInput) {
      chatMessageInput.value = '';
    }

    if (chatFeedback) {
      chatFeedback.textContent = 'Mensaje enviado correctamente.';
    }

    await Promise.all([loadChatMessages(), loadNotifications(), loadCoordinationDashboard()]);
  } catch (error) {
    console.error(error);
    if (chatFeedback) {
      chatFeedback.textContent = `No se pudo enviar el mensaje: ${error.message || 'intenta nuevamente.'}`;
    }
  } finally {
    updateChatContextInfo();
  }
}

function setTodayAsDefaultDate() {
  if (!requestDateInput) return;

  const now = new Date();
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000);
  requestDateInput.value = local.toISOString().slice(0, 10);
}

function renderRequestList(items) {
  if (!requestListContainer) return;

  requestListContainer.innerHTML = '';

  if (!Array.isArray(items) || items.length === 0) {
    requestListContainer.innerHTML = `
      <div class="request-item">
        <strong>Sin solicitudes</strong>
        <small>Cuando publiques tu primera solicitud, aparecerá aquí.</small>
      </div>`;
    return;
  }

  items.slice(0, 8).forEach((item) => {
    const card = document.createElement('div');
    const distanceSummary = buildDistanceFinanceSummary(item);
    card.className = 'request-item';
    card.innerHTML = `
      <strong>${item.servicioNombre || 'Servicio'}</strong>
      <small>${item.descripcion || 'Sin descripción'}</small>
      <small>Estado: ${item.estado || 'Pendiente'} · Fecha: ${formatDateLabel(item.fechaRequerida)}</small>
      <small>Presupuesto: ${formatCurrency(item.presupuestoEstimado || 0)}</small>
      ${distanceSummary ? `<small>${distanceSummary}</small>` : ''}
      <div class="inline-actions">
        <button class="action-btn complete" data-open-chat="${item.id}">Abrir chat</button>
      </div>
    `;
    requestListContainer.appendChild(card);
  });
}

function formatRequestTitle(item) {
  return item.servicioNombre || item.ubicacion || `Solicitud #${item.id}`;
}

function buildRoleAwareRequestsUrl() {
  const userId = Number(currentSession?.usuarioId || 0);
  return userId > 0 ? `/api/SolicitudesTrabajo?userId=${userId}` : '/api/SolicitudesTrabajo';
}

function buildUpsertPayload(item, overrides = {}) {
  const payload = {
    usuarioOperadorId: Number(currentSession?.usuarioId || 0),
    clienteId: item.clienteId,
    profesionalId: item.profesionalId,
    servicioId: item.servicioId,
    latitud: item.latitud,
    longitud: item.longitud,
    ubicacion: item.ubicacion,
    descripcion: item.descripcion,
    fechaRequerida: item.fechaRequerida,
    presupuestoEstimado: item.presupuestoEstimado,
    presupuestoFinal: item.presupuestoFinal,
    estado: item.estado,
    distanciaKm: item.distanciaKm,
    costoTraslado: item.costoTraslado,
    incentivo: item.incentivo,
    fechaAceptacion: item.fechaAceptacion,
    fechaCompletacion: item.fechaCompletacion,
    calificacionProfesional: item.calificacionProfesional,
    comentarioProfesional: item.comentarioProfesional,
    calificacionCliente: item.calificacionCliente,
    comentarioCliente: item.comentarioCliente
  };

  return { ...payload, ...overrides };
}

function renderProfessionalLists(items) {
  if (!proPendingList || !proAssignedList) return;

  const selectedProId = Number(proSelect?.value || 0);
  const pending = (items || []).filter((item) => item.estado === 'Pendiente');
  const assigned = (items || []).filter((item) => Number(item.profesionalId || 0) === selectedProId && item.estado !== 'Rechazado');

  proPendingList.innerHTML = '';
  if (pending.length === 0) {
    proPendingList.innerHTML = `
      <div class="request-item">
        <strong>Sin pendientes</strong>
        <small>No hay solicitudes pendientes en este momento.</small>
      </div>`;
  } else {
    pending.slice(0, 10).forEach((item) => {
      const card = document.createElement('div');
      const distanceSummary = buildDistanceFinanceSummary(item);
      card.className = 'request-item';
      card.innerHTML = `
        <strong>${formatRequestTitle(item)}</strong>
        <small>${item.descripcion || 'Sin descripción'}</small>
        <small>Cliente: ${item.clienteNombre || 'N/A'} · Fecha: ${formatDateLabel(item.fechaRequerida)}</small>
        <small>Presupuesto: ${formatCurrency(item.presupuestoEstimado || 0)}</small>
        ${distanceSummary ? `<small>${distanceSummary}</small>` : ''}
        <div class="inline-actions">
          <button class="action-btn accept" data-action="accept" data-id="${item.id}">Aceptar</button>
          <button class="action-btn reject" data-action="reject" data-id="${item.id}">Rechazar</button>
        </div>
      `;
      proPendingList.appendChild(card);
    });
  }

  proAssignedList.innerHTML = '';
  if (!selectedProId) {
    proAssignedList.innerHTML = `
      <div class="request-item">
        <strong>Selecciona un profesional</strong>
        <small>Elige un perfil para ver sus trabajos activos.</small>
      </div>`;
    return;
  }

  if (assigned.length === 0) {
    proAssignedList.innerHTML = `
      <div class="request-item">
        <strong>Sin trabajos activos</strong>
        <small>Cuando aceptes solicitudes aparecerán en este panel.</small>
      </div>`;
    return;
  }

  assigned.slice(0, 10).forEach((item) => {
    const card = document.createElement('div');
    const distanceSummary = buildDistanceFinanceSummary(item);
    card.className = 'request-item';
    const canComplete = item.estado === 'Aceptado';
    card.innerHTML = `
      <strong>${formatRequestTitle(item)}</strong>
      <small>Estado: ${item.estado}</small>
      <small>${item.descripcion || 'Sin descripción'}</small>
      <small>Presupuesto: ${formatCurrency(item.presupuestoEstimado || 0)}</small>
      ${distanceSummary ? `<small>${distanceSummary}</small>` : ''}
      <div class="inline-actions">
        ${canComplete ? `<button class="action-btn complete" data-action="complete" data-id="${item.id}">Marcar completado</button>` : ''}
        <button class="action-btn complete" data-open-chat="${item.id}">Abrir chat</button>
      </div>
    `;
    proAssignedList.appendChild(card);
  });
}

function fillProfessionalSelector() {
  if (!proSelect) return;

  proSelect.innerHTML = '';
  if (cachedProfesionales.length === 0) {
    proSelect.innerHTML = '<option value="">No hay profesionales disponibles</option>';
    return;
  }

  cachedProfesionales.forEach((pro) => {
    const option = document.createElement('option');
    option.value = String(pro.id);
    option.textContent = pro.usuarioNombre || `Profesional #${pro.id}`;
    proSelect.appendChild(option);
  });

  const preferredProfessionalId = Number(currentSession?.profesionalId || lastRegisteredProfessionalId || 0);
  if (preferredProfessionalId && cachedProfesionales.some((pro) => Number(pro.id) === preferredProfessionalId)) {
    proSelect.value = String(preferredProfessionalId);
  }

  proSelect.disabled = !!currentSession?.profesionalId;
}

async function loadProfessionalDashboard() {
  try {
    const response = await fetch(buildRoleAwareRequestsUrl());
    if (!response.ok) throw new Error('No se pudieron recuperar solicitudes para profesionales.');
    const requests = await response.json();
    cachedRequests = Array.isArray(requests) ? requests : [];
    renderProfessionalLists(cachedRequests);
    fillChatRequestSelect();
    if (proState) {
      proState.textContent = currentSession?.profesionalId ? 'Sesión profesional activa' : 'Panel actualizado';
    }
  } catch (error) {
    console.error(error);
    if (proState) proState.textContent = 'Error al cargar';
    if (proPendingList) {
      proPendingList.innerHTML = `
        <div class="request-item">
          <strong>Error de carga</strong>
          <small>No fue posible recuperar la cola de solicitudes.</small>
        </div>`;
    }
  }
}

async function updateRequestStatus(requestId, action) {
  if (!currentSession?.usuarioId || (currentSession.rol !== 'Profesional' && currentSession.rol !== 'Administrador')) {
    if (proFeedback) proFeedback.textContent = 'Debes iniciar sesión como profesional o administrador para cambiar estados.';
    return;
  }

  try {
    const listRes = await fetch(buildRoleAwareRequestsUrl());
    if (!listRes.ok) throw new Error('No se pudo consultar el detalle de la solicitud.');
    const requests = await listRes.json();
    const selected = (requests || []).find((item) => Number(item.id) === Number(requestId));
    if (!selected) throw new Error('Solicitud no encontrada.');

    const selectedProId = Number(proSelect?.value || 0);
    const nowIso = new Date().toISOString();

    let overrides = {};
    if (action === 'accept') {
      if (!selectedProId) throw new Error('Selecciona un profesional antes de aceptar.');
      overrides = {
        estado: 'Aceptado',
        profesionalId: selectedProId,
        fechaAceptacion: nowIso
      };
    } else if (action === 'reject') {
      overrides = {
        estado: 'Rechazado',
        profesionalId: null
      };
    } else if (action === 'complete') {
      if (!selectedProId) throw new Error('Selecciona un profesional antes de completar.');
      overrides = {
        estado: 'Completado',
        profesionalId: selectedProId,
        fechaCompletacion: nowIso,
        presupuestoFinal: selected.presupuestoFinal ?? selected.presupuestoEstimado
      };
    }

    const payload = buildUpsertPayload(selected, overrides);
    const updateRes = await fetch(`/api/SolicitudesTrabajo/${selected.id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!updateRes.ok) {
      throw new Error(await extractApiError(updateRes));
    }

    if (proFeedback) proFeedback.textContent = 'Estado actualizado correctamente.';
    await Promise.all([loadProfessionalDashboard(), loadRequests(), loadCoordinationDashboard()]);
  } catch (error) {
    console.error(error);
    if (proFeedback) proFeedback.textContent = error.message || 'No se pudo actualizar la solicitud.';
  }
}

async function loadRequests() {
  if (!requestListContainer) return;

  try {
    const response = await fetch(buildRoleAwareRequestsUrl());
    if (!response.ok) {
      throw new Error('No se pudo obtener el listado de solicitudes.');
    }

    const requests = await response.json();
    cachedRequests = Array.isArray(requests) ? requests : [];
    const filteredRequests = currentSession?.clienteId
      ? cachedRequests.filter((item) => Number(item.clienteId || 0) === Number(currentSession.clienteId))
      : cachedRequests;

    renderRequestList(filteredRequests);
    fillChatRequestSelect();
    renderServiceMap().catch((error) => console.error('No se pudo actualizar el mapa.', error));

    if (requestState && currentSession?.clienteId) {
      requestState.textContent = 'Sesión cliente activa';
    }
  } catch (error) {
    console.error(error);
    requestListContainer.innerHTML = `
      <div class="request-item">
        <strong>Error al cargar</strong>
        <small>No se pudo recuperar el historial de solicitudes.</small>
      </div>`;
  }
}

function fillRequestSelectors() {
  if (requestServiceSelect) {
    requestServiceSelect.innerHTML = '';

    if (cachedServicios.length === 0) {
      requestServiceSelect.innerHTML = '<option value="">No hay servicios disponibles</option>';
    } else {
      cachedServicios.forEach((servicio) => {
        const option = document.createElement('option');
        option.value = String(servicio.id);
        option.textContent = servicio.nombre;
        requestServiceSelect.appendChild(option);
      });
    }
  }

  if (requestClientSelect) {
    requestClientSelect.innerHTML = '';

    if (cachedClientes.length === 0) {
      requestClientSelect.innerHTML = '<option value="">No hay clientes disponibles</option>';
    } else {
      cachedClientes.forEach((cliente) => {
        const option = document.createElement('option');
        option.value = String(cliente.id);
        option.textContent = cliente.usuarioNombre || `Cliente #${cliente.id}`;
        requestClientSelect.appendChild(option);
      });

      const preferredClientId = Number(currentSession?.clienteId || lastRegisteredClientId || 0);
      if (preferredClientId && cachedClientes.some((cliente) => Number(cliente.id) === preferredClientId)) {
        requestClientSelect.value = String(preferredClientId);
      }
    }

    requestClientSelect.disabled = !!currentSession?.clienteId;
  }
}

async function publishRequest() {
  if (!requestClientSelect || !requestServiceSelect || !requestDateInput || !requestBudgetInput || !requestLocationInput || !requestDescriptionInput) {
    return;
  }

  const clienteId = Number(requestClientSelect.value);
  const servicioId = Number(requestServiceSelect.value);
  const presupuesto = Number(requestBudgetInput.value);
  const ubicacion = requestLocationInput.value.trim();
  const descripcion = requestDescriptionInput.value.trim();
  const fecha = requestDateInput.value;

  if (!clienteId || !servicioId || !fecha || presupuesto <= 0 || ubicacion.length < 3 || descripcion.length < 10) {
    if (requestFeedback) {
      requestFeedback.textContent = 'Revisa cliente, servicio, fecha, ubicación y descripción (mínimo 10 caracteres).';
    }
    return;
  }

  if (!currentSession?.usuarioId || (currentSession.rol !== 'Cliente' && currentSession.rol !== 'Administrador')) {
    if (requestFeedback) {
      requestFeedback.textContent = 'Inicia sesión como cliente para publicar solicitudes reales.';
    }
    return;
  }

  const requestCoords = await resolveCoordinatesForPayload(ubicacion);

  const payload = {
    usuarioOperadorId: Number(currentSession.usuarioId || 0),
    clienteId,
    profesionalId: null,
    servicioId,
    latitud: requestCoords.lat,
    longitud: requestCoords.lng,
    ubicacion,
    descripcion,
    fechaRequerida: new Date(`${fecha}T12:00:00`).toISOString(),
    presupuestoEstimado: presupuesto,
    presupuestoFinal: null,
    estado: 'Pendiente',
    distanciaKm: null,
    costoTraslado: null,
    incentivo: null,
    fechaAceptacion: null,
    fechaCompletacion: null,
    calificacionProfesional: null,
    comentarioProfesional: null,
    calificacionCliente: null,
    comentarioCliente: null
  };

  if (requestSubmitButton) requestSubmitButton.setAttribute('disabled', 'true');
  if (requestState) requestState.textContent = 'Publicando...';
  if (requestFeedback) requestFeedback.textContent = 'Enviando solicitud al backend...';

  try {
    const response = await fetch('/api/SolicitudesTrabajo', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      const errorBody = await response.text();
      throw new Error(errorBody || 'No se pudo publicar la solicitud.');
    }

    if (requestState) requestState.textContent = 'Solicitud publicada';
    if (requestFeedback) requestFeedback.textContent = 'Listo: la solicitud fue creada correctamente.';

    await Promise.all([loadRequests(), loadCoordinationDashboard()]);
  } catch (error) {
    console.error(error);
    if (requestState) requestState.textContent = 'Error al publicar';
    if (requestFeedback) requestFeedback.textContent = 'No se pudo publicar. Verifica clientes y servicios existentes.';
  } finally {
    if (requestSubmitButton) requestSubmitButton.removeAttribute('disabled');
  }
}

function updateInstallButtonState(visible, label = 'Instalar app') {
  if (!installAppButton) return;
  installAppButton.hidden = !visible;
  installAppButton.textContent = label;
}

async function promptInstallApp() {
  if (!deferredInstallPrompt) {
    updateInstallButtonState(false);
    return;
  }

  deferredInstallPrompt.prompt();
  const { outcome } = await deferredInstallPrompt.userChoice;
  deferredInstallPrompt = null;
  updateInstallButtonState(false, outcome === 'accepted' ? 'App instalada' : 'Instalación pendiente');
}

function registerPwaFeatures() {
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('/service-worker.js')
        .then((registration) => registration.update())
        .catch((error) => {
          console.error('No se pudo registrar el service worker.', error);
        });
    });
  }

  window.addEventListener('beforeinstallprompt', (event) => {
    event.preventDefault();
    deferredInstallPrompt = event;
    updateInstallButtonState(true, 'Instalar app');
  });

  window.addEventListener('appinstalled', () => {
    deferredInstallPrompt = null;
    updateInstallButtonState(false, 'App instalada');
    if (loginFeedback && !loginFeedback.textContent) {
      loginFeedback.textContent = 'La app ya puede abrirse como acceso directo instalable en Android.';
    }
  });
}

roleTabs.forEach((tab) => {
  tab.addEventListener('click', () => setActiveRole(tab.dataset.roleTab || 'cliente'));
});

authTabs.forEach((tab) => {
  tab.addEventListener('click', () => setActiveAuthTab(tab.dataset.authTab || 'login'));
});

accountRoleButtons.forEach((button) => {
  button.addEventListener('click', () => setAccountRole(button.dataset.accountRole || 'cliente'));
});

sectorButtons.forEach((button) => {
  button.addEventListener('click', () => {
    invalidateProfessionalPaymentIfNeeded();
    const sector = button.dataset.sector || '';
    setSelectedSector(sector);
    if (aiRubroResult) {
      aiRubroResult.textContent = `Perfecto: tu rubro principal quedó orientado hacia ${sector}.`;
    }
  });
});

if (aiRubroButton) {
  aiRubroButton.addEventListener('click', () => {
    invalidateProfessionalPaymentIfNeeded();
    const analysis = suggestSectorFromDescription(aiRubroDescription?.value || '');
    setSelectedSector(analysis.sector);

    if (aiRubroResult) {
      aiRubroResult.textContent = analysis.confident
        ? `La IA sugiere: ${analysis.sector}. Puedes ajustar la selección si quieres.`
        : 'No detecté una coincidencia fuerte, pero te orientaría primero hacia CONSTRUCCION Y SERVICIOS GENERALES. Revisa y ajústalo manualmente si hace falta.';
    }
  });
}

if (termsCheckbox) {
  termsCheckbox.addEventListener('change', updateRegisterGate);
}

if (proFarJobsInput) {
  proFarJobsInput.addEventListener('change', syncDistanceBonusInputState);
}

if (startPaymentButton) {
  startPaymentButton.addEventListener('click', startProfessionalPayment);
}

if (verifyPaymentButton) {
  verifyPaymentButton.addEventListener('click', verifyMercadoPagoPayment);
}

if (confirmPaymentButton) {
  confirmPaymentButton.addEventListener('click', confirmProfessionalPayment);
}

if (loginButton) {
  loginButton.addEventListener('click', handleLogin);
}

if (logoutButton) {
  logoutButton.addEventListener('click', clearSession);
}

if (installAppButton) {
  installAppButton.addEventListener('click', () => {
    promptInstallApp().catch((error) => console.error(error));
  });
}

if (notificationsRefreshButton) {
  notificationsRefreshButton.addEventListener('click', loadNotifications);
}

if (coordRefreshButton) {
  coordRefreshButton.addEventListener('click', loadCoordinationDashboard);
}

if (coordExportButton) {
  coordExportButton.addEventListener('click', exportCoordinationReport);
}

if (coordPdfButton) {
  coordPdfButton.addEventListener('click', exportCoordinationPdf);
}

[coordDateRange, coordCityFilter, coordRubroFilter].forEach((element) => {
  if (!element) return;
  element.addEventListener('change', loadCoordinationDashboard);
});

if (registerContinueButton) {
  registerContinueButton.addEventListener('click', submitRegistration);
}

[
  registerNameInput,
  registerEmailInput,
  registerPhoneInput,
  registerDniInput,
  registerBirthDateInput,
  registerLocationInput,
  proExperienceInput,
  proRateInput,
  proReachInput,
  proGoalInput,
  proFarJobsInput,
  proDistanceBonusInput,
  proDescriptionInput
].forEach((element) => {
  if (element) {
    element.addEventListener('input', () => invalidateProfessionalPaymentIfNeeded());
    element.addEventListener('change', () => invalidateProfessionalPaymentIfNeeded());
  }
});

[servicePicker, urgencyPicker, budgetRange, messageInput].forEach((element) => {
  if (element) {
    element.addEventListener('input', updatePreview);
    element.addEventListener('change', updatePreview);
  }
});

if (requestSubmitButton) {
  requestSubmitButton.addEventListener('click', publishRequest);
}

if (requestRefreshButton) {
  requestRefreshButton.addEventListener('click', () => {
    if (requestState) requestState.textContent = 'Actualizando...';
    loadRequests().finally(() => {
      if (requestState) requestState.textContent = 'Listo para publicar';
    });
  });
}

[mapAudienceFilter, mapRadiusFilter].forEach((element) => {
  if (!element) return;

  element.addEventListener('change', () => {
    if (mapStatus) mapStatus.textContent = 'Actualizando mapa...';
    renderServiceMap().catch((error) => console.error('No se pudo filtrar el mapa.', error));
  });
});

if (useCurrentLocationButton) {
  useCurrentLocationButton.addEventListener('click', () => {
    requestCurrentLocationForMap().catch((error) => {
      console.error('No se pudo activar la ubicación actual.', error);
    });
  });
}

if (routeNearestButton) {
  routeNearestButton.addEventListener('click', () => {
    const nearestItem = getNearestVisibleItem();
    previewRouteToItem(nearestItem).catch((error) => console.error('No se pudo calcular la ruta más cercana.', error));
  });
}

if (mapRefreshButton) {
  mapRefreshButton.addEventListener('click', () => {
    if (mapStatus) mapStatus.textContent = 'Actualizando mapa...';
    renderServiceMap().catch((error) => console.error('No se pudo refrescar el mapa.', error));
  });
}

if (requestListContainer) {
  requestListContainer.addEventListener('click', (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;
    const requestId = Number(target.dataset.openChat || 0);
    if (!requestId) return;
    openChatForRequest(requestId);
  });
}

if (proSelect) {
  proSelect.addEventListener('change', () => {
    if (proState) proState.textContent = 'Profesional seleccionado';
    loadProfessionalDashboard();
  });
}

if (proRefreshButton) {
  proRefreshButton.addEventListener('click', () => {
    if (proState) proState.textContent = 'Actualizando panel';
    loadProfessionalDashboard();
  });
}

if (proPendingList) {
  proPendingList.addEventListener('click', (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;
    const requestId = Number(target.dataset.openChat || 0);
    if (requestId) {
      openChatForRequest(requestId);
      return;
    }
    const action = target.dataset.action;
    const id = Number(target.dataset.id || 0);
    if (!action || !id) return;
    updateRequestStatus(id, action);
  });
}

if (proAssignedList) {
  proAssignedList.addEventListener('click', (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;
    const requestId = Number(target.dataset.openChat || 0);
    if (requestId) {
      openChatForRequest(requestId);
      return;
    }
    const action = target.dataset.action;
    const id = Number(target.dataset.id || 0);
    if (!action || !id) return;
    updateRequestStatus(id, action);
  });
}

if (notificationsList) {
  notificationsList.addEventListener('click', (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;
    const id = Number(target.dataset.notificationRead || 0);
    if (!id) return;
    markNotificationAsRead(id);
  });
}

if (coordAdminUsersBody) {
  coordAdminUsersBody.addEventListener('click', async (event) => {
    const target = event.target;
    if (!(target instanceof HTMLElement)) return;

    const action = target.dataset.adminAction || '';
    const userId = Number(target.dataset.userId || 0);
    const paymentId = Number(target.dataset.paymentId || 0);

    if (!action) return;

    try {
      if (action === 'toggle-active' && userId) {
        const currentActive = target.dataset.currentActive === 'true';
        if (currentActive && !window.confirm('¿Quieres suspender esta cuenta desde el panel admin?')) {
          return;
        }

        await runAdminUserAction(
          userId,
          {
            activo: !currentActive,
            motivo: currentActive ? 'Suspensión manual desde panel ejecutivo' : 'Reactivación manual desde panel ejecutivo'
          },
          currentActive ? 'Cuenta suspendida' : 'Cuenta reactivada');
        return;
      }

      if (action === 'toggle-verify' && userId) {
        const currentVerified = target.dataset.currentVerified === 'true';
        await runAdminUserAction(
          userId,
          {
            verificadoRenaper: !currentVerified,
            motivo: !currentVerified ? 'Validación manual de identidad' : 'Reversión manual de verificación'
          },
          !currentVerified ? 'Identidad verificada' : 'Verificación retirada');
        return;
      }

      if (action === 'toggle-notifications' && userId) {
        const currentNotify = target.dataset.currentNotify === 'true';
        await runAdminUserAction(
          userId,
          {
            recibeNotificaciones: !currentNotify,
            motivo: currentNotify ? 'Silencio operativo manual' : 'Rehabilitación de notificaciones'
          },
          !currentNotify ? 'Notificaciones habilitadas' : 'Notificaciones silenciadas');
        return;
      }

      if (action === 'approve-payment' && paymentId) {
        await runAdminPaymentApproval(paymentId);
      }
    } catch (error) {
      console.error(error);
      setAdminFeedback(`No se pudo ejecutar la acción admin: ${error.message || 'intenta nuevamente.'}`);
    }
  });
}

if (chatRequestSelect) {
  chatRequestSelect.addEventListener('change', () => {
    selectedChatRequestId = Number(chatRequestSelect.value || 0);
    loadChatMessages();
  });
}

if (chatSendButton) {
  chatSendButton.addEventListener('click', sendChatMessage);
}

setActiveRole('cliente');
setActiveAuthTab('login');
setSelectedSector('TECNOLOGIA Y SISTEMAS');
setAccountRole('cliente');
setTodayAsDefaultDate();
syncDistanceBonusInputState();
resetProfessionalPaymentState();
applySessionUI();
updateRegisterGate();
updatePreview();
registerPwaFeatures();

async function loadHomeData() {
  try {
    const [healthRes, rubrosRes, serviciosRes, profesionalesRes, clientesRes] = await Promise.all([
      fetch('/health'),
      fetch('/api/Rubros'),
      fetch('/api/Servicios'),
      fetch('/api/Profesionales'),
      fetch('/api/Clientes')
    ]);

    if (!healthRes.ok || !rubrosRes.ok || !serviciosRes.ok || !profesionalesRes.ok || !clientesRes.ok) {
      throw new Error('No se pudieron obtener los datos iniciales.');
    }

    const health = await healthRes.json();
    const rubros = await rubrosRes.json();
    const servicios = await serviciosRes.json();
    const profesionales = await profesionalesRes.json();
    const clientes = await clientesRes.json();

    cachedRubros = Array.isArray(rubros) ? rubros : [];
    cachedServicios = Array.isArray(servicios) ? servicios : [];
    cachedClientes = Array.isArray(clientes) ? clientes : [];
    cachedProfesionales = Array.isArray(profesionales) ? profesionales : [];

    healthStatus.textContent = health.status === 'healthy' ? 'Online' : 'Revisar estado';
    apiState.textContent = health.status === 'healthy' ? 'API activa' : 'API con aviso';

    rubroCount.textContent = String(cachedRubros.length);
    serviceCount.textContent = String(cachedServicios.length);
    proCount.textContent = String(cachedProfesionales.length);

    if (servicePicker) {
      servicePicker.innerHTML = '';

      if (cachedServicios.length === 0) {
        const demoOptions = [
          'Electricista urgente',
          'Plomería para hogar',
          'Soporte técnico a domicilio'
        ];

        demoOptions.forEach((name, index) => {
          const option = document.createElement('option');
          option.value = `demo-${index + 1}`;
          option.textContent = name;
          servicePicker.appendChild(option);
        });
      } else {
        cachedServicios.slice(0, 12).forEach((servicio) => {
          const option = document.createElement('option');
          option.value = String(servicio.id ?? servicio.nombre ?? '');
          option.textContent = servicio.nombre;
          servicePicker.appendChild(option);
        });
      }
    }

    fillRequestSelectors();
    fillProfessionalSelector();
    fillCoordinationRubroFilter();

    rubrosContainer.innerHTML = '';

    const visibleRubros = cachedRubros.slice(0, 10);
    visibleRubros.forEach((rubro) => {
      const chip = document.createElement('span');
      chip.className = 'chip';
      chip.textContent = rubro.nombre;
      rubrosContainer.appendChild(chip);
    });

    if (cachedRubros.length === 0) {
      rubrosContainer.innerHTML = '<span class="chip">Todavía no hay rubros cargados</span>';
    }

    await Promise.all([loadRequests(), loadProfessionalDashboard(), loadCoordinationDashboard()]);
    renderServiceMap()
      .then(() => {
        const isNativePlatform = typeof window.Capacitor?.isNativePlatform === 'function' && window.Capacitor.isNativePlatform();
        if (isNativePlatform) {
          return requestCurrentLocationForMap({ silent: true, autoWatch: true });
        }

        if (currentDeviceLocation) {
          updateCurrentLocationHint(currentDeviceLocation, currentDeviceLocation.label);
        }

        return Promise.resolve();
      })
      .catch((error) => console.error('No se pudo renderizar el mapa.', error));
    updatePreview();
  } catch (error) {
    console.error(error);
    setOfflineState();
  }
}

loadHomeData().finally(() => {
  startCoordinationPolling();
  restoreSavedSession();
});
