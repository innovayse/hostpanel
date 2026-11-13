<script setup lang="ts">
/**
 * Client domains list: shows all domain registrations belonging to the current client.
 * Navigates to the domain detail page when a row is clicked. The page lives in the address (`useFilterQuery`),
 * so a reload or a shared link opens the same page.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useDomainsStore } from '@/stores/domains'
import { domainStatusTone } from '@/lib/tones'
import { formatDate, EMPTY_VALUE } from '@/lib/format'
import type { DomainRegistration } from '@/types/domain'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { ClientSection, clientItemPath } from '@/lib/routes'
import { useFilterQuery } from '@/composables/useFilterQuery'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Router, used to open a domain. */
const router = useRouter()

/** Domains store, the only caller of the domain endpoints. */
const store = useDomainsStore()

/** Client ID from route params. */
const clientId = computed(() => route.params.id as string)

/** Loaded domains list for the current page. */
const domains = ref<DomainRegistration[]>([])

/** True while a request is in flight (owned by the store). */
const loading = computed(() => store.loading)

/** True while the first load is in flight and there are no rows yet; the table then draws its placeholder rows. */
const initialLoading = computed(() => loading.value && domains.value.length === 0)

/** Error message, null when no error (owned by the store). */
const error = computed(() => store.error)

/** The page of the list, kept in the address. */
const { page, setPage } = useFilterQuery([])

/** Items per page. */
const pageSize = ref(DEFAULT_PAGE_SIZE)

/** Total number of matching items across all pages. */
const totalCount = ref(0)

/**
 * Fetches paginated domains for this client from the backend.
 *
 * @returns Promise that resolves when data is loaded.
 */
const fetchDomains = async (): Promise<void> => {
  const result = await store.fetchClientPage(clientId.value, page.value, pageSize.value)
  if (result) {
    domains.value = result.items
    totalCount.value = result.totalCount
  }
}

/**
 * Navigates to the domain detail page for the given domain.
 *
 * @param domainId - The ID of the domain to view.
 * @returns Nothing.
 */
const viewDomain = (domainId: number): void => {
  router.push(clientItemPath(clientId.value, ClientSection.Domains, domainId))
}

// A page chosen, or the back button, loads the page the address names.
watch(page, fetchDomains)

onMounted(() => fetchDomains())
</script>

<template>
  <UiStack>
    <UiErrorState
      v-if="error"
      :message="error"
      :loading="loading"
      testid="client-domains-error"
      @retry="fetchDomains()"
    />

    <UiCard v-else-if="initialLoading || domains.length > 0">
      <UiTable
        :density="UiTableDensity.Comfortable"
        :loading="initialLoading"
        data-testid="client-domains-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.id') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.domainName') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.registrar') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.expiryDate') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.autoRenew') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="domain in domains"
            :key="domain.id"
            hoverable
            class="cursor-pointer"
            :data-testid="`client-domains-row-${domain.id}`"
            @click="viewDomain(domain.id)"
          >
            <UiTableCell muted>{{ domain.id }}</UiTableCell>
            <UiTableCell>{{ domain.name }}</UiTableCell>
            <UiTableCell muted>{{ domain.registrar ?? EMPTY_VALUE }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="domainStatusTone(domain.status)" small>{{ domain.status }}</UiBadge>
            </UiTableCell>
            <UiTableCell muted>{{ formatDate(domain.expiresAt) }}</UiTableCell>
            <UiTableCell>
              <UiText :tone="domain.autoRenew ? UiTextTone.Success : UiTextTone.Muted">
                {{ domain.autoRenew ? t('shared.yes') : t('shared.no') }}
              </UiText>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
      <template v-if="initialLoading || totalCount > pageSize" #footer>
        <UiStack
          direction="row"
          wrap
          :justify="UiJustify.Between"
          :align="UiAlignItems.Center"
        >
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
            {{ t('shared.totalcountDomainValue', { totalCount }) }}
          </UiText>
          <UiPagination
            :page="page"
            :total="totalCount"
            :page-size="pageSize"
            :loading="initialLoading"
            testid="client-domains-pagination"
            @update:page="setPage"
          />
        </UiStack>
      </template>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState :title="t('clients.clientDomainsList.noDomainsFoundForThis')" />
    </UiCard>
  </UiStack>
</template>
