import type {
  BusinessSettings, Consumable, Dashboard, ExtraMaterial, Printer, Profile, QuoteCalculation,
  QuoteDetail, QuoteRequest, QuoteSummary, Report, TwoFactorSetup, UserAccount, InventoryAlert, Tenant,
} from './types'

export type { Dashboard, Profile } from './types'

type ApiError = Error & { status: number; requiresTwoFactor?: boolean }

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const tenant = sessionStorage.getItem('sanjcorp.tenant')
  const response = await fetch(path, {
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(tenant ? { 'X-Tenant-Id': tenant } : {}), ...options?.headers },
    ...options,
  })
  if (!response.ok) {
    const body = await response.json().catch(() => ({})) as { message?: string; title?: string; requiresTwoFactor?: boolean }
    const error = new Error(body.message ?? (response.status >= 500 ? 'El servidor no pudo completar la operación. Revisa los registros de Render.' : body.title) ?? 'No se pudo completar la operación.') as ApiError
    error.status = response.status
    error.requiresTwoFactor = body.requiresTwoFactor
    throw error
  }
  return response.status === 204 ? undefined as T : response.json()
}

function query(values: Record<string, string | number | boolean | undefined>) {
  const params = new URLSearchParams()
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined && value !== '') params.set(key, String(value))
  })
  const text = params.toString()
  return text ? `?${text}` : ''
}

async function download(path: string, fallbackName: string) {
  const tenant = sessionStorage.getItem('sanjcorp.tenant')
  const response = await fetch(path, { credentials: 'include', headers: tenant ? { 'X-Tenant-Id': tenant } : {} })
  if (!response.ok) throw new Error('No se pudo descargar el archivo.')
  const disposition = response.headers.get('content-disposition') ?? ''
  const fileName = disposition.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i)?.[1] ?? fallbackName
  const url = URL.createObjectURL(await response.blob())
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = decodeURIComponent(fileName.replaceAll('"', ''))
  anchor.click()
  URL.revokeObjectURL(url)
}

export const api = {
  me: () => request<Profile>('/api/auth/me'),
  login: (username: string, password: string, twoFactorCode?: string, workspace = 'technology') => request<Profile>('/api/auth/login', { method: 'POST', body: JSON.stringify({ username, password, twoFactorCode, workspace }) }),
  logout: () => request<void>('/api/auth/logout', { method: 'POST' }),
  setupTwoFactor: () => request<TwoFactorSetup>('/api/auth/2fa/setup', { method: 'POST' }),
  enableTwoFactor: (code: string) => request<{ enabled: boolean; recoveryCodes: string[] }>('/api/auth/2fa/enable', { method: 'POST', body: JSON.stringify({ code }) }),
  disableTwoFactor: () => request<void>('/api/auth/2fa/disable', { method: 'POST' }),
  dashboard: () => request<Dashboard>('/api/dashboard'),
  printers: (includeArchived = false) => request<Printer[]>(`/api/printers${query({ includeArchived })}`),
  savePrinter: (item: Printer) => item.id ? request<Printer>(`/api/printers/${item.id}`, { method: 'PUT', body: JSON.stringify(item) }) : request<Printer>('/api/printers', { method: 'POST', body: JSON.stringify(item) }),
  archivePrinter: (id: number) => request<void>(`/api/printers/${id}`, { method: 'DELETE' }),
  favoritePrinter: (id: number, favorite: boolean) => request<Printer>(`/api/printers/${id}/favorite`, { method: 'PUT', body: JSON.stringify({ favorite }) }),
  consumables: (includeArchived = false) => request<Consumable[]>(`/api/consumables${query({ includeArchived })}`),
  saveConsumable: (item: Consumable) => item.id ? request<Consumable>(`/api/consumables/${item.id}`, { method: 'PUT', body: JSON.stringify(item) }) : request<Consumable>('/api/consumables', { method: 'POST', body: JSON.stringify(item) }),
  updateStock: (id: number, stockGrams: number, lowStockGrams: number) => request<Consumable>(`/api/consumables/${id}/stock`, { method: 'PATCH', body: JSON.stringify({ stockGrams, lowStockGrams }) }),
  addStock: (id: number, kilograms: number, grams: number) => request<Consumable>(`/api/consumables/${id}/stock/add`, { method: 'POST', body: JSON.stringify({ kilograms, grams }) }),
  archiveConsumable: (id: number) => request<void>(`/api/consumables/${id}`, { method: 'DELETE' }),
  alerts: () => request<InventoryAlert[]>('/api/alerts'),
  materials: (includeArchived = false) => request<ExtraMaterial[]>(`/api/materials${query({ includeArchived })}`),
  saveMaterial: (item: ExtraMaterial) => item.id ? request<ExtraMaterial>(`/api/materials/${item.id}`, { method: 'PUT', body: JSON.stringify(item) }) : request<ExtraMaterial>('/api/materials', { method: 'POST', body: JSON.stringify(item) }),
  archiveMaterial: (id: number) => request<void>(`/api/materials/${id}`, { method: 'DELETE' }),
  settings: () => request<BusinessSettings>('/api/settings'),
  saveSettings: (settings: BusinessSettings) => request<BusinessSettings>('/api/settings', { method: 'PUT', body: JSON.stringify(settings) }),
  calculateQuote: (quote: QuoteRequest) => request<QuoteCalculation>('/api/quotes/calculate', { method: 'POST', body: JSON.stringify(quote) }),
  createQuote: (quote: QuoteRequest) => request<QuoteSummary>('/api/quotes', { method: 'POST', body: JSON.stringify(quote) }),
  quotes: (filters: { search?: string; from?: string; to?: string } = {}) => request<QuoteSummary[]>(`/api/quotes${query(filters)}`),
  quote: (id: number) => request<QuoteDetail>(`/api/quotes/${id}`),
  confirmSale: (id: number) => request<{ id: number; quoteId: number; soldAtUtc: string; saleAmount: number }>(`/api/quotes/${id}/sale`, { method: 'POST' }),
  deleteQuote: (id: number) => request<void>(`/api/quotes/${id}`, { method: 'DELETE' }),
  exportQuotes: (filters: { search?: string; from?: string; to?: string }) => download(`/api/quotes/export${query(filters)}`, 'cotizaciones.csv'),
  report: (from: string, to: string, userId?: string) => request<Report>(`/api/reports${query({ from, to, userId })}`),
  exportSales: (from: string, to: string) => download(`/api/reports/sales/export${query({ from, to })}`, 'ventas.csv'),
  users: () => request<UserAccount[]>('/api/users'),
  roles: () => request<string[]>('/api/users/roles'),
  createUser: (item: { username: string; displayName: string; email?: string; password: string; role: string }) => request<UserAccount>('/api/users', { method: 'POST', body: JSON.stringify(item) }),
  updateUser: (id: string, item: { displayName: string; email?: string; role: string; active: boolean }) => request<UserAccount>(`/api/users/${id}`, { method: 'PUT', body: JSON.stringify(item) }),
  resetPassword: (id: string, password: string) => request<void>(`/api/users/${id}/password`, { method: 'POST', body: JSON.stringify({ password }) }),
  deleteUser: (id: string) => request<void>(`/api/users/${id}`, { method: 'DELETE' }),
  exportBackup: () => download('/api/backup', 'sanjcorp3d-backup.json'),
  restoreBackup: (backup: unknown, confirmation: string) => request<{ message: string; quotes: number }>('/api/backup/restore', { method: 'POST', body: JSON.stringify({ backup, confirmation }) }),
  tenants: () => request<Tenant[]>('/api/tenants'),
  createMakerTenant: (item: { name: string; slug: string; logoUrl?: string; username: string; displayName: string; email?: string; password: string }) => request<Tenant>('/api/tenants', { method: 'POST', body: JSON.stringify(item) }),
  updateTenant: (id: string, item: { name: string; logoUrl?: string; active: boolean }) => request<Tenant>(`/api/tenants/${id}`, { method: 'PUT', body: JSON.stringify(item) }),
  deleteMakerTenant: (id: string) => request<void>(`/api/tenants/${id}`, { method: 'DELETE' }),
  createMakerUser: (tenantId: string, item: { username: string; displayName: string; email?: string; password: string }) => request<UserAccount>(`/api/tenants/${tenantId}/users`, { method: 'POST', body: JSON.stringify(item) }),
}
