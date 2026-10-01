# Seguridad de pagos y acceso — 30/09/2026

## Cambios

1. La ruta de confirmación demo responde 410 en todos los entornos. La confirmación administrativa del alta profesional consulta Mercado Pago; no acredita manualmente.
2. Cada cobro se consulta por API autenticada y se contrasta con la orden: importe decimal exacto, moneda, referencia, receptor (cuenta de `/users/me`), modo real/prueba, estado aprobado y ausencia de devolución. Dos aprobaciones para una referencia requieren revisión. No se confía en el regreso del checkout ni en campos enviados por el cliente. El token no se envía a otros hosts ni sigue redirects. Un cobro se vincula a una sola orden mediante índices únicos.
3. Las operaciones monetarias se ejecutan dentro de una transacción PostgreSQL y un bloqueo asesor compartido entre procesos. El bloqueo se toma antes de leer el saldo; auditoría y movimientos se confirman juntos. Es una decisión conservadora para el volumen inicial: serializa operaciones financieras (incluidas consultas del controlador). Escalar requiere locks por cuentas adquiridos en orden y nuevas pruebas de concurrencia. Las solicitudes con orden conservan sus participantes e importes. Los saldos inconsistentes se rechazan; no se ocultan con `Math.Max(0, ...)`.
4. El reintegro llama a Mercado Pago con clave de idempotencia estable y confirma el resultado. Si se interrumpe el guardado local después de la devolución, el reintento reconcilia el estado del proveedor. No acredita además saldo disponible al cliente. El plazo de liberación comienza al marcar completado el trabajo; un tercero no puede liberar pagos ajenos aunque estén vencidos. La liberación interna **no transfiere fondos al profesional**: la liquidación bancaria/split marketplace sigue siendo una integración distinta.
5. Pagos profesionales y recursos privados (direcciones/certificados) requieren propiedad o administración. Se impide enviar entidades anidadas para modificar cuentas y autoverificar certificados. Cada JWT comprueba cuenta activa y rol vigente. Cerrar sesión revoca ese JWT en la base; administradores reciben sesiones de 30 minutos. No se implementó autenticación de dos factores en esta entrega. Se retiró la comparación de contraseñas en texto plano.
6. Límites de escritura por cuenta y límites de acceso por email normalizado se guardan en PostgreSQL, también entre réplicas y reinicios. Los nombres se guardan como SHA-256 en contadores temporales. No se confía directamente en `CF-Connecting-IP` o `X-Forwarded-For`. El bloqueo IP heredado queda deshabilitado por defecto para no bloquear clientes detrás del proxy compartido. Los límites no sustituyen una defensa DDoS en el borde.
7. Fallos de conciliación y límites de acceso generan logs, auditoría y avisos internos para administradores (agrupados por tipo cada 10 minutos). Consultables en notificaciones y `/api/Seguridad/alertas`. No se enviaron correos ni mensajes externos.

## Turnstile: activación pendiente de claves

Crear un widget Managed en Cloudflare para `appservicios-mn6i.onrender.com`. No requiere dominio propio ni cambiar DNS.
En **Environment** del servicio Render `appservicios-mn6i`:

- `Turnstile__SiteKey`: clave pública del widget.
- `Turnstile__SecretKey`: clave secreta, solo en Render; nunca en Git ni en el AAB.
- `Turnstile__Hostname`: `appservicios-mn6i.onrender.com` (es el valor predeterminado).

Guardar y desplegar. La presencia de cualquiera de las claves activa el control; una configuración parcial bloquea el ingreso, por lo que deben cargarse juntas. `/api/Seguridad/publica` informa enabled/ready y solo la clave pública. El navegador solicita un token nuevo por ingreso/registro; el servidor valida `success`, hostname y action mediante Siteverify. Los tokens no son reutilizables. No hay bypass por fallos del proveedor. El stub HTTP de pruebas solo funciona en Development. Comprobar también el widget en el AAB instalado (WebView) antes de dar por cerrada la activación.

## Respaldos

La migración de esta entrega agrega tres tablas y un índice único nuevo. No borra ni recalcula operaciones históricas. El servidor falla al iniciar si una migración falla, en vez de servir una versión con esquema incompleto.

**Pendiente de comprobar en la cuenta Render:** plan de PostgreSQL, última copia y ventana de recuperación. No se afirma que existan copias productivas sin revisar esa pantalla.

1. Abrir PostgreSQL → Recovery en Render, comprobar que haya punto de recuperación y su fecha. El plan Free no incluye la protección de respaldos del plan pago.
2. Crear un export y conservarlo en almacenamiento privado cifrado con acceso restringido; contiene información de identidad y pagos. No guardarlo en el repositorio.
3. Probar la recuperación en una base nueva y vacía, manteniendo la base original. Comparar usuarios, órdenes, movimientos, disputas, `CobrosVerificados`, migraciones e índices. Nunca usar `--clean` sobre producción para probar.
4. Confirmar retención y responsable de revisión. Para copias externas programadas hace falta definir el destino y credenciales; no se inventó una configuración ni se activó un servicio pago.

La prueba `tests/payment_security.py` hace `pg_dump` y restaura a `restore_test` en un clúster temporal local, comprobando la recuperación de los vínculos de cobros. Esto verifica el procedimiento técnico; no reemplaza una copia de producción ni garantiza el RPO del servicio Render.

## Verificación reproducible

- `dotnet build AppServicios.Api --no-restore`
- `python tests/payment_security.py`: proveedor HTTP local, importes/monedas/receptor alterados, ocho confirmaciones y liberaciones concurrentes, fallo de reintegro y fallo local posterior al reintegro, permisos, revocación, límites, restauración, Turnstile, y bloqueo demo en Production.
- `python tests/auth_flow.py`: PostgreSQL temporal y Chrome móvil; registro, ingreso, paneles, perfil/foto, documentación, Telefónica y alta profesional.
- `node tests/native_push.cjs`

Las pruebas no usan claves de Mercado Pago ni operaciones reales. El pago real histórico no se modifica como parte del despliegue. Las operaciones antiguas pendientes de liberación se vuelven a contrastar con el proveedor antes de generar más movimientos. Una integración de webhooks firmados y conciliación automática de contracargos requiere trabajo adicional; por ahora se consulta al verificar/liberar.

Fuentes: [Mercado Pago — reintegros](https://www.mercadopago.com.ar/developers/en/reference/online-payments/checkout-api-payments/create-refund/post), [Cloudflare — validación servidor](https://developers.cloudflare.com/turnstile/get-started/server-side-validation/), [Render — recuperación](https://render.com/docs/postgresql-backups).
