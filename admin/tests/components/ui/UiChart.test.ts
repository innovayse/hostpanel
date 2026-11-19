/**
 * Unit tests for UiChart: it builds the chart.js configuration from labels and series, shows the
 * empty state, formats values and tears the chart down.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UiChart from '@/components/ui/UiChart.vue'
import { ChartKind } from '@/utils/ui/chart'
import { createTestI18n } from '../../helpers/i18n'

/** The part of the chart.js configuration these tests read. */
interface CapturedConfig {
  type: string
  data: {
    labels: string[]
    datasets: { type?: string; label: string; data: number[]; borderColor?: string; backgroundColor?: unknown; borderDash?: number[] }[]
  }
  options: {
    indexAxis: string
    plugins: {
      legend: { display: boolean; position: string }
      tooltip: { callbacks: { label: (context: { raw: number; label: string; dataset: { label: string } }) => string } }
    }
    scales: {
      x?: { stacked: boolean }
      y?: { ticks: { callback: (value: number) => string } }
    }
  }
}

/** Charts built by the mocked constructor, newest last. */
const created = vi.hoisted(() => [] as { config: CapturedConfig; destroy: ReturnType<typeof vi.fn> }[])

// chart.js needs a real canvas, which the test environment does not have; the library is the one
// thing replaced, so the test still exercises UiChart's own configuration logic.
vi.mock('chart.js', () => {
  /** Stand-in for a chart.js element or controller class. */
  class Registerable {}
  /** Stand-in for the Chart class that records its configuration. */
  class Chart {
    static register = vi.fn()
    config: CapturedConfig
    destroy = vi.fn()
    constructor(_canvas: unknown, config: CapturedConfig) {
      this.config = config
      created.push(this)
    }
  }
  return {
    Chart,
    ArcElement: Registerable,
    BarController: Registerable,
    BarElement: Registerable,
    CategoryScale: Registerable,
    DoughnutController: Registerable,
    Filler: Registerable,
    Legend: Registerable,
    LineController: Registerable,
    LineElement: Registerable,
    LinearScale: Registerable,
    PointElement: Registerable,
    Tooltip: Registerable,
  }
})

/**
 * Mounts the chart with the English catalogue.
 *
 * @param props - Props for the chart.
 * @returns The wrapper.
 */
const mountChart = (props: InstanceType<typeof UiChart>['$props']) =>
  mount(UiChart, { props, global: { plugins: [createTestI18n()] } })

beforeEach(() => {
  created.length = 0
})

describe('UiChart', () => {
  it('builds a bar chart from labels and series', async () => {
    mountChart({ kind: ChartKind.GroupedBar, labels: ['Jan', 'Feb'], series: [{ label: 'Income', data: [1, 2] }] })
    await flushPromises()

    const config = created[0]?.config
    expect(config?.type).toBe('bar')
    expect(config?.data.labels).toEqual(['Jan', 'Feb'])
    expect(config?.data.datasets[0]?.label).toBe('Income')
    expect(config?.data.datasets[0]?.data).toEqual([1, 2])
  })

  it('colours series from the theme palette unless one is given', async () => {
    mountChart({
      kind: ChartKind.Line,
      labels: ['a'],
      series: [{ label: 'A', data: [1] }, { label: 'B', data: [2], color: '#123456' }],
    })
    await flushPromises()

    const datasets = created[0]?.config.data.datasets
    expect(datasets?.[0]?.borderColor).toBeTruthy()
    expect(datasets?.[1]?.borderColor).toBe('#123456')
  })

  it('fills bars with a gradient and lines with the plain colour', async () => {
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ kind: ChartKind.Line, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    await flushPromises()

    expect(typeof created[0]?.config.data.datasets[0]?.backgroundColor).toBe('function')
    expect(typeof created[1]?.config.data.datasets[0]?.backgroundColor).toBe('string')
  })

  it('draws an optional dashed target line across a vertical chart', async () => {
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a', 'b'], series: [{ label: 'A', data: [1, 2] }], target: 5 })
    await flushPromises()

    const datasets = created[0]?.config.data.datasets
    expect(datasets).toHaveLength(2)
    expect(datasets?.[1]).toMatchObject({ type: 'line', label: 'Target', data: [5, 5], borderDash: [4, 4], borderWidth: 1 })
  })

  it('rounds bars as the concept does and fades the ones below the target', async () => {
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a', 'b'], series: [{ label: 'A', data: [1, 9] }], target: 5 })
    await flushPromises()

    const dataset = created[0]?.config.data.datasets[0] as { borderRadius?: unknown, maxBarThickness?: number, backgroundColor?: (context: unknown) => unknown }
    expect(dataset.borderRadius).toEqual({ topLeft: 8, topRight: 8, bottomLeft: 3, bottomRight: 3 })
    expect(dataset.maxBarThickness).toBe(28)
    expect(dataset.backgroundColor?.({ raw: 1, chart: {} })).toBe('rgb(124 160 255 / 0.28)')
  })

  it('drops the value axis and adds the glow in the minimal look', async () => {
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a', 'b'], series: [{ label: 'A', data: [1, 9] }], target: 5, variant: 'minimal' })
    await flushPromises()

    const options = created[0]?.config.options as unknown as { scales: { y: { display: boolean } }, plugins: { barGlow: unknown } }
    expect(options.scales.y.display).toBe(false)
    expect(options.plugins.barGlow).toMatchObject({ target: 5 })
  })

  it('keeps the value axis and draws no glow in the default look', async () => {
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }], target: 5 })
    await flushPromises()

    const options = created[0]?.config.options as unknown as { scales: { y: { display: boolean } }, plugins: { barGlow: unknown } }
    expect(options.scales.y.display).toBe(true)
    expect(options.plugins.barGlow).toBe(false)
  })

  it('sizes the plot by the height option', async () => {
    const wrapper = mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }], height: 'short' })
    await flushPromises()

    expect(wrapper.classes()).toContain('h-52')
  })

  it('takes the phone height of its kind below the plot height', async () => {
    const wrapper = mountChart({ kind: ChartKind.Donut, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    await flushPromises()

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['h-64', 'max-sm:h-72']))
  })

  it('puts the legend above the plot on a wide screen and under it on a phone, for a donut and a bar chart alike', async () => {
    mountChart({ kind: ChartKind.Donut, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    await flushPromises()
    expect(created[0]?.config.options.plugins.legend.position).toBe('top')

    vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))
    mountChart({ kind: ChartKind.Donut, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }, { label: 'B', data: [2] }] })
    await flushPromises()
    vi.unstubAllGlobals()

    expect(created[1]?.config.options.plugins.legend.position).toBe('bottom')
    expect(created[2]?.config.options.plugins.legend.position).toBe('bottom')
  })

  it('draws placeholder bars and builds no chart while loading', async () => {
    const wrapper = mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }], loading: true })
    await flushPromises()

    expect(wrapper.find('[data-testid="chart-skeleton"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(20)
    expect(wrapper.find('canvas').exists()).toBe(false)
    expect(created).toHaveLength(0)
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })

  it('names the target line as the screen asks', async () => {
    mountChart({ kind: ChartKind.Line, labels: ['a'], series: [{ label: 'A', data: [1] }], target: 3, targetLabel: 'Goal' })
    await flushPromises()

    expect(created[0]?.config.data.datasets[1]?.label).toBe('Goal')
  })

  it('draws no target line without a target, on a horizontal chart or on a donut', async () => {
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ kind: ChartKind.HorizontalBar, labels: ['a'], series: [{ label: 'A', data: [1] }], target: 5 })
    mountChart({ kind: ChartKind.Donut, labels: ['a'], series: [{ label: 'A', data: [1] }], target: 5 })
    await flushPromises()

    expect(created.map(chart => chart.config.data.datasets.length)).toEqual([1, 1, 1])
  })

  it('colours each slice of a donut, cuts its hole and drops the axes', async () => {
    mountChart({ kind: ChartKind.Donut, labels: ['x', 'y', 'z'], series: [{ label: 'Share', data: [1, 2, 3] }] })
    await flushPromises()

    const config = created[0]?.config
    expect(config?.data.datasets[0]?.backgroundColor).toHaveLength(3)
    expect(config?.type).toBe('doughnut')
    const doughnut = config?.options as unknown as { cutout: string } | undefined
    expect(doughnut?.cutout).toBe('68%')
    expect(config?.options.scales).toEqual({})
    expect(config?.options.plugins.legend.display).toBe(true)
  })

  it('shows the legend only when it says something', async () => {
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }], legend: true })
    await flushPromises()

    expect(created[0]?.config.options.plugins.legend.display).toBe(false)
    expect(created[1]?.config.options.plugins.legend.display).toBe(true)
  })

  it('formats tooltip and axis numbers with the formatter', async () => {
    mountChart({
      kind: ChartKind.GroupedBar,
      labels: ['a'],
      series: [{ label: 'Income', data: [5] }],
      valueFormatter: (v: number) => `$${v}`,
    })
    await flushPromises()

    const options = created[0]?.config.options
    expect(options?.plugins.tooltip.callbacks.label({ raw: 5, label: 'a', dataset: { label: 'Income' } })).toBe('Income: $5')
    expect(options?.scales.y?.ticks.callback(10)).toBe('$10')
  })

  it('draws the horizontal bar kind sideways and the vertical ones upright', async () => {
    mountChart({ kind: ChartKind.HorizontalBar, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    await flushPromises()

    expect(created[0]?.config.type).toBe('bar')
    expect(created[0]?.config.options.indexAxis).toBe('y')
    expect(created[1]?.config.options.indexAxis).toBe('x')
  })

  it('stacks the bars of the stacked kind only', async () => {
    mountChart({ kind: ChartKind.StackedBar, labels: ['a'], series: [{ label: 'A', data: [1] }, { label: 'B', data: [2] }] })
    mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }, { label: 'B', data: [2] }] })
    await flushPromises()

    expect(created[0]?.config.options.scales.x?.stacked).toBe(true)
    expect(created[1]?.config.options.scales.x?.stacked).toBe(false)
  })

  it('fills the area under the line of the area kind only', async () => {
    mountChart({ kind: ChartKind.Area, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ kind: ChartKind.Line, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    await flushPromises()

    const filled = created[0]?.config.data.datasets[0] as { fill?: boolean, backgroundColor?: string }
    expect(created[0]?.config.type).toBe('line')
    expect(filled.fill).toBe(true)
    expect(filled.backgroundColor).toContain('transparent')
    const unfilled = created[1]?.config.data.datasets[0] as { fill?: boolean } | undefined
    expect(unfilled?.fill).toBe(false)
  })

  it('names the canvas for assistive technology', async () => {
    const wrapper = mountChart({ kind: ChartKind.Line, labels: ['a'], series: [{ label: 'A', data: [1] }], label: 'Income per day' })
    await flushPromises()

    expect(wrapper.get('canvas').attributes('role')).toBe('img')
    expect(wrapper.get('canvas').attributes('aria-label')).toBe('Income per day')
  })

  it('falls back to a generic name for the canvas', async () => {
    const wrapper = mountChart({ kind: ChartKind.Line, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    await flushPromises()

    expect(wrapper.get('canvas').attributes('aria-label')).toBe('Chart')
  })

  describe('the kinds drawn as markup', () => {
    it('draws the heat strip with one square per value and the label in its tooltip', async () => {
      const wrapper = mountChart({ kind: ChartKind.HeatStrip, labels: ['Mon', 'Tue'], series: [{ label: 'Replies', data: [0, 4] }], valueFormatter: (v: number) => `${v} replies` })
      await flushPromises()

      expect(wrapper.findAll('[data-testid="ui-chart-heat-cell"]')).toHaveLength(2)
      expect(wrapper.text()).toContain('Tue: 4 replies')
      expect(wrapper.find('canvas').exists()).toBe(false)
      expect(created).toHaveLength(0)
    })

    it('draws the distribution as one wide bar over its key', async () => {
      const wrapper = mountChart({ kind: ChartKind.Distribution, labels: ['Poor', 'Good'], series: [{ label: 'Ratings', data: [1, 3] }] })
      await flushPromises()

      expect(wrapper.findAll('[data-testid="ui-chart-distribution-part"]')).toHaveLength(2)
      expect(wrapper.get('[data-testid="ui-chart-distribution"]').classes()).toContain('w-full')
      expect(wrapper.get('[data-testid="ui-chart-legend"]').text()).toContain('Good')
      expect(wrapper.text()).toContain('Good: 3')
    })

    it('draws the gauge with its figure and scale', async () => {
      const wrapper = mountChart({ kind: ChartKind.Gauge, labels: ['Load'], series: [{ label: 'Server load', data: [40] }], max: 80, valueFormatter: (v: number) => `${v}%` })
      await flushPromises()

      const meter = wrapper.get('[role="meter"]')
      expect(meter.attributes('aria-valuenow')).toBe('40')
      expect(meter.attributes('aria-valuemax')).toBe('80')
      expect(wrapper.get('[data-testid="ui-chart-gauge-text"]').text()).toBe('40%')
      expect(wrapper.get('[data-testid="ui-chart-gauge-arc"]').attributes('stroke-dasharray')).toBe('50 100')
    })

    it('draws the placeholder of the kind while loading', async () => {
      const wrapper = mountChart({ kind: ChartKind.Gauge, labels: ['Load'], series: [{ label: 'Load', data: [1] }], loading: true })
      await flushPromises()

      expect(wrapper.get('[data-testid="chart-skeleton"]').attributes('data-kind')).toBe(ChartKind.Gauge)
    })
  })

  it('shows the empty state and builds no chart without data', async () => {
    const wrapper = mountChart({ kind: ChartKind.GroupedBar, labels: [], series: [] })
    await flushPromises()

    expect(wrapper.get('[data-testid="chart-empty"]').text()).toBe('No data to chart')
    expect(wrapper.find('canvas').exists()).toBe(false)
    expect(created).toHaveLength(0)
  })

  it('rebuilds when the data changes and destroys on unmount', async () => {
    const wrapper = mountChart({ kind: ChartKind.GroupedBar, labels: ['a'], series: [{ label: 'A', data: [1] }] })
    await flushPromises()
    const first = created[0]

    await wrapper.setProps({ labels: ['a', 'b'], series: [{ label: 'A', data: [1, 2] }] })
    await flushPromises()

    expect(first?.destroy).toHaveBeenCalled()
    expect(created).toHaveLength(2)

    wrapper.unmount()
    expect(created[1]?.destroy).toHaveBeenCalled()
  })
})
