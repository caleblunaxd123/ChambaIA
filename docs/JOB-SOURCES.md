# Fuentes de empleo

## Política (no negociable)

Antes de construir cada conector:

1. ¿Existe **API oficial** o programa de afiliados? → úsala.
2. ¿Existe **feed** (RSS/XML/JSON)? → úsalo.
3. ¿Hay páginas públicas cuyos términos y `robots.txt` lo permiten? → solo entonces, con:
   rate limiting, caché (ETag/If-Modified-Since), backoff exponencial, `User-Agent` identificable con contacto, y respeto de `Crawl-delay`.
4. Si la plataforma no lo permite → **adaptador + interfaz + mock + documentación**, nada más.

Prohibido: evadir CAPTCHA, rotar proxies para esquivar bloqueos, cookies robadas, saltarse Cloudflare, cuentas falsas o automatizar contra protecciones.

## Contrato (fase 3, implementado)

```csharp
public interface IJobSource
{
    string Key { get; }
    string Name { get; }
    JobSourceKind Kind { get; }
    string? BaseUrl { get; }
    Task<IReadOnlyList<RawJob>> FetchJobsAsync(DateTimeOffset? since, CancellationToken ct);
}
```

Cada conector produce `RawJob`; `JobNormalizer` genera la oferta normalizada y `IngestionService` deduplica (clave →
hash → similitud), guarda, retira lo vencido y recalcula matches. La unicidad `(SourceId, ExternalId)` hace la ingesta
idempotente. La tabla `JobSource` guarda `Key`, tipo, última lectura correcta y el último error.

## Formato de feed JSON (para empresas y socios)

La vía preferida después de una API oficial: la propia empresa decide qué publica. Se configura en
`Ingestion:Feeds` con `Key`, `Name`, `Url` y **`Permission`** (quién lo autorizó y en qué términos; sin esto no se lee).

```jsonc
{
  "jobs": [
    {
      "id": "ADM-2026-114",                 // obligatorio, estable
      "title": "Asistente Administrativo",  // obligatorio
      "company": "Clínica Ejemplo",         // obligatorio
      "description": "<p>…</p>",            // obligatorio (se acepta HTML)
      "url": "https://…/ADM-2026-114",      // obligatorio: adonde postula el usuario
      "location": "Los Olivos, Lima",       // opcional (o "Remoto")
      "salary": "S/ 1,900 - 2,100",         // opcional; o salaryMin / salaryMax numéricos
      "modality": "Híbrido",                // opcional; si falta se deduce del texto
      "employmentType": "Tiempo completo",  // opcional
      "schedule": "Lunes a viernes 8 a 17", // opcional
      "postedAt": "2026-10-01T09:00:00-05:00",
      "expiresAt": null,                    // por defecto 30 días después de publicada
      "skills": ["Excel", "facturacion"],   // opcional; si viene, manda sobre el texto
      "niceToHave": ["SAP"]
    }
  ]
}
```

Ejemplo completo: [examples/feed-ejemplo.json](examples/feed-ejemplo.json). El lector usa `If-None-Match` (ETag),
timeout de 30 s y `User-Agent: ChambaIA-FeedReader/1.0`; un ítem inválido se salta sin descartar el resto del feed.

## Estado

| Fuente | Estado | Camino |
|---|---|---|
| Demo (`demo`) | ✅ pasa por el pipeline real (seed de la API y worker en Development) | datos ficticios |
| Feeds JSON de empresas/socios | ✅ conector genérico listo | configurar `Ingestion:Feeds` con permiso documentado |
| Computrabajo, Bumeran | pendiente | revisar API/afiliados y términos; si no, solo mock |
| LinkedIn Jobs | pendiente | sin scraping; solo si hay API/socio autorizado |
| Indeed, Jooble | pendiente | verificar API de publishers / afiliados |
| Talento Perú / SERVIR | pendiente | portal público; revisar términos y datos abiertos |
| Universidades, institutos, clínicas, empresas | pendiente | páginas de carreras públicas + feeds, una por una |

**Ningún portal real está conectado todavía**; el pipeline y el conector de feeds ya están listos para recibirlos. La investigación de términos de cada una es el primer paso de la fase 10.
