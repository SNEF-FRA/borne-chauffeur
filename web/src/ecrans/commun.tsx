import { createContext, useContext } from 'react'
import type { ApiBorne, Etape, VueControle, VueSession } from '../api/types'
import type { CuveAffichee } from '../composants/Camion'
import { Notice } from '../composants/Cadre'
import { dateCourte, nb, useTextes } from '../i18n'
import type { Textes } from '../i18n/fr'

export interface ContexteParcours {
  session: VueSession
  api: ApiBorne
  enLigne: boolean
  erreur: { code: string; details?: Record<string, unknown> | null } | null
  /** Appelle l'API ; met à jour la session ou affiche l'erreur. Renvoie true si l'appel a réussi. */
  agir: (f: (api: ApiBorne, id: string) => Promise<VueSession>) => Promise<boolean>
  annuler: () => void
  afficher: (etape: Etape | null) => void
  changerLangue: () => void
}

export const Parcours = createContext<ContexteParcours>(null!)
export const useParcours = () => useContext(Parcours)

export function MessageErreur() {
  const { erreur } = useParcours()
  const { t } = useTextes()
  if (!erreur) return null
  return <Notice type="critique">{t.erreur[erreur.code] ?? erreur.code}</Notice>
}

export function texteControle(t: Textes, c: VueControle, langue: string) {
  const f = t.controle[c.code]
  return f ? f({ cible: c.cible, date: dateCourte(c.date, langue), jours: c.jours, texte: c.texte }) : c.code
}

export function nomComplet(s: VueSession) {
  return { prenom: s.chauffeur.prenom, nom: s.chauffeur.nom.charAt(0) + s.chauffeur.nom.slice(1).toLowerCase(), transporteur: s.chauffeur.transporteur, badge: s.chauffeur.numeroBadge }
}

/** Compartiments de l'ensemble, avec les restes déclarés en hachures. */
export function cuvesEnsemble(s: VueSession, t: Textes, options: { anneau?: number | null; onClick?: (idCuve: number) => void } = {}): CuveAffichee[] {
  return s.ensemble.compartiments.map((c, i, tout) => {
    const reste = s.restes.find((r) => r.idCuve === c.idCuve)
    return {
      cle: c.idCuve,
      libelle: c.libelle,
      capacite: c.capacite,
      hachure: !!reste,
      titre: reste?.produit,
      lignes: reste ? [`${t.reste.toLowerCase()} ${nb(reste.volume)} L`] : undefined,
      anneau: options.anneau === c.idCuve || (!!reste && !!options.onClick),
      attelage: i > 0 && c.arriere && !tout[i - 1].arriere,
      onClick: options.onClick ? () => options.onClick!(c.idCuve) : undefined,
    }
  })
}

export function libelleEnsemble(s: VueSession, t: Textes) {
  return t.ensembleLibelle[s.ensemble.libelle] ?? s.ensemble.libelle
}
