# Asistente (Fase 9)

Dos capacidades que usan el [router de IA](AI-COSTS.md) y comparten una regla: **la IA propone, la persona decide**. Nada se aplica, se envía ni se guarda sin un «sí» explícito, y todo funciona (con las reglas de siempre) cuando la IA está apagada.

## 1. Respaldo con IA para frases libres

El agente por reglas sigue siendo el primero: gratis, instantáneo y se aplica al momento. Solo cuando **no entiende nada** y la IA está activada (`Ai__Routes__CommandFallback__0`), un modelo pequeño intenta interpretar la frase.

```
frase ──▶ reglas ──(entienden)──▶ se aplica (como siempre)
              └─(nada)──▶ modelo ──▶ validador cerrado ──▶ PROPUESTA ──▶ [Sí, aplicar] ──▶ validador otra vez ──▶ se aplica
```

- **Esquema cerrado** (`AgentCommandValidator`): solo los 14 comandos que ya existían; sueldos entre 500 y 50 000, viajes entre 5 y 240 minutos, distritos que **existen** en Lima, cargos/palabras como frases cortas de texto plano (sin enlaces, correos, símbolos ni marcado). Todo lo demás se descarta.
- **Nada se aplica solo.** El servidor devuelve una *propuesta* (`proposal`) con una línea por cambio; la app muestra «Lo entendí con IA» con **Sí, aplicar / No**. `POST /api/v1/agent/apply` valida **otra vez** los comandos que vuelven de la app (un request manipulado tampoco puede escribir nada raro).
- La frase del usuario va entre marcas y se declara *dato*, no instrucciones. Aun así, lo máximo que puede lograr es una propuesta válida sobre **sus propias** preferencias.
- Cuota diaria **propia de esta tarea** (Free 15, Pro 50, Pro+ 120 por día): interpretar una frase cuesta una fracción de un borrador, así que no compiten. Al agotarse, el agente sigue con reglas y lo dice.
- Probado con `llama3.1:8b` local: «prefiero que me quede cerca, no más de media hora en micro» → viaje máximo 30 min; «trabajar desde mi casa y solo entre semana» → remoto + lunes a viernes; «dos mil quinientos soles» → sueldo mínimo S/ 2,500. 5–25 s por frase con un modelo local (la primera, mientras carga).

## 2. Preparar mi postulación (acción premium, con aprobación explícita)

En el detalle de una oferta, **solo si el servidor tiene IA activada** (`Ai__Routes__Premium__0`), aparece «Preparar mi postulación». Son dos pasos separados a propósito:

1. **Vista previa** (`GET /api/v1/jobs/{id}/application-prep`): no envía nada. Muestra qué *categorías* de datos se enviarían y cuáles **nunca**, el **texto exacto** que viajaría al modelo, el tono (formal/cercano) y cuántos borradores quedan hoy.
2. **Generar** (`POST` con `approved: true`): sin esa aprobación el servidor responde 400. Lo que se envía es **exactamente** el texto de la vista previa (hay un test que lo comprueba).

| Se envía | Nunca se envía |
|---|---|
| Cargo, empresa y descripción de la oferta (público) | Apellido, correo, teléfono, dirección |
| Nombre de pila, titular, experiencia total, nivel de estudios | CV como archivo, documentos de identidad |
| Habilidades con nivel y últimos 3 puestos (cargo, empresa, duración) | Notas, postulaciones y preferencias |

**El borrador no se envía a nadie ni se guarda en ningún lado**: la app lo muestra, permite copiarlo y la persona lo revisa y lo manda por su cuenta.

### Honestidad: no inventar experiencia

- El prompt prohíbe inventar y pide anotar en `gaps` lo que la oferta exige y el perfil no muestra (para que la persona no lo afirme). Las frases de «por qué encajas» van en tercera persona y citan un dato concreto del perfil.
- **Verificador sin IA** (`ApplicationPrepChecker`) sobre el mensaje **y** esas frases: avisa si mencionan una habilidad que no está en tu perfil, si dicen más años de experiencia de los que registras (con 6 meses de tolerancia) o si quedan campos `[entre corchetes]`. Son **advertencias visibles**, nunca ediciones silenciosas.
- Un borrador con enlaces, correos o teléfonos se **rechaza** (serían datos de contacto inventados): la app dice que no se pudo generar uno confiable.
- Medido con un modelo local: la primera versión del prompt producía frases en primera persona que inventaban hechos («en la misma empresa»); con frases en tercera persona citando datos del perfil, las frases resultaron respaldadas por el perfil y el verificador detectó un «Gestión documentaria» que no era tuyo.

### Cuotas y errores

Borradores por día según el plan (Free 3, Pro 20, Pro+ 60), por tarea. 429 si se agotan (mensaje claro), 503 si la IA no está disponible, 502 si la respuesta no fue confiable. La app espera hasta 2 minutos (un modelo local puede tardar ~75 s la primera vez); con un proveedor en la nube suele ser de segundos.

## Privacidad

- Con IA **apagada** (por defecto) no sale nada a ningún modelo.
- Con **Ollama local** nada sale de tu equipo/servidor.
- Con un proveedor en la nube, lo que viaja es lo listado arriba (borradores, tras tu aprobación) y **la frase libre** del chat cuando las reglas no la entienden. Eso último se decide a nivel de servidor al activar la ruta `CommandFallback`: si no quieres que frases libres salgan a la nube, enruta esa tarea solo a un modelo local.

## Configuración

```bash
Ai__Enabled=true
Ai__Providers__local__Type=Ollama
Ai__Providers__local__Model=llama3.1:8b       # buen español; llama3.2 es más rápido pero redacta peor
Ai__Providers__local__TimeoutSeconds=180
Ai__Routes__CommandFallback__0=local
Ai__Routes__Premium__0=local
```
