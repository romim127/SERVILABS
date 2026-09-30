const parameters = new URLSearchParams(location.hash.slice(1));
const state = parameters.get('state'), ticket = parameters.get('ticket');
history.replaceState({}, '', location.pathname);
document.getElementById('continueVerification').addEventListener('click', async event => {
  const button = event.target; button.disabled = true;
  const feedback = document.getElementById('verificationFeedback');
  try {
    if (!state || !ticket) throw new Error('Volvé a iniciar la comprobación desde Mi cuenta.');
    const response = await fetch('/api/Verificaciones/abrir', { method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({state,ticket}) });
    if (!response.ok) throw new Error('El enlace venció o ya fue utilizado. Volvé a la app para intentarlo nuevamente.');
    location.assign((await response.json()).url);
  } catch(error) { feedback.textContent = error.message; }
});
