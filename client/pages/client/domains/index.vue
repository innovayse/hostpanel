<template>
  <UiStack gap="lg">
    <UiPageHeader :title="$t('client.domains.title')" :description="$t('client.domains.subtitle')" :icon="Globe" :actions="headerActions" class="!mb-0" />

    <UiTabs v-model="activeTab" :tabs="tabs" />

    <!-- Not a customer account. Checked before the failure branch: this is a state the
         API reported, not a fault, and it carries its own explanation and a way out. -->
    <ClientNoProfileNotice v-if="!store.domainsLoading && store.clientProfileMissing" />

    <!-- Failed. Checked before the list, which a failure otherwise renders as the toolbar
         above an empty state: a search box over "no domains" rather than a reason. -->
    <UiAlert v-else-if="store.domainsError" variant="error" data-testid="domains-error">{{ store.domainsError }}</UiAlert>

    <template v-else>
      <UiStack direction="row" align="center" justify="between" wrap>
        <UiText size="sm" tone="muted">
          {{ $t('ui.pagination.showing', { from: pageFrom, to: pageTo, total: filtered.length }) }}
        </UiText>
        <UiSearchInput v-model="search" :placeholder="$t('client.domains.search')" class="sm:w-64" testid="domains-search" />
      </UiStack>

      <UiTable
        scrollable
        :label="$t('client.domains.title')"
        :columns="5"
        :loading="store.domainsLoading"
        :empty="!filtered.length"
        :empty-text="$t('client.domains.empty')"
        :empty-icon="Globe"
        data-testid="domains-table"
      >
        <template #head>
          <UiTableHead>
            <UiTableRow :hoverable="false">
              <UiTableTh>{{ $t('client.domains.colDomain') }}</UiTableTh>
              <UiTableTh class="hidden lg:table-cell">{{ $t('client.domains.colRegistrationDate') }}</UiTableTh>
              <UiTableTh class="hidden md:table-cell">{{ $t('client.domains.colNextDueDate') }}</UiTableTh>
              <UiTableTh align="center">{{ $t('client.domains.colStatus') }}</UiTableTh>
              <UiTableTh actions />
            </UiTableRow>
          </UiTableHead>
        </template>
        <template v-if="!search" #empty>
          <UiButton variant="primary" :to="SITE_DOMAINS_PATH" data-testid="domains-search-domains">{{ $t('client.domains.searchDomains') }}</UiButton>
        </template>
        <UiTableBody>
          <UiTableRow v-for="domain in paged" :key="domain.id" group :to="localePath(clientDomainPath(domain.id))" :data-testid="`domain-row-${domain.id}`">
            <UiTableTd>
              <UiStack direction="row" align="center" gap="sm">
                <Globe :size="16" :stroke-width="2" class="text-primary-400 flex-shrink-0" />
                <UiText :as="UiTextTag.Span" size="sm" weight="medium">{{ domain.name }}</UiText>
              </UiStack>
            </UiTableTd>
            <UiTableTd class="hidden lg:table-cell">
              <UiText :as="UiTextTag.Span" size="sm" tone="subtle">{{ formatDate(domain.registeredAt) }}</UiText>
            </UiTableTd>
            <UiTableTd class="hidden md:table-cell">
              <UiText :as="UiTextTag.Span" size="sm" :tone="isExpiringSoon(domain.expiresAt) ? 'danger' : 'subtle'">
                {{ formatDate(domain.expiresAt) }}
              </UiText>
              <UiBadge v-if="isExpiringSoon(domain.expiresAt)" variant="warning" size="sm" class="ml-2">{{ $t('client.domains.expiringSoon') }}</UiBadge>
            </UiTableTd>
            <UiTableTd align="center">
              <ClientStatusBadge :status="domain.status" />
            </UiTableTd>
            <UiTableTd align="right">
              <UiButton :to="clientDomainPath(domain.id)" variant="subtle" size="sm" :data-testid="`domain-manage-${domain.id}`">
                {{ $t('common.manage') }}
              </UiButton>
            </UiTableTd>
          </UiTableRow>
        </UiTableBody>
      </UiTable>

      <UiPagination v-if="filtered.length > 0" v-model="page" v-model:per-page="perPage" :total-pages="totalPages" />
    </template>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Domain list: filter tabs (all / active / expired), a search box and a paged table of the
 * client's domains, each row linking to the domain. Loading, missing-profile and failure states
 * come from the client store.
 */
import { clientDomainPath } from '~/constants/clientRoutes'
import { Globe, Plus } from 'lucide-vue-next'
import { DEFAULT_PAGE_SIZE } from '~/constants/uiDefaults'
import { useClientStore } from '~/stores/client'
import { DomainStatus } from '~/types/domainstatus'
import { ListFilter } from '~/types/listfilter'
import type { UiHeaderAction } from '~/types/uiheaderaction'
import { isExpiringSoon } from '~/utils/domainExpiry'
import { formatDate } from '~/utils/formatDate'
import { SITE_DOMAINS_PATH } from '~/constants/siteRoutes'
import { UiTextTag } from '~/utils/ui/enums'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The client store that owns the domain list and its loading and failure state. */
const store = useClientStore()

await useAsyncData('client-domains', () => store.fetchDomains())

/** The active filter tab, a `ListFilter` value (a string so the tab bar can v-model it). */
const activeTab = ref<string>(ListFilter.All)

/** The search text. */
const search = ref('')

/** The current page, 1-based. */
const page = ref(1)

/** Rows per page. */
const perPage = ref(DEFAULT_PAGE_SIZE)

// A new filter or search starts again from the first page.
watch([activeTab, search, perPage], () => { page.value = 1 })

/** The header's one action: ordering a new domain. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { key: 'register-new', label: t('client.domains.registerNew'), icon: Plus, to: SITE_DOMAINS_PATH, primary: true, testid: 'domains-register-new' },
])

/** The filter tabs, each with the number of domains it holds. */
const tabs = computed(() => [
  { value: ListFilter.All, label: t('client.domains.tabAll'), badge: store.domains.length },
  { value: ListFilter.Current, label: t('client.domains.tabActive'), badge: store.domains.filter((domain) => domain.status === DomainStatus.Active).length },
  { value: ListFilter.Finished, label: t('client.domains.tabExpired'), badge: store.domains.filter((domain) => domain.status === DomainStatus.Expired).length },
])

/** The domains the active tab and the search text leave. */
const filtered = computed(() => {
  let list = store.domains
  if (activeTab.value === ListFilter.Current) {
    list = list.filter((domain) => domain.status === DomainStatus.Active)
  } else if (activeTab.value === ListFilter.Finished) {
    list = list.filter((domain) => domain.status === DomainStatus.Expired)
  }
  const query = search.value.trim().toLowerCase()
  return query ? list.filter((domain) => domain.name.toLowerCase().includes(query)) : list
})

/** Number of pages the filtered list needs. */
const totalPages = computed(() => Math.max(1, Math.ceil(filtered.value.length / perPage.value)))

/** The rows of the current page. */
const paged = computed(() => filtered.value.slice((page.value - 1) * perPage.value, page.value * perPage.value))

/** 1-based index of the first row shown, 0 when there is none. */
const pageFrom = computed(() => (filtered.value.length === 0 ? 0 : (page.value - 1) * perPage.value + 1))

/** 1-based index of the last row shown. */
const pageTo = computed(() => Math.min(page.value * perPage.value, filtered.value.length))
</script>
