/**
 * Touch gestures of a bottom sheet (`UiDrawer` in sheet mode), on pointer events and without a dependency.
 *
 * Dragging the grip or the header down moves the sheet with the finger; past a distance or a fast flick it closes,
 * a short drag springs back. Dragging up from a partial height expands it to the full height. A touch or a pen can
 * also drag from the body, but only while the body is scrolled to its top (and only where the browser leaves the
 * gesture to the script, as `touch-action` allows); a mouse drags only from the grip or the header, and nothing
 * starts from a text field, a select or a listbox. The decisions are pure functions ({@link resolveRelease}, {@link flickVelocity}) so the thresholds are
 * tested without a browser; the composable only feeds them the pointer.
 */
import { ref, watch } from 'vue'
import type { Ref } from 'vue'

/** Fraction of the sheet's height it must be dragged down by to close on release. */
export const SHEET_CLOSE_RATIO = 0.3

/** Distance in px a sheet at a partial height must be dragged up by to expand on release. */
export const SHEET_EXPAND_DISTANCE = 48

/** Speed in px per ms (down is positive) from which a release is a flick that closes, or expands when upward. */
export const SHEET_FLICK_VELOCITY = 0.5

/** Distance in px a pointer must move before the gesture is a drag, so a tap on a control stays a tap. */
export const SHEET_DRAG_SLOP = 6

/** Window in ms of the last pointer samples a release speed is measured over. */
export const SHEET_VELOCITY_WINDOW = 100

/** Selector of the elements a mouse may start a drag from: the grip and the header carry this attribute. */
export const SHEET_HANDLE_SELECTOR = '[data-sheet-handle]'

/** Selector of the controls a drag never starts from, so typing, selecting and choosing keep working. */
export const SHEET_IGNORED_SELECTOR = 'input, textarea, select, [role="listbox"]'

/** Closed set of pointer kinds a drag can start from. */
export const SheetPointerType = {
  /** A finger. */
  Touch: 'touch',
  /** A stylus. */
  Pen: 'pen',
  /** A mouse, which drags only from the grip or the header. */
  Mouse: 'mouse',
} as const

/** One of the SheetPointerType values. */
export type SheetPointerType = (typeof SheetPointerType)[keyof typeof SheetPointerType]

/** Closed set of outcomes of releasing a drag. */
export const SheetRelease = {
  /** The sheet leaves. */
  Close: 'close',
  /** The sheet opens to its full height. */
  Expand: 'expand',
  /** The sheet springs back to where it was. */
  Restore: 'restore',
} as const

/** One of the SheetRelease values. */
export type SheetRelease = (typeof SheetRelease)[keyof typeof SheetRelease]

/** One pointer position at a moment. */
export interface SheetSample {
  /** Vertical position in px. */
  y: number
  /** Time in ms. */
  t: number
}

/** The part of a pointer event the gesture reads, so a test needs no real event. */
export interface SheetPointerEvent {
  /** Which pointer (finger) the event belongs to. */
  pointerId: number
  /** Vertical position in px. */
  clientY: number
  /** Time in ms. */
  timeStamp: number
  /** The element the pointer is over. */
  target: EventTarget | null
  /** The mouse button, 0 for a finger or the primary button. */
  button: number
  /** The kind of pointer, one of {@link SheetPointerType} (any other kind never starts a drag). */
  pointerType: string
}

/**
 * Speed of the pointer over the last {@link SHEET_VELOCITY_WINDOW} ms, so a pause before lifting the finger is a
 * slow release and a quick swipe at the end is a flick.
 *
 * @param samples - Pointer samples, oldest first.
 * @returns Speed in px per ms, positive when moving down; 0 when there is not enough to measure.
 */
export const flickVelocity = (samples: readonly SheetSample[]): number => {
  const last = samples[samples.length - 1]
  if (last === undefined) {
    return 0
  }
  const first = samples.find((sample) => last.t - sample.t <= SHEET_VELOCITY_WINDOW)
  if (first === undefined || last.t === first.t) {
    return 0
  }
  return (last.y - first.y) / (last.t - first.t)
}

/**
 * Records a pointer sample and drops those older than {@link SHEET_VELOCITY_WINDOW}, so a long drag keeps a short list.
 *
 * @param samples - The samples of the gesture, oldest first; changed in place.
 * @param sample - The new sample.
 * @returns Nothing; the list is updated.
 */
const recordSample = (samples: SheetSample[], sample: SheetSample): void => {
  samples.push(sample)
  while (samples.length > 1 && sample.t - (samples[0] as SheetSample).t > SHEET_VELOCITY_WINDOW) {
    samples.shift()
  }
}

/**
 * Decides what a released drag does.
 *
 * @param offset - Distance dragged in px, positive downward.
 * @param velocity - Release speed in px per ms, positive downward.
 * @param height - Height of the sheet in px.
 * @param expanded - Whether the sheet is already at its full height.
 * @returns Close past the distance or on a downward flick, expand past the distance or on an upward flick (only from
 *   a partial height), otherwise restore.
 */
export const resolveRelease = (offset: number, velocity: number, height: number, expanded: boolean): SheetRelease => {
  if (offset > 0 && (offset >= height * SHEET_CLOSE_RATIO || velocity >= SHEET_FLICK_VELOCITY)) {
    return SheetRelease.Close
  }
  if (offset < 0 && !expanded && (-offset >= SHEET_EXPAND_DISTANCE || velocity <= -SHEET_FLICK_VELOCITY)) {
    return SheetRelease.Expand
  }
  return SheetRelease.Restore
}

/** Inputs of {@link useSheetDrag}. */
export interface SheetDragOptions {
  /** The sheet element; it is moved through its `--sheet-drag` custom property. */
  panel: Ref<HTMLElement | null>
  /** The scrolling body inside the sheet, whose scroll position decides whether a drag from it moves the sheet. */
  body: Ref<HTMLElement | null>
  /** Whether the drawer is a bottom sheet right now; a side panel never drags. */
  enabled: Readonly<Ref<boolean>>
  /** Asks the drawer to close. */
  close: () => void
}

/** A drag that has started and not yet ended. */
interface Gesture {
  /** The pointer (finger) that owns it. */
  pointerId: number
  /** Where it started in px. */
  startY: number
  /** Whether it started inside the scrolling body. */
  fromBody: boolean
  /** Whether it has moved past the slop and now moves the sheet. */
  active: boolean
  /** Recent positions for the release speed. */
  samples: SheetSample[]
}

/**
 * Wires the pointer gestures of a bottom sheet.
 *
 * Bind `onPointerdown`, `onPointermove`, `onPointerup` and `onPointercancel` on the sheet. The grip and header
 * should be `touch-none` so the browser leaves their gesture to the script; the body keeps its own scroll.
 *
 * @param options - The sheet and body elements, whether the sheet is active, and how to close it.
 * @returns Whether a drag is in progress, whether the sheet is expanded, the pointer handlers and a reset.
 */
export const useSheetDrag = (options: SheetDragOptions) => {
  /** Whether a drag is moving the sheet, which turns its transition off so it follows the finger. */
  const dragging = ref(false)

  /** Whether the sheet is at its full height. */
  const expanded = ref(false)

  /** The gesture in progress, if any. */
  let gesture: Gesture | null = null

  /**
   * Moves the sheet to a distance below its resting place.
   *
   * @param px - Distance in px; 0 puts it back.
   * @returns Nothing; the custom property is set.
   */
  const moveTo = (px: number): void => {
    options.panel.value?.style.setProperty('--sheet-drag', `${px}px`)
  }

  /**
   * Puts the sheet back to its resting place and its partial height, for the next time it opens.
   *
   * @returns Nothing; the drag state is cleared.
   */
  const reset = (): void => {
    gesture = null
    dragging.value = false
    expanded.value = false
    moveTo(0)
  }

  // A drawer that stops being a bottom sheet (the screen widened) must not keep a half-made gesture or full height.
  watch(options.enabled, reset)

  /**
   * Whether a press may start a drag: a touch or a pen anywhere on the sheet, a mouse only on the grip or the
   * header, and never on a text field, a select or a listbox.
   *
   * @param event - The pointer press.
   * @returns True when a drag may start from it.
   */
  const startsDrag = (event: SheetPointerEvent): boolean => {
    const target = event.target instanceof Element ? event.target : null
    if (target?.closest(SHEET_IGNORED_SELECTOR) != null) {
      return false
    }
    if (event.pointerType === SheetPointerType.Touch || event.pointerType === SheetPointerType.Pen) {
      return true
    }
    return event.pointerType === SheetPointerType.Mouse && target?.closest(SHEET_HANDLE_SELECTOR) != null
  }

  /**
   * Starts a gesture, unless the drawer is not a sheet, the press cannot start a drag ({@link startsDrag}) or the
   * content under the pointer is scrolled down (its scroll wins until it is back at the top).
   *
   * @param event - The pointer press.
   * @returns Nothing; a gesture is recorded.
   */
  const onPointerdown = (event: SheetPointerEvent): void => {
    if (!options.enabled.value || event.button !== 0 || !startsDrag(event)) {
      return
    }
    const body = options.body.value
    const fromBody = body !== null && event.target instanceof Node && body.contains(event.target)
    if (fromBody && body.scrollTop > 0) {
      return
    }
    gesture = { pointerId: event.pointerId, startY: event.clientY, fromBody, active: false, samples: [{ y: event.clientY, t: event.timeStamp }] }
  }

  /**
   * Follows the pointer: past the slop the sheet moves with it (never above its resting place).
   *
   * @param event - The pointer move.
   * @returns Nothing; the sheet follows.
   */
  const onPointermove = (event: SheetPointerEvent): void => {
    if (gesture === null || gesture.pointerId !== event.pointerId) {
      return
    }
    const offset = event.clientY - gesture.startY
    if (!gesture.active) {
      if (Math.abs(offset) < SHEET_DRAG_SLOP) {
        return
      }
      // A full-height sheet whose body is dragged up is scrolling its content, not moving the sheet.
      if (gesture.fromBody && offset < 0 && expanded.value) {
        gesture = null
        return
      }
      gesture.active = true
      dragging.value = true
      options.panel.value?.setPointerCapture(event.pointerId)
    }
    recordSample(gesture.samples, { y: event.clientY, t: event.timeStamp })
    moveTo(Math.max(0, offset))
  }

  /**
   * Ends the gesture and applies the outcome of the release.
   *
   * @param event - The pointer release.
   * @returns Nothing; the sheet closes, expands or springs back.
   */
  const onPointerup = (event: SheetPointerEvent): void => {
    if (gesture === null || gesture.pointerId !== event.pointerId) {
      return
    }
    const ended = gesture
    gesture = null
    if (!ended.active) {
      return
    }
    dragging.value = false
    // The release is a sample too, so a pause before lifting the finger is a slow release.
    recordSample(ended.samples, { y: event.clientY, t: event.timeStamp })
    const height = options.panel.value?.offsetHeight ?? 0
    const outcome = resolveRelease(event.clientY - ended.startY, flickVelocity(ended.samples), height, expanded.value)
    if (outcome === SheetRelease.Expand) {
      expanded.value = true
    }
    if (outcome === SheetRelease.Close) {
      options.close()
    }
    // After a close too: a vetoed or ignored close must not leave the sheet hanging where the finger let go.
    moveTo(0)
  }

  /**
   * Abandons the gesture, as when the browser takes the touch over to scroll: the sheet springs back.
   *
   * @returns Nothing; the sheet is put back.
   */
  const onPointercancel = (): void => {
    gesture = null
    dragging.value = false
    moveTo(0)
  }

  return { dragging, expanded, onPointerdown, onPointermove, onPointerup, onPointercancel, reset }
}
