# SERVILABS / AppServicios

Plataforma digital de intermediacion laboral y servicios profesionales, pensada
para conectar clientes con trabajadores, profesionales, oficios y equipos de
servicio en un entorno transaccional, seguro y verificable.

El proyecto integra autenticacion, usuarios, profesionales, catalogo de
servicios, pagos, notificaciones push, mensajeria interna, asistente con IA y
capas progresivas de seguridad antifraude.

## Seguridad del Chat y Validacion Antifraude

SERVILABS incorpora un modulo de seguridad en cascada preparado para integrarse
con herramientas de reputacion telefonica, validacion de SIM y verificacion de
ubicacion de red. El objetivo es reducir el anonimato abusivo, proteger a
clientes y trabajadores, y dejar trazabilidad ante incidentes graves dentro de
la mensajeria interna.

El flujo esta disenado alrededor de cinco controles:

- **IPQualityScore:** detecta lineas de alto riesgo, numeros virtuales o VoIP y
  senales asociadas a fraude, spam o abuso digital.
- **MaxMind minFraud:** agrega una segunda lectura de riesgo financiero e
  identidad para operaciones sensibles.
- **Resolucion geoespacial:** transforma la ciudad de referencia en coordenadas
  utiles para verificaciones posteriores.
- **Open Gateway SIM Swap:** permite validar si una SIM fue modificada o
  duplicada recientemente antes de habilitar acciones criticas.
- **Open Gateway Device Location:** permite verificar, cuando el operador lo
  autorice, la concordancia aproximada entre la linea movil y una zona esperada.

## Como Protege a Mujeres, Trabajadores y Usuarios

Este enfoque refuerza la moderacion del chat antes de que una persona pueda
usar la mensajeria para acosar, estafar o suplantar identidad.

- **Reduce el anonimato del acosador:** los agresores pueden intentar usar
  numeros virtuales para crear cuentas descartables. La verificacion telefonica
  ayuda a detectar lineas VoIP o de alto riesgo y enviarlas a revision o bloqueo
  preventivo segun la politica interna.
- **Evita secuestros de cuentas legitimas:** si una cuenta honesta fue
  comprometida mediante SIM Swap, la validacion de red puede frenar acciones
  sensibles antes de que el atacante use el perfil para acosar, estafar o danar
  la reputacion del usuario real.
- **Aporta trazabilidad forense:** ante una falta grave o delito reportado, el
  sistema puede generar un reporte HTML interno con resultado de verificaciones,
  estado de la cuenta e identificadores de transaccion disponibles. Ese material
  debe usarse bajo criterios legales, privacidad de datos y requerimientos de
  autoridad competente.

El modulo no reemplaza la moderacion humana, la denuncia formal ni el debido
proceso. Funciona como una capa tecnica de prevencion, evidencia y control para
elevar la seguridad del ecosistema.

## Estado de Open Gateway

La base tecnica ya incluye:

- `auth_gateway.py`: gestor OAuth2 con cache de token en memoria.
- `security_bunker.py`: sandbox antifraude con IPQualityScore, MaxMind, SIM
  Swap, Device Location, reporte HTML y persistencia opcional.
- Variables placeholder en `.env.example` para configurar credenciales reales.

Hasta recibir credenciales y URLs oficiales del sandbox/productivo de
Telefonica/Open Gateway, este modulo debe permanecer aislado del flujo real de
usuarios.

## Configuracion Sensible

Los secretos deben vivir solamente en `.env` local o en variables de entorno del
hosting. No deben subirse al repositorio.

Variables principales:

```env
OPEN_GATEWAY_AUTH_URL=
OPEN_GATEWAY_CLIENT_ID=
OPEN_GATEWAY_CLIENT_SECRET=
OPEN_GATEWAY_SCOPE=
OPEN_GATEWAY_AUDIENCE=
OPEN_GATEWAY_SIM_SWAP_URL=
OPEN_GATEWAY_DEVICE_LOCATION_URL=
IQS_API_KEY=
MAXMIND_ACCOUNT_ID=
MAXMIND_LICENSE_KEY=
```

Los reportes locales se generan en `reportes_antifraude/` y estan excluidos de
Git porque pueden contener datos personales o informacion sensible.
