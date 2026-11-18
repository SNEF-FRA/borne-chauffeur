import { describe, expect, it } from 'vitest'
import { DecodeurTrames } from './trame'

const insertion = [2, 1, 0, 8, 0, 0x04, 0xa1, 0xb2, 0xc3, 0xd4, 0xe5, 0xf6, 0x90, 0x00, 3]
const retrait = [2, 1, 0, 1, 17, 0, 3]

describe('DecodeurTrames', () => {
  it('lit un badge inséré', () => {
    expect(new DecodeurTrames().pousser(insertion)).toEqual([{ type: 'insertion', code: '04A1B2C3D4E5F6' }])
  })

  it('lit un retrait de badge', () => {
    expect(new DecodeurTrames().pousser(retrait)).toEqual([{ type: 'retrait' }])
  })

  it('reconstitue une trame reçue en plusieurs morceaux', () => {
    const d = new DecodeurTrames()
    expect(d.pousser(insertion.slice(0, 6))).toEqual([])
    expect(d.pousser(insertion.slice(6))).toEqual([{ type: 'insertion', code: '04A1B2C3D4E5F6' }])
  })

  it('ignore le bruit et enchaîne insertion puis retrait', () => {
    const d = new DecodeurTrames()
    expect(d.pousser([0xff, 0x13, ...insertion, ...retrait])).toEqual([
      { type: 'insertion', code: '04A1B2C3D4E5F6' },
      { type: 'retrait' },
    ])
  })
})
