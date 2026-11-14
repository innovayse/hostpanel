/**
 * Placement rules of the kit's floating panels (select lists, menus, pickers), kept pure so they are tested
 * without a DOM. A panel is drawn in the page body with `position: fixed` (a frosted card around its trigger would
 * otherwise paint it under the next card), so its place is worked out from the trigger's box: which edge it hangs
 * from, whether it opens above when there is little room below, and how wide the trigger is.
 */

/** Which edge of the trigger the panel lines up with. */
export enum FloatingAlign {
  /** The panel's left edge meets the trigger's left edge and it grows to the right. */
  Start = 'start',
  /** The panel's right edge meets the trigger's right edge and it grows to the left. */
  End = 'end',
}

/** The box of a trigger in viewport pixels (a `DOMRect` carries more; only these are read). */
export interface FloatingAnchor {
  /** Distance of the left edge from the viewport's left edge. */
  left: number
  /** Distance of the right edge from the viewport's left edge. */
  right: number
  /** Distance of the top edge from the viewport's top edge. */
  top: number
  /** Distance of the bottom edge from the viewport's top edge. */
  bottom: number
  /** Width of the box. */
  width: number
}

/** The size of the viewport without the page scrollbars, which a fixed box's `right` and `bottom` are measured from. */
export interface FloatingViewport {
  /** Viewport width. */
  width: number
  /** Viewport height. */
  height: number
}

/** How a panel hangs from its trigger. */
export interface FloatingOptions {
  /** The trigger edge the panel lines up with. */
  align: FloatingAlign
  /** Free room below the trigger, in pixels, under which the panel opens above it (when there is more room there). */
  minRoom: number
  /** Gap between the trigger and the panel, in pixels. */
  gap: number
  /**
   * Width of a panel that must stay inside the viewport horizontally (a wide calendar): its start edge is pulled
   * back so it does not reach past the right edge. Left out for a panel that is narrower than the room it has.
   */
  keepInsideWidth?: number
}

/** Where a panel sits, in pixels from the viewport edges; the unused edge of each axis is left out. */
export interface FloatingPlace {
  /** Distance from the viewport's left edge (start aligned panels). */
  left?: number
  /** Distance from the viewport's right edge (end aligned panels). */
  right?: number
  /** Distance from the viewport's top edge (panels opening below). */
  top?: number
  /** Distance from the viewport's bottom edge (panels opening above). */
  bottom?: number
  /** Width of the trigger, for a list that is as wide as its field. */
  width: number
}

/** Smallest distance kept between a clamped panel and the viewport's left edge, in pixels. */
const VIEWPORT_MARGIN_PX = 8

/** Distance kept between a clamped panel and the viewport's right edge, in pixels. */
const VIEWPORT_EDGE_GAP_PX = 16

/**
 * The classes that put a panel where {@link applyFloatingPlace} says: fixed, each edge read from a CSS variable
 * (written from script, never an inline style attribute) and `auto` when the variable is absent. The classes are
 * literals because an interpolated Tailwind class emits no CSS.
 */
export const FLOATING_PLACE_CLASSES = 'fixed left-[var(--fp-left,auto)] right-[var(--fp-right,auto)] top-[var(--fp-top,auto)] bottom-[var(--fp-bottom,auto)]'

/**
 * The surface every floating panel wears: a fully opaque raised ground (`--surface-elevated` is solid in both
 * themes), a hairline border and the popover shadow. Never a frosted glass utility, which would let the fields
 * behind an open list show through. Shape and size stay with each panel.
 */
export const FLOATING_PANEL_CLASSES = 'bg-surface-elevated border border-border shadow-popover'

/**
 * Works out where a panel sits for a trigger box.
 *
 * @param anchor - The trigger's box in viewport pixels.
 * @param viewport - The viewport without scrollbars.
 * @param options - How the panel hangs from the trigger.
 * @returns The edges to draw the panel at.
 */
export const computeFloatingPlace = (anchor: FloatingAnchor, viewport: FloatingViewport, options: FloatingOptions): FloatingPlace => {
  const below = viewport.height - anchor.bottom
  const opensAbove = below < options.minRoom && anchor.top > below
  const place: FloatingPlace = { width: anchor.width }
  if (options.align === FloatingAlign.End) {
    place.right = viewport.width - anchor.right
  } else if (options.keepInsideWidth !== undefined) {
    const furthest = viewport.width - options.keepInsideWidth - VIEWPORT_EDGE_GAP_PX
    place.left = Math.max(Math.min(anchor.left, furthest), VIEWPORT_MARGIN_PX)
  } else {
    place.left = anchor.left
  }
  if (opensAbove) {
    place.bottom = viewport.height - anchor.top + options.gap
  } else {
    place.top = anchor.bottom + options.gap
  }
  return place
}

/**
 * Writes one CSS variable on the panel element, or removes it when the edge is not used, so the class fallback
 * (`auto`) applies.
 *
 * @param element - The panel element.
 * @param name - The CSS variable.
 * @param value - Pixels, or undefined to remove the variable.
 */
const writeVariable = (element: HTMLElement, name: string, value: number | undefined): void => {
  if (value === undefined) {
    element.style.removeProperty(name)
  } else {
    element.style.setProperty(name, `${value}px`)
  }
}

/**
 * Writes a place into CSS variables on the panel element (see {@link FLOATING_PLACE_CLASSES}).
 *
 * @param element - The panel element.
 * @param place - Where the panel sits.
 */
export const applyFloatingPlace = (element: HTMLElement, place: FloatingPlace): void => {
  writeVariable(element, '--fp-left', place.left)
  writeVariable(element, '--fp-right', place.right)
  writeVariable(element, '--fp-top', place.top)
  writeVariable(element, '--fp-bottom', place.bottom)
  writeVariable(element, '--fp-width', place.width)
}
