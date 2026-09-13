import { useEffect, useMemo, useState } from 'react'
import { Calculator, CheckCircle2, Plus, RotateCcw, Save, ShoppingBag, Trash2 } from 'lucide-react'
import { api } from '../api'
import type { BusinessSettings, Consumable, ConsumableUsage, ExtraMaterial, MaterialUsage, Printer, QuoteCalculation, QuoteRequest, QuoteSummary } from '../types'
import { Empty, ErrorMessage, Loading, PageHeader, SuccessMessage, money, number, weight } from '../ui'

type BaseForm = { customer: string; projectName: string; printerId: number; hours: number; minutes: number; quantity: number; additionalManualCost: number; profitMultiplier: number; notes: string }
const emptyForm: BaseForm = { customer: '', projectName: '', printerId: 0, hours: 0, minutes: 0, quantity: 1, additionalManualCost: 0, profitMultiplier: 1.3, notes: '' }

export function QuotePage({ canWrite }: { canWrite: boolean }) {
  const [printers, setPrinters] = useState<Printer[]>([])
  const [consumables, setConsumables] = useState<Consumable[]>([])
  const [materials, setMaterials] = useState<ExtraMaterial[]>([])
  const [settings, setSettings] = useState<BusinessSettings>()
  const [form, setForm] = useState<BaseForm>(emptyForm)
  const [consumableLines, setConsumableLines] = useState<ConsumableUsage[]>([])
  const [materialLines, setMaterialLines] = useState<MaterialUsage[]>([])
  const [selectedConsumable, setSelectedConsumable] = useState(0)
  const [grams, setGrams] = useState(0)
  const [selectedMaterial, setSelectedMaterial] = useState(0)
  const [materialQuantity, setMaterialQuantity] = useState(1)
  const [calculation, setCalculation] = useState<QuoteCalculation>()
  const [saved, setSaved] = useState<QuoteSummary>()
  const [sold, setSold] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    Promise.all([api.printers(), api.consumables(), api.materials(), api.settings()])
      .then(([printerItems, consumableItems, materialItems, configuration]) => {
        const favoritePrinters = printerItems.filter(x => x.isDefault)
        setPrinters(favoritePrinters); setConsumables(consumableItems); setMaterials(materialItems); setSettings(configuration)
        setForm(current => ({ ...current, printerId: favoritePrinters[0]?.id ?? 0, profitMultiplier: configuration.defaultProfitMultiplier }))
        setSelectedConsumable(consumableItems.find(x => x.isDefault)?.id ?? consumableItems[0]?.id ?? 0)
        setSelectedMaterial(materialItems[0]?.id ?? 0)
      })
      .catch(reason => setError((reason as Error).message)).finally(() => setLoading(false))
  }, [])

  const consumableById = useMemo(() => new Map(consumables.map(x => [x.id, x])), [consumables])
  const materialById = useMemo(() => new Map(materials.map(x => [x.id, x])), [materials])

  function invalidate() { setCalculation(undefined); setSaved(undefined); setSold(false); setSuccess('') }
  function update<K extends keyof BaseForm>(key: K, value: BaseForm[K]) { setForm(current => ({ ...current, [key]: value })); invalidate() }

  function addConsumable() {
    const item = consumableById.get(selectedConsumable)
    if (!item || grams <= 0) { setError('Selecciona un consumible e indica gramos mayores que cero.'); return }
    if ((item.stockGrams ?? item.stockQuantity * 1000) <= 0) { setError(`No hay existencia de ${item.name} · ${item.material} · ${item.color}. Actualiza su inventario primero.`); return }
    setConsumableLines(current => [...current, { consumableId: item.id, grams }]); setGrams(0); setError(''); invalidate()
  }

  function addMaterial() {
    if (!materialById.has(selectedMaterial) || materialQuantity <= 0) { setError('Selecciona un material e indica una cantidad mayor que cero.'); return }
    setMaterialLines(current => [...current, { materialId: selectedMaterial, quantity: materialQuantity }]); setMaterialQuantity(1); setError(''); invalidate()
  }

  function request(): QuoteRequest {
    return { customer: form.customer, projectName: form.projectName, printerId: form.printerId, printHours: form.hours + form.minutes / 60, quantity: form.quantity, additionalManualCost: form.additionalManualCost, profitMultiplier: form.profitMultiplier, notes: form.notes, consumables: consumableLines, materials: materialLines }
  }

  async function calculate() {
    setBusy(true); setError(''); setSuccess('')
    try { setCalculation(await api.calculateQuote(request())) }
    catch (reason) { setError((reason as Error).message) }
    finally { setBusy(false) }
  }

  async function save() {
    setBusy(true); setError(''); setSuccess('')
    try {
      const payload = request()
      const [result, totals] = await Promise.all([api.createQuote(payload), api.calculateQuote(payload)])
      setSaved(result); setCalculation(totals); setSuccess(`Cotización ${result.orderCode} guardada correctamente.`)
    } catch (reason) { setError((reason as Error).message) }
    finally { setBusy(false) }
  }

  async function confirmSale() {
    if (!saved || !window.confirm(`¿Confirmar la venta ${saved.orderCode} por ${money(saved.recommendedPrice, settings?.currencySymbol)}?`)) return
    setBusy(true); setError('')
    try { await api.confirmSale(saved.id); setSold(true); setSuccess('Venta confirmada y filamento descontado del inventario.'); window.dispatchEvent(new Event('inventory-changed')) }
    catch (reason) { setError((reason as Error).message) }
    finally { setBusy(false) }
  }

  function clear() {
    setForm({ ...emptyForm, printerId: printers[0]?.id ?? 0, profitMultiplier: settings?.defaultProfitMultiplier ?? 1.3 })
    setConsumableLines([]); setMaterialLines([]); setCalculation(undefined); setSaved(undefined); setSold(false); setError(''); setSuccess('')
  }

  if (loading) return <Loading label="Cargando catálogos…" />
  if (!canWrite) return <><PageHeader eyebrow="COTIZADOR" title="Nueva cotización" description="Cálculo de costos y precio recomendado." /><div className="panel locked-panel"><h2>Acceso de consulta</h2><p>Tu rol puede revisar el historial, pero no crear cotizaciones. Solicita al administrador el rol Ventas si necesitas esta función.</p></div></>

  return <>
    <PageHeader eyebrow="COTIZADOR 3D" title="Nueva cotización" description="Registra los datos de impresión, combina consumibles y obtén el precio con la fórmula original." />
    <ErrorMessage error={error} /><SuccessMessage message={success} />
    {printers.length === 0 ? <div className="alert error">Elige al menos una impresora favorita desde el apartado Impresoras antes de cotizar.</div> : null}
    {consumables.length === 0 ? <div className="alert error">Necesitas al menos un consumible activo antes de cotizar.</div> : null}
    <div className="quote-layout">
      <div className="form-stack">
        <section className="panel">
          <div className="section-title"><div><span className="step">01</span><h2>Proyecto e impresión</h2></div></div>
          <div className="form-grid two">
            <label>Cliente<input value={form.customer} onChange={e => update('customer', e.target.value)} placeholder="Nombre del cliente" /></label>
            <label>Pieza o proyecto<input value={form.projectName} onChange={e => update('projectName', e.target.value)} placeholder="Ej. Soporte personalizado" /></label>
            <label className="span-2">Impresora<select value={form.printerId} onChange={e => update('printerId', Number(e.target.value))}>{printers.map(x => <option key={x.id} value={x.id}>{x.name} · {x.buildX}×{x.buildY}×{x.buildZ} mm</option>)}</select></label>
            <label>Horas<input type="number" min="0" step="1" value={form.hours} onChange={e => update('hours', Number(e.target.value))} /></label>
            <label>Minutos<input type="number" min="0" max="59" step="1" value={form.minutes} onChange={e => update('minutes', Number(e.target.value))} /></label>
            <label>Cantidad de piezas<input type="number" min="1" step="1" value={form.quantity} onChange={e => update('quantity', Number(e.target.value))} /></label>
            <label>Multiplicador de ganancia<input type="number" min="1" step="0.01" value={form.profitMultiplier} onChange={e => update('profitMultiplier', Number(e.target.value))} /></label>
          </div>
        </section>

        <section className="panel">
          <div className="section-title"><div><span className="step">02</span><h2>Filamentos y resinas</h2></div></div>
          <div className="inline-form consumable-picker">
            <label>Consumible<select value={selectedConsumable} onChange={e => setSelectedConsumable(Number(e.target.value))}>{consumables.map(x => <option key={x.id} value={x.id}>{x.name} · {x.material} · {x.color} · {weight(x.stockGrams ?? x.stockQuantity * 1000)} disp.</option>)}</select></label>
            <label>Gramos<input type="number" min="0.01" step="0.01" value={grams} onChange={e => setGrams(Number(e.target.value))} /></label>
            <button type="button" onClick={addConsumable}><Plus size={17} />Agregar</button>
          </div>
          {consumableLines.length === 0 ? <Empty>Agrega al menos un filamento o resina.</Empty> : <div className="line-list">{consumableLines.map((line, index) => {
            const item = consumableById.get(line.consumableId)
            return <div className="line-item" key={`${line.consumableId}-${index}`}><span className="color-dot" style={{ background: item?.color.toLowerCase() }} /><div><strong>{item?.name} · {item?.material}</strong><small>{item?.category} · {item?.color}</small></div><b>{number(line.grams)} g</b><button className="icon danger" aria-label="Quitar" onClick={() => { setConsumableLines(current => current.filter((_, i) => i !== index)); invalidate() }}><Trash2 size={16} /></button></div>
          })}</div>}
        </section>

        <section className="panel">
          <div className="section-title"><div><span className="step">03</span><h2>Materiales adicionales</h2></div></div>
          <div className="inline-form material-picker">
            <label>Material<select value={selectedMaterial} onChange={e => setSelectedMaterial(Number(e.target.value))}><option value={0}>Seleccionar…</option>{materials.map(x => <option key={x.id} value={x.id}>{x.name} · {money(x.unitPrice, settings?.currencySymbol)}/{x.unit}</option>)}</select></label>
            <label>Cantidad<input type="number" min="0.01" step="0.01" value={materialQuantity} onChange={e => setMaterialQuantity(Number(e.target.value))} /></label>
            <button type="button" className="secondary" onClick={addMaterial}><Plus size={17} />Agregar</button>
          </div>
          {materialLines.length > 0 && <div className="line-list">{materialLines.map((line, index) => { const item = materialById.get(line.materialId); return <div className="line-item" key={`${line.materialId}-${index}`}><div><strong>{item?.name}</strong><small>{item?.category} · {item?.unit}</small></div><b>{number(line.quantity)} × {money(item?.unitPrice ?? 0, settings?.currencySymbol)}</b><button className="icon danger" aria-label="Quitar" onClick={() => { setMaterialLines(current => current.filter((_, i) => i !== index)); invalidate() }}><Trash2 size={16} /></button></div> })}</div>}
          <div className="form-grid two compact-fields">
            <label>Costo adicional manual<input type="number" min="0" step="0.01" value={form.additionalManualCost} onChange={e => update('additionalManualCost', Number(e.target.value))} /></label>
            <label className="span-2">Notas<textarea rows={3} value={form.notes} onChange={e => update('notes', e.target.value)} placeholder="Acabados, entrega, observaciones…" /></label>
          </div>
        </section>
      </div>

      <aside className="quote-summary panel">
        <p className="eyebrow">RESULTADO</p><h2>Precio recomendado</h2>
        {!calculation ? <div className="summary-placeholder"><Calculator size={34} /><p>Completa los datos y calcula para ver el desglose.</p></div> : <>
          <div className="price-hero"><small>Total sugerido</small><strong>{money(calculation.recommendedPrice, settings?.currencySymbol)}</strong><span>{number(calculation.totalWeight)} g totales</span></div>
          <dl className="breakdown">
            <div><dt>Consumibles</dt><dd>{money(calculation.materialCost, settings?.currencySymbol)}</dd></div>
            <div><dt>Electricidad</dt><dd>{money(calculation.electricityCost, settings?.currencySymbol)}</dd></div>
            <div><dt>Mantenimiento</dt><dd>{money(calculation.maintenanceCost, settings?.currencySymbol)}</dd></div>
            <div><dt>Adicionales</dt><dd>{money(calculation.additionalCost, settings?.currencySymbol)}</dd></div>
            <div className="subtotal"><dt>Costo total</dt><dd>{money(calculation.subtotal, settings?.currencySymbol)}</dd></div>
            <div><dt>Ganancia</dt><dd>{money(calculation.profitAmount, settings?.currencySymbol)}</dd></div>
            <div><dt>Impuesto</dt><dd>{money(calculation.taxAmount, settings?.currencySymbol)}</dd></div>
          </dl>
        </>}
        {saved && <div className="saved-ticket"><CheckCircle2 size={19} /><div><small>Código guardado</small><strong>{saved.orderCode}</strong></div><StatusSale sold={sold} /></div>}
        <div className="summary-actions">
          <button className="secondary" disabled={busy || printers.length === 0 || consumables.length === 0} onClick={calculate}><Calculator size={17} />Calcular precio</button>
          <button disabled={busy || printers.length === 0 || consumables.length === 0} onClick={save}><Save size={17} />Guardar cotización</button>
          {saved && !sold && <button className="sale-button" disabled={busy} onClick={confirmSale}><ShoppingBag size={17} />Confirmar venta</button>}
          <button className="ghost" disabled={busy} onClick={clear}><RotateCcw size={16} />Limpiar</button>
        </div>
      </aside>
    </div>
  </>
}

function StatusSale({ sold }: { sold: boolean }) { return <span className={`status ${sold ? 'ok' : 'warning'}`}>{sold ? 'VENDIDA' : 'PENDIENTE'}</span> }
