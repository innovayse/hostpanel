<script setup lang="ts">
/**
 * Client domains list: shows all domain registrations belonging to the current client.
 * Navigates to the domain detail page when a row is clicked.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useDomainsStore } from '@/stores/domains'
import { domainStatusTone } from '@/lib/tones'
import { formatDate } from '@/lib/format'
import type { DomainRegistration } from '@/types/domain'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
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

/** Error message, null when no error (owned by the store). */
const error = computed(() => store.error)

/** Current page (1-based). */
const page = ref(1)

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
 * Navigate to a specific page and re-fetch.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToPage = (p: number): void => {
  page.value = p
  fetchDomains()
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

onMounted(() => fetchDomains())
</script>

<template>
  <UiStack>
    <UiStack v-if="loading && domains.length === 0" data-testid="client-domains-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientDomainsList.loadingDomains') }}</UiText>
    </UiStack>

    <UiErrorState
      v-else-if="error"
      :message="error"
      :loading="loading"
      testid="client-domains-error"
      @retry="fetchDomains()"
    />

    <UiCard v-else-if="domains.length > 0" class="overflow-x-auto">
      <UiTable density="comfortable" data-testid="client-domains-table">
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
            <UiTableCell muted>{{ domain.registrar ?? '—' }}</UiTableCell>
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
      <UiStack
        v-if="totalCount > pageSize"
        direction="row"
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
          testid="client-domains-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState :title="t('clients.clientDomainsList.noDomainsFoundForThis')" />
    </UiCard>
  </UiStack>
</template>
