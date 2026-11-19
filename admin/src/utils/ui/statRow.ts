/**
 * Types of `UiStatRow`: how a row of stat tiles is drawn and what a screen hands it for each tile.
 */
import type { UiTone } from '@/utils/ui/tone'

/** Closed set of the looks of a stat row. */
export const UiStatRowVariant = {
  /** A card per figure: the caption, a large value and the change. */
  Tile: 'tile',
  /** Plain figures for a card that already frames them (the header card of a layout): a small caption over the value. */
  Inline: 'inline',
} as const

/** One of the stat row looks. */
export type UiStatRowVariant = (typeof UiStatRowVariant)[keyof typeof UiStatRowVariant]

/** What a screen knows about one tile of its stat row, by the tile's key; everything but the value is optional. */
export interface UiStatFigure {
  /** The figure, already formatted. */
  value: string | number
  /** Caption that replaces the schema's (a tile whose caption names the chosen period); already translated. */
  label?: string
  /** Colour of the value. */
  tone?: UiTone
  /** Change since the last period, already formatted with its sign. */
  delta?: string
  /** Colour of the change. */
  deltaTone?: UiTone
  /** Whether this tile's data is still being read, for a row whose tiles arrive one by one. */
  loading?: boolean
}
