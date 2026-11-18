import { useEffect, useState } from 'react'
import type { ProduitReste } from '../api/types'
import { Camion } from '../composants/Camion'
import { Fenetre, Notice, Page, Pied } from '../composants/Cadre'
import { PaveNumerique } from '../composants/Claviers'
import { nb, useTextes } from '../i18n'
import { cuvesEnsemble, MessageErreur, useParcours } from './commun'

type ResteLocal = { idCuve: number; idProdCharger: number; volume: number }
type Question = 'essences' | 'energie' | null

export function EcranType() {
  const { session: s, api, agir, annuler, enLigne, changerLangue, afficher } = useParcours()
  const { t } = useTextes()
  const [mode, setMode] = useState(s.mode)
  const [restes, setRestes] = useState<ResteLocal[]>(s.restes.map((r) => ({ idCuve: r.idCuve, idProdCharger: r.idProdCharger, volume: r.volume })))
  const [declarer, setDeclarer] = useState(s.restes.length > 0 || s.poids.ecartTareExcessif)
  const [produits, setProduits] = useState<ProduitReste[]>([])
  const [cuveActive, setCuveActive] = useState<number | null>(null)
  const [question, setQuestion] = useState<Question>(null)
  const [refus, setRefus] = useState<string | null>(null)

  useEffect(() => { void api.produits().then(setProduits).catch(() => undefined) }, [api])

  const envoyer = async (essences: boolean | null, energie: boolean | null) => {
    setQuestion(null)
    await agir((a, id) => a.definirType(id, {
      mode, essencesDansEnsemble: essences, verificationsEnergie: energie, restes: declarer ? restes : [],
    }))
  }

  const continuer = (essences: boolean | null = null, energie: boolean | null = null) => {
    setRefus(null)
    if (mode === 'dome' && essences === null) { setQuestion('essences'); return }
    if (s.verificationsEnergie && energie === null) { setQuestion('energie'); return }
    void envoyer(essences, energie)
  }

  // Affichage : les restes locaux remplacent ceux de la session tant qu'ils ne sont pas envoyés.
  const vue = { ...s, restes: declarer ? restes.map((r) => {
    const c = s.ensemble.compartiments.find((x) => x.idCuve === r.idCuve)!
    return { ...r, libelle: c.libelle, produit: produits.find((p) => p.idProdCharger === r.idProdCharger)?.nom ?? '', poids: 0 }
  }) : [] }

  return (
    <Page fil={[t.accueil, t.etapes[2]]} titre={t.typeTitre} sousTitre={t.typeSous} etape={3} enLigne={enLigne} onLangue={changerLangue}
      pied={<Pied annuler={annuler} aide={t.retirerAnnule} retour={() => afficher('vehicule')}
        primaire={{ libelle: t.continuer, onClick: () => continuer() }} />}>
      <MessageErreur />
      {refus && <Notice type="critique">{refus}</Notice>}
      <div style={{ fontSize: '1.75rem', fontWeight: 600 }}>{t.typeChargement}</div>
      <div className="choix">
        {(['source', 'dome'] as const).map((m) => (
          <button key={m} type="button" className={mode === m ? 'actif' : ''} onClick={() => { setMode(m); setRefus(null) }} aria-pressed={mode === m}>
            <span className="rond" />
            <span>
              <b>{m === 'source' ? t.source : t.dome}</b>
              <span>{m === 'source' ? t.sourceDetail : t.domeDetail}{s.modePropose === m ? ` · ${t.proposeVehicule}` : ''}</span>
            </span>
          </button>
        ))}
      </div>
      {s.poids.ecartTareExcessif && <Notice type="alerte">{t.ecartTare({ v: nb(s.poids.ecartTare) })}</Notice>}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderTop: '1px solid var(--bordure)', paddingTop: '1.25rem' }}>
        <span style={{ fontSize: '1.75rem', fontWeight: 600 }}>{t.resteQuestion}</span>
        <div className="bascule">
          <button type="button" className={!declarer ? 'actif' : ''} onClick={() => setDeclarer(false)}>{t.non}</button>
          <button type="button" className={declarer ? 'actif' : ''} onClick={() => setDeclarer(true)}>{t.oui}</button>
        </div>
      </div>
      {declarer && <>
        <div style={{ width: '100%', maxWidth: '66rem', margin: '0 auto' }}>
          <Camion cuves={cuvesEnsemble(vue, t, { onClick: (idCuve) => setCuveActive(idCuve) })} />
        </div>
        <div style={{ fontSize: '1.3rem', color: 'var(--discret)' }}>{t.resteAide}</div>
      </>}

      {cuveActive !== null && (
        <FenetreReste
          libelle={s.ensemble.compartiments.find((c) => c.idCuve === cuveActive)!.libelle}
          capacite={s.ensemble.compartiments.find((c) => c.idCuve === cuveActive)!.capacite}
          produits={produits}
          actuel={restes.find((r) => r.idCuve === cuveActive)}
          onFermer={() => setCuveActive(null)}
          onRetirer={() => { setRestes(restes.filter((r) => r.idCuve !== cuveActive)); setCuveActive(null) }}
          onEnregistrer={(idProdCharger, volume) => {
            setRestes([...restes.filter((r) => r.idCuve !== cuveActive), { idCuve: cuveActive, idProdCharger, volume }])
            setCuveActive(null)
          }} />
      )}

      {question === 'essences' && (
        <Fenetre titre={t.questionEssences} texte={t.questionEssencesDetail} actions={<>
          <button className="btn" type="button" onClick={() => { setQuestion(null); setRefus(t.domeImpossible) }}>{t.oui}</button>
          <button className="btn primaire" type="button" onClick={() => continuer(false, null)}>{t.non}</button>
        </>} />
      )}
      {question === 'energie' && (
        <Fenetre titre={t.questionEnergie} actions={<>
          <button className="btn" type="button" onClick={() => { setQuestion(null); setRefus(t.erreur.VERIFICATIONS_ENERGIE) }}>{t.non}</button>
          <button className="btn primaire" type="button" onClick={() => continuer(mode === 'dome' ? false : null, true)}>{t.oui}</button>
        </>} />
      )}
    </Page>
  )
}

function FenetreReste({ libelle, capacite, produits, actuel, onFermer, onRetirer, onEnregistrer }: {
  libelle: string; capacite: number; produits: ProduitReste[]; actuel?: ResteLocal
  onFermer: () => void; onRetirer: () => void; onEnregistrer: (idProdCharger: number, volume: number) => void
}) {
  const { t } = useTextes()
  const [produit, setProduit] = useState<number | null>(actuel?.idProdCharger ?? null)
  const [volume, setVolume] = useState(actuel ? String(actuel.volume) : '')
  const [neuf, setNeuf] = useState(true) // la première touche remplace la valeur existante
  const v = Number(volume || 0)
  const valide = produit !== null && v > 0 && v <= capacite
  return (
    <Fenetre large titre={`${t.reste} ${libelle} · ${nb(capacite)} L`} actions={<>
      <button className="btn" type="button" onClick={onFermer}>{t.fermer}</button>
      {actuel && <button className="btn annuler" type="button" onClick={onRetirer}>{t.resteRetirer}</button>}
    </>}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 24rem', gap: '2rem' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div style={{ fontSize: '1.3rem', fontWeight: 600 }}>{t.resteProduit}</div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.6rem' }}>
            {produits.map((p) => (
              <button key={p.idProdCharger} type="button" className="btn petit" onClick={() => setProduit(p.idProdCharger)} aria-pressed={produit === p.idProdCharger}
                style={produit === p.idProdCharger ? { background: 'var(--canard-teinte)', borderColor: 'var(--canard)', borderWidth: 2 } : undefined}>
                {p.nom}
              </button>
            ))}
          </div>
          <div style={{ fontSize: '1.3rem', fontWeight: 600, marginTop: '0.5rem' }}>{t.resteVolume}</div>
          <div className={`saisie${volume ? '' : ' vide'}`} style={v > capacite ? { borderColor: 'var(--critique)' } : undefined}>{nb(v)} L</div>
        </div>
        <PaveNumerique valeur={neuf ? '' : volume} onChange={(x) => { setNeuf(false); setVolume(x) }} max={5} libelleValider={t.valider}
          onValider={() => valide && onEnregistrer(produit!, v)} />
      </div>
    </Fenetre>
  )
}
