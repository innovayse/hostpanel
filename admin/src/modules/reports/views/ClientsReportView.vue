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

interface ClientRow {
  id: number; firstName: string; lastName: string; companyName: string | null
  email: string; address1: string | null; address2: string | null; city: string | null
  state: string | null; postCode: string | null; country: string | null; phone: string | null
  currency: string | null; creditBalance: number; status: string; createdAt: string; notes: string | null
}
const rows = ref<ClientRow[]>([])
const totalCount = ref(0)

const filterFields: FieldOption[] = [
  { value: 'firstName', label: 'First Name' },
  { value: 'lastName', label: 'Last Name' },
  { value: 'companyName', label: 'Company Name' },
  { value: 'email', label: 'Email' },
  { value: 'country', label: 'Country' },
  { value: 'city', label: 'City' },
  { value: 'phone', label: 'Phone Number' },
  { value: 'currency', label: 'Currency' },
  { value: 'status', label: 'Status' },
]

const activeFilters = ref<FilterRow[]>([])
const createdRange = ref<[string, string] | null>(null)
const page = ref(1); const pageSize = 50

const allColumns = [
  { key: 'id', label: 'ID' },
  { key: 'firstName', label: 'First Name' },
  { key: 'lastName', label: 'Last Name' },
  { key: 'companyName', label: 'Company Name' },
  { key: 'email', label: 'Email' },
  { key: 'address1', label: 'Address 1' },
  { key: 'address2', label: 'Address 2' },
  { key: 'city', label: 'City' },
  { key: 'state', label: 'State' },
  { key: 'postCode', label: 'Postcode' },
  { key: 'country', label: 'Country' },
  { key: 'phone', label: 'Phone Number' },
  { key: 'currency', label: 'Currency' },
  { key: 'creditBalance', label: 'Credit' },
  { key: 'status', label: 'Status' },
  { key: 'createdAt', label: 'Creation Date' },
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

function cellValue(row: ClientRow, key: ColKey): string {
  const v = row[key]
  if (v == null || v === '') return '—'
  if (key === 'creditBalance') return `$${(v as number).toFixed(2)}`
  return String(v)
}

const statusBg = (s: string) => ({
  Active: 'bg-status-green/10 text-status-green',
  Inactive: 'bg-gray-500/10 text-gray-400',
  Suspended: 'bg-status-red/10 text-status-red',
}[s] ?? 'text-text-secondary')

function buildParams(): string {
  const p = new URLSearchParams()
  const statusFilter = activeFilters.value.find(f => f.field === 'status')
  if (statusFilter) p.set('status', statusFilter.value)
  const countryFilter = activeFilters.value.find(f => f.field === 'country')
  if (countryFilter) p.set('country', countryFilter.value)
  if (createdRange.value) { p.set('createdFrom', createdRange.value[0]); p.set('createdTo', createdRange.value[1]) }
  p.set('page', String(page.value))
  p.set('pageSize', String(pageSize))
  return p.toString()
}

function applyLocalFilters(items: ClientRow[]): ClientRow[] {
  return items.filter(row => {
    for (const f of activeFilters.value) {
      if (f.field === 'status' || f.field === 'country') continue
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
    const data = await request<{ items: ClientRow[]; totalCount: number }>(`/reports/clients?${buildParams()}`)
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
  <ReportPage title="Clients" description="This report can be used to generate a custom export of clients by applying up to 5 filters. CSV Export is available via the Tools menu to the right." :loading :error>
    <template #filters>
      <FilterCard>
        <template #fields>
          <div class="grid grid-cols-2 gap-3 items-end">
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Fields to Include</label>
              <FieldSelector :fields="allColumns" :selected="visibleCols" @toggle="toggleCol" @select-all="selectAllCols" @clear-all="clearAllCols" />
            </div>
            <div>
              <label class="block text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted mb-1">Creation Date Range</label>
              <DateRangePicker v-model="createdRange" />
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

    <div class="text-[0.78rem] text-text-secondary mb-3">{{ totalCount }} client(s) found</div>

    <div class="bg-surface-card border border-border rounded-2xl overflow-x-auto">
      <table class="w-full text-[0.82rem]">
        <thead>
          <tr class="border-b border-border bg-white/[0.02]">
            <th v-for="col in activeCols" :key="col.key" class="px-4 py-3 text-left text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted whitespace-nowrap">{{ col.label }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="rows.length === 0 && !loading">
            <td :colspan="activeCols.length || 1" class="px-4 py-8 text-center text-text-secondary">No clients found. Click Filter to load results.</td>
          </tr>
          <tr v-for="row in rows" :key="row.id" class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
            <td v-for="col in activeCols" :key="col.key" class="px-4 py-3 whitespace-nowrap">
              <span v-if="col.key === 'status'" :class="['text-[0.72rem] px-2 py-0.5 rounded-full font-medium', statusBg(row.status)]">{{ row.status }}</span>
              <span v-else-if="col.key === 'id'" class="text-text-muted font-mono">#{{ row.id }}</span>
              <span v-else-if="col.key === 'email'" class="text-primary-400">{{ row.email }}</span>
              <span v-else :class="['firstName','lastName','companyName'].includes(col.key) ? 'text-text-primary font-medium' : 'text-text-secondary'">{{ cellValue(row, col.key) }}</span>
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
