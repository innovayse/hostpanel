<script setup lang="ts">
/**
 * Chart frame over chart.js: the only place a screen's data meets the chart library.
 *
 * A screen passes a `kind` (a {@link ChartKind}), `labels` and `series`; this component owns the theme colours (read
 * from the light/dark tokens and re-read when the theme class changes), grid, tooltip format, legend, accessible name
 * and the empty state, so no screen configures chart.js. The line, area, bar and donut kinds are drawn on a canvas; the
 * heat strip, distribution and gauge kinds are markup (`UiChartHeatStrip`, `UiChartDistribution`, `UiChartGauge`)
 * drawn from the same labels and series. `valueFormatter` formats tooltip and axis numbers (currency, percent). Bars
 * are filled with a gradient from a light far end to the base colour and have the concept's corners (see `barFill`).
 * `target` draws a dashed reference line across a vertical bar or line chart and fades the bars that stay below it.
 * `variant` `minimal` is the dashboard look: no value axis labels and no grid, a glow behind the bars that reach the
 * target, and a compact legend. `loading` draws the placeholder of the same kind at the plot height. On a phone the box
 * takes the phone height of its kind (`chartHeightClassOf`) and every legend sits under the plot, so a wrapped legend never crowds it.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  ArcElement,
  BarController,
  BarElement,
  CategoryScale,
  Chart,
  DoughnutController,
  Filler,
  Legend,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip,
} from 'chart.js'
import type { ChartConfiguration, TooltipItem } from 'chart.js'
import UiChartDistribution from '@/components/ui/UiChartDistribution.vue'
import UiChartGauge from '@/components/ui/UiChartGauge.vue'
import UiChartHeatStrip from '@/components/ui/UiChartHeatStrip.vue'
import UiChartLegend from '@/components/ui/UiChartLegend.vue'
import UiChartSkeleton from '@/components/ui/UiChartSkeleton.vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import {
  areaFill,
  BAR_RADIUS,
  barEnds,
  barFill,
  barGlowPlugin,
  canvasShapeOf,
  CanvasChartType,
  ChartKind,
  chartHeightClassOf,
  distributionTone,
  DONUT_CUTOUT,
  GAUGE_DEFAULT_MAX,
  MAX_BAR_THICKNESS,
  paletteColor,
  readChartTheme,
  UiChartHeight,
  UiChartVariant,
} from '@/utils/ui/chart'
import { UI_BELOW_SM_QUERY } from '@/utils/ui/breakpoints'
import type { UiTone } from '@/utils/ui/tone'
import type { CanvasChartShape, UiChartPart, UiChartSeries } from '@/utils/ui/chart'

Chart.register(
  ArcElement,
  barGlowPlugin,
  BarController,
  BarElement,
  CategoryScale,
  DoughnutController,
  Filler,
  Legend,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip,
)

const { t } = useI18n()

/** Props for UiChart. */
const props = withDefaults(defineProps<{
  /** Shape of the chart. */
  kind: ChartKind
  /** Category labels (x axis, or slice, part and square names for the circular, distribution and heat strip kinds). */
  labels: string[]
  /** Data series; the donut, heat strip, distribution and gauge kinds use the first one (the gauge its first value). */
  series: UiChartSeries[]
  /** Formats numbers in tooltips, on the value axis and in the figure of a gauge. */
  valueFormatter?: (value: number) => string
  /** Shows the legend; by default only when it says something (several series, or a donut). */
  legend?: boolean
  /** Height of the plot area. */
  height?: UiChartHeight
  /** Value of a dashed target line across the plot; ignored by every kind but the vertical bars and lines. */
  target?: number
  /** Name of the target line in the legend and tooltip; "Target" when omitted. */
  targetLabel?: string
  /** Full scale of a gauge; 100 when omitted. */
  max?: number
  /** Look of the chart; `minimal` drops the value axis labels and grid and adds the target glow. */
  variant?: UiChartVariant
  /** Accessible name of the chart (already translated); a generic one when omitted. */
  label?: string
  /** Shows the placeholder of the kind at the plot height instead of the chart. */
  loading?: boolean
}>(), {
  valueFormatter: undefined,
  legend: undefined,
  height: UiChartHeight.Md,
  target: undefined,
  targetLabel: undefined,
  max: GAUGE_DEFAULT_MAX,
  variant: UiChartVariant.Default,
  label: undefined,
  loading: false,
})

/** Whether the screen is a phone, where the legend sits under the plot (the ring of a donut) and not above it. */
const isPhone = useMediaQuery(UI_BELOW_SM_QUERY)

/** The canvas chart.js draws on; absent while the empty state shows. */
const canvas = ref<HTMLCanvasElement | null>(null)

/** The live chart instance, destroyed before every rebuild and on unmount. */
let chart: Chart<CanvasChartType> | null = null

/** Watches the root element's class so a light/dark switch recolours the chart. */
let themeObserver: MutationObserver | null = null

/** Whether there is nothing to plot. */
const isEmpty = computed<boolean>(() =>
  props.labels.length === 0 || props.series.every(s => s.data.length === 0),
)

/** How the kind is drawn on the canvas; undefined for the kinds drawn as markup. */
const canvasShape = computed<CanvasChartShape | undefined>(() => canvasShapeOf(props.kind))

/** Whether the chart is the donut. */
const isCircular = computed<boolean>(() => canvasShape.value?.type === CanvasChartType.Doughnut)

/** Whether the dashed target line is drawn: a target is set on a chart with a vertical value axis. */
const showsTarget = computed<boolean>(() => props.target !== undefined && canvasShape.value !== undefined && !isCircular.value && !canvasShape.value.horizontal)

/**
 * Formats a number with the caller's formatter, or as-is.
 *
 * @param value - Number to format.
 * @returns The display text.
 */
const format = (value: number): string => (props.valueFormatter ? props.valueFormatter(value) : String(value))

/** The parts of a distribution kind: one per label, counted by the first series, toned in turn. */
const parts = computed<UiChartPart[]>(() => props.labels.map((name, index) => {
  const count = props.series[0]?.data[index] ?? 0
  return { count, tone: distributionTone(index), tooltip: `${name}: ${format(count)}` }
}))

/** The key of a distribution kind: each label with the tone of its part. */
const partKey = computed<{ tone: UiTone; label: string }[]>(() => parts.value.map((part, index) => ({ tone: part.tone, label: props.labels[index] ?? '' })))

/** The first value of the first series, which a gauge draws. */
const gaugeValue = computed<number>(() => props.series[0]?.data[0] ?? 0)

/**
 * Writes the tooltip of one square of the heat strip.
 *
 * @param index - Zero based index of the square.
 * @param value - Its value.
 * @returns The label of the square and its formatted value.
 */
const describeSquare = (index: number, value: number): string => `${props.labels[index] ?? ''}: ${format(value)}`

/** Whether the minimal look is chosen. */
const isMinimal = computed<boolean>(() => props.variant === UiChartVariant.Minimal)

/**
 * Builds the chart.js configuration from props and the current theme.
 *
 * @param shape - How the chart kind is set up in chart.js.
 *
 * @returns A configuration object for `new Chart`.
 */
const buildConfig = (shape: CanvasChartShape): ChartConfiguration<CanvasChartType> => {
  const theme = readChartTheme()
  const showLegend = props.legend ?? (isCircular.value || props.series.length > 1)
  const circular = isCircular.value
  const isBar = shape.type === CanvasChartType.Bar
  const first = props.series[0]
  const datasets = circular
    ? [{
        label: first?.label ?? '',
        data: first?.data ?? [],
        backgroundColor: props.labels.map((_, index) => paletteColor(theme, index)),
        borderWidth: 0,
      }]
    : props.series.map((s, index) => {
        const color = s.color ?? paletteColor(theme, index)
        const dim = showsTarget.value && props.target !== undefined ? { value: props.target, dim: theme.barDim } : undefined
        return {
          label: s.label,
          data: s.data,
          backgroundColor: isBar ? barFill(barEnds(theme, color), shape.horizontal, dim) : shape.filled ? areaFill(color) : color,
          borderColor: color,
          borderRadius: isBar ? (shape.horizontal ? BAR_RADIUS.horizontal : BAR_RADIUS.vertical) : 0,
          borderSkipped: false,
          maxBarThickness: MAX_BAR_THICKNESS,
          tension: 0.35,
          fill: shape.filled,
        }
      })
  const targetDataset = showsTarget.value
    ? [{
        type: 'line' as const,
        label: props.targetLabel ?? t('ui.chart.target'),
        data: props.labels.map(() => props.target ?? 0),
        borderColor: theme.target,
        borderWidth: 1,
        borderDash: [4, 4],
        pointRadius: 0,
        fill: false,
      }]
    : []
  /**
   * Formats a value-axis tick with the chart formatter.
   *
   * @param value - Tick value as chart.js passes it.
   * @returns The formatted tick label.
   */
  const formatTick = (value: string | number): string => format(Number(value))
  const valueAxis = {
    display: !isMinimal.value,
    stacked: shape.stacked,
    grid: { color: theme.grid },
    border: { display: false },
    ticks: { color: theme.tick, callback: formatTick },
  }
  /**
   * Tooltip line: the slice label for circular charts, the series label otherwise.
   *
   * @param context - Hovered item.
   * @returns The text of the tooltip line.
   */
  const tooltipLine = (context: TooltipItem<CanvasChartType>): string => {
    const raw = Number(context.raw)
    return circular ? `${context.label}: ${format(raw)}` : `${context.dataset.label ?? ''}: ${format(raw)}`
  }
  const legendLabels = isMinimal.value
    ? { color: theme.tick, boxWidth: 8, boxHeight: 8, usePointStyle: true, font: { size: 12 } }
    : { color: theme.tick }
  const glow = isMinimal.value && showsTarget.value && props.target !== undefined && isBar
    ? { color: theme.barGlow, target: props.target }
    : false
  const categoryAxis = { stacked: shape.stacked, grid: { display: false }, border: { color: theme.grid }, ticks: { color: theme.tick } }
  return {
    type: shape.type,
    data: { labels: props.labels, datasets: [...datasets, ...targetDataset] },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      indexAxis: shape.horizontal ? 'y' : 'x',
      cutout: circular ? DONUT_CUTOUT : undefined,
      plugins: {
        legend: { display: showLegend, position: isPhone.value ? 'bottom' : 'top', labels: legendLabels },
        barGlow: glow,
        tooltip: {
          callbacks: {
            label: tooltipLine,
          },
        },
      },
      scales: circular
        ? {}
        : { x: shape.horizontal ? valueAxis : categoryAxis, y: shape.horizontal ? categoryAxis : valueAxis },
    },
  }
}

/** Destroys the current chart, if any. */
const destroy = (): void => {
  chart?.destroy()
  chart = null
}

/** Rebuilds the chart from the current props and theme. */
const render = async (): Promise<void> => {
  destroy()
  // The canvas exists only while the empty state is hidden, so wait for the DOM to settle.
  await nextTick()
  const shape = canvasShape.value
  if (canvas.value !== null && shape !== undefined && !isEmpty.value && !props.loading) {
    chart = new Chart(canvas.value, buildConfig(shape))
  }
}

onMounted(() => {
  void render()
  themeObserver = new MutationObserver(() => {
    void render()
  })
  themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
})

watch(
  () => [props.kind, props.labels, props.series, props.legend, props.target, props.targetLabel, props.variant, props.loading, isPhone.value],
  () => {
    void render()
  },
  { deep: true },
)

onBeforeUnmount(() => {
  themeObserver?.disconnect()
  destroy()
})
</script>

<template>
  <div class="relative" :class="chartHeightClassOf(kind, height)" :aria-busy="loading ? 'true' : undefined">
    <UiChartSkeleton v-if="loading" :kind="kind" />
    <p
      v-else-if="isEmpty"
      class="absolute inset-0 flex items-center justify-center text-[0.82rem] text-text-secondary"
      data-testid="chart-empty"
    >
      {{ t('ui.chart.empty') }}
    </p>
    <UiChartHeatStrip v-else-if="kind === ChartKind.HeatStrip" :values="series[0]?.data ?? []" :describe="describeSquare" />
    <template v-else-if="kind === ChartKind.Distribution">
      <UiChartDistribution :parts="parts" wide />
      <UiChartLegend class="mt-3" :items="partKey" />
    </template>
    <UiChartGauge
      v-else-if="kind === ChartKind.Gauge"
      :value="gaugeValue"
      :max="max"
      :label="label ?? series[0]?.label ?? ''"
      :text="format(gaugeValue)"
    />
    <canvas v-else ref="canvas" role="img" :aria-label="label ?? t('ui.chart.aria')" />
  </div>
</template>
