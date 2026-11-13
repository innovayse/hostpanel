/**
 * Unit tests for the chart theme: tokens are read from the root, with a fallback palette, and the bar fill.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ScriptableContext } from 'chart.js'
import { barEnds, barFill, barGlowPlugin, chartSkeletonBars, MAX_BAR_THICKNESS, paletteColor, readChartTheme } from '@/utils/ui/chart'
import type { UiChartTheme } from '@/utils/ui/chart'

afterEach(() => {
  vi.restoreAllMocks()
})

/** A theme with recognisable values for the bar tests. */
const THEME: UiChartTheme = {
  grid: 'g',
  tick: 't',
  palette: ['#4c81ff', '#8b5cf6'],
  barTop: 'top',
  barBottom: 'bottom',
  barDim: 'dim',
  target: 'target',
  barGlow: 'glow',
}

describe('readChartTheme', () => {
  it('falls back to the built-in palette when the stylesheet declares nothing', () => {
    const theme = readChartTheme()

    expect(theme.palette).toHaveLength(6)
    expect(theme.grid).not.toBe('')
    expect(theme.tick).not.toBe('')
    expect(theme.barTop).not.toBe('')
    expect(theme.barBottom).not.toBe('')
    expect(theme.barDim).not.toBe('')
    expect(theme.target).not.toBe('')
  })

  it('reads the chart variables from the root element', () => {
    const variables: Record<string, string> = { '--chart-grid': 'red', '--chart-1': 'blue', '--chart-bar-top': 'cyan', '--chart-target': 'grey' }
    vi.spyOn(globalThis, 'getComputedStyle').mockReturnValue({
      getPropertyValue: (name: string) => variables[name] ?? '',
    } as CSSStyleDeclaration)

    const theme = readChartTheme()

    expect(theme.grid).toBe('red')
    expect(theme.palette[0]).toBe('blue')
    expect(theme.barTop).toBe('cyan')
    expect(theme.target).toBe('grey')
  })
})

describe('paletteColor', () => {
  it('cycles through the palette', () => {
    expect(paletteColor(THEME, 0)).toBe('#4c81ff')
    expect(paletteColor(THEME, 3)).toBe('#8b5cf6')
  })
})

describe('barEnds', () => {
  it('gives the first palette colour the theme blue pair', () => {
    expect(barEnds(THEME, '#4c81ff')).toEqual({ top: 'top', bottom: 'bottom' })
  })

  it('lightens any other colour at its far end', () => {
    const ends = barEnds(THEME, '#8b5cf6')

    expect(ends.bottom).toBe('#8b5cf6')
    expect(ends.top).toContain('white')
  })
})

describe('barFill', () => {
  /** The colour stops a fake gradient received, as [offset, colour]. */
  const stops: [number, string][] = []

  /** The line a fake canvas was asked to span, as [x0, y0, x1, y1]. */
  const spans: number[][] = []

  /** The gradient ends the tests draw. */
  const ends = { top: 'light', bottom: 'base' }

  /**
   * Builds the part of a chart.js context the fill reads.
   *
   * @param chartArea - The plot area, or undefined before the first layout.
   * @param raw - The value of the bar being filled.
   * @returns A context for the scriptable fill.
   */
  const contextOf = (chartArea: { left: number; right: number; top: number; bottom: number } | undefined, raw = 1): ScriptableContext<'bar'> => {
    stops.length = 0
    spans.length = 0
    const ctx = {
      createLinearGradient: (...args: number[]) => {
        spans.push(args)
        return { addColorStop: (offset: number, color: string) => stops.push([offset, color]) }
      },
    }
    return { raw, chart: { chartArea, ctx } } as unknown as ScriptableContext<'bar'>
  }

  it('uses the base colour until the plot area exists', () => {
    expect(barFill(ends, false)(contextOf(undefined))).toBe('base')
  })

  it('runs a vertical bar from its light far end down to its base', () => {
    barFill(ends, false)(contextOf({ left: 0, right: 100, top: 10, bottom: 90 }))

    expect(spans).toEqual([[0, 10, 0, 90]])
    expect(stops).toEqual([[0, 'light'], [1, 'base']])
  })

  it('runs a horizontal bar from its base on the left to its far end', () => {
    barFill(ends, true)(contextOf({ left: 5, right: 95, top: 0, bottom: 40 }))

    expect(spans).toEqual([[5, 0, 95, 0]])
    expect(stops).toEqual([[0, 'base'], [1, 'light']])
  })

  it('fades a bar below the target and keeps the gradient at or above it', () => {
    const area = { left: 0, right: 100, top: 0, bottom: 100 }
    const fill = barFill(ends, false, { value: 5, dim: 'dim' })

    expect(fill(contextOf(area, 4))).toBe('dim')
    expect(spans).toEqual([])
    fill(contextOf(area, 5))
    expect(spans).toHaveLength(1)
  })
})

describe('chartSkeletonBars', () => {
  it('lists twenty literal height classes', () => {
    expect(chartSkeletonBars).toHaveLength(20)
    expect(chartSkeletonBars.every((cls) => /^h-\[\d+%\]$/.test(cls))).toBe(true)
  })
})

describe('barGlowPlugin', () => {
  /**
   * Runs the plugin on a fake chart with two bars.
   *
   * @param options - The plugin options of the chart.
   * @returns The canvas context the plugin drew on.
   */
  const run = (options: unknown) => {
    const ctx = { save: vi.fn(), restore: vi.fn(), beginPath: vi.fn(), roundRect: vi.fn(), fill: vi.fn() }
    const bar = { getProps: () => ({ x: 50, y: 10, base: 90, width: 20 }) }
    const chart = {
      ctx,
      options: { plugins: { barGlow: options } },
      data: { datasets: [{ data: [1, 9] }] },
      getDatasetMeta: () => ({ type: 'bar', data: [bar, bar] }),
    }
    barGlowPlugin.beforeDatasetDraw?.(chart as never, { index: 0 } as never, {})
    return ctx
  }

  it('glows only the bars that reach the target', () => {
    const ctx = run({ color: 'glow', target: 5 })

    expect(ctx.fill).toHaveBeenCalledTimes(1)
    expect(ctx.roundRect).toHaveBeenCalledWith(40, 10, 20, 80, 8)
  })

  it('draws nothing when the option is off', () => {
    expect(run(false).fill).not.toHaveBeenCalled()
  })
})

describe('MAX_BAR_THICKNESS', () => {
  it('caps a bar at 28 pixels so few categories never stretch it', () => {
    expect(MAX_BAR_THICKNESS).toBe(28)
  })
})
