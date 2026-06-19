<template>
  <div>
    <div class="mb-6 flex items-center justify-between gap-4 flex-wrap">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ $t('client.domains.title') }}</h1>
        <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.domains.subtitle') }}</p>
      </div>
      <NuxtLink
        to="/domains"
        class="px-5 py-2.5 rounded-xl bg-cyan-500/10 border border-cyan-500/20 text-cyan-400 text-sm font-medium hover:bg-cyan-500/20 transition-colors flex items-center gap-2"
      >
        <Plus :size="16" :stroke-width="2" />
        {{ $t('client.domains.registerNew') }}
      </NuxtLink>
    </div>

    <!-- Filter tabs -->
    <UiTabs
      :tabs="tabs.map(t => ({ value: t.key, label: t.label, badge: t.count }))"
      v-model="activeTab"
      class="mb-6"
    />

    <!-- Loading -->
    <div v-if="store.domainsLoading" class="space-y-3">
      <div v-for="i in 4" :key="i" class="h-20 rounded-xl bg-white/5 border border-white/10 animate-pulse" />
    </div>

    <template v-else>
      <!-- Toolbar -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-4">
        <p class="text-sm text-gray-500 dark:text-gray-400">
          {{ $t('ui.pagination.showing', { from: pageFrom, to: pageTo, total: filtered.length }) }}
        </p>
        <UiSearchInput
          v-model="search"
          :placeholder="$t('client.domains.search')"
          class="sm:w-64"
          @update:model-value="page = 1"
        />
      </div>

      <!-- Empty -->
      <div v-if="!filtered.length" class="text-center py-20">
        <Globe :size="48" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-4" />
        <p class="text-gray-500 dark:text-gray-400 mb-4">{{ $t('client.domains.empty') }}</p>
        <NuxtLink
          v-if="!search"
          to="/domains"
          class="px-6 py-2.5 rounded-xl bg-cyan-500 text-white font-semibold text-sm hover:bg-cyan-400 transition-colors"
        >
          {{ $t('client.domains.searchDomains') }}
        </NuxtLink>
      </div>

      <!-- Domains table -->
      <UiTable v-else>
        <UiTableHead>
          <UiTableRow :hoverable="false">
            <UiTableTh>{{ $t('client.domains.colDomain') }}</UiTableTh>
            <UiTableTh class="hidden lg:table-cell">{{ $t('client.domains.colRegistrationDate') }}</UiTableTh>
            <UiTableTh class="hidden md:table-cell">{{ $t('client.domains.colNextDueDate') }}</UiTableTh>
            <UiTableTh align="center">{{ $t('client.domains.colStatus') }}</UiTableTh>
            <UiTableTh align="right">{{ $t('client.domains.colActions') }}</UiTableTh>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody>
          <UiTableRow
            v-for="domain in paged"
            :key="domain.id"
            group
          >
            <UiTableTd>
              <div class="flex items-center gap-3">
                <Globe :size="16" :stroke-width="2" class="text-primary-400 flex-shrink-0" />
                <span class="text-gray-900 dark:text-white font-medium text-sm">{{ domain.name }}</span>
              </div>
            </UiTableTd>
            <UiTableTd class="text-gray-500 dark:text-gray-400 hidden lg:table-cell text-sm">{{ formatExpiry(domain.registeredAt) }}</UiTableTd>
            <UiTableTd class="hidden md:table-cell">
              <div class="text-sm" :class="isExpiringSoon(domain.expiresAt) ? 'text-orange-400 font-medium' : 'text-gray-500 dark:text-gray-400'">
                {{ formatExpiry(domain.expiresAt) }}
                <span v-if="isExpiringSoon(domain.expiresAt)" class="ml-2 text-xs text-orange-400">({{ $t('client.domains.expiringSoon') }})</span>
              </div>
            </UiTableTd>
            <UiTableTd align="center">
              <ClientStatusBadge :status="domain.status" />
            </UiTableTd>
            <UiTableTd align="right">
              <NuxtLink
                :to="`/client/domains/${domain.id}`"
                class="px-3 py-1.5 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-xs hover:border-cyan-500/30 hover:text-gray-900 dark:hover:text-white transition-all"
              >
                {{ $t('client.domains.manage') }}
              </NuxtLink>
            </UiTableTd>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <UiPagination
        v-if="filtered.length > 0"
        v-model="page"
        v-model:per-page="perPage"
        :total-pages="totalPages"
      />
    </template>
  </div>
</template>

<script setup lang="ts">
import { Globe, Plus } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const store = useClientStore()

await useAsyncData('client-domains', () => store.fetchDomains())

// ── Filters ───────────────────────────────────────────────────────────────────
const activeTab = ref('all')
const search    = ref('')
const page      = ref(1)
const perPage   = ref(10)

watch([activeTab, search], () => { page.value = 1 })

const tabs = computed(() => [
  { key: 'all',     label: t('client.domains.tabAll'),     count: store.domains.length },
  { key: 'Active',  label: t('client.domains.tabActive'),  count: store.domains.filter(d => d.status === 'Active').length },
  { key: 'Expired', label: t('client.domains.tabExpired'), count: store.domains.filter(d => d.status === 'Expired').length }
])

const filtered = computed(() => {
  let list = activeTab.value === 'all'
    ? store.domains
    : store.domains.filter(d => d.status === activeTab.value)
  const q = search.value.trim().toLowerCase()
  if (q) list = list.filter(d => d.name.toLowerCase().includes(q))
  return list
})

const totalPages = computed(() => Math.max(1, Math.ceil(filtered.value.length / perPage.value)))
const paged      = computed(() => filtered.value.slice((page.value - 1) * perPage.value, page.value * perPage.value))
const pageFrom   = computed(() => filtered.value.length === 0 ? 0 : (page.value - 1) * perPage.value + 1)
const pageTo     = computed(() => Math.min(page.value * perPage.value, filtered.value.length))

/** Checks whether the given date string represents a valid date. */
function isValidDate(d: string): boolean {
  return !!d && !d.startsWith('0000') && !isNaN(new Date(d).getTime())
}

/**
 * Formats a date string for display, returning a dash for invalid dates.
 *
 * @param d - ISO 8601 date string.
 * @returns Formatted date or em-dash placeholder.
 */
function formatExpiry(d: string): string {
  if (!isValidDate(d)) return '—'
  return new Date(d).toLocaleDateString()
}

/**
 * Checks whether a domain expires within the next 30 days.
 *
 * @param expiryDate - ISO 8601 expiration date string.
 * @returns True if the domain expires within 30 days.
 */
function isExpiringSoon(expiryDate: string): boolean {
  if (!isValidDate(expiryDate)) return false
  const daysLeft = (new Date(expiryDate).getTime() - Date.now()) / (1000 * 60 * 60 * 24)
  return daysLeft >= 0 && daysLeft <= 30
}
</script>