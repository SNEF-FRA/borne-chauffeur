import { useEffect, useState } from 'react'
import { Camion, type CuveAffichee } from '../composants/Camion'
import { Notice, Page, Pied } from '../composants/Cadre'
import { Alerte, CamionIcone, Coche, Croix, Etincelle, Horloge } from '../composants/Icones'
import { nb, useTextes } from '../i18n'
import { MessageErreur, texteControle, useParcours } from './commun'

export function EcranPlan() {
  const { session: s, agir, annuler, enLigne, changerLangue, afficher } = useParcours()
  const { t } = useTextes()
  const plan = s.plan!
  const cuves: CuveAffichee[] = plan.compartiments.map((c, i, tout) => {
    const arriere = s.ensemble.compartiments.find((x) => x.idCuve === c.idCuve)?.arriere
    const precedent = i > 0 ? s.ensemble.compartiments.find((x) => x.idCuve === tout[i - 1].idCuve)?.arriere : arriere
    const base = { cle: c.idCuve, libelle: c.libelle, capacite: c.capacite, attelage: i > 0 && arriere && !precedent }
    if (c.reste) return { ...base, hachure: true, titre: t.reste, lignes: [`${c.reste.produit} ${nb(c.reste.volume)} L`] }
    if (c.contenu.length === 0) return { ...base, titre: t.vide }
    const litres = c.contenu.reduce((x, y) => x + y.litres, 0)
    const premier = c.contenu[0]
    return {
      ...base, remplissage: litres, couleur: premier.couleur.fond, encre: litres / c.capacite > 0.6 ? premier.couleur.texte : undefined,
      titre: premier.produit,
      lignes: c.contenu.map((x) => (c.contenu.length > 1 ? `Cde ${x.numeroCommande} : ${nb(x.litres)} L` : `Cde ${x.numeroCommande}`)),
    }
  })

  return (
    <Page fil={[t.accueil, t.etapes[4]]} titre={t.planTitre} sousTitre={plan.complet ? t.planSous : t.planIncompletSous} etape={5} enLigne={enLigne} onLangue={changerLangue}
      pied={<Pied annuler={annuler} aide={t.retirerAnnule} retour={() => afficher('commandes')}
        primaire={{ libelle: t.validerPlan, onClick: () => agir((api, id) => api.valider(id)), desactive: !plan.complet }} />}>
      <MessageErreur />
      {!plan.complet && <Notice type="critique">{t.planIncomplet}</Notice>}
      <div style={{ position: 'relative', padding: '0.25rem 0 0' }}>
        <span className="puce" style={{ position: 'absolute', right: 0, top: 0, background: 'var(--ok-fond)', color: 'var(--ok)', border: '1px solid var(--ok-bordure)', gap: '0.5rem' }}>
          <span style={{ width: '1.3rem', display: 'inline-flex' }}><Etincelle /></span>{t.calculeAuto}
        </span>
        <div style={{ width: '100%', maxWidth: '68rem', margin: '0 auto' }}><Camion cuves={cuves} /></div>
      </div>
      <table className="tableau compact">
        <thead><tr><th>{t.colCommande}</th><th>{t.colProduit}</th><th>{t.colDemande}</th><th>{t.colPlace}</th><th>{t.colCompartiments}</th><th>{t.colEtat}</th></tr></thead>
        <tbody>
          {plan.lignes.map((l) => {
            const ok = l.place >= l.demande
            return (
              <tr key={l.idComProd}>
                <td style={{ fontWeight: 600 }}>{l.numeroCommande}</td>
                <td><span className="puce" style={{ background: l.couleur.fond, color: l.couleur.texte }}>{l.produit}</span></td>
                <td>{nb(l.demande)} L</td>
                <td>{nb(l.place)} L</td>
                <td>{l.compartiments.join(', ') || '—'}</td>
                <td style={{ color: ok ? 'var(--ok)' : 'var(--critique)', fontWeight: 600 }}>
                  <span style={{ display: 'inline-flex', gap: '0.4rem', alignItems: 'center' }}>
                    <span style={{ width: '1.4rem', display: 'inline-flex' }}>{ok ? <Coche /> : <Alerte />}</span>
                    {Math.floor((100 * l.place) / Math.max(1, l.demande))} %
                  </span>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </Page>
  )
}

export function EcranIlot() {
  const { session: s, agir, enLigne, changerLangue } = useParcours()
  const { t } = useTextes()
  const ilots = s.ilots!
  const [choisi, setChoisi] = useState<number | null>(ilots.recommande ?? null)
  const reco = ilots.liste.find((i) => i.numero === ilots.recommande)
  const volume = s.commandes.flatMap((c) => c.lignes).reduce((x, l) => x + l.quantite, 0)
  const nbProduits = new Set(s.commandes.flatMap((c) => c.lignes.filter((l) => l.quantite > 0).map((l) => l.produit))).size
  const lignes = s.commandes.flatMap((c) => c.lignes).filter((l) => l.quantite > 0).length

  return (
    <Page fil={[t.accueil, t.etapes[5]]} titre={t.ilotTitre} sousTitre={t.ilotSous} etape={6} enLigne={enLigne} onLangue={changerLangue}
      pied={<Pied aide={t.finBadge}
        secondaire={{ libelle: t.laisserChoisir, onClick: () => agir((api, id) => api.ilot(id, null)) }}
        primaire={choisi !== null ? { libelle: t.validerIlot, onClick: () => agir((api, id) => api.ilot(id, choisi)) } : undefined} />}>
      <MessageErreur />
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 31rem', gap: '1.5rem', flex: 1, minHeight: 0 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem', minHeight: 0 }}>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1.25rem' }}>
            {ilots.liste.slice(0, 6).map((i) => {
              const etat = i.recommande ? 'recommande' : i.etat
              const incompatible = i.etat === 'incompatible'
              return (
                <button key={i.numero} type="button" className="stat" disabled={incompatible} onClick={() => setChoisi(i.numero)}
                  aria-pressed={choisi === i.numero} style={{
                  border: choisi === i.numero ? '2px solid var(--canard)' : undefined, opacity: incompatible ? 0.8 : 1,
                  display: 'flex', flexDirection: 'column', gap: '0.6rem', textAlign: 'left', background: choisi === i.numero ? 'var(--canard-teinte)' : undefined,
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <span style={{ fontSize: '2rem', fontWeight: 700, color: incompatible ? 'var(--discret)' : undefined }}>{t.ilot({ n: i.numero })}</span>
                    <span className={`badge-etat ${etat}`}>{i.recommande ? t.recommande : t[i.etat]}</span>
                  </div>
                  <Ligne icone={<CamionIcone />}>{t.camionsDevant({ n: i.camionsDevant })}</Ligne>
                  <Ligne icone={<Horloge />}>{t.attente({ v: i.attenteMinutes })}</Ligne>
                  {incompatible
                    ? <Ligne icone={<Croix />} couleur="var(--critique)">{t.produitIndisponible({ liste: i.produitsIndisponibles.join(', ') || '—' })}</Ligne>
                    : <Ligne icone={<Coche />} couleur="var(--ok)">{t.produitsDisponibles({ n: nbProduits })}</Ligne>}
                </button>
              )
            })}
          </div>
          {ilots.raison && (
            <Notice type="info" titre={reco ? t.pourquoi({ ilot: t.ilot({ n: reco.numero }) }) : undefined}>
              {t.raison[ilots.raison] ?? ''}
            </Notice>
          )}
        </div>
        <aside className="panneau" style={{ alignSelf: 'start' }}>
          <div style={{ fontSize: '1.6rem', fontWeight: 600, marginBottom: '0.5rem' }}>{t.recapitulatif}</div>
          {[
            ...[s.ensemble.avant, s.ensemble.arriere].filter((v) => !!v).map((v) => [t.typeVehicule[v!.type] ?? v!.type, v!.immatriculation]),
            [t.chargementRecap, s.mode === 'dome' ? t.dome : t.source],
            [t.commandesRecap, t.commandesLignes({ c: s.commandes.length, l: lignes })],
            [t.volume, `${nb(volume)} L`],
            [t.poids, `${nb(s.poids.chargement)} / ${nb(s.poids.maximal)} kg`],
            ...(s.planRequis ? [[t.plan, t.planValide]] : []),
          ].map(([a, b]) => (
            <div key={a} style={{ display: 'flex', justifyContent: 'space-between', padding: '0.75rem 0', borderTop: '1px solid var(--bordure)', fontSize: '1.35rem' }}>
              <span style={{ color: 'var(--discret)' }}>{a}</span><b style={{ whiteSpace: 'nowrap' }}>{b}</b>
            </div>
          ))}
        </aside>
      </div>
    </Page>
  )
}

function Ligne({ icone, couleur, children }: { icone: React.ReactNode; couleur?: string; children: React.ReactNode }) {
  return (
    <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', fontSize: '1.4rem', color: couleur }}>
      <span style={{ width: '1.6rem', display: 'inline-flex', flex: 'none' }}>{icone}</span>{children}
    </div>
  )
}

export function EcranFin({ onTermine }: { onTermine: () => void }) {
  const { session: s, enLigne } = useParcours()
  const { t } = useTextes()
  useEffect(() => {
    const m = window.setTimeout(onTermine, 30_000)
    return () => window.clearTimeout(m)
  }, [onTermine])
  return (
    <Page fil={[t.accueil, t.finTitre]} titre={t.finTitre} etape={7} enLigne={enLigne}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '4rem', flex: 1, padding: '0 3rem' }}>
        <div style={{ width: '14rem', height: '14rem', borderRadius: '50%', background: 'var(--ok-fond)', color: 'var(--ok)', display: 'grid', placeItems: 'center', flex: 'none' }}>
          <span style={{ width: '8rem', display: 'inline-flex' }}><Coche /></span>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
          <div style={{ fontSize: '3.5rem', fontWeight: 700 }}>
            {s.ilotChoisi != null ? t.ilot({ n: s.ilotChoisi }) : t.laisserChoisir}
          </div>
          <div style={{ fontSize: '1.9rem' }}>{s.ilotChoisi != null ? t.finIlot({ ilot: t.ilot({ n: s.ilotChoisi }) }) : t.finAuto}</div>
          <div style={{ fontSize: '1.9rem', fontWeight: 600, color: 'var(--canard-fonce)' }}>{t.finBadge}</div>
          <div style={{ fontSize: '1.3rem', color: 'var(--discret)' }}>{t.finRetour}</div>
          <div><button className="btn" type="button" onClick={onTermine}>{t.retirerBadge}</button></div>
        </div>
      </div>
    </Page>
  )
}

export function EcranBlocage() {
  const { session: s, annuler, enLigne, changerLangue } = useParcours()
  const { t, langue } = useTextes()
  const raisons = s.controles.filter((c) => c.gravite === 'bloquant')
  return (
    <Page fil={[t.accueil, t.blocageTitre]} titre={t.blocageTitre} sousTitre={t.blocageSous} enLigne={enLigne} onLangue={changerLangue}
      pied={<Pied primaire={{ libelle: t.retirerBadge, onClick: annuler }} />}>
      <MessageErreur />
      <div style={{ fontSize: '1.6rem', fontWeight: 600 }}>{t.blocageRaisons}</div>
      {s.raisonBlocage === 'VEHICULE_CONTESTE' && <Notice type="critique">{t.blocageConteste}</Notice>}
      {raisons.map((c, i) => <Notice key={i} type="critique">{texteControle(t, c, langue)}</Notice>)}
      {s.controles.filter((c) => c.gravite !== 'bloquant').map((c, i) => <Notice key={`a${i}`} type={c.gravite === 'alerte' ? 'alerte' : 'info'}>{texteControle(t, c, langue)}</Notice>)}
    </Page>
  )
}
