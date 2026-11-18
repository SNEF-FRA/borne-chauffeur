import type { ConfigBorne } from '../api/types'
import { DecodeurTrames, TRAME_INTERROGATION, type EvenementBadge } from './trame'

export type EtatLecteur = 'pret' | 'absent' | 'erreur'

export interface LecteurBadge {
  demarrer(): Promise<void>
  arreter(): void
  /** Demande au navigateur l'accès au port série (bouton de maintenance, une seule fois par poste). */
  connecter?(): Promise<void>
}

type Rappels = { evenement: (e: EvenementBadge) => void; etat: (e: EtatLecteur) => void }

/**
 * Lecteur branché en série sur la borne, lu directement par le navigateur (Web Serial, Edge et Chrome).
 * En mode kiosque, la stratégie Edge « SerialAllowAllPortsForUrls » donne l'accès au port sans question.
 */
class LecteurSerie implements LecteurBadge {
  private port: SerialPort | null = null
  private actif = false
  private badgePresent = false
  private minuterie: number | undefined

  constructor(private config: ConfigBorne, private rappels: Rappels) {}

  async demarrer() {
    if (!('serial' in navigator)) { this.rappels.etat('erreur'); return }
    this.actif = true
    const ports = await navigator.serial.getPorts()
    if (ports.length === 0) { this.rappels.etat('absent'); return }
    await this.ouvrir(ports[0])
  }

  async connecter() {
    const port = await navigator.serial.requestPort()
    this.actif = true
    await this.ouvrir(port)
  }

  private async ouvrir(port: SerialPort) {
    try {
      await port.open({ baudRate: this.config.debitSerie, dataBits: 8 })
    } catch {
      // Déjà ouvert par un autre onglet ou débranché.
      this.rappels.etat('erreur')
      return
    }
    this.port = port
    this.rappels.etat('pret')
    this.minuterie = window.setInterval(() => void this.interroger(), this.config.intervalleInterrogationMs)
    void this.lire(port)
  }

  private async interroger() {
    if (!this.badgePresent || !this.port?.writable) return
    const ecrivain = this.port.writable.getWriter()
    try { await ecrivain.write(TRAME_INTERROGATION) } catch { /* lecteur occupé */ } finally { ecrivain.releaseLock() }
  }

  private async lire(port: SerialPort) {
    const decodeur = new DecodeurTrames()
    while (this.actif && port.readable) {
      const lecteur = port.readable.getReader()
      try {
        for (;;) {
          const { value, done } = await lecteur.read()
          if (done) break
          if (!value) continue
          for (const e of decodeur.pousser(value)) {
            this.badgePresent = e.type === 'insertion'
            this.rappels.evenement(e)
          }
        }
      } catch {
        this.rappels.etat('erreur')
        await new Promise((r) => setTimeout(r, 2000))
      } finally {
        lecteur.releaseLock()
      }
    }
  }

  arreter() {
    this.actif = false
    window.clearInterval(this.minuterie)
    void this.port?.close().catch(() => undefined)
  }
}

/** Lecteur USB qui « tape » l'identifiant du badge suivi de Entrée. Le retrait n'est pas détecté. */
class LecteurClavier implements LecteurBadge {
  private saisie = ''
  private dernier = 0
  private ecouteur = (e: KeyboardEvent) => {
    const maintenant = Date.now()
    if (maintenant - this.dernier > 100) this.saisie = '' // frappe humaine : on repart de zéro
    this.dernier = maintenant
    if (e.key === 'Enter') {
      if (this.saisie.length >= 8) this.rappels.evenement({ type: 'insertion', code: this.saisie.toUpperCase() })
      this.saisie = ''
    } else if (/^[0-9a-fA-F]$/.test(e.key)) {
      this.saisie += e.key
    }
  }

  constructor(private rappels: Rappels) {}
  async demarrer() { window.addEventListener('keydown', this.ecouteur); this.rappels.etat('pret') }
  arreter() { window.removeEventListener('keydown', this.ecouteur) }
}

/** Démonstration et formation : les badges sont simulés par des boutons de l'interface. */
class LecteurSimule implements LecteurBadge {
  constructor(private rappels: Rappels) {}
  async demarrer() { this.rappels.etat('pret') }
  arreter() {}
}

export function creerLecteur(config: ConfigBorne, rappels: Rappels): LecteurBadge {
  switch (config.lecteur) {
    case 'webserial': return new LecteurSerie(config, rappels)
    case 'clavier': return new LecteurClavier(rappels)
    default: return new LecteurSimule(rappels)
  }
}
