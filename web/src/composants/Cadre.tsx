import { useEffect, useState, type ReactNode } from 'react'
import { nomsLangues, useTextes } from '../i18n'
import logo from '../assets/logo-dpl.png'
import { Alerte, Chevron, ChevronGauche, Coche, Croix, Globe, Info } from './Icones'

export function Entete({ fil, enLigne, onLangue }: { fil: string[]; enLigne: boolean; onLangue?: () => void }) {
  const { t, langue } = useTextes()
  const [heure, setHeure] = useState(() => new Date())
  useEffect(() => {
    const m = window.setInterval(() => setHeure(new Date()), 15_000)
    return () => window.clearInterval(m)
  }, [])
  const hh = heure.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit' })
  return (
    <header className="entete">
      <div className="marque">
        <img src={logo} alt="DPL" />
        <span>{t.app.split(' ')[0]} <em>{t.app.split(' ').slice(1).join(' ')}</em></span>
      </div>
      <nav className="fil" aria-label="Fil d’Ariane">
        {fil.map((f, i) => (
          <span key={i} style={{ display: 'contents' }}>
            {i > 0 && <span aria-hidden="true">›</span>}
            {i === fil.length - 1 && fil.length > 1 ? <b>{f}</b> : <span style={fil.length === 1 ? { color: 'var(--encre)', fontWeight: 600 } : undefined}>{f}</span>}
          </span>
        ))}
      </nav>
      <div className="entete-droite">
        <div className="pastille"><span className={enLigne ? 'point' : 'point hs'} />{enLigne ? t.connectee : t.horsLigne} · {hh}</div>
        <button className="pastille langue" onClick={onLangue} disabled={!onLangue} type="button">
          <Globe />{nomsLangues[langue]} <small>{langue.toUpperCase()}</small>
        </button>
      </div>
    </header>
  )
}

export function Etapes({ courante }: { courante: number }) {
  const { t } = useTextes()
  return (
    <div className="etapes" aria-label="Étapes">
      {t.etapes.map((e, i) => (
        <div key={e} className={i + 1 < courante ? 'fait' : i + 1 === courante ? 'courant' : ''} aria-current={i + 1 === courante ? 'step' : undefined}>
          <small>{i + 1}</small>{e}
        </div>
      ))}
    </div>
  )
}

export function Notice({ type, titre, children }: { type: 'info' | 'ok' | 'alerte' | 'critique'; titre?: string; children?: ReactNode }) {
  const icone = type === 'ok' ? <Coche /> : type === 'info' ? <Info /> : <Alerte />
  return (
    <div className={`notice ${type}`} role={type === 'critique' ? 'alert' : 'status'}>
      {icone}
      <p>{titre && <b>{titre} </b>}{children}</p>
    </div>
  )
}

type Bouton = { libelle: string; onClick: () => void; desactive?: boolean }

export function Pied({ annuler, aide, retour, secondaire, primaire }: {
  annuler?: () => void; aide?: string; retour?: () => void; secondaire?: Bouton; primaire?: Bouton
}) {
  const { t } = useTextes()
  return (
    <footer className="pied">
      {annuler && <button className="btn annuler" onClick={annuler} type="button"><Croix />{t.annuler}</button>}
      {aide && <span className="aide">{aide}</span>}
      <div className="droite">
        {retour && <button className="btn" onClick={retour} type="button"><ChevronGauche />{t.retour}</button>}
        {secondaire && <button className="btn" onClick={secondaire.onClick} disabled={secondaire.desactive} type="button">{secondaire.libelle}</button>}
        {primaire && <button className="btn primaire" onClick={primaire.onClick} disabled={primaire.desactive} type="button">{primaire.libelle}<Chevron /></button>}
      </div>
    </footer>
  )
}

export function Page({ fil, titre, sousTitre, etape, enLigne, onLangue, children, pied }: {
  fil: string[]; titre: string; sousTitre?: string; etape?: number; enLigne: boolean; onLangue?: () => void; children: ReactNode; pied?: ReactNode
}) {
  return (
    <div className="page">
      <Entete fil={fil} enLigne={enLigne} onLangue={onLangue} />
      <div className="titre">
        <h1>{titre}</h1>
        {sousTitre && <p>{sousTitre}</p>}
      </div>
      <main className="carte">
        {etape !== undefined && <Etapes courante={etape} />}
        <div className="contenu">{children}</div>
        {pied}
      </main>
    </div>
  )
}

export function Fenetre({ titre, texte, children, actions, large }: { titre: string; texte?: string; children?: ReactNode; actions: ReactNode; large?: boolean }) {
  return (
    <div className="voile" role="dialog" aria-modal="true" aria-label={titre}>
      <div className={large ? 'fenetre large' : 'fenetre'}>
        <h2>{titre}</h2>
        {texte && <p>{texte}</p>}
        {children}
        <div className="actions">{actions}</div>
      </div>
    </div>
  )
}
