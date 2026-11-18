import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ErreurApi, type ApiBorne, type ConfigBorne, type Etape, type VueBorne, type VueSession } from './api/types'
import { creerLecteur, type EtatLecteur, type LecteurBadge } from './badge/lecteur'
import { Accueil } from './ecrans/Accueil'
import { EcranCommandes } from './ecrans/Commandes'
import { Parcours, type ContexteParcours } from './ecrans/commun'
import { EcranBlocage, EcranFin, EcranIlot, EcranPlan } from './ecrans/PlanIlot'
import { EcranType } from './ecrans/Type'
import { EcranPlaques, EcranVehicule } from './ecrans/Vehicule'
import { dictionnaires, LangueContexte } from './i18n'

const CONFIG_DEFAUT: ConfigBorne = {
  lecteur: 'simulation', debitSerie: 115200, intervalleInterrogationMs: 1000, delaiInactiviteSecondes: 180,
  langues: ['fr', 'en'], badgesDemoVisibles: false, nomDepot: 'DPL',
}

export function App({ api }: { api: ApiBorne }) {
  const [config, setConfig] = useState<ConfigBorne | null>(null)
  const [borne, setBorne] = useState<VueBorne | null>(null)
  const [enLigne, setEnLigne] = useState(true)
  const [langue, setLangue] = useState('fr')
  const [session, setSession] = useState<VueSession | null>(null)
  const [erreur, setErreur] = useState<ContexteParcours['erreur']>(null)
  const [erreurAccueil, setErreurAccueil] = useState<string | null>(null)
  const [etatLecteur, setEtatLecteur] = useState<EtatLecteur>('absent')
  const [lecture, setLecture] = useState(false)
  const [occupe, setOccupe] = useState(false)
  const [affichage, setAffichage] = useState<Etape | null>(null)
  const sessionRef = useRef<VueSession | null>(null)
  const lecteurRef = useRef<LecteurBadge | null>(null)
  sessionRef.current = session

  // Configuration de la borne et état du serveur, rafraîchis toutes les 30 s.
  useEffect(() => {
    let arret = false
    const rafraichir = async () => {
      try {
        const [c, b] = await Promise.all([api.config(), api.borne()])
        if (arret) return
        setConfig(c); setBorne(b); setEnLigne(b.baseDisponible)
      } catch (e) {
        if (arret) return
        setConfig((c) => c ?? CONFIG_DEFAUT)
        setEnLigne(false)
        if (e instanceof ErreurApi && e.code === 'BORNE_INCONNUE') setErreurAccueil('BORNE_INCONNUE')
      }
    }
    void rafraichir()
    const m = window.setInterval(rafraichir, 30_000)
    return () => { arret = true; window.clearInterval(m) }
  }, [api])

  const revenirAccueil = useCallback(() => {
    setSession(null); setErreur(null); setAffichage(null); setLangue('fr')
  }, [])

  const fermer = useCallback(async () => {
    const s = sessionRef.current
    revenirAccueil()
    if (s) await api.fermer(s.id).catch(() => undefined)
  }, [api, revenirAccueil])

  const badgeInsere = useCallback(async (code: string) => {
    if (sessionRef.current) return
    setErreurAccueil(null); setLecture(true)
    try {
      setSession(await api.ouvrir(code))
      setErreur(null)
    } catch (e) {
      setErreurAccueil(e instanceof ErreurApi ? e.code : 'ERREUR_BASE')
    } finally {
      setLecture(false)
    }
  }, [api])

  // Lecteur de badge : insertion → ouverture du passage ; retrait → fin ou annulation.
  useEffect(() => {
    if (!config) return
    const lecteur = creerLecteur(config, {
      evenement: (e) => (e.type === 'insertion' ? void badgeInsere(e.code) : void fermer()),
      etat: setEtatLecteur,
    })
    lecteurRef.current = lecteur
    void lecteur.demarrer()
    return () => lecteur.arreter()
  }, [config, badgeInsere, fermer])

  // Retour à l'accueil après inactivité (le lecteur série signale aussi le retrait du badge).
  useEffect(() => {
    if (!session || !config) return
    let m = window.setTimeout(fermer, config.delaiInactiviteSecondes * 1000)
    const relancer = () => { window.clearTimeout(m); m = window.setTimeout(fermer, config.delaiInactiviteSecondes * 1000) }
    window.addEventListener('pointerdown', relancer)
    return () => { window.clearTimeout(m); window.removeEventListener('pointerdown', relancer) }
  }, [session, config, fermer])

  const agir = useCallback<ContexteParcours['agir']>(async (f) => {
    const s = sessionRef.current
    if (!s) return false
    setOccupe(true)
    try {
      const nouvelle = await f(api, s.id)
      setSession(nouvelle); setErreur(null); setAffichage(null)
      return true
    } catch (e) {
      const err = e instanceof ErreurApi ? e : new ErreurApi('ERREUR_BASE')
      if (err.code === 'SESSION_INCONNUE') { revenirAccueil(); setErreurAccueil('SESSION_INCONNUE'); return false }
      setErreur({ code: err.code, details: err.details })
      return false
    } finally {
      setOccupe(false)
    }
  }, [api, revenirAccueil])

  const langues = (config?.langues ?? ['fr']).filter((l, i, tout) => dictionnaires[l] && tout.indexOf(l) === i)
  const changerLangue = useCallback(() => {
    setLangue((l) => langues[(langues.indexOf(l) + 1) % langues.length] ?? 'fr')
  }, [langues])
  const contexteLangue = useMemo(() => ({ langue, t: dictionnaires[langue] ?? dictionnaires.fr, changer: setLangue }), [langue])

  let ecran: React.ReactNode
  if (!session) {
    ecran = <Accueil config={config} borne={borne} enLigne={enLigne} etatLecteur={etatLecteur} lecture={lecture} erreur={erreurAccueil}
      onBadge={badgeInsere} onConnecterLecteur={lecteurRef.current?.connecter ? () => void lecteurRef.current!.connecter!() : undefined} />
  } else {
    const etape = affichage ?? session.etape
    const contexte: ContexteParcours = { session, api, enLigne, erreur, agir, annuler: () => void fermer(), afficher: setAffichage, changerLangue }
    ecran = (
      <Parcours.Provider value={contexte}>
        {etape === 'plaques' && <EcranPlaques />}
        {etape === 'vehicule' && <EcranVehicule />}
        {etape === 'type' && <EcranType key={session.id + session.mode} />}
        {etape === 'commandes' && <EcranCommandes />}
        {etape === 'plan' && (session.plan ? <EcranPlan /> : <EcranCommandes />)}
        {etape === 'ilot' && <EcranIlot />}
        {etape === 'fin' && <EcranFin onTermine={() => void fermer()} />}
        {etape === 'blocage' && <EcranBlocage />}
      </Parcours.Provider>
    )
  }

  return (
    <LangueContexte.Provider value={contexteLangue}>
      {ecran}
      {occupe && <div className="occupe-voile" />}
      {session && config?.lecteur !== 'webserial' && session.etape !== 'fin' && (
        <button type="button" className="btn petit" onClick={() => void fermer()}
          style={{ position: 'fixed', right: '1rem', bottom: '0.4rem', height: '2.2rem', fontSize: '1rem', opacity: 0.7 }}>
          {contexteLangue.t.retirerBadge}
        </button>
      )}
    </LangueContexte.Provider>
  )
}
