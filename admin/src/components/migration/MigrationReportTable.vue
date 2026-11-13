<script setup lang="ts">
/**
 * Import report of a finished migration job: one row per imported, skipped or failed source row,
 * filterable by entity group and outcome, paged. The log comes from the migration store, which
 * owns the request and the refusal sentence; a refused read shows that sentence with a Retry
 * button instead of the "no records" state.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMigrationStore } from '@/stores/migration'
import { formatClock } from '@/lib/format'
import { migrationActionLabelKey, migrationSkipReasonLabelKey } from '@/lib/migrationStatus'
import { migrationActionTone } from '@/lib/tones'
import { MigrationEntityType, MigrationLogAction } from '@/types/migration'
import type { MigrationLogEntry } from '@/types/migration'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPagination from '@/components/ui/UiPagination.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'

/** Translation function and the active locale (for time formatting). */
const { t, locale } = useI18n()

/** Props for MigrationReportTable. */
const props = defineProps<{
  /** Id of the job whose log is shown. */
  jobId: number
}>()

/** Migration store: owns the log request and its refusal sentence. */
const store = useMigrationStore()

/** Rows of the current page. */
const items = ref<MigrationLogEntry[]>([])

/** Rows matching the filters across all pages. */
const totalCount = ref(0)

/** True while a page is being fetched. */
const loading = ref(false)

/** The API's sentence for the last refused read, null when the last read succeeded. */
const loadError = ref<string | null>(null)

/** Selected outcome filter; empty means all. */
const filterAction = ref<MigrationLogAction | ''>('')

/** Selected entity-group filter; empty means all. */
const filterEntityType = ref<MigrationEntityType | ''>('')

/** Current one-based page. */
const page = ref(1)

/**
 * Converts a log entity name to the camel-case key of the locale catalogue.
 *
 * @param type - PascalCase name such as `TicketReplies`.
 * @returns The camel-case key such as `ticketReplies`.
 */
const toEntityKey = (type: MigrationEntityType): string => `${type.charAt(0).toLowerCase()}${type.slice(1)}`

/** Outcome filter options. */
const actionOptions = computed(() => [
  { value: '', label: t('migration.migrationReportTable.allStatuses') },
  ...Object.values(MigrationLogAction).map(action => ({
    value: action,
    label: t(migrationActionLabelKey[action]),
  })),
])

/** Entity-group filter options. */
const entityTypeOptions = computed(() => [
  { value: '', label: t('migration.migrationReportTable.allTypes') },
  ...Object.values(MigrationEntityType).map(type => ({
    value: type,
    label: t(`migrationEntities.${toEntityKey(type)}`),
  })),
])

/**
 * Caption of a row's reason: the translated skip reason, else the failure detail, else a dash.
 *
 * @param entry - The log row.
 * @returns The text shown in the reason column.
 */
const reasonText = (entry: MigrationLogEntry): string =>
  entry.skipReason ? t(migrationSkipReasonLabelKey[entry.skipReason]) : (entry.reason ?? '—')

/**
 * Fetches the current page of the log; a refusal keeps its sentence for the error state.
 *
 * @returns Resolves when the request has settled.
 */
const load = async (): Promise<void> => {
  loading.value = true
  const result = await store.fetchLogs(props.jobId, {
    action: filterAction.value || undefined,
    entityType: filterEntityType.value || undefined,
    page: page.value,
    pageSize: DEFAULT_PAGE_SIZE,
  })
  loading.value = false
  if (result === null) {
    loadError.value = store.jobError ?? t('errors.noAnswer')
    return
  }
  loadError.value = null
  items.value = result.items
  totalCount.value = result.totalCount
}

// A filter change restarts from the first page; the first run loads the report.
watch([filterAction, filterEntityType], () => {
  page.value = 1
  void load()
}, { immediate: true })

// Paging re-reads the log.
watch(page, () => {
  void load()
})
</script>

<template>
  <UiCard padding="md" data-testid="migration-report">
    <UiStack gap="md">
      <!-- Header + filters -->
      <UiStack direction="row" align="center" gap="sm" wrap>
        <UiHeading :level="3" variant="card" class="min-w-0 flex-1">
          {{ t('migration.migrationReportTable.importReport') }}
        </UiHeading>
        <UiBadge :tone="UiTone.Success" small data-testid="migration-report-count">
          {{ t('migration.migrationReportTable.totalcountEntries', { totalCount }) }}
        </UiBadge>
        <UiSelect
          v-model="filterEntityType"
          :options="entityTypeOptions"
          :placeholder="t('migration.migrationReportTable.allTypes')"
          class="w-40"
          data-testid="migration-report-entity-filter"
        />
        <UiSelect
          v-model="filterAction"
          :options="actionOptions"
          :placeholder="t('migration.migrationReportTable.allStatuses')"
          class="w-40"
          data-testid="migration-report-action-filter"
        />
      </UiStack>

      <UiErrorState
        v-if="loadError !== null"
        :message="loadError"
        :loading="loading"
        testid="migration-report-error"
        @retry="load"
      />

      <!-- Table -->
      <UiTable
        v-else
        :density="UiTableDensity.Compact"
        :loading="loading && items.length === 0"
        :empty="!loading && items.length === 0"
        data-testid="migration-report-table"
      >
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
            <UiTableHeader>{{ t('migration.migrationReportTable.identifier') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.reason') }}</UiTableHeader>
            <UiTableHeader>{{ t('migration.migrationReportTable.time') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="entry in items" :key="entry.id" hoverable>
            <UiTableCell muted>{{ t(`migrationEntities.${toEntityKey(entry.entityType)}`) }}</UiTableCell>
            <UiTableCell class="font-mono">{{ entry.identifier }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="migrationActionTone[entry.action]" dot small>
                {{ t(migrationActionLabelKey[entry.action]) }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell muted>{{ reasonText(entry) }}</UiTableCell>
            <UiTableCell muted class="font-mono">{{ formatClock(entry.createdAt, locale) }}</UiTableCell>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          <UiEmptyState :title="t('migration.migrationReportTable.noRecordsFound')" />
        </template>
      </UiTable>

      <!-- Pagination -->
      <UiStack v-if="totalCount > DEFAULT_PAGE_SIZE" direction="row" align="center" justify="between">
        <UiPagination
          v-model:page="page"
          :total="totalCount"
          :page-size="DEFAULT_PAGE_SIZE"
          testid="migration-report-pagination"
        />
      </UiStack>
    </UiStack>
  </UiCard>
</template>
