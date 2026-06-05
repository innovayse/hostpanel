<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import AppSelect from '../../../components/AppSelect.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

const now = new Date()
const selectedYear = ref(now.getFullYear())
const selectedMonth = ref(now.getMonth() + 1)
const selectedCurrency = ref('USD')

const monthOptions = ['January','February','March','April','May','June','July','August','September','October','November'].concat(['December'])
  .map((name, i) => ({ value: i + 1, label: name }))
const yearOptions = Array.from({ length: 5 }, (_, i) => now.getFullYear() - 2 + i).map(y => ({ value: y, label: String(y) }))
const currencyOptions = [
  { value: 'USD', label: 'USD — US Dollar' },
  { value: 'EUR', label: 'EUR — Euro' },
  { value: 'RUB', label: 'RUB — Russian Ruble' },
  { value: 'AMD', label: 'AMD — Armenian Dram' },
]
const rates: Record<string, number> = { USD: 1, EUR: 0.92, RUB: 90.5, AMD: 387 }
const symbols: Record<string, string> = { USD: '$', EUR: '€', RUB: '₽', AMD: '֏' }

interface ProductRow { productId: number; productName: string; unitsSold: number; totalIncome: number }
interface Group { groupName: string; products: ProductRow[]; groupUnitsSold: number; groupIncome: number }
interface ReportData { month: number; year: number; groups: Group[]; totalUnitsSold: number; totalIncome: number }

const data = ref<ReportData | null>(null)

const monthNames = ['January','February','March','April','May','June','July','August','September','October','November','December']
const reportTitle = computed(() => data.value
  ? `Income by Product for ${monthNames[data.value.month - 1]} ${data.value.year}`
  : 'Income by Product')

function fmt(n: number) {
  const converted = n * rates[selectedCurrency.value]
  return `${symbols[selectedCurrency.value]}${converted.toFixed(2)} ${selectedCurrency.value}`
}

async function load() {
  loading.value = true; error.value = null
  try {
    data.value = await request<ReportData>(`/reports/income-by-product-grouped?year=${selectedYear.value}&month=${selectedMonth.value}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage :title="reportTitle" description="This report gives a breakdown per product/service of income paid in a given month." :loading :error>
    <template #filters>
      <div class="bg-surface-card border border-border rounded-2xl p-4 mb-6">
        <div class="flex items-end gap-3">
          <div class="w-[160px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Month</label>
            <AppSelect v-model="selectedMonth" :options="monthOptions" />
          </div>
          <div class="w-[110px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Year</label>
            <AppSelect v-model="selectedYear" :options="yearOptions" />
          </div>
          <div class="w-[180px]">
            <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Currency</label>
            <AppSelect v-model="selectedCurrency" :options="currencyOptions" />
          </div>
          <button class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="load">Generate</button>
        </div>
      </div>
    </template>

    <template v-if="data">
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-5 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Product Name</th>
              <th class="px-5 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Units Sold</th>
              <th class="px-5 py-3 text-right text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Value</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="group in data.groups" :key="group.groupName">
              <tr class="bg-white/[0.03] border-b border-border">
                <td colspan="3" class="px-5 py-2 text-[0.75rem] font-semibold text-text-primary">{{ group.groupName }}</td>
              </tr>
              <tr v-for="product in group.products" :key="product.productId"
                class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
                <td class="px-5 py-2.5 pl-10 text-text-secondary">{{ product.productName }}</td>
                <td class="px-5 py-2.5 text-right" :class="product.unitsSold > 0 ? 'text-text-primary font-medium' : 'text-text-muted'">{{ product.unitsSold }}</td>
                <td class="px-5 py-2.5 text-right" :class="product.totalIncome > 0 ? 'text-status-green font-medium' : 'text-text-muted'">{{ fmt(product.totalIncome) }}</td>
              </tr>
              <tr class="border-b border-border bg-white/[0.02]">
                <td class="px-5 py-2 pl-10 text-[0.72rem] font-semibold text-text-muted">Sub Total</td>
                <td class="px-5 py-2 text-right text-[0.78rem] font-semibold text-text-primary">{{ group.groupUnitsSold }}</td>
                <td class="px-5 py-2 text-right text-[0.78rem] font-semibold text-text-primary">{{ fmt(group.groupIncome) }}</td>
              </tr>
            </template>
          </tbody>
          <tfoot>
            <tr class="border-t-2 border-border bg-white/[0.03]">
              <td class="px-5 py-3 text-[0.78rem] font-bold text-text-primary">Total</td>
              <td class="px-5 py-3 text-right text-[0.88rem] font-bold text-text-primary">{{ data.totalUnitsSold }}</td>
              <td class="px-5 py-3 text-right text-[0.88rem] font-bold text-status-green">{{ fmt(data.totalIncome) }}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </template>
    <ReportTimestamp />
</ReportPage>
</template>
