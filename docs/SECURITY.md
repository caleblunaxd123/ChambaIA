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

## Pendiente (con fase)

- Escaneo antivirus de los CV subidos y límite de páginas/tiempo de parseo bajo carga.
- Confirmación de correo y recuperación de contraseña (antes de producción).
- Cabeceras de seguridad/HSTS y TLS (detrás del proxy en producción).
- Rotación de claves JWT (`kid`) y secret manager en producción.
- Retención: política de borrado de historial y de `AiUsage`.
- Revisión legal de LPDP (Ley 29733) antes de lanzar: consentimiento explícito, finalidad, derechos ARCO.

## Reporte de vulnerabilidades

Pendiente definir canal antes del lanzamiento público.
