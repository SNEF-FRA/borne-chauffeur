import { useState } from 'react'
import type { VueLigne } from '../api/types'
import { Fenetre, Notice, Page, Pied } from '../composants/Cadre'
import { PaveNumerique } from '../composants/Claviers'
import { Croix, Moins, Plus } from '../composants/Icones'
import { dateCourte, nb, useTextes } from '../i18n'
import { MessageErreur, useParcours } from './commun'

const PAS = 500

export function EcranCommandes() {
  const { session: s, agir, annuler, enLigne, changerLangue, afficher } = useParcours()
  const { t, langue } = useTextes()
  const [numero, setNumero] = useState('')
  const [edition, setEdition] = useState<VueLigne | null>(null)
  const p = s.poids
  const aujourdHui = new Date().toISOString().slice(0, 10)

  const ajouter = async () => { if (numero && await agir((api, id) => api.ajouterCommande(id, numero))) setNumero('') }
  const regler = (l: VueLigne, q: number) => agir((api, id) => api.quantite(id, l.idComProd, q))
  const pas = (l: VueLigne, sens: 1 | -1) => {
    const max = l.modifiable ? Number.MAX_SAFE_INTEGER : l.distribuable
    let q = Math.min(max, Math.max(0, Math.round((l.quantite + sens * PAS) / PAS) * PAS))
    if (sens > 0 && q === l.quantite) q = max
    if (q > 0 && q < 200) q = sens > 0 ? 200 : 0
    void regler(l, q)
  }
  const suivant = s.planRequis
    ? { libelle: t.voirPlan, onClick: () => agir((api, id) => api.calculerPlan(id)) }
    : { libelle: t.validerChargement, onClick: () => agir((api, id) => api.valider(id)) }
  const rien = !s.commandes.some((c) => c.lignes.some((l) => l.quantite > 0))
  const echelle = Math.max(p.maximal, p.chargement) || 1

  return (
    <Page fil={[t.accueil, t.etapes[3]]} titre={t.commandesTitre} sousTitre={t.commandesSous} etape={4} enLigne={enLigne} onLangue={changerLangue}
      pied={<Pied annuler={annuler} aide={t.retirerAnnule} retour={() => afficher('type')}
        primaire={{ ...suivant, desactive: rien || p.depasse }} />}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 31rem', gap: '1.5rem', flex: 1, minHeight: 0 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', minHeight: 0 }}>
          <MessageErreur />
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', minHeight: 0, flex: 1, overflow: 'auto' }}>
          {s.modification && <Notice type="info">{t.dejaEnregistre}</Notice>}
          {s.produitsIndisponibles.length > 0 && <Notice type="alerte">{t.indisponibles({ liste: s.produitsIndisponibles.join(', ') })}</Notice>}
          {s.commandes.length === 0 && <Notice type="info">{t.commandesVide}</Notice>}
          {s.commandes.map((c) => (
            <section key={c.numero} className="stat" style={{ padding: '0.75rem 1.5rem', flex: 'none' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', borderBottom: '1px solid var(--bordure)', paddingBottom: '0.6rem' }}>
                <span style={{ fontSize: '1.6rem', fontWeight: 600 }}>{t.commande({ n: c.numero })}</span>
                <span style={{ marginLeft: 'auto', fontSize: '1.2rem', color: 'var(--discret)' }}>
                  {c.client}{c.destination ? `, ${c.destination}` : ''} · {c.globale ? t.commandeGlobale : c.validite.slice(0, 10) === aujourdHui ? t.valableAujourdhui : dateCourte(c.validite, langue)}
                </span>
                <button type="button" className="btn petit" style={{ height: '2.75rem', padding: '0 0.75rem' }} aria-label={t.retirerCommande}
                  onClick={() => agir((api, id) => api.retirerCommande(id, c.numero))}><Croix /></button>
              </div>
              {c.lignes.map((l) => (
                <div key={l.idComProd} style={{ display: 'flex', alignItems: 'center', gap: '1.5rem', padding: '0.4rem 0' }}>
                  <span className="puce" style={{ background: l.couleur.fond, color: l.couleur.texte, border: l.couleur.fond === '#FFFFFF' ? '1px solid var(--bordure)' : undefined, minWidth: '9rem', justifyContent: 'center' }}>{l.produit}</span>
                  <span style={{ fontSize: '1.3rem', color: 'var(--discret)' }}>
                    {c.globale && l.quotaRestant != null ? t.quotaRestant({ v: nb(l.distribuable) }) : t.commandeLe({ v: nb(l.commandee) })}
                  </span>
                  <span style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                    <button type="button" className="btn petit" style={{ width: '3.75rem', padding: 0 }} onClick={() => pas(l, -1)} disabled={l.quantite === 0} aria-label="−"><Moins /></button>
                    <button type="button" onClick={() => setEdition(l)} style={{ border: 0, background: 'none', minWidth: '10rem', fontSize: '2rem', fontWeight: 700 }}>{nb(l.quantite)} L</button>
                    <button type="button" className="btn petit" style={{ width: '3.75rem', padding: 0 }} onClick={() => pas(l, 1)} disabled={!l.modifiable && l.quantite >= l.distribuable} aria-label="+"><Plus /></button>
                  </span>
                </div>
              ))}
            </section>
          ))}
          </div>
          <section className="panneau" style={{ padding: '1rem 1.5rem', flex: 'none' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
              <span style={{ fontSize: '1.5rem', fontWeight: 600 }}>{t.poidsChargement}</span>
              <span style={{ fontSize: '1.75rem' }}><b style={p.depasse ? { color: 'var(--critique)' } : undefined}>{nb(p.chargement)} kg</b>
                <span style={{ color: 'var(--discret)' }}> / {nb(p.maximal)} kg</span></span>
            </div>
            <div style={{ position: 'relative', height: '0.9rem', borderRadius: '0.45rem', background: 'var(--bordure)', overflow: 'hidden', margin: '0.75rem 0' }}>
              <div style={{ display: 'flex', height: '100%' }}>
                {p.detail.map((d) => <div key={d.produit} style={{ width: `${(100 * d.poids) / echelle}%`, background: 'var(--canard)', borderRight: '2px solid #fff' }} />)}
                {p.restes > 0 && <div style={{ width: `${(100 * p.restes) / echelle}%`, background: '#94A3B8' }} />}
              </div>
              {p.depasse && <div style={{ position: 'absolute', top: 0, bottom: 0, left: `${(100 * p.maximal) / echelle}%`, width: 3, background: 'var(--critique)' }} />}
            </div>
            <div style={{ display: 'flex', gap: '1.5rem', fontSize: '1.15rem', color: 'var(--discret)', flexWrap: 'wrap' }}>
              {p.detail.map((d) => <span key={d.produit}>{d.produit} {nb(d.poids)} kg</span>)}
              {s.restes.map((r) => <span key={r.idCuve}>{t.resteCompartiment({ c: r.libelle })} {nb(r.poids)} kg</span>)}
              <b style={{ marginLeft: 'auto', color: p.depasse ? 'var(--critique)' : 'var(--ok)' }}>
                {p.depasse ? t.depassement({ v: nb(-p.marge) }) : t.marge({ v: nb(p.marge) })}
              </b>
            </div>
          </section>
        </div>
        <aside className="stat" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div style={{ fontSize: '1.75rem', fontWeight: 600 }}>{t.ajouterCommande}</div>
          <div style={{ fontSize: '1.2rem', color: 'var(--discret)' }}>{t.numeroCommande}</div>
          <div className={`saisie${numero ? '' : ' vide'}`}>{numero || '—'}</div>
          <PaveNumerique valeur={numero} onChange={setNumero} onValider={ajouter} libelleValider={t.ajouter} />
        </aside>
      </div>
      {edition && <FenetreQuantite ligne={edition} onFermer={() => setEdition(null)}
        onValider={async (q) => { if (await regler(edition, q)) setEdition(null) }} />}
    </Page>
  )
}

function FenetreQuantite({ ligne, onFermer, onValider }: { ligne: VueLigne; onFermer: () => void; onValider: (q: number) => void }) {
  const { t } = useTextes()
  const { erreur } = useParcours()
  const [valeur, setValeur] = useState(String(ligne.quantite || ''))
  const [neuf, setNeuf] = useState(true) // la première touche remplace la quantité affichée
  return (
    <Fenetre titre={t.quantiteTitre({ produit: ligne.produit })} texte={ligne.modifiable ? undefined : t.quantiteMax({ v: nb(ligne.distribuable) })}
      actions={<button className="btn" type="button" onClick={onFermer}>{t.fermer}</button>}>
      {erreur && <Notice type="critique">{t.erreur[erreur.code] ?? erreur.code}</Notice>}
      <div className="saisie">{nb(Number(valeur || 0))} L</div>
      <PaveNumerique valeur={neuf ? '' : valeur} onChange={(x) => { setNeuf(false); setValeur(x) }} max={6} libelleValider={t.valider} onValider={() => onValider(Number(valeur || 0))} />
    </Fenetre>
  )
}
