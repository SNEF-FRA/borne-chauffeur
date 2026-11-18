import { createContext, useContext } from 'react'
import { en } from './en'
import { fr, type Textes } from './fr'

/** Langues traduites. Pour en ajouter une : copier en.ts, traduire, l'enregistrer ici. */
export const dictionnaires: Record<string, Textes> = { fr, en }

export const nomsLangues: Record<string, string> = {
  fr: 'Français', en: 'English', es: 'Español', pt: 'Português', de: 'Deutsch', it: 'Italiano', nl: 'Nederlands', pl: 'Polski', ro: 'Română',
}

export const LangueContexte = createContext<{ langue: string; t: Textes; changer: (l: string) => void }>({
  langue: 'fr', t: fr, changer: () => undefined,
})

export const useTextes = () => useContext(LangueContexte)

/** Nombres au format français : espace fine insécable pour les milliers (13 000). */
export const nb = (v: number) => new Intl.NumberFormat('fr-FR').format(Math.round(v))

export const dateCourte = (iso?: string | null, langue = 'fr') =>
  iso ? new Date(`${iso.slice(0, 10)}T12:00:00`).toLocaleDateString(langue === 'fr' ? 'fr-FR' : 'en-GB') : ''
