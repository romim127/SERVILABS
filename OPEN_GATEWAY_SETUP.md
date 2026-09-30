# Telefónica en SERVILABS

El backend ASP.NET consume las variables OPEN_GATEWAY_* existentes en Render. Los scripts Python anteriores no se ejecutan en el registro.

## Flujo
Mi cuenta → Celular y verificaciones → autorizar consulta → Comprobar. Number Verification empieza desde la conexión del usuario (usar datos móviles de esa línea). OAuth Authorization Code + PKCE, state aleatorio, vinculación al navegador, vencimiento de 10 minutos y consumo atómico de una sola vez. Los tokens del operador no se guardan ni se envían al frontend.

Android 1.0.7 (8) usa @capacitor/browser: entrega única por fragmento URL, cookie del navegador externo, callback del servidor y regreso por servilabs://verificacion sin tokens. Versiones anteriores muestran que deben actualizarse. La web funciona sin instalar Android. No se habilita el puente de Capacitor en dominios de terceros.

## Variables
Conservar CLIENT_ID, CLIENT_SECRET, TOKEN_URL y URLs NUMBER_VERIFICATION_URL, KYC_MATCH_URL, SIM_SWAP_URL y DEVICE_LOCATION_URL con prefijo OPEN_GATEWAY_. No pegar claves en la aplicación ni en Git.

- OPEN_GATEWAY_AUTHORIZE_URL: endpoint de autorización OIDC. El AUTH_URL anterior suele ser bc-authorize (CIBA); no es intercambiable. Para el sandbox oficial se deriva /apigateway/authorize de /apigateway/bc-authorize. Para otros proveedores especificar el endpoint OIDC oficial.
- OPEN_GATEWAY_REDIRECT_URI: https://appservicios-mn6i.onrender.com/api/Verificaciones/callback. Registrar exactamente esta URL en la aplicación del portal de Telefónica. Si se omite, se deriva de APP_PUBLIC_URL (o App:PublicUrl).
- OPEN_GATEWAY_MODE: sandbox por defecto; production únicamente con acceso real contratado y validado. Un endpoint que contiene sandbox siempre conserva el resultado de prueba aunque MODE sea production.
- Scopes específicos opcionales: OPEN_GATEWAY_NUMBER_VERIFICATION_SCOPE, OPEN_GATEWAY_KYC_MATCH_SCOPE, OPEN_GATEWAY_SIM_SWAP_SCOPE, OPEN_GATEWAY_DEVICE_LOCATION_SCOPE. Los valores predeterminados son los documentados por Telefónica, con propósito FraudPreventionAndDetection. El antiguo OPEN_GATEWAY_SCOPE global no se aplica a todas las APIs porque los permisos son diferentes.

GET /api/Verificaciones/configuracion (solo Administrador) devuelve nombres de configuración faltante, nunca claves ni URLs con secretos. Estar configurado no demuestra cobertura, autorización ni resultado positivo del operador.

## Comprobaciones
- Número: devicePhoneNumberVerified debe ser booleano true.
- Titular: deben coincidir idDocumentMatch y birthdateMatch. No equivale a consulta RENAPER ni a reconocimiento facial.
- SIM: swapped distingue cambio reciente (24 horas) de ausencia de cambio; nunca se infiere seguridad de un error.
- Ubicación: compara la zona autorizada por el usuario (radio 1 km); no bloquea por una discrepancia.

Solo un resultado positivo de número fuera de sandbox acredita teléfono verificado; no modifica VerificadoRenaper. Cambiar los datos usados invalida los resultados. Vigencia de 30 días y eliminación de resultados antiguos al iniciar comprobaciones. Límite de cinco intentos por cuenta cada diez minutos. Retrieve Device Phone Number y Retrieve SIM Swap Date no se consultan: no hacen falta para estas cuatro comprobaciones.

## Fotos
PerfilPublico guarda exclusivamente la imagen elegida para publicación. /api/Perfiles/{id} muestra nombre, rol, revisión de identidad y descripción/rubros/experiencia del profesional, sin DNI, correo, teléfono ni ubicación exacta. /foto sirve la imagen pública; subir o borrar requiere sesión de su propietario. IdentidadDocumentos sigue separado y privado.

## Validación
python tests/auth_flow.py utiliza PostgreSQL temporal, API real, Chrome móvil y operador HTTP simulado. Comprueba OAuth, PKCE, consentimiento, respuesta negativa/desconocida, errores, replay, navegador ajeno, cambio de teléfono, entrega al navegador nativo, fotos públicas y privacidad. No acredita una verificación real de Telefónica: esa prueba requiere una línea habilitada y el callback registrado en el portal.

Referencias:
- https://developers.opengateway.telefonica.com/docs/samplecode_numberverification
- https://developers.opengateway.telefonica.com/docs/samplecode_knowyourcustomer
- https://capacitorjs.com/docs/v7/apis/browser
