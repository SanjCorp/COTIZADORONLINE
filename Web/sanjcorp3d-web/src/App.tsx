import { FormEvent, useEffect, useState } from 'react'
import {
  BarChart3, Boxes, Calculator, CircleHelp, FileClock, Gauge, Layers3, LockKeyhole,
  LogOut, Menu, PackageSearch, Printer, Settings, ShieldCheck, Users, X,
} from 'lucide-react'
import { api, type Profile } from './api'
import { ConsumablesPage, MaterialsPage, PrintersPage } from './pages/CatalogPages'
import { DashboardPage } from './pages/DashboardPage'
import { HelpPage } from './pages/HelpPage'
import { HistoryPage } from './pages/HistoryPage'
import { QuotePage } from './pages/QuotePage'
import { ReportsPage } from './pages/ReportsPage'
import { SettingsPage } from './pages/SettingsPage'
import { UsersPage } from './pages/UsersPage'
import { hasAnyRole, roleLabel } from './ui'

type PageKey = 'dashboard' | 'quote' | 'printers' | 'consumables' | 'materials' | 'history' | 'reports' | 'users' | 'settings' | 'help'

export function App() {
  const [profile, setProfile] = useState<Profile | null>(null)
  const [loading, setLoading] = useState(true)
  useEffect(() => { api.me().then(setProfile).catch(() => setProfile(null)).finally(() => setLoading(false)) }, [])
  if (loading) return <main className="center"><div className="spinner" aria-label="Cargando" /></main>
  if (!profile) return <Login onSuccess={setProfile} />
  return <Application profile={profile} onProfileChange={setProfile} onLogout={() => api.logout().finally(() => setProfile(null))} />
}

function Login({ onSuccess }: { onSuccess: (profile: Profile) => void }) {
  const [username, setUsername] = useState(''); const [password, setPassword] = useState(''); const [code, setCode] = useState(''); const [requiresCode, setRequiresCode] = useState(false); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try { onSuccess(await api.login(username, password, code || undefined)); window.location.hash = 'dashboard' }
    catch (reason) { const authError = reason as Error & { requiresTwoFactor?: boolean }; if (authError.requiresTwoFactor) setRequiresCode(true); else setError(authError.message) }
    finally { setBusy(false) }
  }
  return <main className="login-shell"><section className="brand-panel"><div className="brand-mark"><Boxes size={34} /></div><p className="eyebrow">SANJ CORP TECHNOLOGY</p><h1>Control preciso para cada impresión.</h1><p className="lead">Cotizaciones, inventario y ventas en un espacio privado diseñado para tu equipo.</p><div className="security-note"><ShieldCheck size={20} /><span>Acceso protegido, permisos por rol y datos cifrados.</span></div></section><section className="login-panel"><form className="login-card" onSubmit={submit}><div className="lock"><LockKeyhole size={24} /></div><p className="eyebrow">ACCESO AL SISTEMA</p><h2>Iniciar sesión</h2><p className="muted">Utiliza la cuenta proporcionada por el administrador.</p><label>Usuario<input type="text" autoComplete="username" value={username} onChange={event => setUsername(event.target.value)} required autoFocus /></label><label>Contraseña<input type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} required /></label>{requiresCode && <label>Código 2FA o de recuperación<input inputMode="numeric" autoComplete="one-time-code" value={code} onChange={event => setCode(event.target.value)} required autoFocus /></label>}{error && <p className="error" role="alert">{error}</p>}<button disabled={busy}>{busy ? 'Verificando…' : 'Entrar de forma segura'}</button></form></section></main>
}

function Application({ profile, onProfileChange, onLogout }: { profile: Profile; onProfileChange: (profile: Profile) => void; onLogout: () => void }) {
  const [route, setRoute] = useState(() => window.location.hash.slice(1) || 'dashboard')
  const [mobileOpen, setMobileOpen] = useState(false)
  useEffect(() => { const listener = () => setRoute(window.location.hash.slice(1) || 'dashboard'); window.addEventListener('hashchange', listener); return () => window.removeEventListener('hashchange', listener) }, [])
  const page = route.split(':')[0] as PageKey
  const initialHistoryId = page === 'history' ? Number(route.split(':')[1] || 0) || undefined : undefined
  const isAdmin = profile.roles.includes('Administrator')
  const canCatalog = hasAnyRole(profile.roles, ['Administrator', 'Production'])
  const canSale = hasAnyRole(profile.roles, ['Administrator', 'Sales'])
  const goTo = (next: string) => { window.location.hash = next; setRoute(next); setMobileOpen(false) }
  const navigation: Array<{ key: PageKey; label: string; icon: typeof Gauge; admin?: boolean }> = [
    { key: 'dashboard', label: 'Resumen', icon: Gauge }, { key: 'quote', label: 'Cotizador', icon: Calculator },
    { key: 'printers', label: 'Impresoras', icon: Printer }, { key: 'consumables', label: 'Filamentos y resinas', icon: PackageSearch },
    { key: 'materials', label: 'Materiales', icon: Layers3 }, { key: 'history', label: 'Historial', icon: FileClock },
    { key: 'reports', label: 'Reportes y ventas', icon: BarChart3 }, { key: 'users', label: 'Usuarios', icon: Users, admin: true },
    { key: 'settings', label: 'Configuración', icon: Settings }, { key: 'help', label: 'Ayuda', icon: CircleHelp },
  ]
  let content
  switch (page) {
    case 'quote': content = <QuotePage canWrite={canSale} />; break
    case 'printers': content = <PrintersPage canEdit={canCatalog} />; break
    case 'consumables': content = <ConsumablesPage canEdit={canCatalog} />; break
    case 'materials': content = <MaterialsPage canEdit={canCatalog} />; break
    case 'history': content = <HistoryPage initialId={initialHistoryId} canSale={canSale} isAdmin={isAdmin} />; break
    case 'reports': content = <ReportsPage />; break
    case 'users': content = isAdmin ? <UsersPage /> : <DashboardPage goTo={goTo} />; break
    case 'settings': content = <SettingsPage profile={profile} isAdmin={isAdmin} onProfileChange={onProfileChange} />; break
    case 'help': content = <HelpPage />; break
    default: content = <DashboardPage goTo={goTo} />
  }
  return <div className="app-shell">
    {mobileOpen && <button className="sidebar-scrim" aria-label="Cerrar menú" onClick={() => setMobileOpen(false)} />}
    <aside className={`sidebar ${mobileOpen ? 'open' : ''}`}><div className="sidebar-brand"><div className="brand-mark small-mark"><Boxes size={23} /></div><div><strong>SANJ CORP</strong><span>3D OPERATIONS</span></div><button className="icon mobile-close" onClick={() => setMobileOpen(false)}><X size={20} /></button></div><nav>{navigation.filter(item => !item.admin || isAdmin).map(({ key, label, icon: Icon }) => <button key={key} className={page === key ? 'active' : ''} onClick={() => goTo(key)}><Icon size={18} /><span>{label}</span></button>)}</nav><div className="sidebar-footer"><div className="signed-user"><div className="avatar">{profile.displayName.slice(0, 2).toUpperCase()}</div><div><strong>{profile.displayName}</strong><span>{profile.roles.map(roleLabel).join(' · ')}</span></div></div><button className="logout" onClick={onLogout}><LogOut size={17} />Cerrar sesión</button></div></aside>
    <div className="workspace"><header className="topbar"><button className="icon menu-button" onClick={() => setMobileOpen(true)}><Menu size={21} /></button><div><span className="connection-dot" />Servidor conectado</div><button className="profile-chip" onClick={() => goTo('settings')}><span>{profile.displayName}</span><div className="avatar mini">{profile.displayName.slice(0, 2).toUpperCase()}</div></button></header><main className="page-content">{content}</main></div>
  </div>
}
