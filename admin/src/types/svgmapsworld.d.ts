/**
 * Types of `@svg-maps/world`.
 *
 * The package's own declaration imports `svg-maps__common`, a types package it does not depend on and the admin does
 * not install, so every member would be `any`. This declaration states the part of the map the admin reads.
 */
declare module '@svg-maps/world' {
  /** One country of the map. */
  interface SvgMapLocation {
    /** Lower-case ISO 3166-1 alpha-2 code of the country. */
    id: string
    /** Display name of the country in English. */
    name: string
    /** The SVG path of the country's outline. */
    path: string
  }

  /** The world map: its view box and the outline of every country. */
  interface SvgMap {
    /** The label of the map. */
    label: string
    /** The SVG `viewBox` the paths are drawn in. */
    viewBox: string
    /** Every country of the map. */
    locations: SvgMapLocation[]
  }

  const map: SvgMap
  export default map
}
