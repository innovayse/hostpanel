<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import FilterCard from '../../../components/FilterCard.vue'
import AdvancedFilters, { type FilterRow, type FieldOption } from '../../../components/AdvancedFilters.vue'
import FieldSelector from '../../../components/FieldSelector.vue'
import DateRangePicker from '../../../components/DateRangePicker.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)

interface DomainRow {
  id: number; clientId: number; clientName: string; orderId: number | null; orderType: string
  domainName: string; firstPaymentAmount: number; recurringAmount: number
  registrationPeriod: number; registrationDate: string; expiryDate: string
  nextDueDate: string; registrar: string | null; paymentMethod: string | null
  status: string; notes: string | null
}
const rows = ref<DomainRow[]>([])
const totalCount = ref(0)

const filterFields: FieldOption[] = [
  { value: 'domainName', label: 'Domain Name' },
  { value: 'clientName', label: 'Client Name' },
  { value: 'registrar', label: 'Registrar' },
  { value: 'paymentMethod', label: 'Payment Method' },
  { value: 'status', label: 'Status' },
  { value: 'orderType', label: 'Reg Type' },
]

const activeFilters = ref<FilterRow[]>([])
const registeredRange = ref<[string, string] | null>(null)
const expiryRange = ref<[string, string] | null>(null)
const nextDueRange = ref<[string, string] | null>(null)
const page = ref(1); const pageSize = 50

const allColumns = [
  { key: 'id', label: 'ID' },
  { key: 'clientId', label: 'User ID' },
  { key: 'clientName', label: 'Client Name' },
  { key: 'orderId', label: 'Order ID' },
  { key: 'orderType', label: 'Reg Type' },
  { key: 'domainName', label: 'Domain Name' },
  { key: 'firstPaymentAmount', label: 'First Payment Amount' },
  { key: 'recurringAmount', label: 'Recurring Amount' },
  { key: 'registrationPeriod', label: 'Registration Period' },
  { key: 'registrationDate', label: 'Registration Date' },
  { key: 'expiryDate', label: 'Expiry Date' },
  { key: 'nextDueDate', label: 'Next Due Date' },
  { key: 'registrar', label: 'Registrar' },
  { key: 'paymentMethod', label: 'Payment Method' },
  { key: 'status', label: 'Status' },
  { key: 'notes', label: 'Notes' },
] as const

type ColKey = typeof allColumns[number]['key']
const visibleCols = ref<Set<ColKey>>(new Set())

const activeCols = computed(() => allColumns.filter(c => visibleCols.value.has(c.key)))
const totalPages = computed(() => Math.ceil(totalCount.value / pageSize))

function toggleCol(key: string) {
  const k = key as ColKey
  if (visibleCols.value.has(k)) visibleCols.value.delete(k)
  else visibleCols.value.add(k)
}
function selectAllCols() { allColumns.forEach(c => visibleCols.value.add(c.key)) }
function clearAllCols() { visibleCols.value.clear() }

function cellValue(row: DomainRow, key: ColKey): string {
  const v = row[key]
  if (v == null || v === '') return '—'
  if (['firstPaymentAmount', 'recurringAmount'].includes(key)) return `$${(v as number).toFixed(2)}`
  if (key === 'registrationPeriod') return `${v} yr`
  return String(v)
}

const statusBg = (s: string) => ({
  Active: 'bg-status-green/10 text-status-green',
  Expired: 'bg-status-red/10 text-status-red',
  Pending: 'bg-status-yellow/10 text-status-yellow',
  Cancelled: 'bg-gray-500/10 text-gray-400',
  Transferred: 'bg-blue-500/10 text-blue-400',
}[s] ?? 'text-text-secondary')

function buildParams(): string {
  const p = new URLSearchParams()
  const statusF = activeFilters.value.find(f => f.field === 'status')
  if (statusF) p.set('status', statusF.value)
  const registrarF = activeFilters.value.find(f => f.field === 'registrar')
  if (registrarF) p.set('registrar', registrarF.value)
  if (registeredRange.value) { p.set('registeredFrom', registeredRange.value[0]); p.set('registeredTo', registeredRange.value[1]) }
  if (expiryRange.value) { p.set('expiresFrom', expiryRange.value[0]); p.set('expiresTo', expiryRange.value[1]) }
  if (nextDueRange.value) { p.set('nextDueFrom', nextDueRange.value[0]); p.set('nextDueTo', nextDueRange.value[1]) }
  p.set('page', String(page.value))
  p.set('pageSize', String(pageSize))
  return p.toString()
}

function applyLocalFilters(items: DomainRow[]): DomainRow[] {
  return items.filter(row => {
    for (const f of activeFilters.value) {
      if (['status', 'registrar'].includes(f.field)) continue
      const rowVal = String(row[f.field as ColKey] ?? '').toLowerCase()
      const search = f.value.toLowerCase()
      if (f.condition === 'exact' && rowVal !== search) return false
      if (f.condition === 'contains' && !rowVal.includes(search)) return false
    }
    return true
  })
}

async function load() {
  loading.value = true; error.value = null
  try {
    const data = await request<{ items: DomainRow[]; totalCount: number }>(`/reports/domains?${buildParams()}`)
    rows.value = applyLocalFilters(data.items)
    totalCount.value = data.totalCount
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

function applyFilters() { page.value = 1; load() }
function goPage(p: number) { page.value = p; load() }
function printReport() { window.print() }

onMounted(load)
</script>

<template>
  <ReportPage title="Domains" description="This report can be used to generate a custom export of domains by applying up to 5 filters. CSV Export is available via the Tools menu to the right." :loading :error>
    <template #filters>
      <FilterCard>
        <template #fields>
          <div class="flex flex-col gap-3">
            <!-- Row 1: Fields to Include + Registration Date Range -->
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Fields to Include</label>
                <FieldSelector :fields="allColumns" :selected="visibleCols" @toggle="toggleCol" @select-all="selectAllCols" @clear-all="clearAllCols" />
              </div>
              <div>
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Registration Date Range</label>
                <DateRangePicker v-model="registeredRange" />
              </div>
            </div>
            <!-- Row 2: Next Due Date Range + Expiry Date Range -->
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Next Due Date Range</label>
                <DateRangePicker v-model="nextDueRange" />
              </div>
              <div>
                <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Expiry Date Range</label>
                <DateRangePicker v-model="expiryRange" />
              </div>
            </div>
          </div>
        </template>

        <AdvancedFilters :fields="filterFields" @update:filters="activeFilters = $event" />

        <template #actions>
          <div class="flex items-center justify-center gap-3 pt-3 border-t border-border/50">
            <button class="px-5 py-2 gradient-brand text-white text-[0.82rem] font-semibold rounded-[9px] transition-opacity hover:opacity-90" @click="applyFilters">Generate</button>
            <button class="px-4 py-2 bg-white/[0.04] border border-border text-text-secondary text-[0.78rem] font-medium rounded-[9px] hover:bg-white/[0.08] transition-colors" @click="printReport">Print</button>
          </div>
        </template>
      </FilterCard>
    </template>

    <div class="text-[0.78rem] text-text-secondary mb-3">{{ totalCount }} domain(s) found</div>

    <div class="bg-surface-card border border-border rounded-2xl overflow-x-auto">
      <table class="w-full text-[0.82rem]">
        <thead>
          <tr class="border-b border-border bg-white/[0.02]">
            <th v-for="col in activeCols" :key="col.key" class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted whitespace-nowrap">{{ col.label }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0 && !loading">
            <td :colspan="activeCols.length || 1" class="px-4 py-8 text-center text-text-secondary">No domains found.</td>
          </tr>
          <tr v-for="row in rows" :key="row.id" class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
            <td v-for="col in activeCols" :key="col.key" class="px-4 py-3 whitespace-nowrap">
              <span v-if="col.key === 'status'" :class="['text-[0.72rem] px-2 py-0.5 rounded-full font-medium', statusBg(row.status)]">{{ row.status }}</span>
              <span v-else-if="col.key === 'id'" class="text-text-muted font-mono">#{{ row.id }}</span>
              <span v-else-if="col.key === 'domainName'" class="text-primary-400 font-medium">{{ row.domainName }}</span>
              <span v-else class="text-text-secondary">{{ cellValue(row, col.key) }}</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="totalPages > 1" class="flex items-center justify-center gap-2 mt-4">
      <button v-for="p in totalPages" :key="p"
        class="px-3 py-1 rounded-lg text-[0.78rem] transition-colors"
        :class="p === page ? 'bg-primary-500 text-white' : 'bg-white/[0.04] text-text-secondary hover:bg-white/[0.08]'"
        @click="goPage(p)">{{ p }}</button>
    </div>
    <ReportTimestamp />
</ReportPage>
</template>
