import { FormEvent, useCallback, useEffect, useState } from 'react'
import { BarChart3, CheckCircle2, Plus, ShieldCheck, UserPlus, UserRoundX, Users, XCircle } from 'lucide-react'
import { api } from '../api'
import type { Profile, Report, Tenant, UserAccount } from '../types'
import { ErrorMessage, Loading, Metric, PageHeader, firstDayOfMonth, money, todayInput } from '../ui'

type Mode = 'technology' | 'makers'

export function SupremeAdminPage({ profile, mode }: { profile: Profile; mode: Mode }) {
  const [tenants, setTenants] = useState<Tenant[]>([])
  const [selected, setSelected] = useState('')
  const [tab, setTab] = useState<'summary' | 'users'>('summary')
  const [report, setReport] = useState<Report>()
  const [users, setUsers] = useState<UserAccount[]>([])
  const [userId, setUserId] = useState('')
  const [from, setFrom] = useState(firstDayOfMonth())
  const [to, setTo] = useState(todayInput())
  const [draft, setDraft] = useState(false)
  const [makerDraft, setMakerDraft] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const technology = profile.workspaces?.find(x => x.kind === 'technology')?.id ?? '00000000-0000-0000-0000-000000000001'
  const makers = tenants.filter(x => x.kind === 'maker')

  const loadTenants = useCallback(async () => {
    const items = await api.tenants()
    setTenants(items)
    setSelected(current => current && items.some(x => x.id === current) ? current : mode === 'makers' ? (items.find(x => x.kind === 'maker')?.id ?? '') : technology)
  }, [mode, technology])
  useEffect(() => { loadTenants().catch(e => setError((e as Error).message)) }, [loadTenants])
  const tenant = tenants.find(x => x.id === selected)
  const isMaker = tenant?.kind === 'maker'

  useEffect(() => {
    if (!selected) return
    sessionStorage.setItem('sanjcorp.tenant', selected)
    setBusy(true); setError('')
    Promise.all([api.report(from, to, isMaker && userId ? userId : undefined), api.users()])
      .then(([next, accounts]) => { setReport(next); setUsers(accounts) })
      .catch(e => setError((e as Error).message)).finally(() => setBusy(false))
  }, [selected, from, to, userId, isMaker])

  async function createUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selected) return
    const form = new FormData(event.currentTarget); setBusy(true); setError('')
    try {
      const item = { username: String(form.get('username')), displayName: String(form.get('displayName')), email: String(form.get('email') || ''), password: String(form.get('password')) }
      if (isMaker) await api.createMakerUser(selected, item)
      else await api.createUser({ ...item, role: String(form.get('role')) })
      setDraft(false); setUsers(await api.users())
    } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }

  async function createMaker(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = new FormData(event.currentTarget); setBusy(true); setError('')
    try {
      await api.createMakerTenant({ name: String(form.get('name')), slug: String(form.get('slug')), username: String(form.get('username')), displayName: String(form.get('displayName')), email: String(form.get('email') || ''), password: String(form.get('password')) })
      setMakerDraft(false); await loadTenants()
    } catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }

  async function toggleMaker(item: Tenant) {
    setBusy(true); setError('')
    try { await api.updateTenant(item.id, { name: item.name, logoUrl: item.logoUrl, active: !item.active }); await loadTenants() }
    catch (e) { setError((e as Error).message) } finally { setBusy(false) }
  }

  return <>
    <PageHeader eyebrow="ADMINISTRACIÓN SUPREMA" title={mode === 'makers' ? 'Makers' : 'SanjCorp Technology'} description="Consulta reportes y administra los accesos sin mezclar datos entre espacios." actions={mode === 'makers' ? <button onClick={() => setMakerDraft(true)}><Plus size={16} />Nueva cuenta Maker</button> : undefined} />
    <ErrorMessage error={error} />
    <div className="tabs"><button className={tab === 'summary' ? 'active' : ''} onClick={() => setTab('summary')}><BarChart3 size={16} />Reportes y ventas</button><button className={tab === 'users' ? 'active' : ''} onClick={() => setTab('users')}><Users size={16} />Usuarios</button></div>
    {mode === 'makers' && <section className="panel maker-list"><div className="section-title"><div><p className="eyebrow">CUENTAS MAKER</p><h2>Espacios independientes</h2></div><span className="muted">{makers.length} cuenta{makers.length === 1 ? '' : 's'}</span></div>{makers.length === 0 ? <p className="muted">Todavía no hay cuentas Maker.</p> : <div className="maker-cards">{makers.map(item => <article className={`maker-card ${selected === item.id ? 'selected' : ''}`} key={item.id}><button className="maker-card-select" onClick={() => { setSelected(item.id); setUserId('') }}><strong>{item.name}</strong><small>{item.userCount} de 3 usuarios · {item.active ? 'Activa' : 'Inactiva'}</small></button><button className={`ghost small ${item.active ? 'danger' : ''}`} disabled={busy} onClick={() => toggleMaker(item)}>{item.active ? <><UserRoundX size={15} />Inactivar</> : <><CheckCircle2 size={15} />Activar</>}</button></article>)}</div>}</section>}
    {tab === 'summary' && <><section className="panel filters-panel"><div className="filters"><label>Espacio<select value={selected} onChange={e => { setSelected(e.target.value); setUserId('') }}>{mode === 'technology' ? <option value={technology}>SanjCorp Technology</option> : makers.map(x => <option key={x.id} value={x.id} disabled={!x.active}>{x.name}{x.active ? '' : ' (inactiva)'}</option>)}</select></label><label>Desde<input type="date" value={from} onChange={e => setFrom(e.target.value)} /></label><label>Hasta<input type="date" value={to} onChange={e => setTo(e.target.value)} /></label>{isMaker && <label>Usuario Maker<select value={userId} onChange={e => setUserId(e.target.value)}><option value="">Todos los usuarios</option>{users.map(x => <option key={x.id} value={x.id}>{x.displayName} (@{x.userName})</option>)}</select></label>}</div></section>{busy || !report ? <Loading /> : <section className="metrics report-metrics"><Metric label="Ventas confirmadas" value={report.saleCount} icon={<BarChart3 size={20} />} /><Metric label="Ingresos reales" value={money(report.salesRevenue)} icon={<BarChart3 size={20} />} /><Metric label="Ganancia real" value={money(report.salesProfit)} icon={<ShieldCheck size={20} />} /><Metric label="Cotizaciones" value={report.quoteCount} icon={<BarChart3 size={20} />} /></section>}</>}
    {tab === 'users' && <section className="panel"><div className="section-title"><div><p className="eyebrow">{tenant?.name ?? 'ESPACIO'}</p><h2>Usuarios y accesos</h2></div><button disabled={!tenant?.active || (isMaker && users.length >= 3)} onClick={() => setDraft(true)}><UserPlus size={16} />Agregar usuario</button></div>{isMaker && <p className="field-help">Una cuenta Maker admite hasta tres usuarios y comparte datos únicamente dentro de este espacio.</p>}{users.length === 0 ? <p className="muted">No hay usuarios en este espacio.</p> : <div className="user-cards">{users.map(x => <article className="user-card" key={x.id}><div className="avatar">{x.displayName.slice(0, 2).toUpperCase()}</div><div className="user-card-main"><h3>{x.displayName}</h3><p>@{x.userName}{x.email ? ` · ${x.email}` : ''}</p></div><strong>{x.role}</strong></article>)}</div>}{draft && <form className="panel editor top-gap" onSubmit={createUser}><div className="section-title"><h2>Nuevo usuario</h2><button type="button" className="ghost" onClick={() => setDraft(false)}><XCircle size={16} />Cancelar</button></div><label>Usuario<input name="username" required /></label><label>Nombre visible<input name="displayName" required /></label><label>Correo opcional<input name="email" type="email" /></label>{!isMaker && <label>Rol<select name="role" defaultValue="Viewer"><option>Administrator</option><option>Sales</option><option>Production</option><option>Viewer</option></select></label>}<label>Contraseña inicial<input name="password" type="password" minLength={9} required /></label><button disabled={busy}>{busy ? 'Guardando…' : 'Crear usuario'}</button></form>}</section>}
    {makerDraft && <form className="panel editor top-gap" onSubmit={createMaker}><div className="section-title"><div><p className="eyebrow">NUEVO ESPACIO AISLADO</p><h2>Crear cuenta Maker</h2></div><button type="button" className="ghost" onClick={() => setMakerDraft(false)}><XCircle size={16} />Cancelar</button></div><label>Nombre de la cuenta<input name="name" placeholder="Ej. Maker de Ana" required /></label><label>Identificador<input name="slug" placeholder="maker-ana" pattern="[A-Za-z0-9-]+" required /></label><label>Usuario principal<input name="username" required /></label><label>Nombre visible<input name="displayName" required /></label><label>Correo opcional<input name="email" type="email" /></label><label>Contraseña inicial<input name="password" type="password" minLength={9} required /></label><button disabled={busy}>{busy ? 'Creando…' : 'Crear cuenta Maker'}</button></form>}
  </>
}
