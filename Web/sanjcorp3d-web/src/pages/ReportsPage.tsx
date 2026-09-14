import { useCallback, useEffect, useState } from 'react'
import { BadgeDollarSign, CircleDollarSign, Download, FileText, TrendingUp } from 'lucide-react'
import { api } from '../api'
import type { BusinessSettings, Report } from '../types'
import { Empty, ErrorMessage, Loading, Metric, PageHeader, firstDayOfMonth, formatDate, money, number, todayInput } from '../ui'

export function ReportsPage() {
  const [from, setFrom] = useState(firstDayOfMonth()); const [to, setTo] = useState(todayInput())
  const [data, setData] = useState<Report>(); const [settings, setSettings] = useState<BusinessSettings>(); const [error, setError] = useState(''); const [loading, setLoading] = useState(true)
  const load = useCallback(() => { setLoading(true); setError(''); Promise.all([api.report(from, to), api.settings()]).then(([report, configuration]) => { setData(report); setSettings(configuration) }).catch(reason => setError((reason as Error).message)).finally(() => setLoading(false)) }, [from, to])
  useEffect(load, [load])
  const symbol = settings?.currencySymbol

  return <>
    <PageHeader eyebrow="ANÁLISIS" title="Reportes y ventas" description="Resultados proyectados, ventas confirmadas y distribución de costos." actions={<div className="button-group"><button className="secondary" onClick={() => api.exportQuotes({ from, to }).catch(reason => setError((reason as Error).message))}><Download size={16} />Cotizaciones CSV</button><button className="secondary" onClick={() => api.exportSales(from, to).catch(reason => setError((reason as Error).message))}><Download size={16} />Ventas CSV</button></div>} />
    <ErrorMessage error={error} />
    <section className="panel filters-panel"><div className="filters"><label>Desde<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label><label>Hasta<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label><button onClick={load}>Actualizar reporte</button></div></section>
    {loading || !data ? <Loading /> : <>
      <section className="metrics report-metrics"><Metric label="Cotizaciones" value={data.quoteCount} icon={<FileText size={20} />} /><Metric label="Costo proyectado" value={money(data.totalCost, symbol)} icon={<CircleDollarSign size={20} />} /><Metric label="Ingreso proyectado" value={money(data.projectedRevenue, symbol)} icon={<TrendingUp size={20} />} /><Metric label="Ganancia proyectada" value={money(data.projectedProfit, symbol)} icon={<BadgeDollarSign size={20} />} /></section>
      <section className="metrics sales-metrics"><Metric label="Ventas confirmadas" value={data.saleCount} icon={<FileText size={20} />} /><Metric label="Ingresos reales" value={money(data.salesRevenue, symbol)} icon={<CircleDollarSign size={20} />} /><Metric label="Ganancia real" value={money(data.salesProfit, symbol)} icon={<TrendingUp size={20} />} /></section>
      <div className="report-grid top-gap">
        <section className="panel"><div className="section-title"><div><p className="eyebrow">PRODUCTOS</p><h2>Lo más vendido</h2></div></div><Bars items={data.productDistribution.map(x => ({ name: x.name, value: x.quantity, label: `${x.quantity} pieza(s)` }))} /></section>
        <section className="panel"><div className="section-title"><div><p className="eyebrow">CONSUMO</p><h2>Distribución por consumible</h2></div></div><Bars items={data.consumableDistribution.map(x => ({ name: x.name, value: x.grams, label: `${number(x.grams)} g` }))} /></section>
        <section className="panel"><div className="section-title"><div><p className="eyebrow">ESTRUCTURA</p><h2>Distribución de costos</h2></div></div><Bars items={data.costDistribution.map(x => ({ name: x.name, value: x.value, label: money(x.value, symbol) }))} /></section>
      </div>
      <section className="panel top-gap"><div className="section-title"><div><p className="eyebrow">PÉRDIDAS</p><h2>Productos fallidos</h2></div></div><Bars items={data.lossDistribution.map(x => ({ name: x.name, value: x.grams, label: `${number(x.grams)} g` }))} /></section>
      <section className="panel top-gap"><div className="section-title"><div><p className="eyebrow">VENTAS GENERADAS</p><h2>Detalle del período</h2></div></div>{data.sales.length === 0 ? <Empty>No hay ventas confirmadas en este período.</Empty> : <div className="table-wrap"><table><thead><tr><th>Código</th><th>Fecha</th><th>Cliente</th><th>Proyecto</th><th>Costo</th><th>Venta</th><th>Ganancia</th></tr></thead><tbody>{data.sales.map(item => <tr key={item.id}><td className="mono">{item.orderCode}</td><td>{formatDate(item.soldAtUtc)}</td><td>{item.customer}</td><td>{item.projectName}</td><td>{money(item.costTotal, symbol)}</td><td><strong>{money(item.saleAmount, symbol)}</strong></td><td className={item.profit >= 0 ? 'positive' : 'negative'}>{money(item.profit, symbol)}</td></tr>)}</tbody></table></div>}</section>
    </>}
  </>
}

function Bars({ items }: { items: Array<{ name: string; value: number; label: string }> }) {
  const max = Math.max(...items.map(x => x.value), 0)
  if (!items.length || max <= 0) return <Empty>Sin valores para representar.</Empty>
  return <div className="bars">{items.map(item => <div className="bar-row" key={item.name}><div className="bar-label"><span>{item.name}</span><b>{item.label}</b></div><div className="bar-track"><span style={{ width: `${Math.max(2, item.value / max * 100)}%` }} /></div></div>)}</div>
}
