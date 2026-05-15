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

    <!-- Action buttons toolbar -->
    <div class="flex flex-wrap items-center gap-2 mb-4 p-3 rounded-xl bg-gray-50 dark:bg-white/5 border border-gray-200 dark:border-white/10">
      <UiButton variant="subtle" size="sm" @click="handleAction(`${whmcsUrl}/clientarea.php?action=domaindetails&modop=custom&a=manage`)">
        <Network :size="14" :stroke-width="2" class="mr-1.5" />
        {{ $t('client.domains.actionNameservers') }}
      </UiButton>
      <UiButton variant="subtle" size="sm" @click="handleAction(`${whmcsUrl}/clientarea.php?action=contacts`)">
        <UserCog :size="14" :stroke-width="2" class="mr-1.5" />
        {{ $t('client.domains.actionContacts') }}
      </UiButton>
      <UiButton variant="subtle" size="sm" @click="handleAction(`${whmcsUrl}/cart.php?a=add&domain=renew`)">
        <RefreshCw :size="14" :stroke-width="2" class="mr-1.5" />
        {{ $t('client.domains.actionRenew') }}
      </UiButton>

      <!-- More dropdown -->
      <div ref="moreRef" class="relative">
        <UiButton variant="subtle" size="sm" @click="moreOpen = !moreOpen">
          {{ $t('client.domains.actionMore') }}
          <ChevronDown :size="14" :stroke-width="2" :class="moreOpen ? 'rotate-180' : ''" class="ml-1 transition-transform duration-200" />
        </UiButton>
        <Transition
          enter-active-class="transition duration-150 ease-out"
          enter-from-class="opacity-0 translate-y-1 scale-95"
          enter-to-class="opacity-100 translate-y-0 scale-100"
          leave-active-class="transition duration-100 ease-in"
          leave-from-class="opacity-100 translate-y-0 scale-100"
          leave-to-class="opacity-0 translate-y-1 scale-95"
        >
          <div
            v-if="moreOpen"
            class="absolute left-0 top-full mt-1.5 w-52 rounded-xl border border-gray-200 dark:border-white/15 bg-white dark:bg-[#0d0d14] shadow-xl shadow-black/10 dark:shadow-black/40 py-1 z-50"
          >
            <UiButton
              variant="ghost" size="sm"
              class="w-full !justify-start gap-3 !rounded-none px-4"
              @click="handleAction(`${whmcsUrl}/clientarea.php?action=domainshosting`); moreOpen = false"
            >
              <RefreshCcw :size="14" :stroke-width="2" class="text-gray-400 flex-shrink-0" />
              {{ $t('client.domains.actionAutoRenew') }}
            </UiButton>
            <UiButton
              variant="ghost" size="sm"
              class="w-full !justify-start gap-3 !rounded-none px-4"
              @click="handleAction(`${whmcsUrl}/clientarea.php?action=domainshosting`); moreOpen = false"
            >
              <Lock :size="14" :stroke-width="2" class="text-gray-400 flex-shrink-0" />
              {{ $t('client.domains.actionToggleLock') }}
            </UiButton>
          </div>
        </Transition>
      </div>
    </div>

    <!-- No-selection warning -->
    <Transition
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="opacity-0 -translate-y-1"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="opacity-100 translate-y-0"
      leave-to-class="opacity-0 -translate-y-1"
    >
      <UiAlert v-if="noSelectionWarning" :icon-size="16" class="mb-4 !py-3">
        {{ $t('client.domains.selectWarning') }}
      </UiAlert>
    </Transition>

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
          <span v-if="selected.size > 0" class="ml-2 text-primary-600 dark:text-primary-400 font-medium">
            ({{ $t('client.domains.selectedCount', { n: selected.size }) }})
          </span>
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
            <!-- Select-all checkbox -->
            <UiTableTh class="w-10">
              <input
                ref="selectAllRef"
                type="checkbox"
                :checked="allSelected"
                class="w-4 h-4 rounded border-gray-300 dark:border-gray-600 accent-primary-500 cursor-pointer"
                @change="toggleAll"
              />
            </UiTableTh>
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
            :class="selected.has(domain.id) ? 'bg-primary-50/50 dark:bg-primary-500/5' : ''"
          >
            <UiTableTd class="w-10">
              <input
                type="checkbox"
                :checked="selected.has(domain.id)"
                class="w-4 h-4 rounded border-gray-300 dark:border-gray-600 accent-primary-500 cursor-pointer"
                @change="toggleRow(domain.id)"
              />
            </UiTableTd>
            <UiTableTd>
              <div class="flex items-center gap-3">
                <Globe :size="16" :stroke-width="2" class="text-primary-400 flex-shrink-0" />
                <span class="text-gray-900 dark:text-white font-medium text-sm">{{ domain.domainname }}</span>
              </div>
            </UiTableTd>
            <UiTableTd class="text-gray-500 dark:text-gray-400 hidden lg:table-cell text-sm">{{ formatExpiry(domain.regdate) }}</UiTableTd>
            <UiTableTd class="hidden md:table-cell">
              <div class="text-sm" :class="isExpiringSoon(domain.nextduedate) ? 'text-orange-400 font-medium' : 'text-gray-500 dark:text-gray-400'">
                {{ formatExpiry(domain.nextduedate) }}
                <span v-if="isExpiringSoon(domain.nextduedate)" class="ml-2 text-xs text-orange-400">({{ $t('client.domains.expiringSoon') }})</span>
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
import { Globe, Plus, Network, UserCog, RefreshCw, RefreshCcw, ChevronDown, Lock } from 'lucide-vue-next'
import { onClickOutside } from '@vueuse/core'
import { useClientStore } from '~/stores/client'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const store = useClientStore()
const config = useRuntimeConfig()
const whmcsUrl = config.public.whmcsUrl

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
  if (q) list = list.filter(d => d.domainname.toLowerCase().includes(q))
  return list
})

const totalPages = computed(() => Math.max(1, Math.ceil(filtered.value.length / perPage.value)))
const paged      = computed(() => filtered.value.slice((page.value - 1) * perPage.value, page.value * perPage.value))
const pageFrom   = computed(() => filtered.value.length === 0 ? 0 : (page.value - 1) * perPage.value + 1)
const pageTo     = computed(() => Math.min(page.value * perPage.value, filtered.value.length))

// ── Bulk selection ────────────────────────────────────────────────────────────
const selected      = reactive(new Set<number>())
const selectAllRef  = ref<HTMLInputElement | null>(null)

const allSelected  = computed(() => paged.value.length > 0 && paged.value.every(d => selected.has(d.id)))
const someSelected = computed(() => paged.value.some(d => selected.has(d.id)) && !allSelected.value)

// Keep native checkbox indeterminate state in sync
watch(someSelected, (v) => {
  if (selectAllRef.value) selectAllRef.value.indeterminate = v
}, { immediate: true })

function toggleAll() {
  if (allSelected.value) {
    paged.value.forEach(d => selected.delete(d.id))
  } else {
    paged.value.forEach(d => selected.add(d.id))
  }
  noSelectionWarning.value = false
}

function toggleRow(id: number) {
  selected.has(id) ? selected.delete(id) : selected.add(id)
  noSelectionWarning.value = false
}

// Clear selection when filter/page changes
watch([activeTab, search, page], () => selected.clear())

// ── Actions ───────────────────────────────────────────────────────────────────
const noSelectionWarning = ref(false)
let warningTimer: ReturnType<typeof setTimeout> | null = null

function handleAction(url: string) {
  if (selected.size === 0) {
    noSelectionWarning.value = true
    if (warningTimer) clearTimeout(warningTimer)
    warningTimer = setTimeout(() => { noSelectionWarning.value = false }, 4000)
    return
  }
  noSelectionWarning.value = false
  const params = [...selected].map(id => `domainid[]=${id}`).join('&')
  window.open(`${url}&${params}`, '_blank')
}

// ── More dropdown ─────────────────────────────────────────────────────────────
const moreOpen = ref(false)
const moreRef  = ref<HTMLElement | null>(null)
onClickOutside(moreRef, () => { moreOpen.value = false })

function isValidDate(d: string): boolean {
  return !!d && !d.startsWith('0000') && !isNaN(new Date(d).getTime())
}

function formatExpiry(d: string): string {
  return isValidDate(d) ? d : '—'
}

function isExpiringSoon(expiryDate: string): boolean {
  if (!isValidDate(expiryDate)) return false
  const daysLeft = (new Date(expiryDate).getTime() - Date.now()) / (1000 * 60 * 60 * 24)
  return daysLeft >= 0 && daysLeft <= 30
}
</script>