<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Bar } from 'vue-chartjs'
import { Chart as ChartJS, CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend } from 'chart.js'
import ReportPage from '../components/ReportPage.vue'
import WorldMap from '../components/WorldMap.vue'
import { useApi } from '../../../composables/useApi'

ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend)

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface CountryRow { country: string; clientCount: number; totalRevenue: number }
interface CityRow { city: string; country: string; clientCount: number }
const rows = ref<CountryRow[]>([])
const cities = ref<CityRow[]>([])
const selectedCountry = ref<string | null>(null)

const countryNames: Record<string, string> = {
  US: 'United States', GB: 'United Kingdom', AM: 'Armenia', ES: 'Spain',
  KR: 'South Korea', FR: 'France', AU: 'Australia', DE: 'Germany',
  CA: 'Canada', NL: 'Netherlands', JP: 'Japan', BR: 'Brazil',
  IN: 'India', CN: 'China', RU: 'Russia', IT: 'Italy', Unknown: 'Unknown',
}
function displayCountry(code: string) { return countryNames[code] ?? code }

/** Total clients across all countries. */
const totalClients = computed(() => rows.value.reduce((sum, r) => sum + r.clientCount, 0))

/** Top 10 countries, with selected country moved to first position. */
const top10 = computed(() => {
  const sorted = [...rows.value].sort((a, b) => b.clientCount - a.clientCount).slice(0, 10)
  if (selectedCountry.value) {
    const idx = sorted.findIndex(r => r.country === selectedCountry.value)
    if (idx > 0) {
      const [item] = sorted.splice(idx, 1)
      sorted.unshift(item)
    } else if (idx === -1) {
      // Selected country not in top 10 — find it in full list and prepend
      const found = rows.value.find(r => r.country === selectedCountry.value)
      if (found) sorted.unshift(found)
    }
  }
  return sorted
})

const chartData = computed(() => ({
  labels: top10.value.map(r => displayCountry(r.country)),
  datasets: [{
    label: 'Active Clients',
    data: top10.value.map(r => r.clientCount),
    backgroundColor: top10.value.map(r =>
      r.country === selectedCountry.value ? '#16a34a' : '#22c55e'
    ),
    borderRadius: 4,
  }],
}))

const chartOptions = {
  indexAxis: 'y' as const,
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { display: false } },
  scales: {
    x: { grid: { color: 'rgba(255,255,255,0.05)' }, ticks: { color: '#64748b' } },
    y: { grid: { display: false }, ticks: { color: '#94a3b8', font: { size: 12 } } },
  },
}

function onSelectCountry(country: string) {
  selectedCountry.value = selectedCountry.value === country ? null : country
}

async function load() {
  loading.value = true; error.value = null
  try {
    const [countryData, cityData] = await Promise.all([
      request<CountryRow[]>('/reports/clients-by-country'),
      request<CityRow[]>('/reports/clients-by-city'),
    ])
    rows.value = countryData
    cities.value = cityData
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage title="Clients by Country" description="This report shows the total number of active services per country, as well as total active unique clients per country in the table below." :loading :error>
    <!-- World Map with city dots -->
    <div class="mb-6">
      <WorldMap v-if="cities.length > 0" :cityData="cities" @select-country="onSelectCountry" />
    </div>

    <!-- Horizontal Bar chart — Top 10 -->
    <div class="bg-surface-card border border-border rounded-2xl p-6 mb-6">
      <div class="flex items-center justify-between mb-4">
        <span class="text-[0.78rem] text-text-secondary font-medium">Top 10 Countries</span>
        <span v-if="selectedCountry" class="text-[0.72rem] text-primary-400 cursor-pointer hover:underline" @click="selectedCountry = null">Clear selection</span>
      </div>
      <div :style="{ height: Math.max(200, top10.length * 36) + 'px' }">
        <Bar v-if="top10.length > 0" :data="chartData" :options="chartOptions" />
      </div>
    </div>

    <!-- Table -->
    <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
      <table class="w-full text-[0.82rem]">
        <thead>
          <tr class="border-b border-border bg-white/[0.02]">
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Country</th>
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Active Services</th>
            <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Active Clients</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0"><td colspan="3" class="px-5 py-8 text-center text-text-secondary">No data.</td></tr>
          <tr
            v-for="row in rows" :key="row.country"
            class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors"
            :class="row.country === selectedCountry ? 'bg-primary-500/5' : ''"
          >
            <td class="px-5 py-3 text-text-primary">{{ displayCountry(row.country) }}</td>
            <td class="px-5 py-3 text-text-secondary">—</td>
            <td class="px-5 py-3 text-text-primary font-medium">{{ row.clientCount }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Total clients -->
    <div class="mt-4 flex items-center justify-between">
      <div class="text-[0.82rem] text-text-secondary">
        Total Clients: <span class="font-semibold text-text-primary">{{ totalClients }}</span>
      </div>
      <div class="text-[0.68rem] text-text-muted">Report Generated at {{ new Date().toLocaleString() }}</div>
    </div>
  </ReportPage>
</template>
