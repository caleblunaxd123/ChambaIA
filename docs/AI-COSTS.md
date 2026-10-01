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

## Enrutado por tarea (diseño de la fase 8)

| Tarea | Proveedor/modelo objetivo | Notas |
|---|---|---|
| Embeddings | **local** (bge-m3 vía Ollama) | $0 variable; solo hardware |
| Clasificación de comandos | reglas → modelo local pequeño | LLM solo si las reglas fallan |
| Parseo de CV a JSON | modelo barato (Gemini Flash / GPT mini / DeepSeek) | **una vez** por CV, texto ya extraído |
| Extracción de ofertas ambiguas | modelo pequeño | solo cuando el parser determinista no basta |
| Carta, CV adaptado, entrevistas | modelo avanzado | acciones premium (PRO/PRO+) |

## Controles previstos

- `IAiProvider` + `AiRouter`: la lógica de negocio nunca depende de un proveedor concreto.
- Tabla `AiUsage` (ya migrada): usuario, proveedor, modelo, operación, tokens in/out, costo estimado.
- Límites por usuario (día/mes), por proveedor y por plan; timeouts, reintentos con backoff, circuit breaker y *fallback* a otro proveedor o a «sin IA».
- Minimización de datos: nunca se envía el CV completo cuando basta un fragmento; el PDF no se reenvía.
- **Modo sin IA**: ingestar, filtrar, ordenar, mostrar, guardar y notificar siguen funcionando con todos los proveedores caídos.

## Estimación a documentar en la fase 8

Costo por usuario/mes = (parseos de CV × tokens) + (acciones premium × tokens) con los precios vigentes de cada proveedor.
Se calculará con datos reales de `AiUsage`, no con supuestos.
