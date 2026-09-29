# Identity and mobile panels

All authenticated features are grouped in role-appropriate collapsible panels. Opening one closes the others. Panel titles remain visible. Map size is refreshed when its panel opens. Existing request/chat actions open the destination panel before navigating.

## Identity

Mi cuenta > Seguridad accepts a portrait photo and the front of the DNI (JPEG/PNG, maximum 4 MB each). Submission is optional and never grants verification. The overview, account, map profiles and request counterparts show verified/unverified labels based on server values.

Documents are stored as byte arrays in the PostgreSQL IdentidadDocumentos table, outside the public web root. The API requires authentication, checks owner/admin access and sends no-store/nosniff headers. Public DTOs contain status only, not images. The owner can remove documents; replacement or deletion clears verification.

An active configured Super Admin uses Administracion > Ver foto y DNI to compare the images and account details, then Verificar. The approval endpoint rejects accounts without documents. It records the existing administrative audit. This is an internal manual identity review, NOT a RENAPER integration, biometric verification or automatic OCR. The existing VerificadoRenaper column is retained for backward compatibility; effective verification also requires IdentidadPresentada.

The AddIdentityDocuments migration creates protected evidence storage and defaults existing accounts to no evidence. Existing claims alone do not produce a verified badge. Public user creation cannot set its own verification flag.

## Validation

The isolated PostgreSQL/Chrome suite covers unauthorized access, invalid images, upload, pending status, access isolation, no-cache image retrieval, restricted approval, replacement/deletion, UI upload, narrow-screen layout, accordion navigation, registration, request creation, chat and logout.
