# Costos de IA

## Regla

**La IA no busca los empleos, la IA ayuda a entenderlos.** Claude (u otro LLM) se usa *para construir* el software, no
como runtime del SaaS. El runtime gasta tokens solo donde una regla o un modelo local no alcanzan.

## Qué cuesta cero hoy (Fase 1)

| Capacidad | Cómo |
|---|---|
| Normalización, skills, distritos | código determinista |
| Filtros y matching (A+B) | `MatchEngine`, funciones puras |
| Explicaciones «¿por qué encaja?» | plantillas a partir de los datos del match |
| Comandos del agente | `AgentCommandParser` (regex sobre texto normalizado) |
| Búsqueda y orden del feed | SQL |
| Embeddings y similitud semántica (fase 4) | Ollama + `bge-m3` en tu hardware, comparación con pgvector: **S/ 0 por llamada** |

## Cómo funciona (Fase 8, implementado)

```
código de negocio ──▶ AiRouter ──▶ presupuesto ──▶ proveedor 1 ──(falla)──▶ proveedor 2 ──(falla)──▶ «sin IA»
                          │                              │
                          └── AiUsage (cada llamada: tokens, costo estimado) ◀──┘
```

- **Apagado por defecto** (`Ai__Enabled=false`): ninguna llamada a ningún modelo; el producto funciona igual que antes de esta fase.
- **Tres tipos de proveedor**, todos por configuración: `Ollama` (modelo local, costo $0 por llamada), `OpenAICompatible` (OpenAI, DeepSeek, el endpoint compatible de Gemini, Groq, OpenRouter…) y `Anthropic` (Claude). Los precios por millón de tokens los copias tú de la lista vigente del proveedor; el código no trae precios.
- **Rutas por tarea**: `Ai__Routes__JobExtraction__0=local`, `...__1=barato`. Si el primero falla (o su *circuit breaker* está abierto tras 3 fallos seguidos), pasa al siguiente; si ninguno responde, la tarea simplemente no se hace.
- **Presupuestos** (`Ai:Budgets`), verificados *antes* de cada llamada: tope global mensual en USD (por defecto 5), tope del sistema (trabajo en segundo plano), y por plan de usuario (llamadas por día y USD por mes). Los días y meses se cuentan en hora de Lima.
- **Tablero de costos**: `GET /api/v1/admin/ai/usage?days=30` (solo cuentas listadas en `Admin__Emails__0`): totales, por proveedor/modelo, por operación, por día, presupuesto restante, estado de cada proveedor (sin exponer claves) y ofertas pendientes de revisar.

### Primer uso real: ofertas ambiguas (`JobExtraction`)

El normalizador determinista ya lee casi todo. Cuando una oferta larga deja huecos (menciona sueldo o experiencia pero no se pudo leer, o no se detectó ninguna habilidad), el worker (`EnrichmentJob`, cada 15 min) pide al modelo un JSON con esos datos. Reglas para que sea barato y seguro:

1. **Solo si hace falta**: un pre-chequeo determinista decide si vale la pena llamar al modelo; cada oferta se revisa **una vez por versión de su contenido** (`AiExtractionHash`).
2. **Solo texto público de la oferta**: nunca nada de un usuario, ni el nombre de la empresa. El texto va entre marcas y el sistema lo declara *dato, no instrucciones*.
3. **Lo que responde el modelo no se acepta tal cual**: se valida forma y rangos, y luego se **verifica contra el propio texto** (ver abajo).
4. **Solo se llenan huecos**: nunca se pisa lo que ya leyó el parser o dijo la fuente; no toca cargo, empresa, distrito ni modalidad.
5. Las habilidades que aporta el modelo entran siempre como **«deseables»**, nunca como obligatorias.
6. Tras enriquecer, se recalculan solo esas ofertas para los candidatos.

#### Por qué se verifica contra el texto (medido con modelos reales)

Probando el prompt con `llama3.2` (2 GB) y `llama3.1:8b` sobre «Pagamos S/ 1,100 semanales»:

| Modelo | Respondió | Problema |
|---|---|---|
| llama3.1:8b | `salaryMin: 11000` | número que la oferta nunca dijo, pero «plausible» como sueldo mensual |
| llama3.2 | `salaryMin: 1100` | sueldo **semanal** tomado como mensual |

Ambos devolvieron JSON válido y dentro de rangos razonables: la validación de formato no los detecta. Por eso `JobExtractionGrounding` exige que cada dato esté respaldado por el texto: el sueldo debe aparecer escrito (`1,800` · `1.800` · `1800`) y se descarta si la oferta habla de pago semanal, quincenal, diario o por hora; la experiencia debe coincidir con una duración escrita («2 años», «un año y medio», «seis meses») y «0» solo con «sin experiencia»; el horario y los estudios necesitan sus palabras clave. Prefiere dejar el campo vacío antes que guardar una suposición.

**Prueba de extremo a extremo con un modelo real** (worker → router → Ollama `llama3.2` → verificación → base de datos, con 4 ofertas ambiguas servidas por un feed local, una de ellas con una inyección de prompt): 3 llamadas, costo $0, todos los datos guardados respaldados por el texto, y la inyección no cambió nada. Esa misma prueba reveló que el *normalizador determinista* leía «S/ 1,100 semanales» como mensual; ya está corregido y probado.

Rendimiento medido en un PC local: 2–10 s por oferta con `llama3.2` (hasta ~25 s la primera vez, mientras el modelo se carga) y 5–32 s con `llama3.1:8b`. Por eso cada proveedor tiene su propio `TimeoutSeconds` (120 recomendado para modelos locales).

### Segundo uso: el asistente (Fase 9)

Las tareas `CommandFallback` (entender frases libres) y `Premium` (borrador de postulación) usan el mismo router, con **cuotas diarias propias por tarea** (`Budgets.{plan}.DailyCallsByTask`; el resto de tareas usa `DailyCalls`) y el tope en dólares compartido. Detalle, privacidad y verificación en [ASSISTANT.md](ASSISTANT.md).

### Configuración de ejemplo

```bash
# Modelo local (gratis): solo hardware
Ai__Enabled=true
Ai__Providers__local__Type=Ollama
Ai__Providers__local__BaseUrl=http://localhost:11434
Ai__Providers__local__Model=llama3.2
Ai__Providers__local__TimeoutSeconds=120
Ai__Routes__JobExtraction__0=local

# Opcional: respaldo barato de pago (verifica el precio vigente y cópialo; la clave SOLO por variable de entorno)
Ai__Providers__barato__Type=OpenAICompatible
Ai__Providers__barato__BaseUrl=https://api.deepseek.com
Ai__Providers__barato__Model=deepseek-chat
Ai__Providers__barato__ApiKey=<tu clave>
Ai__Providers__barato__PricePerMillionInput=<USD por millón>
Ai__Providers__barato__PricePerMillionOutput=<USD por millón>
Ai__Routes__JobExtraction__1=barato

Admin__Emails__0=tu-correo@ejemplo.com   # para ver el tablero de costos (registra primero esa cuenta)
```

## Enrutado por tarea (objetivo)

| Tarea | Proveedor/modelo objetivo | Notas |
|---|---|---|
| Embeddings | **local** (bge-m3 vía Ollama) | $0 variable; solo hardware (fase 4, ✅) |
| Clasificación de comandos | reglas → modelo local pequeño | LLM solo si las reglas fallan |
| Parseo de CV a JSON | modelo barato (Gemini Flash / GPT mini / DeepSeek) | **una vez** por CV, texto ya extraído |
| Extracción de ofertas ambiguas | modelo pequeño | solo cuando el parser determinista no basta (fase 8, ✅) |
| Carta, CV adaptado, entrevistas | modelo avanzado | acciones premium (PRO/PRO+) |

## Controles (fase 8: implementados salvo lo indicado)

- ✅ `IAiProvider` + `AiRouter`: la lógica de negocio nunca depende de un proveedor concreto.
- ✅ Tabla `AiUsage`: usuario, proveedor, modelo, operación, tokens in/out, costo estimado (se escribe en cada llamada).
- ✅ Límites por usuario/plan y del sistema (día/mes), tope global; timeouts por proveedor, un reintento, circuit breaker y *fallback* a otro proveedor o a «sin IA». (Límite por proveedor: pendiente; el global ya acota el gasto.)
- Minimización de datos: nunca se envía el CV completo cuando basta un fragmento; el PDF no se reenvía.
- **Modo sin IA**: ingestar, filtrar, ordenar, mostrar, guardar y notificar siguen funcionando con todos los proveedores caídos.

## Estimación de costos

Costo por mes = Σ (tokens de entrada × precio) + (tokens de salida × precio) por proveedor. **No se publica una cifra inventada**: el tablero (`/admin/ai/usage`) calcula el costo real con tus precios configurados y los tokens que reporta cada proveedor (estimados por caracteres cuando no los reporta). Con un modelo local el costo variable es $0.
