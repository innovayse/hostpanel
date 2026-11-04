<script setup lang="ts">
/**
 * Displays a paginated, filterable activity log for the current client.
 *
 * Read-only audit trail: shows admin actions with date, description, user, and IP.
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useClientLogStore } from '@/stores/clientLog'
import { formatDateTimeCompact } from '@/lib/format'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDatePicker from '@/components/ui/UiDatePicker.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
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
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'

const { t } = useI18n()

/** Current route, source of the client id. */
const route = useRoute()

/** Client log store, the only caller of the activity-log endpoint. */
const store = useClientLogStore()

/** Client ID from route params. */
const clientId = computed(() => Number(route.params.id))

/** Current 1-based page. */
const page = ref(1)

/** Items per page. */
const pageSize = LEDGER_PAGE_SIZE

/** Whether the filter panel is visible. */
const showFilters = ref(false)

/** Filter: date string (ISO date, e.g. "2026-05-23"). */
const filterDate = ref('')

/** Filter: admin name/email partial match. */
const filterAdmin = ref('')

/** Filter: description partial match. */
const filterDescription = ref('')

/** Filter: IP address partial match. */
const filterIp = ref('')

/**
 * Builds the filter object from current filter inputs.
 *
 * @returns Filter params object.
 */
const buildFilters = () => ({
  date: filterDate.value || undefined,
  adminSearch: filterAdmin.value || undefined,
  description: filterDescription.value || undefined,
  ipAddress: filterIp.value || undefined,
})

/**
 * Loads logs for the current page and filters.
 *
 * @returns Promise that resolves when data is loaded.
 */
const load = async (): Promise<void> => {
  await store.fetchLogs(clientId.value, page.value, pageSize, buildFilters())
}

/**
 * Navigates to a specific page.
 *
 * @param p - Target page number.
 * @returns Nothing.
 */
const goToPage = (p: number): void => {
  page.value = p
  load()
}

/**
 * Applies current filters and resets to page 1.
 *
 * @returns Nothing.
 */
const applyFilters = (): void => {
  page.value = 1
  load()
}

/**
 * Clears all filters and reloads.
 *
 * @returns Nothing.
 */
const clearFilters = (): void => {
  filterDate.value = ''
  filterAdmin.value = ''
  filterDescription.value = ''
  filterIp.value = ''
  page.value = 1
  load()
}

onMounted(load)
</script>

<template>
  <UiStack>
    <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
      <UiHeading :level="1" variant="card">{{ t('clients.clientLog.activityLog') }}</UiHeading>
      <UiButton
        type="button"
        :variant="UiButtonVariant.Secondary"
        data-testid="client-log-toggle-filters"
        @click="showFilters = !showFilters"
      >
        {{ showFilters ? t('shared.hideFilters') : t('clients.clientLog.filterLog') }}
      </UiButton>
    </UiStack>

    <UiCard v-if="showFilters" padding="md" data-testid="client-log-filters">
      <UiStack>
        <UiGrid :columns="2">
          <UiField :label="t('shared.date')">
            <UiDatePicker v-model="filterDate" :placeholder="t('clients.clientLog.selectDate')" />
          </UiField>
          <UiField :label="t('shared.username')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="filterAdmin"
                type="text"
                :placeholder="t('shared.any')"
                data-testid="client-log-filter-admin"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.description')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="filterDescription"
                type="text"
                :placeholder="t('shared.description')"
                data-testid="client-log-filter-description"
              />
            </template>
          </UiField>
          <UiField :label="t('shared.ipAddress')">
            <template #default="{ id }">
              <UiInput
                :id="id"
                v-model="filterIp"
                type="text"
                :placeholder="t('shared.ipAddress')"
                data-testid="client-log-filter-ip"
              />
            </template>
          </UiField>
        </UiGrid>
        <UiStack direction="row" :justify="UiJustify.End" gap="sm">
          <UiButton
            type="button"
            :variant="UiButtonVariant.Secondary"
            data-testid="client-log-clear"
            @click="clearFilters"
          >
            {{ t('shared.clear') }}
          </UiButton>
          <UiButton
            type="button"
            :variant="UiButtonVariant.Primary"
            data-testid="client-log-apply"
            @click="applyFilters"
          >
            {{ t('shared.apply') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiCard>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="client-log-error"
      @retry="load"
    />

    <UiStack v-if="store.loading && store.logs.length === 0" data-testid="client-log-loading">
      <UiSkeleton :lines="3" />
      <UiText :tone="UiTextTone.Secondary">{{ t('clients.clientLog.loadingActivityLog') }}</UiText>
    </UiStack>

    <UiCard v-else class="overflow-x-auto">
      <UiTable
        density="comfortable"
        :empty="store.logs.length === 0"
        :colspan="4"
        data-testid="client-log-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.date') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.clientLog.logEntry') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.clientLog.user') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.ipAddress') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="log in store.logs" :key="log.id" hoverable>
            <UiTableCell muted>{{ formatDateTimeCompact(log.createdAt) }}</UiTableCell>
            <UiTableCell>{{ log.description }}</UiTableCell>
            <UiTableCell>
              <UiStack gap="none">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md">{{ log.adminName ?? '—' }}</UiText>
                <UiText v-if="log.adminEmail" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ log.adminEmail }}</UiText>
              </UiStack>
            </UiTableCell>
            <UiTableCell muted>{{ log.ipAddress ?? '—' }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('shared.noRecordsFound') }}
        </template>
      </UiTable>
      <UiStack
        v-if="store.totalCount > pageSize"
        direction="row"
        :justify="UiJustify.Between"
        :align="UiAlignItems.Center"
      >
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('clients.clientLog.totalcountEntrValue', { totalCount: store.totalCount }) }}
        </UiText>
        <UiPagination
          :page="page"
          :total="store.totalCount"
          :page-size="pageSize"
          testid="client-log-pagination"
          @update:page="goToPage"
        />
      </UiStack>
    </UiCard>
  </UiStack>
</template>
