import { useEffect, useState } from 'react'
import { ArrowRight, CircleDollarSign, FilePlus2, FileText, PackageSearch, ShoppingBag } from 'lucide-react'
import { api } from '../api'
import type { BusinessSettings, Dashboard, QuoteSummary } from '../types'
import { Empty, ErrorMessage, Loading, Metric, PageHeader, Status, formatDate, money } from '../ui'

export function DashboardPage({ goTo }: { goTo: (page: string) => void }) {
  const [data, setData] = useState<Dashboard>()
  const [quotes, setQuotes] = useState<QuoteSummary[]>([])
  const [settings, setSettings] = useState<BusinessSettings>()
  const [error, setError] = useState('')

  useEffect(() => {
    Promise.all([api.dashboard(), api.quotes(), api.settings()])
      .then(([dashboard, recent, configuration]) => { setData(dashboard); setQuotes(recent.slice(0, 5)); setSettings(configuration) })
      .catch(reason => setError((reason as Error).message))
  }, [])

  return <>
    <PageHeader eyebrow="RESUMEN OPERATIVO" title="Panel de control" description="Actividad actual de cotizaciones, ventas e inventario." actions={<button onClick={() => goTo('quote')}><FilePlus2 size={17} />Nueva cotización</button>} />
    <ErrorMessage error={error} />
    {!data ? <Loading /> : <section className="metrics">
      <Metric label="Cotizaciones" value={data.quotes} icon={<FileText size={20} />} />
      <Metric label="Ventas del mes" value={data.salesThisMonth} icon={<ShoppingBag size={20} />} />
      <Metric label="Ingresos del mes" value={money(data.revenueThisMonth, settings?.currencySymbol)} icon={<CircleDollarSign size={20} />} />
      <Metric label="Consumibles con stock bajo" value={data.lowStock} icon={<PackageSearch size={20} />} />
    </section>}
    <section className="panel top-gap">
      <div className="section-title"><div><p className="eyebrow">MOVIMIENTOS RECIENTES</p><h2>Últimas cotizaciones</h2></div><button className="ghost" onClick={() => goTo('history')}>Ver historial <ArrowRight size={16} /></button></div>
      {!data ? <Loading /> : quotes.length === 0 ? <Empty>Aún no existen cotizaciones.</Empty> : <div className="table-wrap"><table>
        <thead><tr><th>Código</th><th>Fecha</th><th>Cliente</th><th>Proyecto</th><th>Precio</th><th>Estado</th></tr></thead>
        <tbody>{quotes.map(item => <tr key={item.id} className="clickable" onClick={() => goTo(`history:${item.id}`)}>
          <td className="mono">{item.orderCode}</td><td>{formatDate(item.createdAtUtc)}</td><td>{item.customer}</td><td>{item.projectName}</td>
          <td>{money(item.recommendedPrice, settings?.currencySymbol)}</td><td><Status active={Boolean(item.soldAtUtc)} trueLabel="VENDIDA" falseLabel="PENDIENTE" /></td>
        </tr>)}</tbody>
      </table></div>}
    </section>
  </>
}
