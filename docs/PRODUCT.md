# Producto

**ChambaIA**: un agente de búsqueda laboral (no una bolsa de trabajo). Foco inicial: Perú, Lima.

## Experiencia ideal

> «Este es mi CV. Quiero trabajar en Lima, mínimo S/1800, cerca de Los Olivos y sin trabajos que exijan universidad terminada.»
>
> El agente trabaja solo y luego avisa: «Encontré 7 oportunidades nuevas. 3 encajan muy bien contigo. 2 tienen requisitos que podrías cumplir. 2 no te las recomiendo.»

El usuario conversa («no me muestres trabajos en Ate», «máximo una hora de viaje») y el sistema **recuerda**.

## MVP (lo que define «terminado»)

1. Cuenta + onboarding con CV (PDF/DOCX), perfil editable (la IA nunca se da por cierta).
2. Preferencias laborales y agente por conversación.
3. Ingesta desde fuentes lícitas, normalización y deduplicación.
4. Matching explicable con categorías (Excelente / Muy compatible / Compatible / Revisar / Poco compatible).
5. Feed (Para ti · Nuevas · Guardadas), detalle con «¿por qué encaja conmigo?» y **Postular = abrir la página oficial**.
6. Alertas push sin bombardear (frecuencia configurable).
7. Tablero de postulaciones.
8. Funciona sin IA; la IA mejora, no sostiene.

Fuera del MVP: postulación automática, pagos, WhatsApp/Telegram, multi-país.

## Principios de UX

- Honestidad: nada de porcentajes engañosos; siempre motivos y riesgos concretos.
- Control: el usuario edita todo lo que el sistema infiere.
- Calma: empty states útiles («Tu agente seguirá buscando»), skeletons, errores claros, sin spam.
- Español peruano, tono cercano.

## Planes (arquitectura lista, sin pagos)

| | FREE | PRO | PRO+ |
|---|---|---|---|
| CV | 1 | 1 | varios |
| Búsqueda | periódica | más frecuente | más frecuente |
| Matching | básico | inteligente | inteligente |
| Push | sí | sí | sí |
| CV adaptado / ayuda de postulación | – | sí | sí |
| Entrevistas / automatización | – | – | sí |

`ApplicationUser.Plan` ya existe; los límites se aplicarán en el router de IA (fase 8).

## Privacidad

El CV es dato personal: borrado de cuenta ya implementado; borrado de CV e historial en fase 2. Minimización: solo el texto necesario llega a proveedores de IA.
