# Android push notifications

Android project: servilabs-fe09a, applicationId com.appservicios.app.
Client config: android/app/google-services.json. This is not a server credential.

## Server configuration on Render

1. In Firebase Project settings > Service accounts, generate a service-account key for this project.
2. Add its JSON as a Render Secret File named firebase-admin.json (never commit it or include it in the Android bundle).
3. Set Firebase__CredentialsPath=/etc/secrets/firebase-admin.json and Firebase__ProjectId=servilabs-fe09a in the API service environment, then redeploy.
4. Ensure the Firebase Cloud Messaging API is enabled and the service account has permission to send messages.

For local use: Firebase__CredentialsPath=C:/Apps/servilabs-firebase-admin.json.
The native-status endpoint reports whether a server file exists; successful delivery still requires a real-device test.

## Behavior and verification

Install the new Android release, log in and accept the automatic Android permission prompt. Permission status and retry are under Mi cuenta > Opciones de la cuenta. Repeat on a professional account/device.

Test new matching job -> professional, acceptance/completion -> client, chat -> counterpart, both foreground and after leaving the app. Tapping a notification opens its section. Closing the account session unregisters that device; The only exit action in Mi cuenta is Cerrar sesión: it returns to the main access screen. Mi cuenta is a collapsible panel alongside requests, map, messages and wallet. Android force-stop and denied permissions prevent delivery.

Native subscriptions share the existing PushSubscriptions table using an fcm: endpoint prefix; web subscriptions retain their original format. Native endpoints derive the user from JWT, never from a client-supplied user id. Device registration transfers the subscription to the active account; logout only removes a subscription owned by that account.

Delivery failures are logged without tokens or credentials; there is currently no persistent push retry queue. In-app notifications remain in the database independently of push delivery.
