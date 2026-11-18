import type { ReactNode } from 'react'
import { nb } from '../i18n'

/** Un compartiment dessiné dans la citerne. */
export interface CuveAffichee {
  cle: number
  libelle: string
  capacite: number
  remplissage?: number
  couleur?: string
  encre?: string
  titre?: string
  lignes?: string[]
  hachure?: boolean
  anneau?: boolean
  attelage?: boolean
  onClick?: () => void
  etiquette?: string
}

// Repère de base du dessin (identique aux vues validées).
const BW = 1150, TX = 252, TY = 64, TW = 858, TH = 142, IX = 260, IW = 842

function Roue({ cx }: { cx: number }) {
  return (
    <g>
      <circle cx={cx} cy={246} r={30} fill="#1E293B" />
      <circle cx={cx} cy={246} r={21} fill="#334155" />
      <circle cx={cx} cy={246} r={16} fill="#E2E8F0" />
      <circle cx={cx} cy={246} r={5} fill="#475569" />
    </g>
  )
}

/** Tracteur et citerne de profil ; les compartiments sont proportionnels à leur capacité. */
export function Camion({ cuves }: { cuves: CuveAffichee[] }) {
  const total = cuves.reduce((s, c) => s + c.capacite, 0) || 1
  let cumul = IX
  const centres = cuves.map((c) => {
    const l = (IW * c.capacite) / total
    const centre = cumul + l / 2
    cumul += l
    return Math.round(centre)
  })
  const poteaux: number[] = []
  for (let x = TX + 70; x < TX + TW - 40; x += 110) poteaux.push(x)

  return (
    <div className="camion">
      <svg viewBox={`0 0 ${BW} 284`} fill="none" aria-hidden="true">
        <ellipse cx={BW / 2} cy={276} rx={BW / 2 - 10} ry={7} fill="#0F172A" fillOpacity={0.08} />
        <rect x={TX + 40} y={TY - 36} width={TW - 80} height={5} rx={2.5} fill="#94A3B8" />
        {poteaux.map((x) => <line key={x} x1={x} y1={TY - 34} x2={x} y2={TY - 4} stroke="#94A3B8" strokeWidth={3} />)}
        {centres.map((c, i) => (
          <g key={i}>
            <rect x={c - 22} y={TY - 12} width={44} height={14} rx={5} fill="#E2E8F0" stroke="#94A3B8" strokeWidth={2} />
            <rect x={c - 12} y={TY - 18} width={24} height={8} rx={3} fill="#CBD5E1" />
          </g>
        ))}
        <rect x={TX} y={TY} width={TW} height={TH} rx={TH / 2} fill="#F8FAFC" stroke="#94A3B8" strokeWidth={3} />
        <rect x={TX + 30} y={TY + TH - 2} width={TW - 60} height={12} fill="#334155" />
        <rect x={TX + 6} y={TY + TH + 8} width={TW - 10} height={10} rx={3} fill="#334155" />
        <rect x={520} y={TY + TH + 16} width={230} height={40} rx={6} fill="#E2E8F0" stroke="#94A3B8" strokeWidth={2} />
        {[578, 636, 694].map((x) => <line key={x} x1={x} y1={TY + TH + 22} x2={x} y2={TY + TH + 50} stroke="#94A3B8" strokeWidth={2} />)}
        <path d={`M858 ${TY + TH + 30} q0 -28 34 -28 h168 q34 0 34 28`} stroke="#334155" strokeWidth={7} strokeLinecap="round" />
        <rect x={TX + TW - 14} y={TY + TH + 12} width={22} height={34} rx={3} fill="#334155" />
        <rect x={TX + TW - 6} y={TY + TH + 4} width={12} height={9} rx={2} fill="#EF4444" />
        <Roue cx={895} /><Roue cx={963} /><Roue cx={1031} />
        <rect x={10} y={226} width={290} height={12} rx={3} fill="#334155" />
        <rect x={232} y={214} width={62} height={12} rx={3} fill="#475569" />
        <rect x={208} y={20} width={8} height={150} rx={4} fill="#94A3B8" />
        <path d="M14 232 V112 Q14 98 21 88 L50 44 Q56 34 70 34 H186 Q202 34 202 50 V232 Z" fill="#14385C" />
        <path d="M72 34 Q122 8 192 18 Q202 20 202 30 V34 Z" fill="#1F4F7E" />
        <path d="M25 106 L52 54 Q55 50 60 50 H80 V106 Z" fill="#CFE6E8" />
        <rect x={90} y={52} width={58} height={54} rx={7} fill="#CFE6E8" />
        <rect x={14} y={150} width={188} height={12} fill="#157F86" />
        <line x1={86} y1={114} x2={86} y2={214} stroke="#0E2A47" strokeWidth={2} />
        <line x1={156} y1={50} x2={156} y2={214} stroke="#0E2A47" strokeWidth={2} />
        <rect x={164} y={118} width={16} height={5} rx={2.5} fill="#CBD5E1" />
        <rect x={18} y={172} width={16} height={11} rx={3} fill="#FDE68A" />
        <rect x={6} y={196} width={30} height={40} rx={5} fill="#0F172A" />
        <path d="M24 76 H10" stroke="#0E2A47" strokeWidth={4} strokeLinecap="round" />
        <rect x={2} y={62} width={10} height={32} rx={4} fill="#0E2A47" />
        <rect x={100} y={206} width={76} height={24} rx={9} fill="#CBD5E1" stroke="#94A3B8" strokeWidth={2} />
        <path d="M40 238 q0 -34 38 -34 q38 0 38 34" stroke="#0F172A" strokeWidth={7} strokeLinecap="round" />
        <path d="M200 238 q0 -34 38 -34 q38 0 38 34" stroke="#0F172A" strokeWidth={7} strokeLinecap="round" />
        <Roue cx={78} /><Roue cx={238} />
      </svg>
      <div className="cuves">
        {cuves.map((c) => <Cuve key={c.cle} c={c} />)}
      </div>
    </div>
  )
}

function Cuve({ c }: { c: CuveAffichee }) {
  const pct = c.remplissage ? Math.min(100, Math.round((100 * c.remplissage) / c.capacite)) : 0
  const encre = c.encre ?? '#1E293B'
  const contenu: ReactNode = (
    <>
      <span className="num" style={{ color: encre }}>{c.libelle}</span>
      <span className="capa" style={{ color: encre }}>{nb(c.capacite)} L</span>
      {c.titre && <span className="prod" style={{ color: encre }}>{c.titre}</span>}
      {c.lignes?.map((l, i) => <span key={i} className="info" style={{ color: encre }}>{l}</span>)}
    </>
  )
  return (
    <div className={`cuve${c.attelage ? ' attelage' : ''}`} style={{ flex: c.capacite }}>
      {c.hachure ? <div className="hachure" /> : pct > 0 && <div className="niveau" style={{ height: `${pct}%`, background: c.couleur }} />}
      {c.anneau && <div className="anneau" />}
      {c.onClick
        ? <button type="button" onClick={c.onClick} aria-label={c.etiquette ?? c.libelle}>{contenu}</button>
        : <div className="texte">{contenu}</div>}
    </div>
  )
}
