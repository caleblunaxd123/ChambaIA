# ChambaIA

**Tu agente personal para encontrar trabajo.** Subes tu CV una vez y el agente entiende tu perfil, reúne ofertas, descarta
duplicados y trabajos incompatibles, calcula qué tan bien encajas, te explica por qué y te avisa sin bombardearte.

> Principio de arquitectura: **la IA no busca los empleos, la IA ayuda a entenderlos.** El pipeline es determinista y
> barato; los modelos de pago se reservan para acciones premium. Todo funciona aunque las APIs de IA estén caídas.
> Ver [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) y [docs/AI-COSTS.md](docs/AI-COSTS.md).

Estado: **Fases 1 y 2 completas** (base ejecutable + CV, perfil y onboarding). Ver [docs/ROADMAP.md](docs/ROADMAP.md).

## Estructura

```
apps/api       ASP.NET Core 9 (Web API, Identity + JWT, EF Core, Swagger) + Domain + Infrastructure + tests
apps/worker    .NET Worker Service con Quartz.NET (heartbeat hoy; ingesta y alertas en fases 3 y 6)
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

### 4. Worker (opcional en esta fase)

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project apps/worker/ChambaIA.Worker
```

### Todo en contenedores (opcional)

```bash
# .env: JWT_KEY=<32+ caracteres>
docker compose --profile full up -d --build
```

## Verificación (lo que corre antes de cada entrega)

```bash
dotnet build ChambaIA.sln            # 0 errores, 0 advertencias (TreatWarningsAsErrors)
dotnet test ChambaIA.sln             # 150 pruebas (dominio + integración); las de integración levantan PostgreSQL con Testcontainers (requiere Docker)
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

Lo que **todavía no** existe (ver roadmap): enriquecimiento con LLM barato (opcional, fase 8), OCR de CV escaneados, ingesta real de ofertas, embeddings, notificaciones push, abstracción de proveedores de IA, preparación de postulación, dark mode.

## Datos demo

Son ficticios: empresas inventadas, URLs `example.com`, persona inexistente. Se re-fechan en cada arranque para que «Nuevas» nunca quede vacía.
