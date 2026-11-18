// Protocole du lecteur de badges (repris de Module01.PortSerie_DataReceived, v1.6) :
//   badge inséré : 02 01 00 08 00 puis l'identifiant DESFire sur 7 octets (octets 5 à 11)
//   badge retiré : 02 01 00 01 11 00 03
//   interrogation envoyée chaque seconde tant qu'un badge est présent : 02 01 00 98 01 00 00 03

export const TRAME_INTERROGATION = new Uint8Array([2, 1, 0, 152, 1, 0, 0, 3])

export type EvenementBadge = { type: 'insertion'; code: string } | { type: 'retrait' }

const RETRAIT = [2, 1, 0, 1, 17, 0, 3]
const INSERTION = [2, 1, 0, 8, 0]
const LONGUEUR_UID = 7

function commencePar(tampon: number[], debut: number, motif: number[]): boolean | null {
  for (let i = 0; i < motif.length; i++) {
    if (debut + i >= tampon.length) return null // trame incomplète
    if (tampon[debut + i] !== motif[i]) return false
  }
  return true
}

/** Décodeur à état : on lui passe les octets reçus au fil de l'eau, il renvoie les événements complets. */
export class DecodeurTrames {
  private tampon: number[] = []

  pousser(octets: ArrayLike<number>): EvenementBadge[] {
    for (let i = 0; i < octets.length; i++) this.tampon.push(octets[i])
    const evenements: EvenementBadge[] = []

    while (this.tampon.length > 0) {
      const debut = this.tampon.indexOf(2)
      if (debut < 0) { this.tampon = []; break }
      if (debut > 0) this.tampon = this.tampon.slice(debut)

      const retrait = commencePar(this.tampon, 0, RETRAIT)
      if (retrait) {
        evenements.push({ type: 'retrait' })
        this.tampon = this.tampon.slice(RETRAIT.length)
        continue
      }
      const insertion = commencePar(this.tampon, 0, INSERTION)
      if (insertion) {
        if (this.tampon.length < INSERTION.length + LONGUEUR_UID) break // attendre la suite
        const uid = this.tampon.slice(INSERTION.length, INSERTION.length + LONGUEUR_UID)
        evenements.push({ type: 'insertion', code: uid.map((o) => o.toString(16).padStart(2, '0')).join('').toUpperCase() })
        this.tampon = this.tampon.slice(INSERTION.length + LONGUEUR_UID)
        // On ignore la fin de trame éventuelle jusqu'au prochain début (02).
        const suivant = this.tampon.indexOf(2)
        this.tampon = suivant < 0 ? [] : this.tampon.slice(suivant)
        continue
      }
      if (retrait === null || insertion === null) break // début plausible d'une trame : attendre
      this.tampon = this.tampon.slice(1) // octet 02 isolé : réponse d'interrogation ou bruit
    }

    if (this.tampon.length > 256) this.tampon = []
    return evenements
  }
}
