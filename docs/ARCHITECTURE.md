# Arquitectura

## Pipeline objetivo

```
FUENTES → INGESTA → NORMALIZACIÓN → DEDUPLICACIÓN → FILTROS DETERMINÍSTICOS
        → EMBEDDINGS LOCALES → MATCHING VECTORIAL → IA BARATA (solo si hace falta) → PUSH/EMAIL → APP
```

| Etapa | Fase | Estado |
|---|---|---|
| Normalización de texto, skills y distritos | 1 | ✅ `ChambaIA.Domain` |
| Lectura de CV (PDF/DOCX) y parser determinista | 2 | ✅ `Domain/Resumes` + `Infrastructure/Resumes` |
| Filtros duros + matching estructural (A y B) | 1 | ✅ `MatchEngine` |
| Fuentes, normalización, dedup en 3 capas, retiro | 3 | ✅ `Domain/Ingestion` + `Infrastructure/Ingestion` + `IngestionJob` |
| Embeddings locales (Ollama + bge-m3) + pgvector HNSW (C) | 4 | ✅ `Infrastructure/Embeddings` · ver [OLLAMA.md](OLLAMA.md) |
| Explicación por ejes, mejoras what-if y ofertas parecidas | 5 | ✅ `Domain/Matching/MatchExplainer` · `GET /jobs/{id}/similar` |
| Push / workers | 6 | pendiente (Worker + Quartz ya corren la ingesta) |
| Proveedores de IA + router + límites | 8 | pendiente (tabla `AiUsage` lista) |

## Componentes

```
apps/mobile  ──HTTPS/JSON──▶  apps/api  ──▶  PostgreSQL (+pgvector)
                                  │
                                  └────────▶  Redis (caché / límites; hoy: health + IDistributedCache)
apps/worker  ───────────────────▶ misma base y mismas librerías (ChambaIA.Infrastructure)
```

### Backend: 4 proyectos, sin capas ceremoniales

| Proyecto | Contenido | Depende de |
|---|---|---|
| `ChambaIA.Domain` | Entidades, enums y **lógica pura** (matching, normalización, catálogo de skills, distritos, comandos del agente). Sin I/O. | Pgvector (tipo) |
| `ChambaIA.Infrastructure` | `AppDbContext`, migraciones, Identity, JWT/refresh, `MatchRecomputeService`, `TrackerService`, seed | Domain |
| `ChambaIA.Api` | Minimal APIs por feature, contratos (DTO), validación, ProblemDetails, rate limiting, Swagger | Infrastructure |
| `ChambaIA.Worker` | Quartz.NET hospedado | Infrastructure |

Decisión: **no hay proyecto `Application`**. Los casos de uso hoy son consultas EF directas desde los endpoints
(CQRS ligero) o servicios pequeños en Infrastructure. Si en la fase 8 aparece orquestación de IA con varias
dependencias, se evalúa extraerlo; crearlo ahora sería una capa vacía.

La lógica que más cambia y más importa (el matching) vive en Domain como funciones puras: se prueba sin base de datos
y se reutiliza igual desde API (recalcular al cambiar preferencias) y Worker (recalcular tras ingesta).

### Matching (fase 1: etapas A y B)

1. **Reglas duras (A)**: distrito excluido, sueldo bajo el mínimo, modalidad/contrato no deseado, cargo o palabra excluida, estudios exigidos sobre el techo del usuario, experiencia muy superior, viaje > máximo, fin de semana si pidió L-V. Una falla dura → *Poco compatible* y la oferta sale del feed.
2. **Estructural (B)**: skills requeridas por **clave canónica y nivel** («Excel intermedio» vs «básico» ⇒ advertencia), cercanía del título a los cargos buscados, experiencia, estudios.
3. **Semántico (C, fase 4)**: embeddings solo para reordenar lo que sobrevive.

La puntuación **nunca decide sola**: faltantes y brechas *limitan* la categoría (sin faltantes ni brechas ⇒ puede ser «Excelente»; un requisito faltante ⇒ máximo «Muy compatible»…). La app muestra **categorías**, no porcentajes. Cada resultado trae `reasons` (✓) y `warnings` (⚠) en español.

Viaje: heurística (distancia entre centroides de distritos × factor vial ÷ 20 km/h + 8 min), mostrada siempre como «aprox.». Sustituible por un proveedor de rutas.

### CV (fase 2)

`archivo → ResumeFileValidator → ResumeTextExtractor (PdfPig / OpenXML) → ResumeParser (puro) → ParsedResume (JSON en Resume.StructuredData)`.
El parser divide por encabezados típicos en español, ancla cada trabajo en su rango de fechas («ene 2022 – actualidad», «01/2023 – 11/2023», «2019 - 2021») y suma meses **sin contar solapes**. El nivel de cada habilidad sale de lo que dice el CV; sin dato nunca se sobreestima. Es una *propuesta*: el móvil la muestra para revisión y solo el perfil editado se guarda. Un LLM barato podrá enriquecer este JSON (fase 8), nunca reemplazarlo ni saltarse la validación.
Limitaciones conocidas: CV escaneados (sin texto) se rechazan con un mensaje claro; PDFs con columnas muy complejas pueden desordenar líneas; la extracción de empresa/cargo es heurística y por eso se revisa.

### Planificador: Quartz.NET (no Hangfire)

| | Quartz.NET | Hangfire |
|---|---|---|
| Hospedaje | dentro del Worker, sin dashboard que asegurar | requiere storage y dashboard expuesto |
| Licencia | Apache-2.0 | núcleo LGPL, funciones Pro de pago |
| Cron / calendarios | nativo, muy completo | cron simple |
| Reintentos persistentes | no por defecto | sí |

Los jobs de ingesta y de resumen son **idempotentes** (ver `contentHash` y `(SourceId, ExternalId)` únicos), así que si una
ejecución se pierde, la siguiente la compensa y no hace falta persistencia de colas. Si se necesitaran colas durables con
reintentos por mensaje, se añade un outbox en PostgreSQL. *Nota*: Quartz 4.x apunta solo a .NET 10; se fijó la línea 3.22.

### Datos

- Enums como **texto** (legibles y a salvo de reordenamientos). Listas simples en `text[]`; estructuras (skills, experiencia, motivos) en `jsonb` vía owned types.
- Skills de candidato y requisitos de oferta comparten el catálogo canónico (`SkillCatalog`): «atención al usuario» ≡ «servicio al cliente».
- Únicos: `(SourceId, ExternalId)`, `(CandidateId, JobId)`, `(UserId, JobId)` en postulaciones, hash de refresh token.
- Índice HNSW de pgvector: se crea en la fase 4, cuando haya embeddings reales.
- Borrado de cuenta: cascadas por FK + borrado explícito de `AiUsage` (minimización de datos).

### Sesión y estado en el móvil

- TanStack Query para datos de servidor (claves centralizadas, invalidación tras cada mutación que afecta matches). Zustand solo para sesión y chat en memoria.
- Todo response se valida con **Zod** (`src/api/schemas.ts`): un cambio de contrato falla en un solo lugar.
- Refresh de token *single-flight*: 5 peticiones con 401 ⇒ 1 refresh. Un fallo de red durante el refresh **no** cierra la sesión.
- Tokens en Keychain/Keystore (`expo-secure-store`).

### Redis hoy

Registrado como `IDistributedCache` y comprobado por `/health/ready`. Usos planeados: caché de feed, contadores de presupuesto de IA (fase 8), claves de dedupe de ingesta. No se usa aún para nada que no esté justificado.

## Observabilidad

Serilog estructurado con `CorrelationId` (header `X-Correlation-ID`, saneado), ProblemDetails con `traceId`/`correlationId`, health checks live/ready.
