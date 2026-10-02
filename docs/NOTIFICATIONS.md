# Alertas y notificaciones (Fase 6)

Principio: **el agente avisa solo cuando vale la pena**. Un aviso de más cuesta más confianza que un aviso de menos.

## Cómo decide el agente

Cada 5 minutos el worker (`NotificationJob` → `DigestService`) revisa, por usuario con avisos activos, qué apareció desde el último aviso.
La decisión es pura y está probada (`Domain/Notifications/NotificationPolicy`), en este orden:

1. **Solo lo realmente bueno**: debe haber al menos una oferta *muy compatible* o *excelente*. Las demás pueden acompañarla, pero nunca disparan un aviso solas.
2. **Horario de descanso**: de 10 p. m. a 7 a. m. (hora de Lima) no se avisa; lo pendiente sale por la mañana.
3. **Tope diario**: máximo 4 avisos por 24 horas, sea cual sea la frecuencia.
4. **Frecuencia elegida**: instantánea (mín. 30 min entre avisos), cada 2 h, cada 6 h, o *diaria* (un resumen por día, desde las 8 a. m.).

«Nuevo» significa que la oferta **apareció después del último aviso** (o después de terminar el onboarding): lo que viste al configurar tu perfil nunca se anuncia como novedad, y ejecutar el proceso dos veces nunca duplica un aviso (la bandeja es la memoria; se guarda *antes* de enviar el push).

El texto es sobrio y no presiona: «Asistente Administrativa en Clínica Santa Aurora apareció hace 15 minutos. Encaja muy bien contigo.»

## Recordatorios del tracker (Fase 7)

`ReminderService` corre en el mismo `NotificationJob` (cada 5 min, antes del resumen de ofertas). Reglas puras y probadas en `Domain/Notifications/ReminderPolicy`:

| Aviso | Cuándo | Una sola vez por… |
|---|---|---|
| `interviewDayBefore` | Entrevista entre 24 h y 6 h adelante | fecha de entrevista |
| `interviewSoon` | Entrevista dentro de las próximas 2 h | fecha de entrevista |
| `followUp` | Postulada hace ≥ 7 días (y ≤ 30) sin ningún cambio en la tarjeta, entre 9 y 20 h | postulación (y máx. 1 por usuario al día) |

- Respetan el horario de descanso: si una entrevista es a las 8 a. m., el aviso «poco antes» sale a las 7 a. m. en lugar de despertarte.
- Reprogramar la entrevista cambia la fecha de referencia: se vuelve a avisar para la nueva, nunca dos veces para la misma.
- No cuentan contra el tope de 4 avisos/día de ofertas nuevas (son cosas que tú pediste al agendar), pero sí se desactivan con «Avisos en el celular» apagado.
- Tarjetas descartadas o sin etapa nunca generan avisos. Al tocar el aviso se abre la oferta.
- La hora que se muestra es la de Lima (`Push__UtcOffsetHours`), no la del servidor.

## Recálculo incremental

Tras cada ingesta solo se evalúan las ofertas **nuevas o cambiadas** contra cada candidato (`MatchRecomputeService.RecomputeForJobsAsync`), no todo el catálogo. El recálculo completo sigue existiendo para cuando cambia el perfil o las preferencias.

## API

| Endpoint | Para qué |
|---|---|
| `POST /api/v1/devices` `{token, platform}` | Registra el token Expo del celular (idempotente; un token pertenece a una sola cuenta; máx. 5 dispositivos activos) |
| `POST /api/v1/devices/unregister` `{token}` | Se llama al cerrar sesión |
| `GET /api/v1/notifications` | Bandeja paginada |
| `GET /api/v1/notifications/summary` | Sin leer, dispositivos activos, si el envío push está configurado |
| `POST /api/v1/notifications/{id}/read`, `/read-all` | Marcar leídos |
| `POST /api/v1/notifications/test` | Aviso de prueba (máx. 3 por hora) |

La **bandeja funciona siempre**, aunque no haya push configurado: es lo que se ve en el emulador y la red de seguridad si el celular no recibe el push.

## Activar el push real (requiere cuentas externas)

El envío usa [Expo Push](https://docs.expo.dev/push-notifications/overview/) y **no está activo por defecto** (`Push__Provider=None`).
Para activarlo hacen falta cosas que solo tú puedes crear:

1. **Proyecto EAS** (`npx eas-cli init`): genera el `projectId` que va en `app.json` → `expo.extra.eas.projectId`. Sin él, la app dice honestamente «avisos solo en tu bandeja».
2. **Credenciales FCM** (Android): proyecto Firebase + clave de cuenta de servicio, subida a EAS (`eas credentials`). iOS requiere cuenta Apple Developer.
3. **Una build de desarrollo** (`eas build --profile development`): Expo Go en Android ya no recibe push remotos desde el SDK 53, y los emuladores no reciben push reales.
4. En la API y el worker: `Push__Provider=Expo`. Opcional: `Push__AccessToken` (token de acceso de Expo, solo por variable de entorno).

Los tokens de dispositivos que Expo reporta como `DeviceNotRegistered` se desactivan solos.

## Pruebas

- Dominio: política (horario nocturno, tope, frecuencias, «solo lo muy bueno», zona horaria) y redacción.
- Cliente Expo contra un servidor simulado: forma de la petición, lotes de 100, tokens muertos, reintento ante 5xx, caída sin excepciones.
- Integración (PostgreSQL real): aviso extremo a extremo, idempotencia, horario de descanso, usuarios sin avisos, tope diario, token muerto, recálculo incremental, bandeja, dispositivos.
- Móvil: reglas puras de disponibilidad del push, destino al tocar un aviso (payload validado), textos de estado.
