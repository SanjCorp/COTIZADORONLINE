import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react'
import { Archive, Boxes, Edit3, Minus, Plus, Printer as PrinterIcon, RefreshCw } from 'lucide-react'
import { api } from '../api'
import type { Consumable, ExtraMaterial, Printer } from '../types'
import { Empty, ErrorMessage, Loading, PageHeader, Status, SuccessMessage, money, number } from '../ui'

const blankPrinter: Printer = { id: 0, name: '', buildX: 220, buildY: 220, buildZ: 250, nozzle: 0.4, speed: 60, powerWatts: 350, hourlyCost: 0, isDefault: false, active: true }
const blankConsumable: Consumable = { id: 0, name: '', category: 'Filamento', material: 'PLA', color: '', pricePerUnit: 0, density: 1.24, isDefault: false, active: true, stockQuantity: 0 }
const blankMaterial: ExtraMaterial = { id: 0, name: '', category: 'Acabado', unit: 'unidad', unitPrice: 0, active: true }

export function PrintersPage({ canEdit }: { canEdit: boolean }) {
  const [items, setItems] = useState<Printer[]>([])
  const [includeArchived, setIncludeArchived] = useState(false)
  const [draft, setDraft] = useState<Printer | null>(null)
  const [error, setError] = useState(''); const [success, setSuccess] = useState(''); const [loading, setLoading] = useState(true)
  const load = useCallback(() => { setLoading(true); api.printers(includeArchived).then(setItems).catch(reason => setError((reason as Error).message)).finally(() => setLoading(false)) }, [includeArchived])
  useEffect(load, [load])

  async function save(event: FormEvent) {
    event.preventDefault(); if (!draft) return; setError(''); setSuccess('')
    try { await api.savePrinter(draft); setSuccess(draft.id ? 'Impresora actualizada.' : 'Impresora agregada.'); setDraft(null); load() }
    catch (reason) { setError((reason as Error).message) }
  }
  async function archive(item: Printer) { if (!window.confirm(`¿Archivar la impresora ${item.name}? El historial se conservará.`)) return; try { await api.archivePrinter(item.id); setDraft(null); load() } catch (reason) { setError((reason as Error).message) } }

  return <>
    <PageHeader eyebrow="CATÁLOGO" title="Impresoras" description="Volumen, boquilla, velocidad y consumo eléctrico de cada equipo." actions={canEdit && <button onClick={() => setDraft({ ...blankPrinter })}><Plus size={17} />Nueva impresora</button>} />
    <ErrorMessage error={error} /><SuccessMessage message={success} />
    <div className={`catalog-layout ${draft ? 'with-editor' : ''}`}>
      <section className="panel">
        <div className="toolbar"><label className="check"><input type="checkbox" checked={includeArchived} onChange={e => setIncludeArchived(e.target.checked)} />Mostrar archivadas</label><button className="icon ghost" onClick={load} aria-label="Actualizar"><RefreshCw size={16} /></button></div>
        {loading ? <Loading /> : items.length === 0 ? <Empty>No hay impresoras registradas.</Empty> : <div className="card-grid">{items.map(item => <article className={`catalog-card ${!item.active ? 'archived' : ''}`} key={item.id}>
          <div className="catalog-icon"><PrinterIcon size={22} /></div><div className="catalog-main"><div><h3>{item.name}</h3>{item.isDefault && <span className="preferred">PREDETERMINADA</span>}</div><p>{number(item.buildX, 0)} × {number(item.buildY, 0)} × {number(item.buildZ, 0)} mm</p><small>Boquilla {number(item.nozzle)} mm · {number(item.speed)} mm/s · {number(item.powerWatts, 0)} W</small></div><Status active={item.active} />
          {canEdit && <div className="card-actions"><button className="ghost small" onClick={() => setDraft({ ...item })}><Edit3 size={15} />Editar</button>{item.active && <button className="ghost small danger-text" onClick={() => archive(item)}><Archive size={15} />Archivar</button>}</div>}
        </article>)}</div>}
      </section>
      {draft && <form className="panel editor" onSubmit={save}>
        <div className="section-title"><div><p className="eyebrow">{draft.id ? 'EDITAR' : 'NUEVO EQUIPO'}</p><h2>{draft.id ? draft.name : 'Impresora'}</h2></div><button type="button" className="icon ghost" onClick={() => setDraft(null)}>×</button></div>
        <label>Nombre<input required maxLength={100} value={draft.name} onChange={e => setDraft({ ...draft, name: e.target.value })} /></label>
        <div className="form-grid three"><label>Ancho X (mm)<input type="number" min="1" step="0.1" value={draft.buildX} onChange={e => setDraft({ ...draft, buildX: Number(e.target.value) })} /></label><label>Fondo Y (mm)<input type="number" min="1" step="0.1" value={draft.buildY} onChange={e => setDraft({ ...draft, buildY: Number(e.target.value) })} /></label><label>Alto Z (mm)<input type="number" min="1" step="0.1" value={draft.buildZ} onChange={e => setDraft({ ...draft, buildZ: Number(e.target.value) })} /></label></div>
        <div className="form-grid two"><label>Boquilla (mm)<input type="number" min="0.1" step="0.1" value={draft.nozzle} onChange={e => setDraft({ ...draft, nozzle: Number(e.target.value) })} /></label><label>Velocidad (mm/s)<input type="number" min="1" step="1" value={draft.speed} onChange={e => setDraft({ ...draft, speed: Number(e.target.value) })} /></label><label>Potencia (W)<input type="number" min="0" step="1" value={draft.powerWatts} onChange={e => setDraft({ ...draft, powerWatts: Number(e.target.value) })} /></label><label>Costo/hora referencial<input type="number" min="0" step="0.01" value={draft.hourlyCost} onChange={e => setDraft({ ...draft, hourlyCost: Number(e.target.value) })} /></label></div>
        <label className="check"><input type="checkbox" checked={draft.isDefault} onChange={e => setDraft({ ...draft, isDefault: e.target.checked })} />Usar como impresora predeterminada</label>
        {draft.id > 0 && <label className="check"><input type="checkbox" checked={draft.active} onChange={e => setDraft({ ...draft, active: e.target.checked })} />Activa</label>}
        <div className="editor-actions"><button type="button" className="ghost" onClick={() => setDraft(null)}>Cancelar</button><button>Guardar</button></div>
      </form>}
    </div>
  </>
}

export function ConsumablesPage({ canEdit, currency = 'Bs' }: { canEdit: boolean; currency?: string }) {
  const [items, setItems] = useState<Consumable[]>([]); const [includeArchived, setIncludeArchived] = useState(false); const [category, setCategory] = useState('Todos'); const [draft, setDraft] = useState<Consumable | null>(null)
  const [error, setError] = useState(''); const [success, setSuccess] = useState(''); const [loading, setLoading] = useState(true)
  const load = useCallback(() => { setLoading(true); api.consumables(includeArchived).then(setItems).catch(reason => setError((reason as Error).message)).finally(() => setLoading(false)) }, [includeArchived])
  useEffect(load, [load])
  const visible = useMemo(() => category === 'Todos' ? items : items.filter(x => x.category.toLowerCase() === category.toLowerCase()), [items, category])
  async function save(event: FormEvent) { event.preventDefault(); if (!draft) return; try { await api.saveConsumable(draft); setSuccess(draft.id ? 'Consumible actualizado.' : 'Consumible agregado.'); setError(''); setDraft(null); load() } catch (reason) { setError((reason as Error).message) } }
  async function stock(item: Consumable, quantity: number) { try { await api.updateStock(item.id, Math.max(0, quantity)); load() } catch (reason) { setError((reason as Error).message) } }
  async function archive(item: Consumable) { if (!window.confirm(`¿Archivar ${item.name} · ${item.material} · ${item.color}?`)) return; try { await api.archiveConsumable(item.id); setDraft(null); load() } catch (reason) { setError((reason as Error).message) } }
  return <>
    <PageHeader eyebrow="INVENTARIO" title="Filamentos y resinas" description="Catálogo de consumibles, precios, densidad y existencia disponible." actions={canEdit && <button onClick={() => setDraft({ ...blankConsumable })}><Plus size={17} />Nuevo consumible</button>} />
    <ErrorMessage error={error} /><SuccessMessage message={success} />
    <div className={`catalog-layout ${draft ? 'with-editor' : ''}`}><section className="panel">
      <div className="toolbar"><div className="segmented">{['Todos', 'Filamento', 'Resina'].map(x => <button key={x} className={category === x ? 'active' : ''} onClick={() => setCategory(x)}>{x}</button>)}</div><label className="check"><input type="checkbox" checked={includeArchived} onChange={e => setIncludeArchived(e.target.checked)} />Mostrar archivados</label></div>
      {loading ? <Loading /> : visible.length === 0 ? <Empty>No hay consumibles en esta categoría.</Empty> : <div className="table-wrap"><table><thead><tr><th>Consumible</th><th>Tipo / color</th><th>Precio</th><th>Existencia</th><th>Estado</th>{canEdit && <th>Acciones</th>}</tr></thead><tbody>{visible.map(item => <tr key={item.id} className={!item.active ? 'archived-row' : ''}><td><strong>{item.name}</strong>{item.isDefault && <span className="preferred inline">PREDET.</span>}</td><td>{item.material} · {item.color}<small className="cell-note">{item.category} · densidad {number(item.density)}</small></td><td>{money(item.pricePerUnit, currency)}</td><td><div className="stock-control">{canEdit && item.active && <button className="icon ghost" onClick={() => stock(item, item.stockQuantity - 1)}><Minus size={14} /></button>}<b className={item.stockQuantity <= 1 ? 'low-stock' : ''}>{item.stockQuantity}</b>{canEdit && item.active && <button className="icon ghost" onClick={() => stock(item, item.stockQuantity + 1)}><Plus size={14} /></button>}</div></td><td><Status active={item.active && item.stockQuantity > 0} trueLabel="DISPONIBLE" falseLabel={item.active ? 'SIN EXISTENCIA' : 'ARCHIVADO'} /></td>{canEdit && <td><div className="row-actions"><button className="icon ghost" title="Editar" onClick={() => setDraft({ ...item })}><Edit3 size={15} /></button>{item.active && <button className="icon ghost danger-text" title="Archivar" onClick={() => archive(item)}><Archive size={15} /></button>}</div></td>}</tr>)}</tbody></table></div>}
    </section>{draft && <form className="panel editor" onSubmit={save}><div className="section-title"><div><p className="eyebrow">{draft.id ? 'EDITAR' : 'NUEVO'}</p><h2>Consumible</h2></div><button type="button" className="icon ghost" onClick={() => setDraft(null)}>×</button></div>
      <label>Nombre<input required value={draft.name} onChange={e => setDraft({ ...draft, name: e.target.value })} placeholder="Ej. eSUN" /></label><div className="form-grid two"><label>Categoría<select value={draft.category} onChange={e => setDraft({ ...draft, category: e.target.value })}><option>Filamento</option><option>Resina</option></select></label><label>Material / tipo<input required value={draft.material} onChange={e => setDraft({ ...draft, material: e.target.value })} placeholder="PLA, PETG, estándar…" /></label><label>Color<input required value={draft.color} onChange={e => setDraft({ ...draft, color: e.target.value })} /></label><label>Existencia<input type="number" min="0" step="1" value={draft.stockQuantity} onChange={e => setDraft({ ...draft, stockQuantity: Number(e.target.value) })} /></label><label>Precio por {draft.category === 'Resina' ? 'litro' : 'kg'}<input type="number" min="0" step="0.01" value={draft.pricePerUnit} onChange={e => setDraft({ ...draft, pricePerUnit: Number(e.target.value) })} /></label><label>Densidad<input type="number" min="0.01" step="0.01" value={draft.density} onChange={e => setDraft({ ...draft, density: Number(e.target.value) })} /></label></div>
      <label className="check"><input type="checkbox" checked={draft.isDefault} onChange={e => setDraft({ ...draft, isDefault: e.target.checked })} />Consumible predeterminado</label>{draft.id > 0 && <label className="check"><input type="checkbox" checked={draft.active} onChange={e => setDraft({ ...draft, active: e.target.checked })} />Activo</label>}<div className="editor-actions"><button type="button" className="ghost" onClick={() => setDraft(null)}>Cancelar</button><button>Guardar</button></div>
    </form>}</div>
  </>
}

export function MaterialsPage({ canEdit, currency = 'Bs' }: { canEdit: boolean; currency?: string }) {
  const [items, setItems] = useState<ExtraMaterial[]>([]); const [includeArchived, setIncludeArchived] = useState(false); const [draft, setDraft] = useState<ExtraMaterial | null>(null); const [error, setError] = useState(''); const [success, setSuccess] = useState(''); const [loading, setLoading] = useState(true)
  const load = useCallback(() => { setLoading(true); api.materials(includeArchived).then(setItems).catch(reason => setError((reason as Error).message)).finally(() => setLoading(false)) }, [includeArchived]); useEffect(load, [load])
  async function save(event: FormEvent) { event.preventDefault(); if (!draft) return; try { await api.saveMaterial(draft); setSuccess(draft.id ? 'Material actualizado.' : 'Material agregado.'); setError(''); setDraft(null); load() } catch (reason) { setError((reason as Error).message) } }
  async function archive(item: ExtraMaterial) { if (!window.confirm(`¿Archivar ${item.name}?`)) return; try { await api.archiveMaterial(item.id); setDraft(null); load() } catch (reason) { setError((reason as Error).message) } }
  return <><PageHeader eyebrow="CATÁLOGO" title="Materiales adicionales" description="Imanes, tornillos, pegamento, pintura, empaque y otros costos por unidad." actions={canEdit && <button onClick={() => setDraft({ ...blankMaterial })}><Plus size={17} />Nuevo material</button>} /><ErrorMessage error={error} /><SuccessMessage message={success} />
    <div className={`catalog-layout ${draft ? 'with-editor' : ''}`}><section className="panel"><div className="toolbar"><label className="check"><input type="checkbox" checked={includeArchived} onChange={e => setIncludeArchived(e.target.checked)} />Mostrar archivados</label></div>{loading ? <Loading /> : items.length === 0 ? <Empty>No hay materiales registrados.</Empty> : <div className="card-grid">{items.map(item => <article className={`catalog-card ${!item.active ? 'archived' : ''}`} key={item.id}><div className="catalog-icon"><Boxes size={22} /></div><div className="catalog-main"><h3>{item.name}</h3><p>{item.category}</p><small>{money(item.unitPrice, currency)} / {item.unit}</small></div><Status active={item.active} />{canEdit && <div className="card-actions"><button className="ghost small" onClick={() => setDraft({ ...item })}><Edit3 size={15} />Editar</button>{item.active && <button className="ghost small danger-text" onClick={() => archive(item)}><Archive size={15} />Archivar</button>}</div>}</article>)}</div>}</section>
      {draft && <form className="panel editor" onSubmit={save}><div className="section-title"><div><p className="eyebrow">{draft.id ? 'EDITAR' : 'NUEVO'}</p><h2>Material adicional</h2></div><button type="button" className="icon ghost" onClick={() => setDraft(null)}>×</button></div><label>Nombre<input required value={draft.name} onChange={e => setDraft({ ...draft, name: e.target.value })} /></label><label>Categoría<input required value={draft.category} onChange={e => setDraft({ ...draft, category: e.target.value })} /></label><div className="form-grid two"><label>Unidad<input required value={draft.unit} onChange={e => setDraft({ ...draft, unit: e.target.value })} /></label><label>Precio unitario<input type="number" min="0" step="0.01" value={draft.unitPrice} onChange={e => setDraft({ ...draft, unitPrice: Number(e.target.value) })} /></label></div>{draft.id > 0 && <label className="check"><input type="checkbox" checked={draft.active} onChange={e => setDraft({ ...draft, active: e.target.checked })} />Activo</label>}<div className="editor-actions"><button type="button" className="ghost" onClick={() => setDraft(null)}>Cancelar</button><button>Guardar</button></div></form>}
    </div>
  </>
}
