// Types renvoyés par l'API (voir src/Dpl.Borne.Api/Parcours/Vues.cs).

export type Etape = 'plaques' | 'vehicule' | 'type' | 'commandes' | 'plan' | 'ilot' | 'fin' | 'blocage'
export type Gravite = 'info' | 'alerte' | 'bloquant'

export interface Couleur { fond: string; texte: string }
export interface MessageDepot { texte: string; couleur?: string | null }
export interface VueBorne { nom: string; enService: boolean; baseDisponible: boolean; messages: MessageDepot[] }

export interface ConfigBorne {
  lecteur: 'webserial' | 'clavier' | 'simulation'
  debitSerie: number
  intervalleInterrogationMs: number
  delaiInactiviteSecondes: number
  langues: string[]
  badgesDemoVisibles: boolean
  nomDepot: string
}

export interface VueControle { code: string; gravite: Gravite; cible?: string | null; date?: string | null; jours?: number | null; texte?: string | null }
export interface VueVehicule { immatriculation: string; type: string; transporteur: string; compartiments: number; capacite: number; verificationsEnergie: boolean }
export interface VueCompartiment { position: number; idCuve: number; numero: number; libelle: string; immatriculation: string; capacite: number; arriere: boolean }
export interface VueEnsemble { etat: string; libelle: string; avant?: VueVehicule | null; arriere?: VueVehicule | null; compartiments: VueCompartiment[]; capaciteTotale: number }
export interface VueReste { idCuve: number; libelle: string; idProdCharger: number; produit: string; volume: number; poids: number }
export interface VuePoids {
  ptracRetenu: number; tareReelle: number; tareTheorique: number; tareReelleConnue: boolean; maximal: number
  produits: number; restes: number; chargement: number; marge: number; depasse: boolean; ecartTare: number; ecartTareExcessif: boolean
  detail: { produit: string; couleur: Couleur; poids: number }[]
}
export interface VueLigne { idComProd: number; produit: string; couleur: Couleur; commandee: number; distribuable: number; quantite: number; densite: number; modifiable: boolean; quotaRestant?: number | null }
export interface VueCommande { numero: string; client: string; destination?: string | null; validite: string; globale: boolean; lignes: VueLigne[] }
export interface VueContenu { idComProd: number; numeroCommande: string; produit: string; couleur: Couleur; litres: number }
export interface VueCompartimentPlan { position: number; idCuve: number; libelle: string; capacite: number; contenu: VueContenu[]; reste?: VueReste | null }
export interface VueLignePlan { idComProd: number; numeroCommande: string; produit: string; couleur: Couleur; demande: number; place: number; compartiments: string[] }
export interface VuePlan { complet: boolean; optimal: boolean; compartiments: VueCompartimentPlan[]; lignes: VueLignePlan[] }
export interface VueIlot { numero: number; etat: 'libre' | 'occupe' | 'incompatible'; camionsDevant: number; attenteMinutes: number; recommande: boolean; produitsIndisponibles: string[] }
export interface VueIlots { recommande?: number | null; raison?: string | null; liste: VueIlot[] }

export interface VueSession {
  id: string
  etape: Etape
  raisonBlocage?: string | null
  borne: string
  chauffeur: { nom: string; prenom: string; numeroBadge: string; transporteur?: string | null }
  controles: VueControle[]
  bloque: boolean
  plaqueAttendue?: 'avant' | 'arriere' | 'remorque' | null
  ensemble: VueEnsemble
  verificationsEnergie: boolean
  mode: 'source' | 'dome'
  modePropose: 'source' | 'dome'
  restes: VueReste[]
  poids: VuePoids
  commandes: VueCommande[]
  produitsIndisponibles: string[]
  planRequis: boolean
  plan?: VuePlan | null
  chargementEnregistre: boolean
  modification: boolean
  ilots?: VueIlots | null
  ilotChoisi?: number | null
  modeAutomatique: boolean
}

export interface ProduitReste { idProdCharger: number; nom: string; densite: number; idCouleur?: number | null }
export interface DemandeType { mode: 'source' | 'dome'; essencesDansEnsemble?: boolean | null; verificationsEnergie?: boolean | null; restes: { idCuve: number; idProdCharger: number; volume: number }[] }

export class ErreurApi extends Error {
  constructor(public code: string, public details?: Record<string, unknown> | null, public statut = 0) {
    super(code)
  }
}

/** Opérations de la borne ; implémentées par le client HTTP et par la démonstration autonome. */
export interface ApiBorne {
  config(): Promise<ConfigBorne>
  borne(): Promise<VueBorne>
  produits(): Promise<ProduitReste[]>
  ouvrir(codeBadge: string): Promise<VueSession>
  fermer(id: string): Promise<void>
  plaque(id: string, immatriculation: string): Promise<VueSession>
  confirmerVehicule(id: string): Promise<VueSession>
  contesterVehicule(id: string): Promise<VueSession>
  definirType(id: string, demande: DemandeType): Promise<VueSession>
  ajouterCommande(id: string, numero: string): Promise<VueSession>
  retirerCommande(id: string, numero: string): Promise<VueSession>
  quantite(id: string, idComProd: number, quantite: number): Promise<VueSession>
  calculerPlan(id: string): Promise<VueSession>
  valider(id: string): Promise<VueSession>
  ilot(id: string, numero: number | null): Promise<VueSession>
}
