// Démonstration autonome : la page simule l'API avec les mêmes données fictives que StockageDemo (côté serveur)
// et une version simplifiée des mêmes règles. Sert à présenter la borne sans serveur ; rien n'est enregistré.
import {
  ErreurApi, type ApiBorne, type Couleur, type DemandeType, type ProduitReste, type VueCommande, type VueCompartiment,
  type VueIlot, type VueLigne, type VueSession, type VueVehicule,
} from './types'

const jour = (d: number) => new Date(Date.now() + d * 86_400_000).toISOString().slice(0, 10)
const COULEURS: Record<number, Couleur> = {
  0: { fond: '#CBD5E1', texte: '#1E293B' }, 1: { fond: '#EAB308', texte: '#1E293B' }, 2: { fond: '#DC2626', texte: '#FFFFFF' },
  4: { fond: '#92400E', texte: '#FFFFFF' }, 5: { fond: '#16A34A', texte: '#FFFFFF' },
}
const PRODUITS: (ProduitReste & { couleur: number })[] = [
  { idProdCharger: 9, nom: 'Gazole B7', densite: 0.845, couleur: 1 },
  { idProdCharger: 4, nom: 'SP95-E10', densite: 0.745, couleur: 5 },
  { idProdCharger: 6, nom: 'SP98', densite: 0.75, couleur: 2 },
  { idProdCharger: 12, nom: 'GNR', densite: 0.845, couleur: 2 },
  { idProdCharger: 15, nom: 'Fioul domestique', densite: 0.85, couleur: 4 },
]
type Veh = VueVehicule & { tare: number; ptac: number; ptrac: number; capas: number[]; premierId: number }
const FLOTTE: Veh[] = [
  { immatriculation: 'AB-123-CD', type: 'tracteur', transporteur: 'Transports Exemple', compartiments: 0, capacite: 0, verificationsEnergie: false, tare: 8120, ptac: 19000, ptrac: 44000, capas: [], premierId: 0 },
  { immatriculation: 'EF-456-GH', type: 'citerne', transporteur: 'Transports Exemple', compartiments: 6, capacite: 36000, verificationsEnergie: false, tare: 6700, ptac: 38000, ptrac: 0, capas: [7000, 6000, 5000, 5000, 6000, 7000], premierId: 1000 },
  { immatriculation: 'CD-234-EF', type: 'tracteur', transporteur: 'Transports Exemple', compartiments: 0, capacite: 0, verificationsEnergie: false, tare: 8000, ptac: 19000, ptrac: 44000, capas: [], premierId: 0 },
  { immatriculation: 'GH-345-IJ', type: 'citerne', transporteur: 'Transports Exemple', compartiments: 4, capacite: 30000, verificationsEnergie: false, tare: 6700, ptac: 38000, ptrac: 0, capas: [8000, 7000, 7000, 8000], premierId: 1100 },
  { immatriculation: 'KL-567-MN', type: 'porteur', transporteur: 'Livraisons Test', compartiments: 4, capacite: 14000, verificationsEnergie: false, tare: 11200, ptac: 19000, ptrac: 40000, capas: [5000, 4000, 3000, 2000], premierId: 1200 },
  { immatriculation: 'OP-890-QR', type: 'remorque', transporteur: 'Livraisons Test', compartiments: 2, capacite: 11000, verificationsEnergie: false, tare: 5100, ptac: 18000, ptrac: 0, capas: [6000, 5000], premierId: 1300 },
  { immatriculation: 'ST-123-UV', type: 'tracteur', transporteur: 'Livraisons Test', compartiments: 0, capacite: 0, verificationsEnergie: false, tare: 7900, ptac: 19000, ptrac: 44000, capas: [], premierId: 0 },
  { immatriculation: 'WX-456-YZ', type: 'citerne', transporteur: 'Livraisons Test', compartiments: 4, capacite: 30000, verificationsEnergie: false, tare: 6500, ptac: 38000, ptrac: 0, capas: [9000, 6000, 6000, 9000], premierId: 1400 },
]
const CHAUFFEURS: Record<string, { nom: string; prenom: string; badge: string; adr: number; vehicules: string[]; tare: number; message?: string }> = {
  '04A1B2C3D4E5F6': { nom: 'MARTIN', prenom: 'Jean', badge: '104233', adr: 18, vehicules: ['AB-123-CD', 'EF-456-GH'], tare: 14920 },
  '04B2C3D4E5F6A7': { nom: 'LEROY', prenom: 'Sophie', badge: '104871', adr: -1, vehicules: ['CD-234-EF', 'GH-345-IJ'], tare: 14700 },
  '04C3D4E5F6A7B8': { nom: 'BENALI', prenom: 'Karim', badge: '105102', adr: 700, vehicules: [], tare: 0, message: 'Pensez à faire signer votre bon de livraison.' },
}
type LigneDemo = { id: number; prod: number; commandee: number; distribuable: number }
const COMMANDES: Record<string, { client: string; destination: string; globale: boolean; modifiable: boolean; lignes: LigneDemo[] }> = {
  '4518203': { client: 'Station Exemple Nord', destination: 'Lyon 7e', globale: false, modifiable: false, lignes: [{ id: 91001, prod: 9, commandee: 13000, distribuable: 13000 }, { id: 91002, prod: 4, commandee: 6000, distribuable: 6000 }] },
  '4518377': { client: 'Garage Exemple', destination: 'Villeurbanne', globale: true, modifiable: false, lignes: [{ id: 91003, prod: 9, commandee: 20000, distribuable: 9500 }] },
  '4519250': { client: 'Transports Rhône Sud', destination: 'Vénissieux', globale: false, modifiable: false, lignes: [{ id: 91004, prod: 6, commandee: 8000, distribuable: 8000 }] },
  '4519400': { client: 'Coopérative Agricole', destination: 'Bourg-en-Bresse', globale: false, modifiable: true, lignes: [{ id: 91005, prod: 12, commandee: 5000, distribuable: 5000 }] },
}
const ILOTS: { n: number; prods: number[]; temps: number; occupe: boolean; attente: number; dome: boolean }[] = [
  { n: 21, prods: [9, 4, 6, 15], temps: 16, occupe: false, attente: 0, dome: true },
  { n: 22, prods: [9, 4, 12], temps: 18, occupe: true, attente: 1, dome: true },
  { n: 3, prods: [9, 4, 6, 12], temps: 12, occupe: false, attente: 1, dome: false },
  { n: 5, prods: [9, 12, 15], temps: 10, occupe: false, attente: 0, dome: false },
  { n: 6, prods: [9, 4, 6], temps: 17, occupe: true, attente: 2, dome: false },
  { n: 7, prods: [9, 6], temps: 14, occupe: false, attente: 0, dome: false },
]

const prod = (id: number) => PRODUITS.find((p) => p.idProdCharger === id)!
const poidsDe = (l: number, d: number) => Math.round(l * d)
const erreur = (code: string): never => { throw new ErreurApi(code) }

type Etat = {
  s: VueSession
  vehicules: Veh[]
  tare: number
  restesSaisis: { idCuve: number; idProdCharger: number; volume: number }[]
}
let courant: Etat | null = null

function compartiments(vs: Veh[]): VueCompartiment[] {
  const avant = vs.find((v) => v.type === 'tracteur' || v.type === 'porteur')
  const arriere = vs.find((v) => v.type === 'citerne' || v.type === 'remorque')
  const liste: VueCompartiment[] = []
  const ajouter = (v: Veh, debut: number, estArriere: boolean) => v.capas.forEach((c, i) => liste.push({
    position: debut + i, idCuve: v.premierId + i, numero: i + 1, libelle: `C${i + 1}`, immatriculation: v.immatriculation, capacite: c, arriere: estArriere,
  }))
  if (avant?.type === 'porteur') { ajouter(avant, 1, false); if (arriere) ajouter(arriere, avant.capas.length + 2, true) } else if (arriere) ajouter(arriere, 1, true)
  return liste
}

function recalculer(e: Etat) {
  const s = e.s
  const avant = e.vehicules.find((v) => v.type === 'tracteur' || v.type === 'porteur')
  const arriere = e.vehicules.find((v) => v.type === 'citerne' || v.type === 'remorque')
  const libelle = avant?.type === 'tracteur' ? 'Tracteur + citerne' : arriere ? 'Porteur + remorque' : 'Porteur'
  s.ensemble = {
    etat: !avant ? 'ManqueAvant' : avant.type === 'tracteur' && !arriere ? 'ManqueArriere' : 'Complete', libelle,
    avant, arriere, compartiments: compartiments(e.vehicules), capaciteTotale: e.vehicules.reduce((x, v) => x + v.capacite, 0),
  }
  s.plaqueAttendue = !avant ? 'avant' : avant.type === 'tracteur' && !arriere ? 'arriere' : avant.type === 'porteur' && !arriere ? 'remorque' : null
  const sommePtrac = e.vehicules.reduce((x, v) => x + v.ptrac, 0), sommePtac = e.vehicules.reduce((x, v) => x + v.ptac, 0)
  const ptrac = sommePtrac >= 44000 && sommePtac >= 38000 ? 44000 : sommePtrac >= 44000 && sommePtac >= 34000 ? 41000 : sommePtrac >= 40000 && sommePtac >= 34000 ? 40000 : sommePtrac
  const theo = e.vehicules.reduce((x, v) => x + v.tare, 0)
  const lignes = s.commandes.flatMap((c) => c.lignes)
  const detail = [...new Set(lignes.filter((l) => l.quantite > 0).map((l) => l.produit))].map((p) => ({
    produit: p, couleur: lignes.find((l) => l.produit === p)!.couleur,
    poids: lignes.filter((l) => l.produit === p).reduce((x, l) => x + poidsDe(l.quantite, l.densite), 0),
  }))
  const produits = detail.reduce((x, d) => x + d.poids, 0), restes = s.restes.reduce((x, r) => x + r.poids, 0)
  const tare = e.tare > 0 ? e.tare : theo
  const maximal = ptrac - tare + 200
  s.poids = {
    ptracRetenu: ptrac, tareReelle: e.tare, tareTheorique: theo, tareReelleConnue: e.tare > 0, maximal, produits, restes,
    chargement: produits + restes, marge: maximal - produits - restes, depasse: produits + restes > maximal,
    ecartTare: e.tare > 0 ? e.tare - theo : 0, ecartTareExcessif: e.tare > 0 && e.tare - theo > 200, detail,
  }
}

/** Plan : même principe que PlanificateurChargement (un produit par compartiment, le moins de compartiments et de vide). */
function planifier(s: VueSession) {
  const comps = s.ensemble.compartiments.filter((c) => !s.restes.some((r) => r.idCuve === c.idCuve))
  const lignes = s.commandes.flatMap((c) => c.lignes.map((l) => ({ ...l, numero: c.numero }))).filter((l) => l.quantite > 0)
  const groupes = [...new Set(lignes.map((l) => l.produit))].map((p) => ({ p, lignes: lignes.filter((l) => l.produit === p), v: lignes.filter((l) => l.produit === p).reduce((x, l) => x + l.quantite, 0) }))
  let meilleur: { attr: number[]; place: number; nb: number; vide: number } | null = null
  const attr = comps.map(() => -1), capa = groupes.map(() => 0)
  const explorer = (i: number, nb: number) => {
    const place = groupes.reduce((x, g, k) => x + Math.min(g.v, capa[k]), 0)
    const vide = groupes.reduce((x, g, k) => x + Math.max(0, capa[k] - g.v), 0)
    const fini = groupes.every((g, k) => capa[k] >= g.v)
    if (i === comps.length || fini) {
      if (!meilleur || place > meilleur.place || (place === meilleur.place && (nb < meilleur.nb || (nb === meilleur.nb && vide < meilleur.vide))))
        meilleur = { attr: attr.map((a, k) => (k < i ? a : -1)), place, nb, vide }
      return
    }
    groupes.forEach((g, k) => { if (capa[k] < g.v) { attr[i] = k; capa[k] += comps[i].capacite; explorer(i + 1, nb + 1); capa[k] -= comps[i].capacite; attr[i] = -1 } })
    explorer(i + 1, nb)
  }
  explorer(0, 0)
  const contenu = new Map<number, { idComProd: number; numeroCommande: string; produit: string; couleur: Couleur; litres: number }[]>()
  const place = new Map<number, { litres: number; comps: string[] }>()
  groupes.forEach((g, k) => {
    const mes = comps.filter((_, i) => meilleur!.attr[i] === k)
    let j = 0
    const libre = mes.map((c) => c.capacite)
    for (const l of [...g.lignes].sort((a, b) => b.quantite - a.quantite)) {
      let reste = l.quantite
      while (reste > 0 && j < mes.length) {
        const q = Math.min(reste, libre[j])
        const liste = contenu.get(mes[j].idCuve) ?? []
        liste.push({ idComProd: l.idComProd, numeroCommande: l.numero, produit: l.produit, couleur: l.couleur, litres: q })
        contenu.set(mes[j].idCuve, liste)
        const p = place.get(l.idComProd) ?? { litres: 0, comps: [] }
        p.litres += q; p.comps.push(mes[j].libelle); place.set(l.idComProd, p)
        libre[j] -= q; reste -= q
        if (libre[j] === 0) j++
      }
    }
  })
  const lignesPlan = lignes.map((l) => ({ idComProd: l.idComProd, numeroCommande: l.numero, produit: l.produit, couleur: l.couleur, demande: l.quantite, place: place.get(l.idComProd)?.litres ?? 0, compartiments: place.get(l.idComProd)?.comps ?? [] }))
  s.plan = {
    complet: lignesPlan.every((l) => l.place >= l.demande), optimal: true, lignes: lignesPlan,
    compartiments: s.ensemble.compartiments.map((c) => ({ position: c.position, idCuve: c.idCuve, libelle: c.libelle, capacite: c.capacite, contenu: contenu.get(c.idCuve) ?? [], reste: s.restes.find((r) => r.idCuve === c.idCuve) ?? null })),
  }
}

function ilots(s: VueSession): VueIlot[] {
  const prods = s.commandes.flatMap((c) => c.lignes).filter((l) => l.quantite > 0).map((l) => PRODUITS.find((p) => p.nom === l.produit)!)
  const liste = ILOTS.filter((i) => i.dome === (s.mode === 'dome')).map((i) => {
    const manquants = [...new Set(prods.filter((p) => !i.prods.includes(p.idProdCharger)).map((p) => p.nom))]
    const devant = (i.occupe ? 1 : 0) + i.attente
    return { numero: i.n, etat: manquants.length ? 'incompatible' : i.occupe ? 'occupe' : 'libre', camionsDevant: devant, attenteMinutes: devant * i.temps, recommande: false, produitsIndisponibles: manquants } as VueIlot
  })
  const choix = liste.filter((i) => i.etat !== 'incompatible').sort((a, b) => a.attenteMinutes - b.attenteMinutes)[0]
  if (choix) choix.recommande = true
  s.ilots = { recommande: choix?.numero ?? null, raison: !choix ? 'AUCUN_COMPATIBLE' : liste.filter((i) => i.etat !== 'incompatible').length === 1 ? 'SEUL_COMPATIBLE' : 'ATTENTE_LA_PLUS_COURTE', liste: [...liste].sort((a, b) => Number(b.recommande) - Number(a.recommande) || Number(a.etat === 'incompatible') - Number(b.etat === 'incompatible') || a.attenteMinutes - b.attenteMinutes) }
  return liste
}

function etat(id: string): Etat {
  if (!courant || courant.s.id !== id) erreur('SESSION_INCONNUE')
  return courant!
}

const repondre = (e: Etat) => { recalculer(e); return new Promise<VueSession>((r) => setTimeout(() => r(structuredClone(e.s)), 120)) }

export const apiDemo: ApiBorne = {
  config: async () => ({ lecteur: 'simulation', debitSerie: 115200, intervalleInterrogationMs: 1000, delaiInactiviteSecondes: 600, langues: ['fr', 'en'], badgesDemoVisibles: true, nomDepot: 'DPL' }),
  borne: async () => ({ nom: 'DEMO1', enService: true, baseDisponible: true, messages: [{ texte: 'L’îlot 4 est en maintenance jusqu’à 15 h 00.' }] }),
  produits: async () => PRODUITS,
  async ouvrir(code) {
    const c = CHAUFFEURS[code] ?? erreur('BADGE_INCONNU')
    const vehicules = FLOTTE.filter((v) => c.vehicules.includes(v.immatriculation))
    const controles = [
      ...(c.adr < 0 ? [{ code: 'ADR_EXPIRE', gravite: 'bloquant' as const, date: jour(c.adr) }] : c.adr <= 30 ? [{ code: 'ADR_BIENTOT', gravite: 'alerte' as const, date: jour(c.adr), jours: c.adr }] : []),
      ...(c.message ? [{ code: 'MESSAGE_PERSONNEL', gravite: 'info' as const, texte: c.message }] : []),
    ]
    const s = {
      id: crypto.randomUUID(), etape: 'vehicule', borne: 'DEMO1', chauffeur: { nom: c.nom, prenom: c.prenom, numeroBadge: c.badge, transporteur: vehicules[0]?.transporteur },
      controles, bloque: c.adr < 0, verificationsEnergie: false, mode: 'source', modePropose: 'source', restes: [], commandes: [], produitsIndisponibles: [],
      planRequis: true, chargementEnregistre: false, modification: false, modeAutomatique: false,
    } as unknown as VueSession
    courant = { s, vehicules, tare: c.tare, restesSaisis: [] }
    recalculer(courant)
    s.etape = s.plaqueAttendue && s.plaqueAttendue !== 'remorque' ? 'plaques' : s.bloque ? 'blocage' : 'vehicule'
    return repondre(courant)
  },
  async fermer() { courant = null },
  async plaque(id, immat) {
    const e = etat(id)
    const v = FLOTTE.find((x) => x.immatriculation.replace(/-/g, '') === immat.toUpperCase().replace(/[^A-Z0-9]/g, '')) ?? erreur('PLAQUE_INCONNUE')
    const attendue = e.s.plaqueAttendue
    const avant = v.type === 'tracteur' || v.type === 'porteur'
    if ((attendue === 'avant' && !avant) || (attendue === 'arriere' && v.type !== 'citerne') || (attendue === 'remorque' && v.type !== 'remorque')) erreur('PLAQUE_MAUVAISE_POSITION')
    e.vehicules.push(v)
    e.tare = e.vehicules.reduce((x, y) => x + y.tare, 0)
    e.s.chauffeur.transporteur = e.vehicules[0].transporteur
    recalculer(e)
    e.s.etape = e.s.plaqueAttendue && e.s.plaqueAttendue !== 'remorque' ? 'plaques' : 'vehicule'
    return repondre(e)
  },
  async confirmerVehicule(id) { const e = etat(id); e.s.etape = 'type'; return repondre(e) },
  async contesterVehicule(id) { const e = etat(id); e.s.etape = 'blocage'; e.s.raisonBlocage = 'VEHICULE_CONTESTE'; return repondre(e) },
  async definirType(id, d: DemandeType) {
    const e = etat(id)
    if (d.mode === 'dome' && d.essencesDansEnsemble !== false) erreur('DOME_IMPOSSIBLE')
    e.s.mode = d.mode
    e.s.restes = d.restes.map((r) => {
      const c = e.s.ensemble.compartiments.find((x) => x.idCuve === r.idCuve)!
      if (r.volume <= 0 || r.volume > c.capacite) erreur('RESTE_INVALIDE')
      return { ...r, libelle: c.libelle, produit: prod(r.idProdCharger).nom, poids: poidsDe(r.volume, prod(r.idProdCharger).densite) }
    })
    e.s.plan = null; e.s.etape = 'commandes'
    return repondre(e)
  },
  async ajouterCommande(id, numero) {
    const e = etat(id)
    if (e.s.commandes.some((c) => c.numero === numero)) erreur('COMMANDE_DEJA_SAISIE')
    if (numero === '4519001') erreur('COMMANDE_INVALIDE')
    const c = COMMANDES[numero] ?? erreur('COMMANDE_INCONNUE')
    const lignes: VueLigne[] = c.lignes.map((l) => ({
      idComProd: l.id, produit: prod(l.prod).nom, couleur: COULEURS[prod(l.prod).couleur], commandee: l.commandee, distribuable: l.distribuable,
      quantite: c.globale ? 0 : l.distribuable, densite: prod(l.prod).densite, modifiable: c.modifiable, quotaRestant: c.globale ? l.distribuable : null,
    }))
    e.s.commandes.push({ numero, client: c.client, destination: c.destination, validite: jour(c.globale ? 20 : 0), globale: c.globale, lignes } as VueCommande)
    e.s.plan = null; e.s.etape = 'commandes'
    return repondre(e)
  },
  async retirerCommande(id, numero) { const e = etat(id); e.s.commandes = e.s.commandes.filter((c) => c.numero !== numero); e.s.plan = null; e.s.etape = 'commandes'; return repondre(e) },
  async quantite(id, idComProd, q) {
    const e = etat(id)
    const l = e.s.commandes.flatMap((c) => c.lignes).find((x) => x.idComProd === idComProd) ?? erreur('LIGNE_INCONNUE')
    if (q < 0) erreur('QUANTITE_NEGATIVE')
    if (!l.modifiable && q > l.distribuable) erreur('QUANTITE_SUPERIEURE_DISTRIBUABLE')
    if (!l.modifiable && q > 0 && q < 200) erreur('QUANTITE_TROP_FAIBLE')
    l.quantite = q; e.s.plan = null; e.s.etape = 'commandes'
    return repondre(e)
  },
  async calculerPlan(id) {
    const e = etat(id); recalculer(e)
    if (e.s.poids.depasse) erreur('POIDS_DEPASSE')
    planifier(e.s); e.s.etape = 'plan'
    return repondre(e)
  },
  async valider(id) {
    const e = etat(id)
    if (!e.s.plan) planifier(e.s)
    if (!e.s.plan!.complet) erreur('PLAN_INCOMPLET')
    e.s.chargementEnregistre = true; ilots(e.s); e.s.etape = 'ilot'
    return repondre(e)
  },
  async ilot(id, numero) { const e = etat(id); e.s.ilotChoisi = numero; e.s.modeAutomatique = numero === null; e.s.etape = 'fin'; return repondre(e) },
}
