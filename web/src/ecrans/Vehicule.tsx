import { useState } from 'react'
import { Camion } from '../composants/Camion'
import { Notice, Page, Pied } from '../composants/Cadre'
import { ClavierPlaque } from '../composants/Claviers'
import { Coche } from '../composants/Icones'
import { nb, useTextes } from '../i18n'
import { cuvesEnsemble, libelleEnsemble, MessageErreur, nomComplet, texteControle, useParcours } from './commun'

/** Saisie au clavier d'une plaque (véhicule non enregistré à l'entrée). */
export function EcranPlaques({ remorqueFacultative = false, onFermer }: { remorqueFacultative?: boolean; onFermer?: () => void }) {
  const { session: s, agir, annuler, enLigne, changerLangue } = useParcours()
  const { t } = useTextes()
  const [plaque, setPlaque] = useState('')
  const attendue = s.plaqueAttendue ?? 'avant'
  const valider = async () => {
    if (await agir((api, id) => api.plaque(id, plaque))) { setPlaque(''); onFermer?.() }
  }
  return (
    <Page fil={[t.accueil, t.etapes[1]]} titre={t.plaquesTitre[attendue]} sousTitre={`${t.bonjour(nomComplet(s))} · ${t.plaquesSous[attendue]}`}
      etape={2} enLigne={enLigne} onLangue={changerLangue}
      pied={<Pied annuler={annuler} aide={t.retirerAnnule}
        secondaire={remorqueFacultative ? { libelle: t.sansRemorque, onClick: () => onFermer?.() } : undefined}
        primaire={{ libelle: t.validerPlaque, onClick: valider, desactive: plaque.replace(/[^A-Z0-9]/g, '').length < 5 }} />}>
      <MessageErreur />
      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '1.5rem', marginTop: '1rem' }}>
        <div className={`saisie${plaque ? '' : ' vide'}`} style={{ width: '44rem', justifyContent: 'center', height: '5.5rem', fontSize: plaque ? '3rem' : undefined }}>
          {plaque || t.plaquesAide}
        </div>
        <ClavierPlaque valeur={plaque} onChange={setPlaque} />
      </div>
    </Page>
  )
}

export function EcranVehicule() {
  const { session: s, agir, annuler, enLigne, changerLangue, afficher } = useParcours()
  const { t, langue } = useTextes()
  const [saisieRemorque, setSaisieRemorque] = useState(false)
  if (saisieRemorque) return <EcranPlaques remorqueFacultative onFermer={() => setSaisieRemorque(false)} />

  const e = s.ensemble
  const alertes = s.controles.filter((c) => c.gravite !== 'bloquant')
  const echeancesOk = !s.controles.some((c) => c.code.startsWith('CT_') || c.code.startsWith('AGREMENT_'))
  const p = s.poids
  const confirmer = () => (s.etape === 'vehicule' ? agir((api, id) => api.confirmerVehicule(id)) : afficher(null))

  return (
    <Page fil={[t.accueil, t.etapes[1]]} titre={t.vehiculeTitre} sousTitre={t.bonjour(nomComplet(s))} etape={2} enLigne={enLigne} onLangue={changerLangue}
      pied={<Pied annuler={annuler} aide={t.retirerAnnule}
        secondaire={e.libelle === 'Porteur' && s.etape === 'vehicule'
          ? { libelle: t.ajouterRemorque, onClick: () => setSaisieRemorque(true) }
          : { libelle: t.pasMonVehicule, onClick: () => agir((api, id) => api.contesterVehicule(id)) }}
        primaire={{ libelle: t.confirmer, onClick: confirmer }} />}>
      <MessageErreur />
      {alertes.map((c, i) => <Notice key={i} type={c.gravite === 'alerte' ? 'alerte' : 'info'}>{texteControle(t, c, langue)}</Notice>)}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 29rem', gap: '1.5rem', flex: 1, minHeight: 0 }}>
        <div className="panneau" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between', gap: '1rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
            <span style={{ fontSize: '1.75rem', fontWeight: 600 }}>{libelleEnsemble(s, t)}</span>
            <span style={{ fontSize: '1.35rem', color: 'var(--discret)' }}>{t.compartimentsCapacite({ n: e.compartiments.length, v: nb(e.capaciteTotale) })}</span>
          </div>
          <Camion cuves={cuvesEnsemble(s, t)} />
          <div style={{ display: 'flex', gap: '2.5rem', fontSize: '1.4rem', color: 'var(--discret)', flexWrap: 'wrap' }}>
            {[e.avant, e.arriere].filter((v) => !!v).map((v) => (
              <span key={v!.immatriculation}>{t.typeVehicule[v!.type] ?? v!.type} <b style={{ color: 'var(--encre)' }}>{v!.immatriculation}</b></span>
            ))}
            {echeancesOk && <span style={{ color: 'var(--ok)', display: 'inline-flex', gap: '0.4rem', alignItems: 'center' }}><span style={{ width: '1.5rem', display: 'inline-flex' }}><Coche /></span>{t.ctValides}</span>}
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div className="stat accent">
            <div className="libelle">{t.poidsMaximal}</div>
            <div className="valeur">{nb(p.maximal)} kg</div>
            <div className="detail">{t.poidsMaximalDetail({ ptrac: nb(p.ptracRetenu), tare: nb(p.tareReelleConnue ? p.tareReelle : p.tareTheorique) })}</div>
          </div>
          <div className="stat">
            <div className="libelle">{t.tareReelle}</div>
            <div className="valeur">{p.tareReelleConnue ? `${nb(p.tareReelle)} kg` : '—'}</div>
            <div className="detail">{p.tareReelleConnue ? t.tareReelleDetail : t.tareNonPesee}</div>
          </div>
          <div className="stat">
            <div className="libelle">{t.tareTheorique}</div>
            <div className="valeur">{nb(p.tareTheorique)} kg</div>
            {p.tareReelleConnue && <div className="detail" style={p.ecartTareExcessif ? { color: 'var(--alerte)' } : undefined}>{t.ecart({ v: nb(p.ecartTare) })}</div>}
          </div>
        </div>
      </div>
    </Page>
  )
}
