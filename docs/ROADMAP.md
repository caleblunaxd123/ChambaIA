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

## Fase 3 — Ingesta ✅
- [x] `IJobSource` + `RawJob`, normalizador determinista (sueldo, distrito, modalidad, experiencia, estudios, horario, skills con nivel)
- [x] Deduplicación en 3 capas (clave, hash, similitud); duplicados agrupados con `DuplicateOfId` (equivale a `JobCluster`)
- [x] Fuente demo como primer conector del pipeline; conector genérico de feeds JSON con permiso obligatorio
- [x] `IngestionJob` (Quartz, cada 30 min + al arrancar), retiro de ofertas vencidas/abandonadas, recálculo de matches
- [x] `GET /sources` (transparencia) y hoja «¿De dónde salen las ofertas?» en la app
- [ ] Recálculo incremental (solo ofertas nuevas × usuarios) cuando el volumen lo pida

## Fase 4 — Embeddings y matching vectorial ✅
- [x] `IEmbeddingProvider` (Ollama + proveedor nulo), timeout, reintento, circuit breaker, validación de dimensiones
- [x] `EmbeddingService`: vectores de ofertas y perfiles solo cuando cambia su texto (hash), catch-up en el worker
- [x] Etapa C en `MatchEngine` (25% del puntaje; nunca salta reglas duras ni requisitos faltantes); similitud con pgvector + índice HNSW
- [x] Modo sin embeddings idéntico al anterior; calibración medida con bge-m3 real (`infra/scripts/calibrate-embeddings.mjs`)
- [ ] Calibrar con ofertas reales etiquetadas; métricas de latencia/errores del servidor de embeddings

## Fase 5 — Feed y explicación ✅
- [x] Explicación por ejes (cargo, habilidades, experiencia, condiciones, parecido con tu perfil): nivel + una frase, nunca porcentajes
- [x] «Cómo mejorar esta compatibilidad»: simulaciones del propio motor («Agrega X → pasaría a Excelente»), solo para ofertas no filtradas y siempre con «solo si ya lo sabes hacer»
- [x] Ofertas parecidas por vecinos más cercanos de pgvector (HNSW), con respaldo por empresa/sector sin embeddings; no sugiere lo ya descartado
- [x] Orden del feed: recomendado (por tab), más recientes, mejor sueldo
- [ ] Explicación con texto generado por IA (fase 8, opcional; hoy es determinista y gratis)

## Fase 6 — Alertas y workers ✅
- [x] Política anti-bombardeo probada: solo ofertas muy compatibles, horario de descanso (22–7 h Lima), tope 4/día, frecuencia instantánea/2 h/6 h/diaria
- [x] Bandeja de avisos (API + app: campana con contador, marcar leídos, aviso de prueba) y memoria anti-duplicados
- [x] Registro de dispositivos (idempotente, un token = una cuenta, máx. 5) y baja al cerrar sesión
- [x] Cliente Expo Push: lotes de 100, reintento ante 5xx, tokens muertos desactivados, nunca lanza excepciones
- [x] Recálculo incremental tras la ingesta (solo ofertas nuevas o cambiadas)
- [x] `NotificationJob` en el worker (cada 5 min)
- [ ] Entrega push real: requiere proyecto EAS + credenciales FCM + build de desarrollo (ver [NOTIFICATIONS.md](NOTIFICATIONS.md))
- [ ] Hora de descanso y tope configurables por usuario (hoy son del servidor)

## Fase 7 — Tracker
- [x] Fechas de entrevista con selector (días + horarios, sin módulo nativo), próxima entrevista en Inicio
- [x] Avance de etapa en un toque con deshacer
- [x] Métricas: embudo postulaciones → entrevistas → ofertas y tasa de respuesta
- [x] Quitar la fecha de entrevista (`clearInterviewDate`)
- [ ] Recordatorios de entrevista (la infraestructura de avisos ya existe; falta programarlos)

## Fase 8 — Proveedores de IA
- [ ] `IAiProvider`, router por tarea, límites diarios/mensuales, circuit breaker, `AiUsage`, dashboard de costos

## Fase 9 — Asistente
- [ ] Fallback LLM pequeño para comandos no reconocidos; preparación de postulación (aprobación explícita)

## Fase 10 — Fuentes reales
- [ ] Conectores por API/feeds/páginas públicas respetando robots.txt y términos (ver JOB-SOURCES.md)

## UX transversal ✅
- [x] Modo oscuro con preferencia persistida (Automático / Claro / Oscuro)
- [x] Pantalla de bienvenida, toasts con «Deshacer» (`POST /matches/{jobId}/reset`), mutaciones optimistas
- [x] Fuerza del perfil con siguiente paso, accesos directos filtrados, aviso de cambios sin guardar, 404

## Transversal pendiente
- [x] CI (GitHub Actions: build, 192 pruebas con Testcontainers, typecheck/lint/jest)
- [ ] Íconos/splash propios, E2E móvil en CI
