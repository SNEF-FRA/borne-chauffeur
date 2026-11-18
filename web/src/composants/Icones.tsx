const base = { fill: 'none', stroke: 'currentColor', strokeWidth: 2, strokeLinecap: 'round' as const, strokeLinejoin: 'round' as const, viewBox: '0 0 24 24', 'aria-hidden': true }

export const Chevron = () => <svg {...base}><path d="m9 6 6 6-6 6" /></svg>
export const ChevronGauche = () => <svg {...base}><path d="m15 6-6 6 6 6" /></svg>
export const Croix = () => <svg {...base}><path d="M6 6l12 12M18 6 6 18" /></svg>
export const Info = () => <svg {...base}><circle cx="12" cy="12" r="9" /><path d="M12 11v5M12 8h.01" /></svg>
export const Alerte = () => <svg {...base}><path d="M12 4 2.5 20h19L12 4Z" /><path d="M12 10v4M12 17h.01" /></svg>
export const Coche = () => <svg {...base}><path d="m5 12.5 4.5 4.5L19 7.5" /></svg>
export const Globe = () => <svg {...base}><circle cx="12" cy="12" r="9" /><path d="M3 12h18M12 3c2.8 3 2.8 15 0 18M12 3c-2.8 3-2.8 15 0 18" /></svg>
export const Horloge = () => <svg {...base}><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></svg>
export const CamionIcone = () => <svg {...base}><path d="M2 7h11v8H2zM13 10h4l3 3v2h-7" /><circle cx="6" cy="17" r="1.8" /><circle cx="17" cy="17" r="1.8" /></svg>
export const Etincelle = () => <svg {...base}><path d="M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5 18 18M6 18l2.5-2.5M15.5 8.5 18 6" /></svg>
export const Effacer = () => <svg {...base}><path d="M9 6h11v12H9l-6-6 6-6Z" /><path d="m12 10 4 4M16 10l-4 4" /></svg>
export const Moins = () => <svg {...base}><path d="M5 12h14" /></svg>
export const Plus = () => <svg {...base}><path d="M12 5v14M5 12h14" /></svg>
