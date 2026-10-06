# Seguridad y privacidad

## Implementado

| Tema | Detalle |
|---|---|
| Contraseñas | ASP.NET Identity (hash PBKDF2), mínimo 8 + letra + número, bloqueo 15 min tras 5 fallos |
| Sesión | Access JWT de 15 min (HS256, emisor/audiencia/expiración validados, `MapInboundClaims=false`); refresh opaco de 48 bytes, **solo su SHA-256 en BD**, rotación en cada uso |
| Robo de refresh | Reusar un refresh ya rotado revoca **todas** las sesiones del usuario |
| Enumeración de cuentas | Login responde igual para correo inexistente y contraseña errónea |
| Autorización | Todo endpoint privado exige JWT y filtra por el usuario del token (candidato/usuario); probado: otro usuario recibe 404 |
| Rate limiting | Global por IP y política estricta `auth` (10/min) en login/registro/refresh/borrado |
| Validación | DataAnnotations + `IValidatableObject` → 400 ProblemDetails; límites de tamaño en listas |
| Secretos | Nada hardcodeado: `Jwt__Key`, cadenas de conexión por variables de entorno. En Development la clave JWT se genera en `.dev/jwt.key` (git-ignored). La contraseña del usuario demo solo existe en `appsettings.Development.json` y el seed está apagado fuera de Development |
| Móvil | Tokens en Keychain/Keystore; en web (solo desarrollo) `localStorage` |
| Contenedores | API y Worker corren como usuario no-root |
| CORS | Abierto solo en Development; en otros entornos, lista `Cors:Origins` |
| Errores | ProblemDetails sin trazas; `correlationId` para soporte |
| Eliminación | `POST /account/delete` con contraseña: borra perfil, preferencias, matches, postulaciones, CVs, tokens y `AiUsage` |
| Subida de CV | Extensión + MIME + **firma real** (`%PDF-` / ZIP con `word/document.xml`), máx. 5 MB, rechazo de zip bombs (>30 MB descomprimidos), nombre en disco aleatorio (GUID), nombre visible saneado, política de rate limit `upload`, almacenamiento fuera de carpetas servidas, errores de lectura nunca son 500 |
| Privacidad del CV | El archivo no se expone por ninguna ruta; se analiza una vez; borrar CV/cuenta elimina fila **y** archivo; solo el texto necesario llegaría a un proveedor de IA (fase 8) |
| Embeddings | Solo viaja el texto necesario (cargos, habilidades, experiencia; **nunca** nombre, correo ni contacto). Ollama **no tiene autenticación**: nunca se expone a internet; se usa en loopback, red privada/túnel o detrás de un proxy con TLS y token (`Embeddings__ApiKey`). Ver [OLLAMA.md](OLLAMA.md) |
| Notificaciones | Los tokens de dispositivo se validan por formato, pertenecen a **una sola cuenta** (si el teléfono inicia sesión con otra, el token se mueve) y se borran al cerrar sesión o al eliminar la cuenta. El destino de un aviso tocado se valida (solo un GUID real abre una oferta). Límite de 3 avisos de prueba por hora y 4 avisos reales por día. Los avisos solo llevan cargo, empresa y hora. `Push__AccessToken` solo por variable de entorno |
| Historial del tracker | `GET /applications/{id}/history` solo responde por tarjetas del propio usuario (otro usuario recibe 404) y se borra junto con la tarjeta o la cuenta (cascada). Solo guarda etapas y fechas; las notas nunca se copian al historial ni a los avisos |
| IA | **Apagada por defecto.** Las claves de proveedores solo por variable de entorno (`Ai__Providers__{n}__ApiKey`); nunca se registran en logs, errores ni en el tablero (que solo dice si hay clave). A los modelos solo viaja texto público de la oferta (sin datos de usuarios ni nombre de empresa). El texto de la oferta es **no confiable**: va entre marcas declaradas como dato, y la respuesta se valida y se verifica contra ese mismo texto; aun si un modelo obedece una inyección, solo puede llenar un hueco con un valor respaldado por el texto. Presupuestos (global, sistema, por plan) acotan el gasto máximo. Los mensajes de error de proveedores no incluyen cuerpos de respuesta ni prompts |
| Panel de administración | `/api/v1/admin/*` exige un correo de `Admin__Emails`. Es solo lectura y no muestra datos personales. **Registra primero la cuenta de administrador y después agrégala a la lista** (no hay confirmación de correo todavía: quien registre antes ese correo lo controlaría) |

## Pendiente (con fase)

- Escaneo antivirus de los CV subidos y límite de páginas/tiempo de parseo bajo carga.
- Confirmación de correo y recuperación de contraseña (antes de producción).
- Cabeceras de seguridad/HSTS y TLS (detrás del proxy en producción).
- Rotación de claves JWT (`kid`) y secret manager en producción.
- Retención: política de borrado de historial y de `AiUsage`.
- Revisión legal de LPDP (Ley 29733) antes de lanzar: consentimiento explícito, finalidad, derechos ARCO.

## Reporte de vulnerabilidades

Pendiente definir canal antes del lanzamiento público.
