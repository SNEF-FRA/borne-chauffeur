import type { ConfigBorne, VueBorne } from '../api/types'
import type { EtatLecteur } from '../badge/lecteur'
import { Entete, Notice } from '../composants/Cadre'
import { Chevron } from '../composants/Icones'
import { dictionnaires, useTextes } from '../i18n'

const BADGES_DEMO = { accepte: '04A1B2C3D4E5F6', refuse: '04B2C3D4E5F6A7', sansVehicule: '04C3D4E5F6A7B8' }

function Illustration() {
  return (
    <svg viewBox="0 0 420 420" aria-hidden="true" style={{ width: '26rem', height: '26rem', flex: 'none' }}>
      <circle cx="210" cy="210" r="210" fill="#E8F3F4" />
      <rect x="146" y="58" width="132" height="200" rx="14" fill="#fff" stroke="#14385C" strokeWidth="5" />
      <rect x="146" y="58" width="132" height="36" rx="14" fill="#157F86" />
      <rect x="146" y="80" width="132" height="14" fill="#157F86" />
      <circle cx="212" cy="140" r="27" fill="none" stroke="#14385C" strokeWidth="5" />
      <path d="M170 210 q42 -40 84 0" fill="none" stroke="#14385C" strokeWidth="5" strokeLinecap="round" />
      <rect x="100" y="226" width="220" height="138" rx="18" fill="#14385C" />
      <rect x="136" y="250" width="148" height="10" rx="5" fill="#0E2A47" />
      <circle cx="290" cy="336" r="10" fill="#34D399" />
      <path d="M78 106 v74 m-22 -22 22 22 22 -22" fill="none" stroke="#157F86" strokeWidth="6" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M342 106 v74 m-22 -22 22 22 22 -22" fill="none" stroke="#157F86" strokeWidth="6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

export function Accueil({ config, borne, enLigne, etatLecteur, lecture, erreur, onBadge, onConnecterLecteur }: {
  config: ConfigBorne | null; borne: VueBorne | null; enLigne: boolean; etatLecteur: EtatLecteur; lecture: boolean
  erreur: string | null; onBadge: (code: string) => void; onConnecterLecteur?: () => void
}) {
  const { t, langue, changer } = useTextes()
  const langues = (config?.langues ?? ['fr', 'en']).filter((l, i, tout) => dictionnaires[l] && tout.indexOf(l) === i)
  const horsService = borne && !borne.enService
  return (
    <div className="page">
      <Entete fil={[t.accueil]} enLigne={enLigne} />
      <div className="titre">
        <h1>{t.bienvenue({ depot: config?.nomDepot ?? 'DPL' })}</h1>
        <p>{t.bienvenueSous}</p>
      </div>
      <main className="carte">
        <div className="contenu" style={{ flexDirection: 'row', alignItems: 'center', gap: '6rem', padding: '0 2rem' }}>
          <Illustration />
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem', flex: 1 }}>
            <div>
              <div style={{ fontSize: '4.25rem', fontWeight: 700, letterSpacing: '-0.015em' }}>{lecture ? t.lectureBadge : t.inserezBadge}</div>
              <div style={{ fontSize: '1.75rem', color: 'var(--discret)', marginTop: '0.5rem' }}>{t.inserezAutres}</div>
            </div>
            <div>
              <div style={{ fontSize: '1.4rem', fontWeight: 600, marginBottom: '0.75rem' }}>{t.langue}</div>
              <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                {langues.map((l) => (
                  <button key={l} type="button" onClick={() => changer(l)} className="btn petit"
                    style={l === langue ? { background: 'var(--canard)', borderColor: 'var(--canard)', color: '#fff', minWidth: '5.25rem' } : { minWidth: '5.25rem' }}>
                    {l.toUpperCase()}
                  </button>
                ))}
              </div>
            </div>
            {horsService && <Notice type="critique">{t.borneHorsService}</Notice>}
            {erreur && <Notice type="critique">{t.erreur[erreur] ?? erreur}</Notice>}
            {borne?.messages.map((m, i) => <Notice key={i} type="info" titre={t.infoDepot}>{m.texte}</Notice>)}
          </div>
        </div>
        <footer className="pied">
          <span className="aide" style={{ display: 'flex', gap: '2rem' }}>
            <span><span style={{ color: etatLecteur === 'pret' ? '#10b981' : 'var(--critique)' }}>● </span>
              {etatLecteur === 'pret' ? t.lecteurPret : etatLecteur === 'absent' ? t.lecteurAbsent : t.lecteurErreur}</span>
            <span><span style={{ color: enLigne ? '#10b981' : 'var(--critique)' }}>● </span>{enLigne ? t.serveurConnecte : t.serveurIndisponible}</span>
          </span>
          <div className="droite">
            {etatLecteur === 'absent' && onConnecterLecteur && <button className="btn petit" type="button" onClick={onConnecterLecteur}>{t.connecterLecteur}</button>}
            {config?.badgesDemoVisibles && <>
              <button className="btn" type="button" style={{ color: 'var(--critique)' }} onClick={() => onBadge(BADGES_DEMO.refuse)}>{t.demoRefuse}</button>
              <button className="btn" type="button" onClick={() => onBadge(BADGES_DEMO.sansVehicule)}>{t.demoSansVehicule}</button>
              <button className="btn primaire" type="button" onClick={() => onBadge(BADGES_DEMO.accepte)}>{t.demoAccepte}<Chevron /></button>
            </>}
          </div>
        </footer>
      </main>
    </div>
  )
}
