# Roadmap

Leyenda: [x] hecho · [ ] pendiente

## Fase 1 — Base ejecutable ✅
- [x] Monorepo, Docker Compose (PostgreSQL+pgvector, Redis), Dockerfiles API/Worker
- [x] Solución .NET 9, migración inicial, Swagger, health checks, ProblemDetails, correlation id, rate limiting
- [x] Auth: Identity + JWT + refresh rotativo con detección de reutilización, bloqueo, borrado de cuenta
- [x] Seed demo (Areli + 28 ofertas ficticias)
- [x] Matching determinista (A+B) con explicaciones, endpoints de feed/overview/tracker
- [x] Agente por reglas (comandos de preferencias)
- [x] App Expo: navegación inferior, design system, Inicio, Empleos, detalle, Postulaciones, Agente, Perfil, formularios
- [x] Tests: backend (dominio + integración con Testcontainers), móvil (lógica)

## Fase 2 — CV y perfil ✅
- [x] `POST/GET/DELETE /resumes`: validación de MIME/extensión/firma/tamaño, nombre aleatorio, almacenamiento fuera del webroot, 1 CV por plan FREE/PRO
- [x] Extracción determinista PDF/DOCX → texto → estructura (skills por catálogo, experiencia por fechas, estudios, idiomas, cursos, cargos sugeridos)
- [x] Onboarding completo con revisión editable de lo detectado y guard de rutas
- [x] Borrado de CV, de historial y de cuenta (incluye archivos)
- [ ] LLM barato opcional para enriquecer el JSON (validado con JSON Schema) → fase 8
- [ ] OCR para CV escaneados (hoy se pide el archivo original)
- [ ] Escaneo antivirus del archivo subido (antes de producción)

## Fase 3 — Ingesta
- [ ] `IJobSource`, normalización, `JobCluster`, deduplicación en 3 capas (clave, hash, similitud)
- [ ] Fuente demo como primer conector real del pipeline; job Quartz de ingesta

## Fase 4 — Embeddings y matching vectorial
- [ ] Servicio local de embeddings (Ollama + bge-m3 / multilingual-e5), índice HNSW, etapa C

## Fase 5 — Feed y explicación
- [ ] Refinar feed con ranking semántico; explicación enriquecida

## Fase 6 — Alertas y workers
- [ ] Expo Push, registro de dispositivos, resúmenes por frecuencia, recálculo incremental tras ingesta

## Fase 7 — Tracker
- [ ] Fechas de entrevista con selector, recordatorios, métricas

## Fase 8 — Proveedores de IA
- [ ] `IAiProvider`, router por tarea, límites diarios/mensuales, circuit breaker, `AiUsage`, dashboard de costos

## Fase 9 — Asistente
- [ ] Fallback LLM pequeño para comandos no reconocidos; preparación de postulación (aprobación explícita)

## Fase 10 — Fuentes reales
- [ ] Conectores por API/feeds/páginas públicas respetando robots.txt y términos (ver JOB-SOURCES.md)

## Transversal pendiente
- [ ] Dark mode, íconos/splash propios, E2E móvil, CI
