import { useTextes } from '../i18n'
import { Effacer } from './Icones'

/** Pavé numérique tactile : chiffres, effacement et validation. */
export function PaveNumerique({ valeur, onChange, onValider, libelleValider, max = 12 }: {
  valeur: string; onChange: (v: string) => void; onValider: () => void; libelleValider: string; max?: number
}) {
  const { t } = useTextes()
  const taper = (c: string) => valeur.length < max && onChange(valeur + c)
  return (
    <div className="pave">
      {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((c) => <button key={c} type="button" onClick={() => taper(c)}>{c}</button>)}
      <button type="button" onClick={() => onChange(valeur.slice(0, -1))} aria-label={t.effacer}><Effacer /></button>
      <button type="button" onClick={() => taper('0')}>0</button>
      <button type="button" className="ok" onClick={onValider}>{libelleValider}</button>
    </div>
  )
}

const RANGEES = ['1234567890', 'AZERTYUIOP', 'QSDFGHJKLM', 'WXCVBN']

/** Clavier AZERTY pour la saisie d'une plaque d'immatriculation. */
export function ClavierPlaque({ valeur, onChange }: { valeur: string; onChange: (v: string) => void }) {
  const { t } = useTextes()
  const taper = (c: string) => valeur.length < 12 && onChange(valeur + c)
  return (
    <div className="clavier-plaque">
      {RANGEES.map((r, i) => (
        <div className="rangee" key={r}>
          {r.split('').map((c) => <button key={c} type="button" onClick={() => taper(c)}>{c}</button>)}
          {i === 3 && <>
            <button type="button" className="large" onClick={() => taper('-')}>-</button>
            <button type="button" className="large" onClick={() => onChange(valeur.slice(0, -1))} aria-label={t.effacer}><Effacer /></button>
          </>}
        </div>
      ))}
    </div>
  )
}
