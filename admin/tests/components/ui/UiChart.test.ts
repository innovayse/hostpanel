/**
 * Unit tests for UiChart: it builds the chart.js configuration from labels and series, shows the
 * empty state, formats values and tears the chart down.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import UiChart from '@/components/ui/UiChart.vue'
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
      legend: { display: boolean }
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
    PieController: Registerable,
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
    mountChart({ type: 'bar', labels: ['Jan', 'Feb'], series: [{ label: 'Income', data: [1, 2] }] })
    await flushPromises()

    const config = created[0]?.config
    expect(config?.type).toBe('bar')
    expect(config?.data.labels).toEqual(['Jan', 'Feb'])
    expect(config?.data.datasets[0]?.label).toBe('Income')
    expect(config?.data.datasets[0]?.data).toEqual([1, 2])
  })

  it('colours series from the theme palette unless one is given', async () => {
    mountChart({
      type: 'line',
      labels: ['a'],
      series: [{ label: 'A', data: [1] }, { label: 'B', data: [2], color: '#123456' }],
    })
    await flushPromises()

    const datasets = created[0]?.config.data.datasets
    expect(datasets?.[0]?.borderColor).toBeTruthy()
    expect(datasets?.[1]?.borderColor).toBe('#123456')
  })

  it('fills bars with a gradient and lines with the plain colour', async () => {
    mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ type: 'line', labels: ['a'], series: [{ label: 'A', data: [1] }] })
    await flushPromises()

    expect(typeof created[0]?.config.data.datasets[0]?.backgroundColor).toBe('function')
    expect(typeof created[1]?.config.data.datasets[0]?.backgroundColor).toBe('string')
  })

  it('draws an optional dashed target line across a vertical chart', async () => {
    mountChart({ type: 'bar', labels: ['a', 'b'], series: [{ label: 'A', data: [1, 2] }], target: 5 })
    await flushPromises()

    const datasets = created[0]?.config.data.datasets
    expect(datasets).toHaveLength(2)
    expect(datasets?.[1]).toMatchObject({ type: 'line', label: 'Target', data: [5, 5], borderDash: [4, 4], borderWidth: 1 })
  })

  it('rounds bars as the concept does and fades the ones below the target', async () => {
    mountChart({ type: 'bar', labels: ['a', 'b'], series: [{ label: 'A', data: [1, 9] }], target: 5 })
    await flushPromises()

    const dataset = created[0]?.config.data.datasets[0] as { borderRadius?: unknown, maxBarThickness?: number, backgroundColor?: (context: unknown) => unknown }
    expect(dataset.borderRadius).toEqual({ topLeft: 8, topRight: 8, bottomLeft: 3, bottomRight: 3 })
    expect(dataset.maxBarThickness).toBe(28)
    expect(dataset.backgroundColor?.({ raw: 1, chart: {} })).toBe('rgb(124 160 255 / 0.28)')
  })

  it('drops the value axis and adds the glow in the minimal look', async () => {
    mountChart({ type: 'bar', labels: ['a', 'b'], series: [{ label: 'A', data: [1, 9] }], target: 5, variant: 'minimal' })
    await flushPromises()

    const options = created[0]?.config.options as unknown as { scales: { y: { display: boolean } }, plugins: { barGlow: unknown } }
    expect(options.scales.y.display).toBe(false)
    expect(options.plugins.barGlow).toMatchObject({ target: 5 })
  })

  it('keeps the value axis and draws no glow in the default look', async () => {
    mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }], target: 5 })
    await flushPromises()

    const options = created[0]?.config.options as unknown as { scales: { y: { display: boolean } }, plugins: { barGlow: unknown } }
    expect(options.scales.y.display).toBe(true)
    expect(options.plugins.barGlow).toBe(false)
  })

  it('sizes the plot by the height option', async () => {
    const wrapper = mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }], height: 'short' })
    await flushPromises()

    expect(wrapper.classes()).toContain('h-52')
  })

  it('draws placeholder bars and builds no chart while loading', async () => {
    const wrapper = mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }], loading: true })
    await flushPromises()

    expect(wrapper.find('[data-testid="chart-skeleton"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(20)
    expect(wrapper.find('canvas').exists()).toBe(false)
    expect(created).toHaveLength(0)
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })

  it('names the target line as the screen asks', async () => {
    mountChart({ type: 'line', labels: ['a'], series: [{ label: 'A', data: [1] }], target: 3, targetLabel: 'Goal' })
    await flushPromises()

    expect(created[0]?.config.data.datasets[1]?.label).toBe('Goal')
  })

  it('draws no target line without a target, on a horizontal chart or on a pie', async () => {
    mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }], target: 5, horizontal: true })
    mountChart({ type: 'pie', labels: ['a'], series: [{ label: 'A', data: [1] }], target: 5 })
    await flushPromises()

    expect(created.map(chart => chart.config.data.datasets.length)).toEqual([1, 1, 1])
  })

  it('colours each slice of a pie and drops the axes', async () => {
    mountChart({ type: 'pie', labels: ['x', 'y', 'z'], series: [{ label: 'Share', data: [1, 2, 3] }] })
    await flushPromises()

    const config = created[0]?.config
    expect(config?.data.datasets[0]?.backgroundColor).toHaveLength(3)
    expect(config?.options.scales).toEqual({})
    expect(config?.options.plugins.legend.display).toBe(true)
  })

  it('shows the legend only when it says something', async () => {
    mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }] })
    mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }], legend: true })
    await flushPromises()

    expect(created[0]?.config.options.plugins.legend.display).toBe(false)
    expect(created[1]?.config.options.plugins.legend.display).toBe(true)
  })

  it('formats tooltip and axis numbers with the formatter', async () => {
    mountChart({
      type: 'bar',
      labels: ['a'],
      series: [{ label: 'Income', data: [5] }],
      valueFormatter: (v: number) => `$${v}`,
    })
    await flushPromises()

    const options = created[0]?.config.options
    expect(options?.plugins.tooltip.callbacks.label({ raw: 5, label: 'a', dataset: { label: 'Income' } })).toBe('Income: $5')
    expect(options?.scales.y?.ticks.callback(10)).toBe('$10')
  })

  it('draws bars horizontally and stacks them on request', async () => {
    mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }], horizontal: true, stacked: true })
    await flushPromises()

    const options = created[0]?.config.options
    expect(options?.indexAxis).toBe('y')
    expect(options?.scales.x?.stacked).toBe(true)
  })

  it('shows the empty state and builds no chart without data', async () => {
    const wrapper = mountChart({ type: 'bar', labels: [], series: [] })
    await flushPromises()

    expect(wrapper.get('[data-testid="chart-empty"]').text()).toBe('No data to chart')
    expect(wrapper.find('canvas').exists()).toBe(false)
    expect(created).toHaveLength(0)
  })

  it('rebuilds when the data changes and destroys on unmount', async () => {
    const wrapper = mountChart({ type: 'bar', labels: ['a'], series: [{ label: 'A', data: [1] }] })
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
