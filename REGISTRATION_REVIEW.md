# Revisión de acceso y registro — 25/09/2026

## Causas reproducidas

- Chrome móvil carga el formulario, pero app.js se detenía en la línea 305 con `Cannot access mapStatus before initialization`. Los eventos de registro no llegaban a conectarse. Los manejadores inline abrían una pantalla que parecía funcionar, ocultando el fallo.
- El frontend pedía `/api/Auth/usuarios/{id}/context`, una ruta inexistente. Render respondía HTML con estado 200; recuperar la sesión fallaba al interpretar JSON.
- El alta cliente creaba usuario y perfil por separado, sin token de acceso y con posibilidad de cuentas incompletas.
- La migración de billetera carecía del diseñador/contexto y el snapshot estaba desactualizado. Una base nueva no se inicializaba por cambios pendientes del modelo, aunque `/health` decía healthy.
- La validación decimal dependía de la cultura del servidor: `0.01` provocaba un error 500 con configuración regional en español. Las fechas se comparaban en UTC, rechazando el día local al avanzar la fecha UTC.

## Correcciones

Inicialización JavaScript, formulario único sin datos ficticios, errores visibles de login y registro, alta cliente transaccional con JWT y recuperación autenticada de altas incompletas. Contexto de sesión protegido y conservación del token al recargar. Geocodificación y llamadas API con tiempo límite. Alta profesional autenticada antes del pago y reintentos sin actualizar usuarios mediante rutas exclusivas del administrador. Un solo service worker para caché y push. Metadatos de migración reparados conservando su identificador. Health comprueba conexión y migraciones pendientes. Importes independientes de cultura y fechas de solicitudes según el desfase horario del dispositivo.

## Verificación reproducible

```powershell
dotnet build AppServicios.Api/AppServicios.Api.csproj --no-restore
dotnet ef migrations has-pending-model-changes --project AppServicios.Api --no-build
python -m pip install playwright "psycopg[binary]"
python tests/auth_flow.py
```

Las pruebas crean y eliminan una instancia PostgreSQL temporal en un puerto libre. Requieren Chrome, .NET 10 y PostgreSQL; `PG_BIN` permite cambiar la ubicación de sus ejecutables. Prueban registro, login, recarga, logout, recuperación de altas incompletas, duplicados, autorización, rollback, errores visibles, solicitudes, chat, billetera y alta profesional. Las llamadas externas del navegador están bloqueadas y el pago profesional se simula solo en la base temporal. No se usan cuentas de producción.

## Android y publicación

El AAB anterior era 1.0.4 (versionCode 5); su app.js incorporado estaba atrasado respecto del repositorio. La configuración del bundle y APP_PUBLIC_URL apuntan a `https://appservicios-mn6i.onrender.com`.

La nueva versión es 1.0.5 (versionCode 6), sincronizada con Capacitor. Salida: `android/app/build/outputs/bundle/release/app-release.aab`.

**El dispositivo carga la web de Render. Cambiar el AAB no despliega el backend ni corrige por sí solo la aplicación instalada.** Se deben publicar juntos backend y wwwroot, dejando que se aplique la migración pendiente. Verificar `/health`, que el contexto sin token devuelva 401 y que el registro abra el panel con una cuenta de prueba autorizada. Luego distribuir el nuevo AAB si corresponde. Estos cambios no se publicaron en Render ni en Google Play durante esta revisión.

No se realizó una compra real ni una prueba de instalación en un teléfono Android físico. La verificación de interfaz utiliza Chrome con tamaño móvil y API real local.
