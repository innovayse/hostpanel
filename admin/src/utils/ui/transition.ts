/**
 * The kit's one set of open and close animations, as full class strings per kind of surface.
 *
 * Every surface that animates (a dropdown list, a dialog, a drawer, a collapsible block) takes its
 * classes from here through `UiTransition`, so they all move the same way and a change is made once. The
 * strings are written out in full on purpose: Tailwind finds the classes it must generate by reading the
 * source text, so a string built from pieces would be missed. Durations are the named tokens of the theme
 * (`--duration-fast`, `--duration-base`, `--duration-slow` in `main.css`), never a bare number.
 *
 * Under reduced motion nothing moves: each animated class has a `motion-reduce:` partner that removes the
 * transform and the transition, so the surface appears and goes away at once and nothing waits for an
 * animation to end.
 */

/** Closed set of surfaces that animate. */
export const UiTransitionKind = {
  /** The `dropdown` option: a floating list that fades in with a slight scale and lift. 100 ms. */
  Dropdown: 'dropdown',
  /** The `dialog` option: a modal's backdrop, fading while its panel scales in. 200 ms. */
  Dialog: 'dialog',
  /** The `drawer` option: a side panel's backdrop, fading while the panel slides in. 300 ms. */
  Drawer: 'drawer',
  /** The `collapse` option: a block that opens and closes by its height. 200 ms. */
  Collapse: 'collapse',
} as const

/** One of the UiTransitionKind values. */
export type UiTransitionKind = (typeof UiTransitionKind)[keyof typeof UiTransitionKind]

/** The six classes of a Vue `<Transition>`, in the property names it takes. */
export interface UiTransitionClasses {
  /** Applied for the whole enter. */
  enterActiveClass: string
  /** Applied at the start of the enter. */
  enterFromClass: string
  /** Applied at the end of the enter. */
  enterToClass: string
  /** Applied for the whole leave. */
  leaveActiveClass: string
  /** Applied at the start of the leave. */
  leaveFromClass: string
  /** Applied at the end of the leave. */
  leaveToClass: string
}

/** The classes of every kind, one full literal per class. */
export const transitionSet: Record<UiTransitionKind, UiTransitionClasses> = {
  [UiTransitionKind.Dropdown]: {
    enterActiveClass: 'transition duration-(--duration-fast) ease-out motion-reduce:transition-none',
    enterFromClass: 'opacity-0 scale-95 -translate-y-1 motion-reduce:opacity-100 motion-reduce:scale-100 motion-reduce:translate-y-0',
    enterToClass: 'opacity-100 scale-100 translate-y-0',
    leaveActiveClass: 'transition duration-(--duration-fast) ease-in motion-reduce:transition-none',
    leaveFromClass: 'opacity-100 scale-100 translate-y-0',
    leaveToClass: 'opacity-0 scale-95 -translate-y-1 motion-reduce:opacity-100 motion-reduce:scale-100 motion-reduce:translate-y-0',
  },
  [UiTransitionKind.Dialog]: {
    enterActiveClass: 'transition-opacity duration-(--duration-base) ease-out motion-reduce:transition-none',
    enterFromClass: 'ui-from opacity-0 motion-reduce:opacity-100',
    enterToClass: 'opacity-100',
    leaveActiveClass: 'transition-opacity duration-(--duration-base) ease-in motion-reduce:transition-none',
    leaveFromClass: 'opacity-100',
    leaveToClass: 'ui-from opacity-0 motion-reduce:opacity-100',
  },
  [UiTransitionKind.Drawer]: {
    enterActiveClass: 'transition-opacity duration-(--duration-slow) ease-out motion-reduce:transition-none',
    enterFromClass: 'ui-from opacity-0 motion-reduce:opacity-100',
    enterToClass: 'opacity-100',
    leaveActiveClass: 'transition-opacity duration-(--duration-slow) ease-in motion-reduce:transition-none',
    leaveFromClass: 'opacity-100',
    leaveToClass: 'ui-from opacity-0 motion-reduce:opacity-100',
  },
  [UiTransitionKind.Collapse]: {
    enterActiveClass: 'transition-[grid-template-rows,opacity] duration-(--duration-base) ease-out motion-reduce:transition-none',
    enterFromClass: 'grid-rows-[0fr] opacity-0 motion-reduce:grid-rows-[1fr] motion-reduce:opacity-100',
    enterToClass: 'grid-rows-[1fr] opacity-100',
    leaveActiveClass: 'transition-[grid-template-rows,opacity] duration-(--duration-base) ease-in motion-reduce:transition-none',
    leaveFromClass: 'grid-rows-[1fr] opacity-100',
    leaveToClass: 'grid-rows-[0fr] opacity-0 motion-reduce:grid-rows-[1fr] motion-reduce:opacity-100',
  },
}

/**
 * Classes of a modal's panel, which move while the backdrop around it (the `group/overlay` element, carrying
 * the `ui-from` marker class that the Dialog and Drawer sets above put on it) fades. They read the marker, so
 * the panel needs no transition of its own and no second `<Transition>` that could end at a different moment.
 */
export const modalPanelClasses = {
  /** A dialog's panel: scales in a little. */
  dialog: 'transition-transform duration-(--duration-base) ease-out group-[.ui-from]/overlay:scale-95 motion-reduce:transition-none motion-reduce:group-[.ui-from]/overlay:scale-100',
  /** A drawer's panel on the left edge: slides in from the left. */
  drawerLeft: 'transition-transform duration-(--duration-slow) ease-out group-[.ui-from]/overlay:-translate-x-full motion-reduce:transition-none motion-reduce:group-[.ui-from]/overlay:translate-x-0',
  /** A drawer's panel on the right edge: slides in from the right. */
  drawerRight: 'transition-transform duration-(--duration-slow) ease-out group-[.ui-from]/overlay:translate-x-full motion-reduce:transition-none motion-reduce:group-[.ui-from]/overlay:translate-x-0',
} as const

/**
 * Splits a class string into its classes.
 *
 * @param classes - A class string.
 * @returns The classes.
 */
const classList = (classes: string): string[] => classes.split(/\s+/).filter(token => token !== '')

/** Upper bound for a ghost's life, in case the browser never reports the end of its transition. */
const GHOST_MAX_MS = 1000

/**
 * Plays the leave of a surface whose component the parent has already removed (`<UiDialog v-if="x">`).
 *
 * Vue runs a leave only while the `<Transition>` outlives the element, and a `v-if` on the component takes the
 * transition away with it, so the surface would vanish at once. This copies the element into `<body>`, makes the
 * copy inert and unnamed (no test id, id or role, hidden from assistive tech, no pointer events, so nothing finds it
 * or reaches it) and runs the kind's leave classes on it. The copy removes itself when the transition ends.
 * Under reduced motion nothing is copied.
 *
 * @param element - The surface element that is about to be removed (the backdrop of a dialog or drawer).
 * @param kind - Which kind of surface it is, so its leave classes are used.
 * @returns Nothing; the copy plays and removes itself.
 */
export const playGhostLeave = (element: HTMLElement, kind: UiTransitionKind): void => {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    return
  }
  const set = transitionSet[kind]
  const ghost = element.cloneNode(true) as HTMLElement
  for (const node of [ghost, ...ghost.querySelectorAll<HTMLElement>('[id], [data-testid], [role], [aria-modal]')]) {
    for (const attribute of ['id', 'data-testid', 'role', 'aria-modal', 'aria-labelledby']) {
      node.removeAttribute(attribute)
    }
  }
  ghost.setAttribute('aria-hidden', 'true')
  ghost.setAttribute('inert', '')
  ghost.classList.remove(...classList(set.enterActiveClass), ...classList(set.enterFromClass), ...classList(set.enterToClass))
  ghost.classList.add('pointer-events-none', ...classList(set.leaveActiveClass), ...classList(set.leaveFromClass))
  document.body.appendChild(ghost)
  // Read a layout value so the browser paints the start state before the end state is applied.
  void ghost.offsetWidth
  ghost.classList.remove(...classList(set.leaveFromClass))
  ghost.classList.add(...classList(set.leaveToClass))
  /**
   * Takes the copy out of the page.
   *
   * @returns Nothing; the copy is removed.
   */
  const remove = (): void => ghost.remove()
  ghost.addEventListener('transitionend', (event) => {
    if (event.target === ghost) {
      remove()
    }
  })
  window.setTimeout(remove, GHOST_MAX_MS)
}
