# ChambaIA

**Tu agente personal para encontrar trabajo.** Subes tu CV una vez y el agente entiende tu perfil, reúne ofertas, descarta
duplicados y trabajos incompatibles, calcula qué tan bien encajas, te explica por qué y te avisa sin bombardearte.

> Principio de arquitectura: **la IA no busca los empleos, la IA ayuda a entenderlos.** El pipeline es determinista y
> barato; los modelos de pago se reservan para acciones premium. Todo funciona aunque las APIs de IA estén caídas.
> Ver [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) y [docs/AI-COSTS.md](docs/AI-COSTS.md).

Estado: **Fases 1 a 4 completas** (base ejecutable, CV y onboarding, ingesta con deduplicación y embeddings con Ollama) **+ renovación de UX/diseño** (modo oscuro, deshacer, tablero con fechas de entrevista). Ver [docs/ROADMAP.md](docs/ROADMAP.md).

## Estructura

```
apps/api       ASP.NET Core 9 (Web API, Identity + JWT, EF Core, Swagger) + Domain + Infrastructure + tests
apps/worker    .NET Worker Service con Quartz.NET: ingesta de ofertas cada 30 min (fase 3) + heartbeat; alertas en fase 6
apps/mobile    Expo SDK 57 · React Native · TypeScript · Expo Router · TanStack Query · Zustand · RHF + Zod
infra/docker   Dockerfiles de API y Worker
docs/          Producto, arquitectura, roadmap, costos de IA, fuentes, seguridad
```

## Requisitos

| Herramienta | Versión probada |
|---|---|
| .NET SDK | 9.0 |
| Node.js | 22 |
| Docker | 27 (Compose v2) |
| Android Studio emulator **o** Expo Go en un teléfono | – |

## Puesta en marcha

### 1. Base de datos y Redis

```bash
docker compose up -d
```

Levanta PostgreSQL 17 con **pgvector** (puerto **5442**) y Redis 7 (puerto **6382**). Los puertos no son los
estándar a propósito, para no chocar con otros proyectos locales; se cambian en `.env` (copia de `.env.example`).

### 2. API

```bash
dotnet run --project apps/api/src/ChambaIA.Api
```

- Escucha en `http://localhost:5180` (todas las interfaces, para que el teléfono/emulador la alcance).
- En Development aplica las **migraciones** automáticamente y carga los **datos demo** (usuaria *Areli Demo* + 28 ofertas ficticias).
- Swagger: <http://localhost:5180/swagger> · Salud: `/health`, `/health/live`, `/health/ready`.
- No necesitas configurar secretos: en Development la clave JWT se genera una vez en `apps/api/src/ChambaIA.Api/.dev/jwt.key` (ignorado por git).
  Fuera de Development debes definir `Jwt__Key` (32+ caracteres) y `ConnectionStrings__Postgres`.

Cuenta demo: `areli.demo@chambaia.dev` / `Demo12345` (solo para desarrollo; definida en `appsettings.Development.json`).

### 3. App móvil

```bash
cd apps/mobile
npm install
npx expo start --port 8082      # o: npm run android / npm run web
```

(`8081` suele estar ocupado por otros proyectos; por eso los scripts usan `8082`.)

La app descubre la API sola:

| Dónde corre | URL de la API |
|---|---|
| Emulador Android | `http://10.0.2.2:5180` (ponla en `apps/mobile/.env` → `EXPO_PUBLIC_API_URL`) |
| Teléfono con Expo Go (misma Wi-Fi) | automática: usa la IP del equipo donde corre Metro |
| Web | `http://localhost:5180` |

Copia `apps/mobile/.env.example` a `.env` para activar el botón dev **«Entrar con la cuenta demo»**.
Si Windows pregunta por el firewall, permite `dotnet` en redes privadas para que el teléfono llegue a la API.

### 4. Worker (ingesta de ofertas)

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project apps/worker/ChambaIA.Worker
```

Corre la **ingesta** al arrancar (a los 15 s) y luego cada 30 minutos (`Worker:IngestionCron`): todas las fuentes →
normalización → deduplicación → retiro de ofertas vencidas → recálculo de matches. En Development incluye las ofertas
demo (`Ingestion:IncludeDemo`). Para conectar un feed JSON (formato en [docs/JOB-SOURCES.md](docs/JOB-SOURCES.md)):

```bash
Ingestion__Feeds__0__Key=clinica-x \
Ingestion__Feeds__0__Name="Clínica X" \
Ingestion__Feeds__0__Url=https://empleos.clinica-x.pe/feed.json \
Ingestion__Feeds__0__Permission="Convenio firmado 2026-10-01" \
DOTNET_ENVIRONMENT=Development dotnet run --project apps/worker/ChambaIA.Worker
```

Un feed **sin `Permission`** no se lee (la fuente queda marcada con error). En Development también puedes forzar una
corrida desde Swagger: `POST /api/v1/sources/ingest`.

### Todo en contenedores (opcional)

```bash
# .env: JWT_KEY=<32+ caracteres>
docker compose --profile full up -d --build
```

## Verificación (lo que corre antes de cada entrega)

```bash
dotnet build ChambaIA.sln            # 0 errores, 0 advertencias (TreatWarningsAsErrors)
dotnet test ChambaIA.sln             # 224 pruebas (dominio + integración); las de integración levantan PostgreSQL con Testcontainers (requiere Docker)
cd apps/mobile
npm run typecheck && npm run lint && npm test
```

## Qué incluye la Fase 1

- Autenticación completa: registro, login, refresh con **rotación y detección de reutilización**, logout, bloqueo por intentos, eliminación de cuenta.
- Modelo de datos y migración inicial (perfil, preferencias, CV, fuentes, ofertas, matches, postulaciones, uso de IA, tokens) con índices y columnas `vector(1024)`.
- **Motor de matching determinista** (reglas duras + skills + cargo + experiencia + estudios + viaje) con explicaciones en español. Sin IA, sin costo.
- API `/api/v1`: auth, profile, preferences, jobs, matches (feed, overview, interesado, descartar), applications, catalog, agent.
- **Agente conversacional por reglas** (sin LLM): «no me muestres trabajos en Ate», «mínimo 1800 soles», «busca también facturación», «máximo una hora de viaje», «solo lunes a viernes», «no quiero call center».
- App móvil: login/registro, Inicio, Empleos (filtros, búsqueda, scroll infinito, pull-to-refresh), detalle con «¿por qué encaja conmigo?», Postulaciones (tablero por etapas), Agente (memoria de preferencias + chat), Perfil (editar perfil/preferencias, cerrar sesión, eliminar cuenta), design system propio.

## Qué incluye la Fase 2

- **CV**: `POST/GET/DELETE /api/v1/resumes`. Se valida extensión, tipo MIME **y firma real del archivo** (PDF/DOCX, máx. 5 MB, anti zip-bomb), se guarda con **nombre aleatorio** fuera del webroot y se analiza **una sola vez**.
- **Parser determinista** (sin IA, sin costo): texto con PdfPig / OpenXML → secciones → experiencia con fechas (suma meses sin contar superposiciones), estudios con estado, habilidades por catálogo con el nivel que declara el CV (si no declara: «intermedio» solo si la usó en un trabajo, si no «básico»), idiomas, cursos y cargos sugeridos.
- **El CV propone, el usuario decide**: subir un CV no modifica el perfil; la app muestra lo detectado para que lo edites y recién al confirmar se guarda.
- **Onboarding** de 5 pasos (sobre ti → CV → procesando → revisión editable → preferencias → «tu agente está listo»), con guard de rutas: un usuario sin onboarding solo puede ver el flujo. Alternativa «Prefiero llenarlo a mano».
- **Perfil**: CV actual (reemplazar / eliminar), borrar historial y eliminar cuenta (borra también los archivos de CV).
- Plan FREE/PRO: 1 CV (subir otro reemplaza y borra el anterior del disco); PRO+: hasta 5.

## Qué incluye la Fase 4 (embeddings)

- **Etapa C del pipeline**: cada oferta y cada candidato tienen un vector de 1024 dimensiones (`bge-m3` vía **Ollama**, local o en tu servidor); **pgvector** calcula la similitud dentro de PostgreSQL con índice HNSW.
- Aporta el 25% del puntaje y reconoce cargos equivalentes con otro nombre; **no** salta reglas duras ni requisitos faltantes.
- **Opcional y resiliente**: `Embeddings__Provider=None` (por defecto) = el producto funciona igual; si Ollama cae, un circuit breaker evita martillarlo y el worker completa los vectores cuando vuelve.
- Guía de instalación (PC, Docker y servidor Contabo), seguridad y calibración: [docs/OLLAMA.md](docs/OLLAMA.md).

Lo que **todavía no** existe (ver roadmap): conectores a portales reales (fase 10, requiere revisar términos de cada uno), notificaciones push, enriquecimiento con LLM barato (fase 8), OCR de CV escaneados, preparación de postulación.

## Renovación de UX y diseño

- **Sistema de diseño con modo claro y oscuro**: todos los colores son tokens (`src/ui/theme.ts`); el usuario elige *Automático / Claro / Oscuro* en Perfil y se recuerda. Gradiente de marca en tarjetas héroe, avatares de empresa con color estable, barras de progreso, filas tipo ajustes, toasts.
- **Bienvenida** para quien no tiene sesión (propuesta de valor + «Crear mi cuenta» / «Ya tengo cuenta»). Registro con checklist de contraseña en vivo.
- **Inicio**: estado del agente, reparto de nuevas ofertas (muy compatibles / posibles / no convienen), accesos directos que abren Empleos ya filtrado, **fuerza del perfil** con el siguiente paso concreto, resumen de postulaciones con la próxima entrevista.
- **Empleos**: contador de resultados, filtros rápidos por modalidad, filtro por compatibilidad, tarjetas rediseñadas (sueldo destacado, motivos, «Ver por qué encaja»).
- **Acciones reversibles**: guardar, quitar de guardadas y descartar son optimistas y muestran un toast con **«Deshacer»** (nuevo endpoint `POST /matches/{jobId}/reset`).
- **Detalle**: datos clave en mosaicos, veredicto con color por categoría, «Tienes X de Y habilidades», descripción plegable, compartir, barra fija con Guardar + Postular.
- **Postulaciones**: mosaicos por etapa con contadores, botón de **siguiente etapa en un toque** («Ya postulé», «Me llamaron a entrevista»…) con deshacer, y **fecha/hora de entrevista** sin dependencias nativas.
- **Agente**: chat a pantalla completa, cada respuesta muestra qué cambió en tu búsqueda, memoria en una hoja («Recuerdo N»), sugerencias que desaparecen al usarse.
- **Formularios**: aviso de cambios sin guardar al cerrar, botón deshabilitado si no hay cambios, errores en línea consistentes, anillo de foco.
- Correcciones: botones anidados dentro de botones (HTML inválido en web), etiquetas de la barra inferior cortadas, campo de años/meses que borraba la letra «D» en vez de no-dígitos, títulos de error en inglés («Not Found»), saneado de nombres de CV con rutas de Windows en servidores Linux.

## Ingesta de ofertas (Fase 3)

- **Contrato** `IJobSource` → `RawJob`; cada conector solo trae y traduce. Normalización, dedup y almacenamiento son comunes.
- **Normalizador determinista** (`Domain/Ingestion/JobNormalizer`): limpia HTML, deduce distrito (ubicación o título),
  modalidad, tipo de contrato, sueldo («S/ 1,800 - 2,200», «2500 soles»; en descripciones exige moneda para no leer años
  ni teléfonos), experiencia («2 años de experiencia», «sin experiencia»), estudios mínimos, «lunes a viernes» y
  habilidades del catálogo con nivel («Excel intermedio»), separando las «deseables». Lo estructurado de la fuente siempre gana.
- **Deduplicación en 3 capas**: `(fuente, id externo)` → hash de contenido → similitud (misma empresa, título ≈, mismo
  distrito, sueldos compatibles, ≤ 21 días). Los duplicados se guardan inactivos con `DuplicateOfId`: nunca se muestran dos veces.
- **Retiro**: ofertas vencidas o que una fuente sana dejó de listar hace `StaleAfterDays` (7) días.
- **Robustez**: una fuente caída o un lote que no se puede guardar no detiene a las demás; el error queda en `JobSource.LastError`.
- **Transparencia**: `GET /api/v1/sources` y, en la app, Perfil → «¿De dónde salen las ofertas?».
- Fuentes incluidas: demo (a través del pipeline real) y **feeds JSON** (`JsonFeedSource`, con ETag y User-Agent identificable).
  Ejemplo de feed: [docs/examples/feed-ejemplo.json](docs/examples/feed-ejemplo.json).

## CI

`.github/workflows/ci.yml`: en cada PR y en `main` compila (.NET, advertencias = errores), corre las 224 pruebas (con
PostgreSQL real vía Testcontainers) y, para la app, `typecheck`, `lint` y `jest`.

## Datos demo

Son ficticios: empresas inventadas, URLs `example.com`, persona inexistente. Se re-fechan en cada arranque para que «Nuevas» nunca quede vacía.
