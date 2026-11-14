/**
 * Unit tests for the sheet gestures: the release speed, the close / expand / restore thresholds and the
 * pointer handling around them.
 */
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { nextTick, ref } from 'vue'
import {
  flickVelocity,
  resolveRelease,
  SHEET_CLOSE_RATIO,
  SHEET_DRAG_SLOP,
  SHEET_EXPAND_DISTANCE,
  SHEET_FLICK_VELOCITY,
  SheetPointerType,
  SheetRelease,
  useSheetDrag,
} from '@/composables/useSheetDrag'
import type { SheetPointerEvent } from '@/composables/useSheetDrag'

describe('flickVelocity', () => {
  it('is zero without samples or without elapsed time', () => {
    expect(flickVelocity([])).toBe(0)
    expect(flickVelocity([{ y: 10, t: 5 }])).toBe(0)
    expect(flickVelocity([{ y: 10, t: 5 }, { y: 50, t: 5 }])).toBe(0)
  })

  it('is signed: positive moving down, negative moving up', () => {
    expect(flickVelocity([{ y: 0, t: 0 }, { y: 50, t: 50 }])).toBe(1)
    expect(flickVelocity([{ y: 100, t: 0 }, { y: 50, t: 50 }])).toBe(-1)
  })

  it('measures only the last window, so a pause before lifting the finger is slow', () => {
    const samples = [{ y: 0, t: 0 }, { y: 200, t: 50 }, { y: 200, t: 400 }, { y: 202, t: 460 }]

    expect(flickVelocity(samples)).toBeLessThan(SHEET_FLICK_VELOCITY)
  })
})

describe('resolveRelease', () => {
  it('closes past the share of the height, or on a fast downward flick', () => {
    expect(resolveRelease(400 * SHEET_CLOSE_RATIO, 0, 400, false)).toBe(SheetRelease.Close)
    expect(resolveRelease(10, SHEET_FLICK_VELOCITY, 400, false)).toBe(SheetRelease.Close)
  })

  it('springs back from a short, slow drag down', () => {
    expect(resolveRelease(400 * SHEET_CLOSE_RATIO - 1, SHEET_FLICK_VELOCITY - 0.1, 400, false)).toBe(SheetRelease.Restore)
  })

  it('expands from a partial height past the distance or on an upward flick', () => {
    expect(resolveRelease(-SHEET_EXPAND_DISTANCE, 0, 400, false)).toBe(SheetRelease.Expand)
    expect(resolveRelease(-5, -SHEET_FLICK_VELOCITY, 400, false)).toBe(SheetRelease.Expand)
  })

  it('does not expand a sheet that is already full, nor on a short upward drag', () => {
    expect(resolveRelease(-200, -2, 400, true)).toBe(SheetRelease.Restore)
    expect(resolveRelease(-(SHEET_EXPAND_DISTANCE - 1), 0, 400, false)).toBe(SheetRelease.Restore)
  })
})

beforeAll(() => {
  // jsdom has no pointer capture; the sheet takes it for the finger that drags.
  HTMLElement.prototype.setPointerCapture = vi.fn()
})

afterAll(() => {
  Reflect.deleteProperty(HTMLElement.prototype, 'setPointerCapture')
})

/**
 * Builds a pointer event for the gesture.
 *
 * @param clientY - Vertical position.
 * @param timeStamp - Time in ms.
 * @param target - Element under the pointer.
 * @param pointerType - Kind of pointer.
 * @returns The event.
 */
const pointer = (
  clientY: number,
  timeStamp: number,
  target: EventTarget | null = null,
  pointerType: string = SheetPointerType.Touch,
): SheetPointerEvent => ({
  pointerId: 1,
  clientY,
  timeStamp,
  target,
  button: 0,
  pointerType,
})

/**
 * Sets up a 400 px tall sheet with a grip, a body and a text field inside it.
 *
 * @param enabled - Whether the drawer is a bottom sheet.
 * @returns The gesture, the elements, the enabled flag and the close spy.
 */
const setup = (enabled = true) => {
  const panelEl = document.createElement('aside')
  const bodyEl = document.createElement('div')
  const gripEl = document.createElement('div')
  gripEl.setAttribute('data-sheet-handle', '')
  const inputEl = document.createElement('input')
  bodyEl.appendChild(inputEl)
  panelEl.append(gripEl, bodyEl)
  Object.defineProperty(panelEl, 'offsetHeight', { value: 400 })
  const close = vi.fn()
  const active = ref(enabled)
  const drag = useSheetDrag({ panel: ref(panelEl), body: ref(bodyEl), enabled: active, close })
  return { drag, panelEl, bodyEl, gripEl, inputEl, active, close }
}

describe('useSheetDrag', () => {
  it('ignores a press when the drawer is not a sheet', () => {
    const { drag, close } = setup(false)

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove(pointer(300, 50))
    drag.onPointerup(pointer(300, 60))

    expect(close).not.toHaveBeenCalled()
    expect(drag.dragging.value).toBe(false)
  })

  it('does not start a drag inside the slop, so a tap stays a tap', () => {
    const { drag, panelEl } = setup()

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove(pointer(SHEET_DRAG_SLOP - 1, 10))

    expect(drag.dragging.value).toBe(false)
    expect(panelEl.style.getPropertyValue('--sheet-drag')).toBe('')
  })

  it('moves with the finger and never above its resting place', () => {
    const { drag, panelEl } = setup()

    drag.onPointerdown(pointer(100, 0))
    drag.onPointermove(pointer(160, 50))
    expect(panelEl.style.getPropertyValue('--sheet-drag')).toBe('60px')
    drag.onPointermove(pointer(60, 100))
    expect(panelEl.style.getPropertyValue('--sheet-drag')).toBe('0px')
    expect(drag.dragging.value).toBe(true)
  })

  it('closes past the distance', () => {
    const { drag, close } = setup()

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove(pointer(130, 1000))
    drag.onPointerup(pointer(130, 2000))

    expect(close).toHaveBeenCalledTimes(1)
    expect(drag.dragging.value).toBe(false)
  })

  it('closes on a flick that is short in distance', () => {
    const { drag, close } = setup()

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove(pointer(20, 20))
    drag.onPointerup(pointer(40, 40))

    expect(close).toHaveBeenCalledTimes(1)
  })

  it('measures the release from the moment of lifting, so a pause before it is a slow release', () => {
    const { drag, close } = setup()

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove(pointer(20, 20))
    drag.onPointerup(pointer(20, 400))

    expect(close).not.toHaveBeenCalled()
  })

  it('puts the sheet back after a close that did nothing, so it does not hang where the finger let go', () => {
    const { drag, panelEl, close } = setup()

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove(pointer(130, 1000))
    drag.onPointerup(pointer(130, 2000))

    expect(close).toHaveBeenCalledTimes(1)
    expect(panelEl.style.getPropertyValue('--sheet-drag')).toBe('0px')
  })

  it('starts for a touch or a pen anywhere, and for a mouse only from the grip or the header', () => {
    const { drag, bodyEl, gripEl } = setup()

    drag.onPointerdown(pointer(0, 0, bodyEl, SheetPointerType.Touch))
    drag.onPointermove(pointer(50, 10))
    expect(drag.dragging.value).toBe(true)
    drag.onPointercancel()

    drag.onPointerdown(pointer(0, 0, bodyEl, SheetPointerType.Pen))
    drag.onPointermove(pointer(50, 10))
    expect(drag.dragging.value).toBe(true)
    drag.onPointercancel()

    drag.onPointerdown(pointer(0, 0, bodyEl, SheetPointerType.Mouse))
    drag.onPointermove(pointer(50, 10))
    expect(drag.dragging.value).toBe(false)

    drag.onPointerdown(pointer(0, 0, gripEl, SheetPointerType.Mouse))
    drag.onPointermove(pointer(50, 10))
    expect(drag.dragging.value).toBe(true)
  })

  it('never starts from a text field, a select, a textarea or a listbox', () => {
    const { drag, bodyEl, inputEl } = setup()
    const listbox = document.createElement('ul')
    listbox.setAttribute('role', 'listbox')
    const option = document.createElement('li')
    listbox.appendChild(option)
    bodyEl.appendChild(listbox)

    for (const target of [inputEl, document.createElement('textarea'), document.createElement('select'), option]) {
      if (!bodyEl.contains(target)) {
        bodyEl.appendChild(target)
      }
      drag.onPointerdown(pointer(0, 0, target, SheetPointerType.Touch))
      drag.onPointermove(pointer(50, 10))
      expect(drag.dragging.value).toBe(false)
    }
  })

  it('ignores an unknown kind of pointer', () => {
    const { drag, gripEl } = setup()

    drag.onPointerdown(pointer(0, 0, gripEl, 'other'))
    drag.onPointermove(pointer(50, 10))

    expect(drag.dragging.value).toBe(false)
  })

  it('forgets a gesture, a drag and the full height when the drawer stops being a sheet', async () => {
    const { drag, active, panelEl, close } = setup()
    drag.onPointerdown(pointer(300, 0))
    drag.onPointermove(pointer(200, 800))
    drag.onPointerup(pointer(200, 1600))
    expect(drag.expanded.value).toBe(true)
    drag.onPointerdown(pointer(0, 2000))
    drag.onPointermove(pointer(100, 2010))
    expect(drag.dragging.value).toBe(true)

    active.value = false
    await nextTick()

    expect(drag.dragging.value).toBe(false)
    expect(drag.expanded.value).toBe(false)
    expect(panelEl.style.getPropertyValue('--sheet-drag')).toBe('0px')
    drag.onPointerup(pointer(400, 2020))
    expect(close).not.toHaveBeenCalled()
  })

  it('springs back from a short slow drag', () => {
    const { drag, panelEl, close } = setup()

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove(pointer(40, 500))
    drag.onPointerup(pointer(40, 1000))

    expect(close).not.toHaveBeenCalled()
    expect(panelEl.style.getPropertyValue('--sheet-drag')).toBe('0px')
    expect(drag.dragging.value).toBe(false)
  })

  it('expands on an upward drag from a partial height and stays expanded until reset', () => {
    const { drag } = setup()

    drag.onPointerdown(pointer(300, 0))
    drag.onPointermove(pointer(200, 800))
    drag.onPointerup(pointer(200, 1600))
    expect(drag.expanded.value).toBe(true)

    drag.reset()
    expect(drag.expanded.value).toBe(false)
  })

  it('leaves content that is scrolled down to scroll, and takes a drag again once it is at the top', () => {
    const { drag, bodyEl, close } = setup()

    bodyEl.scrollTop = 30
    drag.onPointerdown(pointer(0, 0, bodyEl))
    drag.onPointermove(pointer(300, 10))
    drag.onPointerup(pointer(300, 20))
    expect(close).not.toHaveBeenCalled()

    bodyEl.scrollTop = 0
    drag.onPointerdown(pointer(0, 100, bodyEl))
    drag.onPointermove(pointer(300, 110))
    drag.onPointerup(pointer(300, 120))
    expect(close).toHaveBeenCalledTimes(1)
  })

  it('lets a full-height body scroll up instead of moving the sheet', () => {
    const { drag, bodyEl } = setup()
    drag.onPointerdown(pointer(300, 0))
    drag.onPointermove(pointer(200, 800))
    drag.onPointerup(pointer(200, 1600))

    drag.onPointerdown(pointer(300, 2000, bodyEl))
    drag.onPointermove(pointer(250, 2100))

    expect(drag.dragging.value).toBe(false)
  })

  it('springs back when the browser takes the touch over', () => {
    const { drag, panelEl, close } = setup()

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove(pointer(200, 100))
    drag.onPointercancel()

    expect(panelEl.style.getPropertyValue('--sheet-drag')).toBe('0px')
    expect(drag.dragging.value).toBe(false)
    expect(close).not.toHaveBeenCalled()
  })

  it('ignores another finger', () => {
    const { drag, close } = setup()

    drag.onPointerdown(pointer(0, 0))
    drag.onPointermove({ ...pointer(300, 10), pointerId: 2 })
    drag.onPointerup({ ...pointer(300, 20), pointerId: 2 })

    expect(close).not.toHaveBeenCalled()
  })
})
