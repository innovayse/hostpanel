<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ReportPage from '../components/ReportPage.vue'
import ReportTimestamp from '../components/ReportTimestamp.vue'
import AppCheckbox from '../../../components/AppCheckbox.vue'
import { useApi } from '../../../composables/useApi'

const { request } = useApi()
const loading = ref(false)
const error = ref<string | null>(null)
const includeActive = ref(true)

interface RetentionRow { productName: string; billingCycle: string; productCount: number; avgDaysActive: number; avgYearsMonthsActive: string }
interface RetentionGroup { groupName: string; rows: RetentionRow[] }
interface ReportData { groups: RetentionGroup[] }

const data = ref<ReportData | null>(null)

async function load() {
  loading.value = true; error.value = null
  try {
    data.value = await request<ReportData>(`/reports/customer-retention?includeActive=${includeActive.value}`)
  } catch { error.value = 'Failed to load report.' } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <ReportPage
    title="Average Customer Retention Time"
    description="This report calculates and provides you with the average lifetime of products, services, addons and domains — that is the number of days between the registration date and the termination date. Averages are displayed by product and the associated billing cycle, and are displayed both as a number of days value and a years/months active."
    :loading :error>
    <template v-if="data">
      <label class="flex items-start gap-2 mb-4 cursor-pointer select-none">
        <AppCheckbox v-model="includeActive" @update:modelValue="load" class="mt-0.5 shrink-0" />
        <span class="text-[0.82rem] text-text-secondary">Include Active Products &amp; Services (Assuming active will end Next Due Date) in Calculation of Average Retention Time</span>
      </label>
      <div class="bg-surface-card border border-border rounded-2xl overflow-hidden">
        <table class="w-full text-[0.82rem]">
          <thead>
            <tr class="border-b border-border bg-white/[0.02]">
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Product Name</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Billing Cycle</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Product Count</th>
              <th class="px-4 py-3 text-center text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Average Days Active</th>
              <th class="px-4 py-3 text-left   text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Average Years/Months Active</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="group in data.groups" :key="group.groupName">
              <!-- Group header row -->
              <tr class="border-b border-border bg-white/[0.04]">
                <td colspan="5" class="px-4 py-2 text-[0.78rem] font-bold text-text-primary">{{ group.groupName }}</td>
              </tr>
              <!-- Data rows -->
              <tr v-for="row in group.rows" :key="row.productName + row.billingCycle"
                class="border-b border-border last:border-0 hover:bg-white/[0.02] transition-colors">
                <td class="px-4 py-2.5 text-text-primary pl-8">{{ row.productName }}</td>
                <td class="px-4 py-2.5 text-text-secondary">{{ row.billingCycle }}</td>
                <td class="px-4 py-2.5 text-center text-text-secondary">{{ row.productCount }}</td>
                <td class="px-4 py-2.5 text-center font-mono text-text-secondary">{{ row.avgDaysActive }}</td>
                <td class="px-4 py-2.5 text-text-primary font-medium">{{ row.avgYearsMonthsActive }}</td>
              </tr>
            </template>
            <tr v-if="data.groups.length === 0">
              <td colspan="5" class="px-4 py-8 text-center text-text-secondary">No data found.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
    <ReportTimestamp />
  </ReportPage>
</template>
