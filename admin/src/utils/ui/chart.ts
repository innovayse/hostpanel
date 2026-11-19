/**
 * Chart vocabulary and theme reading for `UiChart`.
 *
 * Report screens hand `UiChart` labels and series; this module turns the active theme tokens
 * (CSS variables on `<html>`) into the concrete colours chart.js needs, so a screen never
 * configures the chart library and a theme switch recolours every chart.
 */
import type { BarElement, Chart, ChartArea, ChartType, Plugin, ScriptableContext } from 'chart.js'
import { UiTone } from '@/utils/ui/tone'

/** Closed set of the shapes a report or dashboard chart takes; the report picks one by what its numbers say. */
export const ChartKind = {
  /** Lines over time, one per series (a trend of a few counters). */
  Line: 'line',
  /** A line filled down to the axis (a running total or a forecast). */
  Area: 'area',
  /** Bars of several series stacked on one another (parts of a total per category). */
  StackedBar: 'stackedBar',
  /** Bars of one or more series side by side (a value per category, vertical). */
  GroupedBar: 'groupedBar',
  /** A ring split into slices (shares of one total). */
  Donut: 'donut',
  /** Bars that grow sideways (a ranking, long category names). */
  HorizontalBar: 'horizontalBar',
  /** One row of squares shaded by size (a value per day in a compact row). */
  HeatStrip: 'heatStrip',
  /** One stacked bar with a legend (how a total splits into parts). */
  Distribution: 'distribution',
  /** A half ring showing one value against its scale. */
  Gauge: 'gauge',
} as const

/** One of the chart kinds. */
export type ChartKind = (typeof ChartKind)[keyof typeof ChartKind]

/** The chart.js controllers a kind can be drawn with. */
export const CanvasChartType = {
  Bar: 'bar',
  Line: 'line',
  Doughnut: 'doughnut',
} as const

/** One of the chart.js controllers. */
export type CanvasChartType = (typeof CanvasChartType)[keyof typeof CanvasChartType]

/** How a canvas kind is set up in chart.js. */
export interface CanvasChartShape {
  /** The chart.js controller. */
  type: CanvasChartType
  /** Whether the bars of several series stack. */
  stacked: boolean
  /** Whether the bars grow sideways. */
  horizontal: boolean
  /** Whether the area under a line is filled. */
  filled: boolean
}

/** The canvas kinds and their chart.js setup; the kinds absent here are drawn as markup. */
const CANVAS_SHAPES: Partial<Record<ChartKind, CanvasChartShape>> = {
  [ChartKind.Line]: { type: CanvasChartType.Line, stacked: false, horizontal: false, filled: false },
  [ChartKind.Area]: { type: CanvasChartType.Line, stacked: false, horizontal: false, filled: true },
  [ChartKind.StackedBar]: { type: CanvasChartType.Bar, stacked: true, horizontal: false, filled: false },
  [ChartKind.GroupedBar]: { type: CanvasChartType.Bar, stacked: false, horizontal: false, filled: false },
  [ChartKind.Donut]: { type: CanvasChartType.Doughnut, stacked: false, horizontal: false, filled: false },
  [ChartKind.HorizontalBar]: { type: CanvasChartType.Bar, stacked: false, horizontal: true, filled: false },
}

/**
 * Looks up how a kind is drawn on the canvas.
 *
 * @param kind - The chart kind.
 * @returns The chart.js setup, or undefined for the kinds drawn as markup (heat strip, distribution, gauge).
 */
export const canvasShapeOf = (kind: ChartKind): CanvasChartShape | undefined => CANVAS_SHAPES[kind]

/** Fill of the area under a line, as a share of the line colour. */
const AREA_FILL_SHARE = 18

/**
 * Builds the translucent fill under an area line.
 *
 * @param color - The line colour.
 * @returns A CSS colour: the line colour at a low share over transparent.
 */
export const areaFill = (color: string): string => `color-mix(in srgb, ${color} ${AREA_FILL_SHARE}%, transparent)`

/** The cut-out of a donut as a share of its radius. */
export const DONUT_CUTOUT = '68%'

/** Closed set of chart heights. */
export const UiChartHeight = {
  /** The `short` option: a 208px plot, between `sm` and `md`, as the dashboard revenue card draws it. */
  Short: 'short',
  /** The `sm` option. */
  Sm: 'sm',
  /** The `md` option. */
  Md: 'md',
  /** The `lg` option. */
  Lg: 'lg',
} as const

/** One of the UiChartHeight values. */
export type UiChartHeight = (typeof UiChartHeight)[keyof typeof UiChartHeight]

/** Plot height classes, literal so Tailwind emits them; the chart and its loading placeholder share them. */
export const chartHeightClasses: Record<UiChartHeight, string> = {
  [UiChartHeight.Short]: 'h-52',
  [UiChartHeight.Sm]: 'h-40',
  [UiChartHeight.Md]: 'h-64',
  [UiChartHeight.Lg]: 'h-80',
}

/** Plot heights on a phone (below `sm`), a step shorter than the wide ones, as literal classes so Tailwind emits them. */
export const chartPhoneHeightClasses: Record<UiChartHeight, string> = {
  [UiChartHeight.Short]: 'max-sm:h-44',
  [UiChartHeight.Sm]: 'max-sm:h-36',
  [UiChartHeight.Md]: 'max-sm:h-56',
  [UiChartHeight.Lg]: 'max-sm:h-64',
}

/**
 * Gives every height step the same phone class.
 *
 * @param classes - The literal phone class.
 * @returns A phone class per height step.
 */
const samePhoneClassAtEveryHeight = (classes: string): Record<UiChartHeight, string> => ({
  [UiChartHeight.Short]: classes,
  [UiChartHeight.Sm]: classes,
  [UiChartHeight.Md]: classes,
  [UiChartHeight.Lg]: classes,
})

/**
 * Phone heights of the kinds whose content does not follow the plain step: the donut keeps room for its legend under the
 * ring, the heat strip and the distribution are one row (and a key) that sets its own height, the gauge is a half ring.
 */
const CHART_PHONE_KIND_HEIGHTS: Partial<Record<ChartKind, Record<UiChartHeight, string>>> = {
  [ChartKind.Donut]: { [UiChartHeight.Short]: 'max-sm:h-64', [UiChartHeight.Sm]: 'max-sm:h-56', [UiChartHeight.Md]: 'max-sm:h-72', [UiChartHeight.Lg]: 'max-sm:h-80' },
  [ChartKind.HeatStrip]: samePhoneClassAtEveryHeight('max-sm:h-auto'),
  [ChartKind.Distribution]: samePhoneClassAtEveryHeight('max-sm:h-auto'),
  [ChartKind.Gauge]: { [UiChartHeight.Short]: 'max-sm:h-40', [UiChartHeight.Sm]: 'max-sm:h-36', [UiChartHeight.Md]: 'max-sm:h-44', [UiChartHeight.Lg]: 'max-sm:h-52' },
}

/**
 * Height classes of a chart box: the plot height on a wide screen, and the phone height of its kind below `sm`. The
 * chart and its loading placeholder both take their box from here, so they are always the same height.
 *
 * @param kind - The chart kind.
 * @param height - The plot height step.
 * @returns The classes to put on the box.
 */
export const chartHeightClassOf = (kind: ChartKind, height: UiChartHeight): string =>
  `${chartHeightClasses[height]} ${CHART_PHONE_KIND_HEIGHTS[kind]?.[height] ?? chartPhoneHeightClasses[height]}`

/** Closed set of chart looks. */
export const UiChartVariant = {
  /** Value axis with labels and grid lines. */
  Default: 'default',
  /** The dashboard look: no value axis labels and no grid, bars at or above the target glow, compact legend. */
  Minimal: 'minimal',
} as const

/** One of the chart looks. */
export type UiChartVariant = (typeof UiChartVariant)[keyof typeof UiChartVariant]

/** One data series of a chart. */
export interface UiChartSeries {
  /** Legend label. */
  label: string
  /** Plotted values, one per label. */
  data: number[]
  /** Explicit colour; omitted, the series takes the next colour of the theme palette. */
  color?: string
}

/** One part of a distribution bar. */
export interface UiChartPart {
  /** What the part counts. */
  count: number
  /** Its colour. */
  tone: UiTone
  /** Tooltip of the part (already translated). */
  tooltip: string
}

/** Concrete colours resolved from the theme tokens. */
export interface UiChartTheme {
  /** Grid line colour. */
  grid: string
  /** Axis tick and legend text colour. */
  tick: string
  /** Series palette, cycled by series (or by slice for pie and doughnut). */
  palette: string[]
  /** Light end of a bar gradient (the far end of the bar). */
  barTop: string
  /** Base end of a bar gradient (where the bar stands on the axis). */
  barBottom: string
  /** Faded fill of a bar that stays below the target. */
  barDim: string
  /** Colour of the dashed target line. */
  target: string
  /** Glow colour behind a bar that reaches the target in the minimal look. */
  barGlow: string
}

/**
 * Palette used when the stylesheet is not loaded (unit tests, first paint). It repeats the dark
 * tokens of `assets/main.css` on purpose: a chart draws on a canvas, which cannot resolve a CSS
 * variable, and it can be created before the stylesheet has applied. `tests/assets/mainCss.test.ts`
 * fails when any value here drifts from the stylesheet.
 */
const FALLBACK_THEME: UiChartTheme = {
  grid: 'rgb(255 255 255 / 0.06)',
  tick: '#9aa6c4',
  palette: ['#4c81ff', '#8b5cf6', '#6ee7b7', '#fcd34d', '#fca5a5', '#7ca0ff'],
  barTop: '#7ca0ff',
  barBottom: '#4c81ff',
  barDim: 'rgb(124 160 255 / 0.28)',
  target: 'rgb(154 166 196 / 0.6)',
  barGlow: 'rgb(76 129 255 / 0.45)',
}

/** Number of palette colours the stylesheet declares (`--chart-1` .. `--chart-6`). */
const PALETTE_SIZE = 6

/**
 * Reads one CSS variable from the root element.
 *
 * @param styles - Computed style of the root element.
 * @param name - Variable name including the leading dashes.
 * @param fallback - Value used when the variable is empty.
 * @returns The trimmed value, or the fallback.
 */
const readVar = (styles: CSSStyleDeclaration, name: string, fallback: string): string => {
  const value = styles.getPropertyValue(name).trim()
  return value === '' ? fallback : value
}

/**
 * Resolves the chart colours from the active theme.
 *
 * @returns Grid, tick and palette colours for the current light/dark mode.
 */
export const readChartTheme = (): UiChartTheme => {
  if (typeof document === 'undefined') {
    return FALLBACK_THEME
  }
  const styles = getComputedStyle(document.documentElement)
  const palette: string[] = []
  for (let index = 1; index <= PALETTE_SIZE; index += 1) {
    palette.push(readVar(styles, `--chart-${index}`, FALLBACK_THEME.palette[index - 1] ?? '#4c81ff'))
  }
  return {
    grid: readVar(styles, '--chart-grid', FALLBACK_THEME.grid),
    tick: readVar(styles, '--chart-tick', FALLBACK_THEME.tick),
    palette,
    barTop: readVar(styles, '--chart-bar-top', FALLBACK_THEME.barTop),
    barBottom: readVar(styles, '--chart-bar-bottom', FALLBACK_THEME.barBottom),
    barDim: readVar(styles, '--chart-bar-dim', FALLBACK_THEME.barDim),
    target: readVar(styles, '--chart-target', FALLBACK_THEME.target),
    barGlow: readVar(styles, '--chart-bar-glow', FALLBACK_THEME.barGlow),
  }
}

/**
 * Picks the colour for the series (or slice) at an index, cycling the palette.
 *
 * @param theme - Resolved theme.
 * @param index - Zero-based series or slice index.
 * @returns A CSS colour string.
 */
export const paletteColor = (theme: UiChartTheme, index: number): string => {
  return theme.palette[index % theme.palette.length] ?? '#4c81ff'
}



/** The two ends of a bar gradient. */
interface UiBarEnds {
  /** Colour at the far end of the bar. */
  top: string
  /** Colour at the base of the bar. */
  bottom: string
}

/** A target that splits the bars: those below it are drawn faded. */
interface UiBarTarget {
  /** The target value. */
  value: number
  /** Fill of a bar below the target. */
  dim: string
}

/** Share of the series colour kept at the far end of a bar of a series other than the first. */
const SERIES_TOP_SHARE = 70

/**
 * Picks the gradient ends of a bar series: the brand blue pair from the theme for the first palette colour,
 * and the series colour lightened toward white at its far end for any other.
 *
 * @param theme - Resolved theme.
 * @param color - The series colour.
 * @returns The far and base colours of the bar gradient.
 */
export const barEnds = (theme: UiChartTheme, color: string): UiBarEnds => {
  if (color === theme.palette[0]) {
    return { top: theme.barTop, bottom: theme.barBottom }
  }
  return { top: `color-mix(in srgb, ${color} ${SERIES_TOP_SHARE}%, white)`, bottom: color }
}

/**
 * Builds the fill of a bar: a gradient from the light far end to the base colour, drawn along the bar. A bar
 * below the target is the faded colour instead. chart.js asks for the fill on every layout pass, and the first
 * pass has no plot area yet, so the base colour stands in until it has one.
 *
 * @param ends - The far and base colours.
 * @param horizontal - True for bars that grow sideways, so the gradient runs along the x axis.
 * @param target - Optional target that fades the bars below it.
 * @returns A scriptable fill for a bar dataset's `backgroundColor`.
 */
export const barFill = (ends: UiBarEnds, horizontal: boolean, target?: UiBarTarget) => (context: ScriptableContext<'bar'>): string | CanvasGradient => {
  if (target !== undefined && Number(context.raw) < target.value) {
    return target.dim
  }
  const { chart } = context
  const area: ChartArea | undefined = chart.chartArea
  if (area === undefined) {
    return ends.bottom
  }
  const gradient = horizontal
    ? chart.ctx.createLinearGradient(area.left, 0, area.right, 0)
    : chart.ctx.createLinearGradient(0, area.top, 0, area.bottom)
  gradient.addColorStop(0, horizontal ? ends.bottom : ends.top)
  gradient.addColorStop(1, horizontal ? ends.top : ends.bottom)
  return gradient
}

/** Bar corners as the concept draws them: large at the far end of the bar, small at its base. */
export const BAR_RADIUS = {
  /** Bars that grow upward. */
  vertical: { topLeft: 8, topRight: 8, bottomLeft: 3, bottomRight: 3 },
  /** Bars that grow sideways. */
  horizontal: { topLeft: 3, bottomLeft: 3, topRight: 8, bottomRight: 8 },
} as const

/** Thickest a bar grows, in pixels, so a chart with few categories keeps slim bars as the concept draws them. */
export const MAX_BAR_THICKNESS = 28

/** Heights of the placeholder bars drawn while a chart loads, as literal classes so Tailwind emits them. */
export const chartSkeletonBars: readonly string[] = [
  'h-[40%]', 'h-[55%]', 'h-[62%]', 'h-[48%]', 'h-[70%]', 'h-[82%]', 'h-[66%]', 'h-[58%]', 'h-[74%]', 'h-[90%]',
  'h-[64%]', 'h-[52%]', 'h-[60%]', 'h-[78%]', 'h-[88%]', 'h-[72%]', 'h-[68%]', 'h-[80%]', 'h-[94%]', 'h-[76%]',
]

/** Widths of the placeholder bars of a horizontal bar chart, as literal classes so Tailwind emits them. */
export const chartSkeletonRows: readonly string[] = ['w-[88%]', 'w-[72%]', 'w-[60%]', 'w-[48%]', 'w-[36%]', 'w-[24%]']

/** How many squares the placeholder of a heat strip draws. */
export const chartSkeletonSquares = 24

/** Blur radius of the glow behind a bar, in pixels. */
const BAR_GLOW_BLUR = 18

/** Options of the `barGlow` plugin. */
interface UiBarGlowOptions {
  /** Glow colour. */
  color: string
  /** Bars whose value is at or above this get the glow. */
  target: number
}

declare module 'chart.js' {
  /** Registers the `barGlow` plugin options with chart.js. */
  interface PluginOptionsByType<TType extends ChartType> {
    /** Glow behind the bars that reach the target; absent, no glow is drawn. */
    barGlow?: UiBarGlowOptions | false
  }
}

/**
 * Draws a soft glow behind every bar of a bar dataset that reaches the target. The glow is the bar's own shape
 * filled with a shadow, painted before the bar so the bar covers it and only the halo shows.
 */
export const barGlowPlugin: Plugin<'bar'> = {
  id: 'barGlow',
  /**
   * Paints the glow of each bar at or over the target, under the bar itself.
   *
   * @param chart - The chart being drawn.
   * @param args - The dataset about to be drawn.
   */
  beforeDatasetDraw: (chart: Chart<'bar'>, args) => {
    const options = chart.options.plugins?.barGlow
    const meta = chart.getDatasetMeta(args.index)
    // chart.js hands plugin options over as a partial of the declared shape, so a half-set one draws nothing.
    if (!options || options.color === undefined || options.target === undefined || meta.type !== 'bar') {
      return
    }
    const { color, target } = options
    const { ctx } = chart
    const values = chart.data.datasets[args.index]?.data ?? []
    meta.data.forEach((element, index) => {
      if (Number(values[index]) < target) {
        return
      }
      const bar = element as BarElement
      const { x, y, base, width } = bar.getProps(['x', 'y', 'base', 'width'], true)
      // A bar not yet laid out (a hidden dataset) has no position to glow behind.
      if (x === null || y === null) {
        return
      }
      ctx.save()
      ctx.shadowColor = color
      ctx.shadowBlur = BAR_GLOW_BLUR
      ctx.fillStyle = color
      ctx.beginPath()
      ctx.roundRect(x - width / 2, y, width, base - y, BAR_RADIUS.vertical.topLeft)
      ctx.fill()
      ctx.restore()
    })
  },
}

/** Tones the parts of a distribution take in order, cycled when there are more parts than tones. */
const DISTRIBUTION_TONES: readonly UiTone[] = [UiTone.Primary, UiTone.Success, UiTone.Warning, UiTone.Danger, UiTone.Info, UiTone.Neutral]

/**
 * Picks the tone of a distribution part.
 *
 * @param index - Zero based index of the part.
 * @returns The tone of that place in the cycle.
 */
export const distributionTone = (index: number): UiTone => DISTRIBUTION_TONES[index % DISTRIBUTION_TONES.length] ?? UiTone.Neutral

/** The full scale of a gauge, in percent, when the caller sets none. */
export const GAUGE_DEFAULT_MAX = 100
