import { ErreurApi, type ApiBorne, type ConfigBorne, type DemandeType, type ProduitReste, type VueBorne, type VueSession } from './types'

/** Nom de la borne (table BORNE) : paramètre ?borne=… de l'adresse, mémorisé sur le poste. */
export function nomBorne(): string {
  const parametre = new URLSearchParams(window.location.search).get('borne')
  try {
    if (parametre) localStorage.setItem('borne', parametre)
    return parametre ?? localStorage.getItem('borne') ?? ''
  } catch {
    return parametre ?? ''
  }
}

async function appel<T>(methode: string, chemin: string, corps?: unknown): Promise<T> {
  let reponse: Response
  try {
    reponse = await fetch(`./api${chemin}`, {
      method: methode,
      headers: { 'Content-Type': 'application/json', 'X-Borne': nomBorne() },
      body: corps === undefined ? undefined : JSON.stringify(corps),
    })
  } catch {
    throw new ErreurApi('SERVEUR_INJOIGNABLE')
  }
  if (reponse.status === 204) return undefined as T
  const texte = await reponse.text()
  const donnees = texte ? JSON.parse(texte) : null
  if (!reponse.ok) throw new ErreurApi(donnees?.code ?? 'ERREUR_BASE', donnees?.details, reponse.status)
  return donnees as T
}

const s = (id: string) => `/sessions/${encodeURIComponent(id)}`

export const apiHttp: ApiBorne = {
  config: () => appel<ConfigBorne>('GET', '/config'),
  borne: () => appel<VueBorne>('GET', '/borne'),
  produits: () => appel<ProduitReste[]>('GET', '/produits'),
  ouvrir: (codeBadge) => appel<VueSession>('POST', '/sessions', { codeBadge }),
  fermer: (id) => appel<void>('DELETE', s(id)),
  plaque: (id, immatriculation) => appel<VueSession>('POST', `${s(id)}/plaques`, { immatriculation }),
  confirmerVehicule: (id) => appel<VueSession>('POST', `${s(id)}/vehicule/confirmer`),
  contesterVehicule: (id) => appel<VueSession>('POST', `${s(id)}/vehicule/contester`),
  definirType: (id, demande: DemandeType) => appel<VueSession>('PUT', `${s(id)}/type`, demande),
  ajouterCommande: (id, numero) => appel<VueSession>('POST', `${s(id)}/commandes`, { numero }),
  retirerCommande: (id, numero) => appel<VueSession>('DELETE', `${s(id)}/commandes/${encodeURIComponent(numero)}`),
  quantite: (id, idComProd, quantite) => appel<VueSession>('PUT', `${s(id)}/lignes/${idComProd}`, { quantite }),
  calculerPlan: (id) => appel<VueSession>('POST', `${s(id)}/plan`),
  valider: (id) => appel<VueSession>('POST', `${s(id)}/valider`),
  ilot: (id, numero) => appel<VueSession>('POST', `${s(id)}/ilot`, { numero }),
}
