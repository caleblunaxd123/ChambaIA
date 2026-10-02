// Measures real cosine similarities between a candidate and job texts, to calibrate
// Embeddings:UnrelatedCosine / IdenticalKindCosine (see docs/OLLAMA.md). Run after changing the model.
//   node infra/scripts/calibrate-embeddings.mjs            (OLLAMA_URL and EMBEDDING_MODEL are optional)
const base = process.env.OLLAMA_URL ?? 'http://localhost:11434';
const model = process.env.EMBEDDING_MODEL ?? 'bge-m3';
const embed = async (input) => {
  const t0 = performance.now();
  const r = await fetch(`${base}/api/embed`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ model, input }) });
  const j = await r.json();
  return { vectors: j.embeddings, ms: Math.round(performance.now() - t0) };
};
const cos = (a, b) => { let d = 0, x = 0, y = 0; for (let i = 0; i < a.length; i++) { d += a[i] * b[i]; x += a[i] * a[i]; y += b[i] * b[i]; } return d / Math.sqrt(x * y); };

const profile = 'Asistente administrativa con experiencia en atención al usuario y facturación. Busco trabajo como: Asistente Administrativo, Auxiliar Administrativo, Back Office. Habilidades: Atención al cliente, Gestión documentaria, Facturación, Excel, Word. Experiencia: Asistente administrativa - atención a padres de familia, archivo y gestión documentaria, facturación.';
const jobs = {
  'Asistente Administrativa (clínica)': 'Asistente Administrativa. Clínica Santa Aurora. Requisitos: Atención al cliente, Gestión documentaria, Excel. Buscamos asistente administrativa para el área de admisión y archivo de historias clínicas.',
  'Auxiliar de Oficina (otro nombre)': 'Auxiliar de Oficina. Distribuidora Andina. Recepción y archivo de guías de remisión, digitación de pedidos y coordinación con almacén.',
  'Encargado de Trámites Documentarios': 'Encargado de Trámites Documentarios. Cooperativa. Recepción, registro y derivación de documentos; atención a socios en ventanilla y archivo digital.',
  'Asistente de Facturación': 'Asistente de Facturación. Importadora Pacífico. Emisión de facturas y boletas electrónicas, conciliación de pagos y atención de consultas de clientes.',
  'Cajera de supermercado': 'Cajera. Supermercados Mi Barrio. Cobro en caja, arqueo y atención de clientes. Turnos rotativos de lunes a domingo.',
  'Teleoperador outbound': 'Teleoperador Outbound. Contacto Directo. Venta de servicios por teléfono desde nuestro call center. Ingreso variable según metas.',
  'Contador Público': 'Contador Público Colegiado. Estudio Contable. Cierre contable mensual, declaraciones tributarias y estados financieros.',
  'Mecánico industrial': 'Mecánico Industrial. Planta metalmecánica. Mantenimiento preventivo y correctivo de maquinaria pesada, soldadura y torneado.',
};
const { vectors: [p], ms: msProfile } = await embed([profile]);
const names = Object.keys(jobs);
const { vectors, ms } = await embed(Object.values(jobs));
console.log(`dims=${p.length}  profile=${msProfile}ms  ${names.length} jobs in one batch=${ms}ms`);
names.map((n, i) => ({ n, c: cos(p, vectors[i]) })).sort((a, b) => b.c - a.c)
  .forEach(({ n, c }) => console.log(c.toFixed(3), '→', Math.round(Math.max(0, Math.min(1, (c - 0.45) / 0.33)) * 100).toString().padStart(3), n));
