# SERVILABS 1.0.7 (versionCode 8)

AAB: android/app/build/outputs/bundle/release/app-release.aab

## Notas para Google Play

<es-419>
Agregamos foto de perfil e iniciales en Mi cuenta y mensajes, con acceso al perfil público.
Mejoramos el ingreso del celular con selector de país y formato para Argentina.
Incorporamos comprobaciones de la línea con el operador y el regreso a la app después de autorizar.
La foto pública permanece separada de los documentos privados de identidad.
</es-419>

## Operación

Se requiere registrar en Telefónica la URL https://appservicios-mn6i.onrender.com/api/Verificaciones/callback. Las comprobaciones usan las credenciales existentes en Render. El sandbox se identifica como prueba y no acredita identidad ni teléfono real. Consultar OPEN_GATEWAY_SETUP.md.

Pruebas: compilación .NET; migración sin diferencias; flujo aislado con PostgreSQL, Chrome móvil y proveedor HTTP simulado; pruebas de notificaciones nativas. La cobertura de una línea real debe comprobarse desde el dispositivo autorizado.
