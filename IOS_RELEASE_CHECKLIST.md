# SERVILABS · Checklist iOS / App Store

## Estado actual

El repo ya tiene carpeta `ios/` creada con Capacitor y sincronizada con la app actual. Desde Windows queda lista la configuracion base, pero para compilar, firmar y subir a App Store se necesita:

- Mac con Xcode, o
- servicio de build cloud con soporte iOS.

## Datos necesarios

- Apple Developer Program activo.
- D-U-N-S disponible para cuenta de organizacion.
- Bundle ID sugerido: `com.appservicios.app`
- Nombre visible: `SERVILABS`
- URL publica: `https://appservicios-mn6i.onrender.com`
- Politica de privacidad: `https://appservicios-mn6i.onrender.com/privacidad.html`

## Preparar proyecto iOS

En una Mac con Node, Xcode y CocoaPods:

```bash
npm install
npm run ios:sync
cd ios/App
pod install
cd ../..
npm run ios:open
```

Luego en Xcode:

1. Seleccionar Team de Apple Developer.
2. Configurar Signing & Capabilities.
3. Revisar Bundle Identifier `com.appservicios.app`.
4. Generar Archive.
5. Subir a App Store Connect.

## Permisos a describir en App Store

### Ubicacion

SERVILABS usa ubicacion para mostrar profesionales y solicitudes cercanas, calcular distancias y facilitar rutas hacia el servicio.

### Notificaciones

SERVILABS usa notificaciones para avisar sobre nuevas oportunidades laborales, mensajes del chat y cambios de estado de solicitudes.

### Datos de usuario

Declarar:

- informacion de contacto;
- identificadores de usuario;
- ubicacion aproximada/precisa si se habilita;
- contenido de usuario en solicitudes y chat;
- informacion de pagos operativa, procesada por proveedor externo.

## App Review Notes sugeridas

SERVILABS conecta clientes con profesionales y oficios. Para revisar el flujo, usar la cuenta de prueba provista en App Store Connect. La app permite iniciar sesion, publicar una solicitud, consultar profesionales, usar chat interno y recibir notificaciones de oportunidades o cambios de estado.

## Pendientes

- Definir cuenta Apple Developer final.
- Configurar certificados/provisioning.
- Probar en iPhone real.
- Subir build a TestFlight antes de App Store.
