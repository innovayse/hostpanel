/**
 * Looks of the Ui icon tile: the small rounded square that carries a navigation icon.
 *
 * Every class is a complete Tailwind name written out literally (Tailwind reads them from this
 * file) and uses classic template tokens only, so the tile follows the operator's brand colour and
 * the light and dark palettes without a colour of its own. The tones that sit on a tinted or filled
 * row exist because the same tile is drawn on a row that is itself the current page, where the
 * resting tint would vanish into the row.
 *
 * @module utils/ui/iconTile
 */

/** Colouring of a UiIconTile. */
export enum UiIconTileTone {
  /** Soft brand tint with a brand icon; turns solid when the parent `group` row is hovered or focused. */
  Soft = 'soft',
  /** Solid brand fill with the on-brand icon: the tile of the current row on a soft background. */
  Solid = 'solid',
  /** Translucent on-brand wash for a row that is already filled with the brand colour. */
  Inverse = 'inverse',
  /** Tinted surface with a brand icon, the lead tile of a nova card or row; no hover change. */
  Nova = 'nova',
}

/** Edge length of a UiIconTile. */
export enum UiIconTileSize {
  /** 2 rem, a compact list row such as the mobile drawer. */
  Sm = 'sm',
  /** 2.25 rem, the compact row of a nova card. */
  Compact = 'compact',
  /** 2.5 rem, a dropdown row. */
  Md = 'md',
  /** 2.75 rem, the lead tile of a card. */
  Lg = 'lg',
}

/** Classes of the square, by tone. */
export const ICON_TILE_TONE_CLASSES: Readonly<Record<`${UiIconTileTone}`, string>> = {
  [UiIconTileTone.Soft]: 'bg-classic-brand-soft text-classic-brand group-hover:bg-classic-fill group-hover:text-classic-on-brand group-focus-visible:bg-classic-fill group-focus-visible:text-classic-on-brand',
  [UiIconTileTone.Solid]: 'bg-classic-fill text-classic-on-brand',
  [UiIconTileTone.Inverse]: 'bg-classic-on-brand/20 text-classic-on-brand',
  [UiIconTileTone.Nova]: 'bg-nova-surface-2 text-nova-brand',
}

/** Classes of the square and of its icon, by size. */
export const ICON_TILE_SIZE_CLASSES: Readonly<Record<`${UiIconTileSize}`, { box: string, icon: string }>> = {
  [UiIconTileSize.Sm]: { box: 'size-8 rounded-lg', icon: 'text-base' },
  [UiIconTileSize.Compact]: { box: 'size-9 rounded-lg', icon: 'text-[18px]' },
  [UiIconTileSize.Md]: { box: 'size-10 rounded-lg', icon: 'text-xl' },
  [UiIconTileSize.Lg]: { box: 'size-11 rounded-xl', icon: 'text-[22px]' },
}
