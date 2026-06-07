<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import AppSelect from '../../../components/AppSelect.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface Row {
  countryName: string
  countryCode: string
  vatRate: number
  numberOfInvoices: number
  totalExclVat: number
  totalVatCollected: number
  currency: string
}
interface ReportData { periodLabel: string; rows: Row[] }

const data = ref<ReportData | null>(null)

const now = new Date()
const currentQuarter = Math.floor(now.getMonth() / 3) + 1
const selectedYear = ref(now.getFullYear())
const selectedQuarter = ref(currentQuarter)
const selectedCurrency = ref('USD')

const rates: Record<string, number> = { USD: 1, EUR: 0.92, RUB: 90.5, AMD: 387 }
const symbols: Record<string, string> = { USD: '$', EUR: '€', RUB: '₽', AMD: '֏' }

// Build quarter options for last 4 years
const quarterOptions = computed(() => {
  const opts = []
  const qNames = ['January - March', 'April - June', 'July - September', 'October - December']
  for (let y = now.getFullYear(); y >= now.getFullYear() - 3; y--) {
    for (let q = 4; q >= 1; q--) {
      opts.push({ value: `${y}-${q}`, label: `${y} Q${q} - ${qNames[q - 1]}` })
    }
  }
  return opts
})

const selectedQuarterValue = computed({
  get: () => `${selectedYear.value}-${selectedQuarter.value}`,
  set: (v: string) => {
    const [y, q] = v.split('-')
    selectedYear.value = parseInt(y)
    selectedQuarter.value = parseInt(q)
  },
})

function fmt(n: number) {
  const v = n * rates[selectedCurrency.value]
  return `${symbols[selectedCurrency.value]}${v.toFixed(2)}`
}

async function load() {
  loading.value = true; error.value = null
  try {
    data.value = await request<ReportData>(
      `/reports/vat-moss?year=${selectedYear.value}&quarter=${selectedQuarter.value}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

const totals = computed(() => {
  if (!data.value) return null
  return {
    invoices: data.value.rows.reduce((s, r) => s + r.numberOfInvoices, 0),
    excl: data.value.rows.reduce((s, r) => s + r.totalExclVat, 0),
    vat: data.value.rows.reduce((s, r) => s + r.totalVatCollected, 0),
  }
})

onMounted(load)
</script>

<template>
  <ReportPage
    title="VAT MOSS Settlement Data"
    description="This report provides the information needed to complete a VATMOSS return. Please check with your tax authority to confirm how you can upload your settlement data into the MOSS portal."
    :loading :error>

    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3 flex-wrap">
          <div class="w-[280px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Select Quarter</label>
            <AppSelect v-model="selectedQuarterValue" :options="quarterOptions" />
          </div>
          <button @click="load" class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90">
            Generate Report
          </button>
        </div>
        <div v-if="data" class="mt-3 flex gap-2 text-[0.78rem] text-text-muted">
          <span>Choose Currency:</span>
          <button v-for="c in ['AMD','EUR','RUB','USD']" :key="c"
            @click="selectedCurrency = c"
            :class="selectedCurrency === c ? 'text-accent font-semibold' : 'hover:text-text-primary'"
            class="transition-colors">{{ c }}</button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <p v-if="data.periodLabel" class="text-[0.85rem] text-text-secondary mb-4 font-medium">
        {{ data.periodLabel }}
      </p>

      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden mb-4">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Country Name</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Country Code</th>
              <th class="px-4 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">VAT Rate</th>
              <th class="px-4 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Number of Invoices</th>
              <th class="px-4 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total Value Invoiced (Excl. VAT)</th>
              <th class="px-4 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Total VAT Collected</th>
              <th class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Currency</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="data.rows.length === 0">
              <td colspan="7" class="px-4 py-8 text-center text-text-secondary">No Data Found For This Report.</td>
            </tr>
            <tr v-for="row in data.rows" :key="`${row.countryCode}-${row.vatRate}`"
              class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
              <td class="px-4 py-2.5 text-text-primary font-medium">{{ row.countryName }}</td>
              <td class="px-4 py-2.5 font-mono text-text-muted">{{ row.countryCode }}</td>
              <td class="px-4 py-2.5 text-right text-text-secondary">{{ row.vatRate }}%</td>
              <td class="px-4 py-2.5 text-right text-text-secondary">{{ row.numberOfInvoices }}</td>
              <td class="px-4 py-2.5 text-right font-mono text-text-secondary">{{ fmt(row.totalExclVat) }}</td>
              <td class="px-4 py-2.5 text-right font-mono text-text-primary font-medium">{{ fmt(row.totalVatCollected) }}</td>
              <td class="px-4 py-2.5 text-text-muted">{{ row.currency }}</td>
            </tr>

            <!-- Totals row -->
            <tr v-if="totals && data.rows.length > 0" class="border-t border-border bg-white/[0.02] font-semibold">
              <td colspan="3" class="px-4 py-2.5 text-text-muted text-[0.78rem] uppercase tracking-wide">Totals</td>
              <td class="px-4 py-2.5 text-right text-text-primary">{{ totals.invoices }}</td>
              <td class="px-4 py-2.5 text-right font-mono text-text-primary">{{ fmt(totals.excl) }}</td>
              <td class="px-4 py-2.5 text-right font-mono text-text-primary">{{ fmt(totals.vat) }}</td>
              <td class="px-4 py-2.5" />
            </tr>
          </tbody>
        </table>
      </div>

      <div class="text-[0.75rem] text-text-muted space-y-1">
        <p>* If a country does not appear in the report, then no VAT was collected from customers in that country during the period selected.</p>
        <p>Isle of Man (GB) and Monaco (FR) are listed in this report as EU Overseas Territories of their respective countries and should be included in any figures provided to tax authorities.</p>
      </div>
    </template>

    <ReportTimestamp />
  </ReportPage>
</template>
