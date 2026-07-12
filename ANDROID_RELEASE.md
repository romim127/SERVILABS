# SERVILABS · Release Android (APK / AAB)

## Métricas recomendadas para el emulador
| Parámetro | Valor recomendado |
|---|---|
| Dispositivo | Pixel 7 o Pixel 8 |
| Android | API 35 (Android 15) o API 34 |
| RAM | 2048 MB a 4096 MB |
| Almacenamiento interno | 4 GB o más |
| Orientación | Portrait |
| Gráficos | Hardware / Automatic |
| ABI | x86_64 |

## Métricas reales del proyecto
- `applicationId`: `com.appservicios.app`
- Nombre visible: `SERVILABS`
- `minSdkVersion`: `23`
- `targetSdkVersion`: `35`
- `compileSdkVersion`: `35`
- `versionCode`: `1`
- `versionName`: `1.0`

## Paso 1 · Crear el keystore de release
Antes de empezar, verificar que existan Node.js y JDK/Android Studio:

```powershell
node --version
npm --version
```

Si `npm` no aparece, instalar Node.js LTS y abrir una nueva terminal.

Luego en Android Studio:
1. `Build` → `Generate Signed App Bundle / APK`
2. Elegir `Android App Bundle` o `APK`
3. `Create new...`
4. Guardar el archivo por ejemplo en:
   `C:\App_Servicios\android\appservicios-release.jks`
5. Alias sugerido: `appservicios`

## Paso 2 · Configurar el keystore
1. Copiar `android/keystore.properties.example` como `android/keystore.properties`
2. Completar:

```properties
storeFile=appservicios-release.jks
storePassword=TU_PASSWORD
keyAlias=appservicios
keyPassword=TU_PASSWORD
```

## Paso 3 · Generar APK o AAB
### Desde Android Studio
- `Build` → `Generate Signed App Bundle / APK`
- Elegir:
  - `APK` para pruebas e instalación manual
  - `AAB` para Google Play

### Desde terminal
```powershell
cd C:\App_Servicios
npm run android:apk
npm run android:aab
```

> Nota: los scripts ya toman el JDK embebido de Android Studio en `C:\Program Files\Android\Android Studio\jbr` para evitar el error de `JAVA_HOME`.

## Salidas esperadas
- APK: `android\app\build\outputs\apk\release\app-release.apk`
- AAB: `android\app\build\outputs\bundle\release\app-release.aab`

## Recomendación
- Probar primero con el **APK** en el emulador.
- Cuando todo esté bien, generar el **AAB** para Play Store.

## Play Console

- Politica de privacidad: `https://appservicios-mn6i.onrender.com/privacidad.html`
- Terminos: `https://appservicios-mn6i.onrender.com/terminos.html`
- Checklist detallado: `PLAY_CONSOLE_CHECKLIST.md`
