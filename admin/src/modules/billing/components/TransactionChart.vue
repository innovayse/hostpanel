<script setup lang="ts">
import { computed } from 'vue'
import type { Transaction } from '../../../types/models'

interface Props {
  transactions: Transaction[]
}

const props = defineProps<Props>()

// Group transactions by date and calculate daily net revenue
const chartData = computed(() => {
  const dailyData: Record<string, number> = {}

  props.transactions.forEach(tx => {
    const parsed = new Date(tx.date)
    if (isNaN(parsed.getTime())) return
    const date = parsed.toISOString().split('T')[0]
    if (!dailyData[date]) {
      dailyData[date] = 0
    }
    // Net = money in − money out − fees
    dailyData[date] += (tx.amountIn || 0) - (tx.amountOut || 0) - (tx.fees || 0)
  })

  // Get last 30 days
  const today = new Date()
  const dates: string[] = []
  for (let i = 29; i >= 0; i--) {
    const date = new Date(today)
    date.setDate(date.getDate() - i)
    dates.push(date.toISOString().split('T')[0])
  }

  return dates.map(date => ({
    date,
    value: dailyData[date] || 0
  }))
})

const maxValue = computed(() => {
  const values = chartData.value.map(d => Math.abs(d.value))
  return Math.max(...values, 1) * 1.1 // Add 10% padding
})

const chartWidth = 900
const chartHeight = 350
const padding = { top: 20, right: 20, bottom: 40, left: 80 }

const innerWidth = chartWidth - padding.left - padding.right
const innerHeight = chartHeight - padding.top - padding.bottom

const points = computed(() => {
  if (chartData.value.length <= 1) return []
  return chartData.value.map((d, i) => ({
    x: padding.left + (i / (chartData.value.length - 1)) * innerWidth,
    y: padding.top + innerHeight - (d.value / maxValue.value) * innerHeight,
    value: d.value
  }))
})

const pathD = computed(() => {
  if (points.value.length === 0) return ''

  let path = `M ${points.value[0].x} ${points.value[0].y}`

  for (let i = 1; i < points.value.length; i++) {
    const curr = points.value[i]
    const prev = points.value[i - 1]
    const cp1x = prev.x + (curr.x - prev.x) / 2
    const cp1y = prev.y
    const cp2x = prev.x + (curr.x - prev.x) / 2
    const cp2y = curr.y
    path += ` C ${cp1x} ${cp1y}, ${cp2x} ${cp2y}, ${curr.x} ${curr.y}`
  }

  return path
})

const areaPathD = computed(() => {
  if (pathD.value === '') return ''
  const lastPoint = points.value[points.value.length - 1]
  return `${pathD.value} L ${lastPoint.x} ${padding.top + innerHeight} L ${padding.left} ${padding.top + innerHeight} Z`
})

const yAxisLabels = computed(() => {
  const labels = []
  const max = maxValue.value

  // Calculate nice step size
  let stepSize = 1
  if (max > 100) stepSize = Math.pow(10, Math.floor(Math.log10(max / 5)))

  for (let i = 0; i <= 4; i++) {
    const value = (i / 4) * max
    const roundedValue = Math.round(value / stepSize) * stepSize
    labels.push({
      value: Math.round(roundedValue),
      y: padding.top + innerHeight - (i / 4) * innerHeight
    })
  }
  return labels
})

const yAxisLabelRotation = computed(() => {
  const cy = padding.top + innerHeight / 2
  return `rotate(-90 15 ${cy})`
})

const xAxisLabels = computed(() => {
  if (chartData.value.length === 0) return []
  const labels = []
  const step = Math.max(1, Math.floor(chartData.value.length / 4))
  for (let i = 0; i < chartData.value.length; i += step) {
    const date = new Date(chartData.value[i].date)
    const x = padding.left + (i / (chartData.value.length - 1)) * innerWidth
    labels.push({
      date: date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
      x
    })
  }
  return labels
})
</script>

<template>
  <div class="w-full">
    <div v-if="!chartData || chartData.length === 0" class="h-48 flex items-center justify-center text-text-muted text-sm">
      No data available
    </div>
    <svg v-else
      :width="chartWidth"
      :height="chartHeight"
      class="w-full"
      viewBox="0 0 900 350"
      preserveAspectRatio="xMidYMid meet"
    >
      <!-- Grid -->
      <g class="opacity-20">
        <line
          v-for="(label, i) in yAxisLabels"
          :key="`grid-${i}`"
          :x1="padding.left"
          :y1="label.y"
          :x2="chartWidth - padding.right"
          :y2="label.y"
          stroke="currentColor"
          stroke-width="0.5"
        />
      </g>

      <!-- Y-axis label -->
      <text
        :x="15"
        :y="padding.top + innerHeight / 2"
        text-anchor="middle"
        font-size="12"
        font-weight="500"
        fill="currentColor"
        class="opacity-70"
        :transform="yAxisLabelRotation"
      >
        Net Revenue (USD)
      </text>

      <!-- Y-axis labels -->
      <text
        v-for="(label, i) in yAxisLabels"
        :key="`label-${i}`"
        :x="padding.left - 10"
        :y="label.y + 4"
        text-anchor="end"
        font-size="11"
        fill="currentColor"
        class="opacity-60"
      >
        {{ label.value > 1000 ? (label.value / 1000).toFixed(0) + 'k' : label.value }}
      </text>

      <!-- X-axis -->
      <line
        :x1="padding.left"
        :y1="padding.top + innerHeight"
        :x2="chartWidth - padding.right"
        :y2="padding.top + innerHeight"
        stroke="currentColor"
        stroke-width="1"
        class="opacity-30"
      />

      <!-- X-axis labels -->
      <text
        v-for="(label, i) in xAxisLabels"
        :key="`x-label-${i}`"
        :x="label.x"
        :y="padding.top + innerHeight + 20"
        text-anchor="middle"
        font-size="11"
        fill="currentColor"
        class="opacity-60"
      >
        {{ label.date }}
      </text>

      <!-- Y-axis -->
      <line
        :x1="padding.left"
        :y1="padding.top"
        :x2="padding.left"
        :y2="padding.top + innerHeight"
        stroke="currentColor"
        stroke-width="1"
        class="opacity-30"
      />

      <!-- Area -->
      <path
        :d="areaPathD"
        fill="#10b981"
        fill-opacity="0.2"
      />

      <!-- Line -->
      <path
        :d="pathD"
        stroke="#10b981"
        stroke-width="2"
        fill="none"
        stroke-linecap="round"
        stroke-linejoin="round"
      />

      <!-- Points -->
      <circle
        v-for="(point, i) in points"
        :key="`point-${i}`"
        :cx="point.x"
        :cy="point.y"
        r="3"
        fill="#10b981"
        class="opacity-0 hover:opacity-100 transition-opacity"
      />
    </svg>
  </div>
</template>
