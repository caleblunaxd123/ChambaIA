# Fuentes de empleo

## Política (no negociable)

Antes de construir cada conector:

1. ¿Existe **API oficial** o programa de afiliados? → úsala.
2. ¿Existe **feed** (RSS/XML/JSON)? → úsalo.
3. ¿Hay páginas públicas cuyos términos y `robots.txt` lo permiten? → solo entonces, con:
   rate limiting, caché (ETag/If-Modified-Since), backoff exponencial, `User-Agent` identificable con contacto, y respeto de `Crawl-delay`.
4. Si la plataforma no lo permite → **adaptador + interfaz + mock + documentación**, nada más.

Prohibido: evadir CAPTCHA, rotar proxies para esquivar bloqueos, cookies robadas, saltarse Cloudflare, cuentas falsas o automatizar contra protecciones.

## Contrato (fase 3)

```csharp
public interface IJobSource
{
    string Key { get; }
    Task<IReadOnlyList<RawJob>> FetchJobsAsync(DateTimeOffset? since, CancellationToken ct);
}
```

Cada conector produce `RawJob`; un normalizador común genera `JobOffer` (título/empresa normalizados, distrito canónico, skills por catálogo, `contentHash`). La unicidad `(SourceId, ExternalId)` hace la ingesta idempotente. La tabla `JobSource` guarda `Key`, tipo, estado y notas legales.

## Estado

| Fuente | Estado | Camino |
|---|---|---|
| Demo (`demo`) | ✅ sembrada en Development | datos ficticios |
| Computrabajo, Bumeran | pendiente | revisar API/afiliados y términos; si no, solo mock |
| LinkedIn Jobs | pendiente | sin scraping; solo si hay API/socio autorizado |
| Indeed, Jooble | pendiente | verificar API de publishers / afiliados |
| Talento Perú / SERVIR | pendiente | portal público; revisar términos y datos abiertos |
| Universidades, institutos, clínicas, empresas | pendiente | páginas de carreras públicas + feeds, una por una |

**Ninguna fuente real está implementada todavía.** La investigación de términos de cada una es el primer paso de la fase 10.
