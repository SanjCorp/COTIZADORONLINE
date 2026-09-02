import { useCallback, useEffect, useState } from 'react'
import { Download, Eye, Printer, Search, ShoppingBag, Trash2, X } from 'lucide-react'
import { api } from '../api'
import type { BusinessSettings, QuoteDetail, QuoteSummary } from '../types'
import { Empty, ErrorMessage, Loading, PageHeader, Status, SuccessMessage, formatDate, money, number } from '../ui'

export function HistoryPage({ initialId, canSale, isAdmin }: { initialId?: number; canSale: boolean; isAdmin: boolean }) {
  const [items, setItems] = useState<QuoteSummary[]>([])
  const [detail, setDetail] = useState<QuoteDetail>()
  const [settings, setSettings] = useState<BusinessSettings>()
  const [search, setSearch] = useState(''); const [from, setFrom] = useState(''); const [to, setTo] = useState('')
  const [error, setError] = useState(''); const [success, setSuccess] = useState(''); const [loading, setLoading] = useState(true); const [detailLoading, setDetailLoading] = useState(false)

  const load = useCallback(() => { setLoading(true); setError(''); api.quotes({ search, from, to }).then(setItems).catch(reason => setError((reason as Error).message)).finally(() => setLoading(false)) }, [search, from, to])
  useEffect(() => { api.settings().then(setSettings).catch(() => undefined) }, [])
  useEffect(() => { load() }, [load])
  useEffect(() => { if (initialId) open(initialId) }, [initialId])

  async function open(id: number) { setDetailLoading(true); setError(''); try { setDetail(await api.quote(id)) } catch (reason) { setError((reason as Error).message) } finally { setDetailLoading(false) } }
  async function sale() { if (!detail || !window.confirm(`¿Confirmar la venta ${detail.orderCode} por ${money(detail.recommendedPrice, settings?.currencySymbol)}?`)) return; try { await api.confirmSale(detail.id); setSuccess('Venta confirmada correctamente.'); await open(detail.id); load() } catch (reason) { setError((reason as Error).message) } }
  async function remove() { if (!detail || !window.confirm(`¿Eliminar definitivamente ${detail.orderCode}?${detail.sale ? ' También se eliminará su venta.' : ''}`)) return; try { await api.deleteQuote(detail.id); setDetail(undefined); setSuccess('Cotización eliminada.'); load() } catch (reason) { setError((reason as Error).message) } }

  return <>
    <PageHeader eyebrow="REGISTRO" title="Historial de cotizaciones" description="Busca por código, cliente o proyecto; revisa el detalle y confirma ventas." actions={<button className="secondary" onClick={() => api.exportQuotes({ search, from, to }).catch(reason => setError((reason as Error).message))}><Download size={17} />Exportar CSV</button>} />
    <ErrorMessage error={error} /><SuccessMessage message={success} />
    <section className="panel">
      <div className="filters"><label className="search-field"><Search size={17} /><input value={search} onChange={e => setSearch(e.target.value)} placeholder="Código, cliente o proyecto…" /></label><label>Desde<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label><label>Hasta<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label><button className="ghost" onClick={load}>Actualizar</button></div>
      {loading ? <Loading /> : items.length === 0 ? <Empty>No se encontraron cotizaciones.</Empty> : <div className="table-wrap"><table><thead><tr><th>Código</th><th>Fecha</th><th>Cliente / proyecto</th><th>Impresora</th><th>Peso</th><th>Costo</th><th>Precio</th><th>Estado</th><th></th></tr></thead><tbody>{items.map(item => <tr key={item.id} className="clickable" onClick={() => open(item.id)}><td className="mono">{item.orderCode}</td><td>{formatDate(item.createdAtUtc)}</td><td><strong>{item.customer}</strong><small className="cell-note">{item.projectName}</small></td><td>{item.printerName}</td><td>{number(item.totalWeight)} g</td><td>{money(item.costTotal, settings?.currencySymbol)}</td><td><strong>{money(item.recommendedPrice, settings?.currencySymbol)}</strong></td><td><Status active={Boolean(item.soldAtUtc)} trueLabel="VENDIDA" falseLabel="PENDIENTE" /></td><td><button className="icon ghost" aria-label="Ver detalle"><Eye size={16} /></button></td></tr>)}</tbody></table></div>}
    </section>
    {(detail || detailLoading) && <div className="drawer-backdrop" onMouseDown={event => { if (event.target === event.currentTarget) setDetail(undefined) }}><aside className="detail-drawer printable">
      {detailLoading && !detail ? <Loading /> : detail && <>
        <div className="drawer-header"><div><p className="eyebrow">COTIZACIÓN</p><h2 className="mono">{detail.orderCode}</h2></div><button className="icon ghost no-print" onClick={() => setDetail(undefined)}><X size={20} /></button></div>
        <div className="detail-status"><Status active={Boolean(detail.sale)} trueLabel="VENTA CONFIRMADA" falseLabel="PENDIENTE" /><span>{formatDate(detail.sale?.soldAtUtc ?? detail.createdAtUtc)}</span></div>
        <section className="detail-section"><h3>Información</h3><dl className="detail-grid"><div><dt>Cliente</dt><dd>{detail.customer}</dd></div><div><dt>Proyecto</dt><dd>{detail.projectName}</dd></div><div><dt>Impresora</dt><dd>{detail.printerName}</dd></div><div><dt>Producción</dt><dd>{number(detail.printHours)} h · {detail.quantity} pieza(s)</dd></div></dl></section>
        <section className="detail-section"><h3>Consumibles</h3>{detail.consumables.map(line => <div className="detail-line" key={line.id}><span>{line.name} · {line.material} · {line.color}<small>{number(line.grams)} g por pieza</small></span><b>{money(line.lineCost, settings?.currencySymbol)}</b></div>)}</section>
        {detail.materials.length > 0 && <section className="detail-section"><h3>Materiales adicionales</h3>{detail.materials.map(line => <div className="detail-line" key={line.id}><span>{line.name}<small>{number(line.quantity)} × {money(line.unitPrice, settings?.currencySymbol)}</small></span><b>{money(line.lineCost, settings?.currencySymbol)}</b></div>)}</section>}
        <section className="detail-section"><h3>Desglose</h3><dl className="breakdown"><div><dt>Consumibles</dt><dd>{money(detail.materialCost, settings?.currencySymbol)}</dd></div><div><dt>Electricidad</dt><dd>{money(detail.electricityCost, settings?.currencySymbol)}</dd></div><div><dt>Mantenimiento</dt><dd>{money(detail.maintenanceCost, settings?.currencySymbol)}</dd></div><div><dt>Adicionales</dt><dd>{money(detail.additionalCost, settings?.currencySymbol)}</dd></div><div className="subtotal"><dt>Costo total</dt><dd>{money(detail.subtotal, settings?.currencySymbol)}</dd></div><div><dt>Ganancia</dt><dd>{money(detail.profitAmount, settings?.currencySymbol)}</dd></div><div><dt>Impuesto</dt><dd>{money(detail.taxAmount, settings?.currencySymbol)}</dd></div></dl><div className="detail-total"><span>Precio recomendado</span><strong>{money(detail.recommendedPrice, settings?.currencySymbol)}</strong></div></section>
        {detail.notes && <section className="detail-section"><h3>Notas</h3><p className="notes">{detail.notes}</p></section>}
        <div className="drawer-actions no-print"><button className="ghost" onClick={() => window.print()}><Printer size={16} />Imprimir</button>{canSale && !detail.sale && <button className="sale-button" onClick={sale}><ShoppingBag size={16} />Confirmar venta</button>}{isAdmin && <button className="ghost danger-text" onClick={remove}><Trash2 size={16} />Eliminar</button>}</div>
      </>}
    </aside></div>}
  </>
}
