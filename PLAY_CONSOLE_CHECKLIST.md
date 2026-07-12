# SERVILABS · Checklist Google Play Console

## Datos base de la app

- Nombre visible: `SERVILABS`
- Package name: `com.appservicios.app`
- Tipo: Aplicacion de servicios / productividad / marketplace laboral
- URL publica: `https://appservicios-mn6i.onrender.com`
- Politica de privacidad: `https://appservicios-mn6i.onrender.com/privacidad.html`
- Terminos: `https://appservicios-mn6i.onrender.com/terminos.html`

## Descripcion corta sugerida

SERVILABS conecta clientes con profesionales y oficios, con chat, ubicacion, alertas de trabajo y pagos protegidos.

## Descripcion completa sugerida

SERVILABS es una plataforma de servicios laborales que conecta clientes con profesionales, oficios y prestadores de servicios. Permite publicar necesidades, encontrar perfiles cercanos, coordinar por chat interno, recibir alertas de oportunidades y avanzar hacia pagos protegidos dentro de la app.

La app busca poner en valor trabajos esenciales y profesiones que muchas veces quedan fuera de las plataformas digitales tradicionales, ofreciendo una experiencia clara, segura e inclusiva para clientes y trabajadores.

## Permisos y explicacion para Play

### Ubicacion aproximada y precisa

Se usa para:

- mostrar profesionales y solicitudes cercanas;
- calcular distancia estimada;
- ordenar oportunidades por cercania;
- abrir rutas hacia el lugar del servicio.

La ubicacion no es obligatoria para navegar toda la app, pero mejora el mapa y la coordinacion del trabajo.

### Notificaciones

Se usan para:

- avisar a profesionales sobre nuevas solicitudes compatibles con su rubro;
- alertar mensajes nuevos del chat;
- informar cambios de estado de solicitudes y pagos.

El usuario puede aceptar o rechazar el permiso desde el sistema operativo.

### Internet

Se usa para conectar con el backend, login, solicitudes, chat, pagos, IA y notificaciones.

## Cuenta de prueba para revision

Crear en produccion una cuenta de prueba estable antes de enviar a revision.

Recomendado:

- Email: `reviewer@servilabs.app`
- Rol: Cliente
- Password: generar una clave temporal y guardarla solo en el panel/secretos.

Tambien conviene crear una cuenta profesional de prueba:

- Email: `profesional.review@servilabs.app`
- Rol: Profesional
- Rubro: Construccion y servicios generales / Electricidad o Plomeria
- Notificaciones: habilitadas

No publicar passwords en GitHub ni en documentos publicos.

## AAB firmado

Antes de subir a Play:

1. Instalar Node.js LTS si `npm` no existe en la terminal.
2. Instalar Android Studio.
3. Crear keystore real de release.
4. Guardar `android/keystore.properties` localmente. No se sube a Git.
5. Ejecutar:

```powershell
npm install
npm run cap:sync
npm run android:aab
```

Salida esperada:

```text
android/app/build/outputs/bundle/release/app-release.aab
```

## Formularios sensibles de Play

Completar con atencion:

- Seguridad de datos: declarar cuenta, ubicacion, mensajes, pagos, notificaciones y datos operativos.
- Publicidad: marcar no, salvo que luego se agreguen anuncios.
- Apps financieras: no declarar como banco. Indicar que se usa proveedor externo de pagos.
- Ubicacion: justificar por mapa, distancia y coordinacion de servicios.
- Contenido generado por usuarios: si aplica por chat/solicitudes, declarar moderacion y bloqueo/reportes.

## Bloqueadores antes de produccion fuerte

- Confirmar politica de privacidad definitiva con email/dominio real.
- Configurar cuenta de prueba para revisores.
- Probar login, registro, mapa, solicitud, chat y notificacion en Android real.
- Generar AAB con keystore real.
- Guardar keystore y passwords fuera del repositorio.
