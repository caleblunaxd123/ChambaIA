# Embeddings con Ollama (fase 4)

ChambaIA usa un modelo de **embeddings** para medir si una oferta *significa* lo mismo que lo que hace y busca el
candidato, aunque el título sea distinto («Auxiliar de oficina» vs «Asistente administrativo»). Es la etapa C del pipeline:
**no reemplaza** reglas ni habilidades; solo aporta el 25% del puntaje y suaviza el tope por título raro. Costo por
llamada: **S/ 0** (corre en tu hardware). Sin Ollama el producto funciona igual (modo sin IA).

## Modelo elegido: `bge-m3`

| | `bge-m3` (elegido) | `multilingual-e5-small/base` |
|---|---|---|
| Español | muy bueno (multilingüe, 100+ idiomas) | bueno |
| Dimensiones | **1024** (coinciden con `vector(1024)`) | 384 / 768 (exige migración) |
| Tamaño en Ollama | ~1.2 GB | 0.1 – 0.5 GB |
| Contexto | 8192 tokens | 512 |
| Prefijos | no necesita | exige `query:` / `passage:` (ya soportado en opciones) |

Medido en tu equipo (i7-14700): 1.ª llamada 3.7 s (carga el modelo), después 8 ofertas en un lote ≈ 0.6 s.

### Calibración (importante)

El coseno crudo no se puede leer directo. Con `bge-m3` y textos laborales en español se midió:

| Par | Coseno | Puntaje 0-100 |
|---|---|---|
| mismo cargo (asistente administrativa ↔ perfil) | 0.79 | 100 |
| cargo parecido con otro nombre (auxiliar de oficina) | 0.65 | 60 |
| oficio relacionado (facturación) | 0.64 | 58 |
| oficio distinto (cajera, contador) | 0.55 / 0.54 | 32 / 26 |
| sin relación (mecánico, teleoperador) | 0.48 | 8-9 |

`Embeddings:UnrelatedCosine = 0.45` (puntaje 0) e `IdenticalKindCosine = 0.78` (puntaje 100). **La muestra es pequeña (8
ofertas)**: antes de producción corre `node infra/scripts/calibrate-embeddings.mjs` con ofertas reales etiquetadas y ajusta.
Si cambias de modelo, vuelve a calibrar: cada modelo tiene otra escala.

## Cómo funciona

- Al ingerir ofertas (API al arrancar en Development / worker cada 30 min) cada oferta nueva o modificada recibe su vector.
  Se guarda `EmbeddingHash` (hash del texto + modelo): si el texto no cambia, **no se vuelve a vectorizar**.
- Al guardar perfil o preferencias se recalcula el vector del candidato **solo si cambió su texto** (cargos, habilidades,
  experiencia; nunca nombre ni contacto). Tiene un presupuesto de 8 s: si Ollama no responde, el guardado sigue sin vector.
- La similitud la calcula **PostgreSQL con pgvector** (`<=>`, distancia coseno) con índice **HNSW**.
- Si Ollama falla 3 veces seguidas, un *circuit breaker* deja de llamarlo 60 s. El job `EmbeddingJob` del worker (cada 10 min)
  completa lo que faltó y recalcula los matches.
- Un modelo que devuelva otra cantidad de dimensiones se **rechaza** (nunca se escriben vectores de tamaño equivocado).

## Local (tu PC)

Si ya tienes Ollama instalado (como en este equipo), solo:

```bash
ollama pull bge-m3
```

y en Development la API ya apunta a `http://localhost:11434` (`appsettings.Development.json`). Alternativa en Docker
(si no tienes Ollama o el puerto 11434 está libre): `docker compose --profile ai up -d` y, si el puerto está ocupado,
`OLLAMA_PORT=11435` en `.env`.

## Servidor Contabo (o cualquier VPS)

**Dimensionado.** Para *solo embeddings* alcanza un VPS pequeño con CPU: 4 vCPU y 8 GB de RAM (el modelo usa ~2-3 GB). No
planees correr LLMs de 7B+ en un VPS sin GPU para tareas interactivas (2-5 tokens/s): para eso se usa un proveedor barato
de API (fase 8) o un modelo chico (3B) solo para clasificar.

**Instalación** (en el VPS, Ubuntu/Debian):

```bash
curl -fsSL https://ollama.com/install.sh | sh          # instala y crea el servicio systemd
sudo systemctl edit ollama                             # añade:
#   [Service]
#   Environment="OLLAMA_HOST=127.0.0.1:11434"          # solo loopback (por defecto)
#   Environment="OLLAMA_KEEP_ALIVE=24h"                # evita la carga en frío de 3-4 s
sudo systemctl restart ollama
ollama pull bge-m3
curl -s http://127.0.0.1:11434/api/embed -d '{"model":"bge-m3","input":["prueba"]}' | head -c 100
```

**Seguridad — esto es lo más importante.** Ollama **no tiene autenticación**. Nunca abras el puerto 11434 a internet (hay
escáneres que lo buscan y usan tu CPU). Elige una de estas opciones, de mejor a peor:

1. **API y worker en el mismo VPS** → `Embeddings__BaseUrl=http://localhost:11434`. Nada expuesto.
2. **Red privada** entre tus servidores (WireGuard o Tailscale) → `BaseUrl=http://10.x.x.x:11434`, y en el VPS
   `OLLAMA_HOST=10.x.x.x:11434` + firewall que solo permita esa IP.
3. **Túnel SSH** desde donde corre la API: `ssh -N -L 11434:127.0.0.1:11434 usuario@tu-vps`.
4. **Proxy inverso con TLS y token** (Caddy/nginx) que exija `Authorization: Bearer <token>` y reenvíe a `127.0.0.1:11434`;
   en ChambaIA: `Embeddings__BaseUrl=https://ollama.tu-dominio.com` y `Embeddings__ApiKey=<token>`. Firewall `ufw` con solo
   22 y 443, `fail2ban`, y rate limit en el proxy.

**Configuración de ChambaIA** (variables de entorno, nunca en el repo):

```
Embeddings__Provider=Ollama
Embeddings__BaseUrl=...        # según la opción elegida
Embeddings__ApiKey=...         # solo si hay proxy con token
Embeddings__Model=bge-m3
```

## Pendiente / límites conocidos

- Las ofertas se comparan en una sola pasada por candidato (decenas de miles de ofertas están bien para Postgres + HNSW);
  para recomendaciones por *top-K* con millones de ofertas habría que usar `ORDER BY ... LIMIT` con el índice.
- No hay (aún) panel de métricas de latencia/errores de embeddings; los logs de Serilog informan cuándo abre el circuito.
