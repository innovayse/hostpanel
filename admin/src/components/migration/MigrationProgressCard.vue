<script setup lang="ts">
/**
 * Progress card of one migration job.
 *
 * Shows the job's state and overall percent, a per-entity checklist with imported/total counts,
 * the migrated/skipped/failed/total tiles, the translated failure reason, and, while the job can still
 * change, the plugin connection state with the "Check" and "Start Import" actions. A check tells
 * a plugin that did not answer apart from a request the API refused (whose sentence is shown), and a
 * refused import start shows the API's sentence beside its button. The job comes in as a prop; the
 * store owns the actions and their refusal sentences. Without a job (still being read) the card keeps its layout and
 * draws bars and loading tiles in place of the state, the checklist and the counts.
 */
import { computed, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMigrationStore } from '@/stores/migration'
import { isJobActive, migrationJobFailureLabelKey, migrationStatusLabelKey } from '@/lib/migrationStatus'
import { migrationStatusTone } from '@/lib/tones'
import { MIGRATION_ENTITY_KEYS, MigrationStatus } from '@/types/migration'
import type { MigrationEntityKey, MigrationJob } from '@/types/migration'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiMetric from '@/components/ui/UiMetric.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatRow from '@/components/ui/UiStatRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { NOTICE_VISIBLE_MS } from '@/lib/limits'
import { EMPTY_VALUE } from '@/lib/format'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiStatRowVariant } from '@/utils/ui/statRow'
import type { UiStatFigure } from '@/utils/ui/statRow'
import type { SkeletonStat } from '@/utils/ui/skeletonSchema'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Closed set of the counters shown under the checklist. */
enum Counter {
  /** Records imported. */
  Migrated = 'migrated',
  /** Records skipped. */
  Skipped = 'skipped',
  /** Records whose import failed. */
  Failed = 'failed',
  /** Source records. */
  Total = 'total',
}

/** The four counters under the checklist, as the shared stat row draws them. */
const COUNTER_STATS: readonly SkeletonStat[] = [
  { key: Counter.Migrated, label: 'migration.migrationProgressCard.migrated' },
  { key: Counter.Skipped, label: 'migration.migrationProgressCard.skipped' },
  { key: Counter.Failed, label: 'migration.migrationProgressCard.failed' },
  { key: Counter.Total, label: 'shared.total' },
]

/** How many checklist rows the loading card draws. */
const PLACEHOLDER_ROWS = 3

/** Translation function. */
const { t } = useI18n()

/** Props for MigrationProgressCard. */
const props = defineProps<{
  /** The job to show; null while it is being read. */
  job: MigrationJob | null
}>()

/** Migration store: owns the connection check and the import start. */
const store = useMigrationStore()

/** True while the connection check is in flight. */
const checking = ref(false)

/** True while the import start request is in flight. */
const starting = ref(false)

/** What the last connection check found. */
enum CheckOutcome {
  /** The plugin answered. */
  Reachable = 'reachable',
  /** The API checked, and the plugin did not answer. */
  Unreachable = 'unreachable',
  /** The API refused the check itself; its sentence is in the store. */
  Refused = 'refused',
}

/** Outcome of the last connection check, null when none is shown. */
const checkOutcome = ref<CheckOutcome | null>(null)

/** The API's sentence for a refused import start, null when none. */
const startError = ref<string | null>(null)

/** The API's sentence for a refused connection check, shown with the refused outcome. */
const checkError = ref<string | null>(null)

/** Timer that hides the connection-check result. */
const checkResultTimer = ref<ReturnType<typeof setTimeout> | null>(null)

/**
 * Asks the server to test the source plugin. Whether the plugin answered is shown for a few
 * seconds; a refused request keeps the API's sentence until the next check.
 *
 * @returns Resolves when the check has settled.
 */
const checkConnection = async (): Promise<void> => {
  const target = props.job
  if (target === null) {
    return
  }
  checking.value = true
  checkOutcome.value = null
  checkError.value = null
  if (checkResultTimer.value !== null) {
    clearTimeout(checkResultTimer.value)
    checkResultTimer.value = null
  }
  const job = await store.testConnection(target.id)
  checking.value = false
  if (job === null) {
    checkOutcome.value = CheckOutcome.Refused
    checkError.value = store.jobError ?? t('errors.noAnswer')
    return
  }
  checkOutcome.value = job.pluginConnected ? CheckOutcome.Reachable : CheckOutcome.Unreachable
  checkResultTimer.value = setTimeout(() => {
    checkOutcome.value = null
  }, NOTICE_VISIBLE_MS)
}

/**
 * Starts the import and follows the job until it finishes; a refusal shows the API's sentence.
 *
 * @returns Resolves when the request has settled.
 */
const startImport = async (): Promise<void> => {
  const target = props.job
  if (target === null) {
    return
  }
  startError.value = null
  starting.value = true
  const job = await store.startImport(target.id)
  starting.value = false
  if (job === null) {
    startError.value = store.jobError ?? t('errors.noAnswer')
    return
  }
  store.startPolling(target.id)
}

/**
 * Queues a failed import again and follows the job; a refusal shows the API's sentence.
 *
 * @returns Resolves when the request has settled.
 */
const retryImport = async (): Promise<void> => {
  const target = props.job
  if (target === null) {
    return
  }
  startError.value = null
  starting.value = true
  const job = await store.retryImport(target.id)
  starting.value = false
  if (job === null) {
    startError.value = store.jobError ?? t('errors.noAnswer')
    return
  }
  store.startPolling(target.id)
}

/** Entity groups this job exports. */
const activeKeys = computed<MigrationEntityKey[]>(() => {
  const job = props.job
  return job === null ? [] : MIGRATION_ENTITY_KEYS.filter(key => job.entitySelection[key])
})

/**
 * Adds up one counter across the selected entity types.
 *
 * @param counter - Which counter to add up.
 * @returns The sum, 0 while there is no job.
 */
const sumOf = (counter: 'imported' | 'skipped' | 'failed' | 'total'): number => {
  const job = props.job
  return job === null ? 0 : activeKeys.value.reduce((sum, key) => sum + job.progress[key][counter], 0)
}

/** Sum of imported records across the selected entity types. */
const totalMigrated = computed<number>(() => sumOf('imported'))

/** Sum of skipped records across the selected entity types. */
const totalSkipped = computed<number>(() => sumOf('skipped'))

/** Sum of source records across the selected entity types. */
const totalRecords = computed<number>(() => sumOf('total'))

/** Sum of records whose import failed, as the server counted them. */
const totalFailed = computed<number>(() => sumOf('failed'))

/** What the stat row shows for each counter. */
const counterFigures = computed<Record<string, UiStatFigure>>(() => ({
  [Counter.Migrated]: { value: totalMigrated.value },
  [Counter.Skipped]: { value: totalSkipped.value },
  [Counter.Failed]: { value: totalFailed.value },
  [Counter.Total]: { value: totalRecords.value },
}))

/** Stops the pending connection-check timer. */
onUnmounted(() => {
  if (checkResultTimer.value !== null) {
    clearTimeout(checkResultTimer.value)
  }
})
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" container :aria-busy="job === null ? 'true' : undefined" :data-testid="job === null ? 'migration-loading' : 'migration-progress-card'">
    <UiStack :gap="UiGap.Md">
      <!-- Header -->
      <UiStack v-if="job === null" :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between" aria-hidden="true">
        <UiStack :gap="UiGap.Xs">
          <UiSkeletonBar class="h-6 w-48 rounded-md" />
          <UiSkeletonBar class="h-3 w-20 rounded-md" />
        </UiStack>
        <UiMetric value="" loading />
      </UiStack>
      <UiStack v-else :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between">
        <UiStack :gap="UiGap.Xs">
          <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm">
            <UiHeading :level="3" :variant="UiHeadingVariant.Card">
              {{ job.label ?? t('migration.migrationProgressCard.migration') }}
            </UiHeading>
            <UiBadge :tone="migrationStatusTone[job.status]" small data-testid="migration-job-status">
              {{ t(migrationStatusLabelKey[job.status]) }}
            </UiBadge>
          </UiStack>
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('migration.migrationProgressCard.jobId', { id: job.id }) }}</UiText>
        </UiStack>
        <UiMetric :value="`${job.overallPercent}%`" data-testid="migration-job-percent" />
      </UiStack>

      <!-- Entity checklist -->
      <UiStack v-if="job === null" :gap="UiGap.Xs" aria-hidden="true">
        <UiStack v-for="row in PLACEHOLDER_ROWS" :key="row" :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between">
          <UiSkeletonBar class="h-4 w-32 rounded-md" />
          <UiSkeletonBar class="h-4 w-12 rounded-md" />
        </UiStack>
      </UiStack>
      <UiStack v-else :gap="UiGap.Xs" data-testid="migration-entities">
        <UiStack
          v-for="key in activeKeys"
          :key="key"
          :direction="UiDirection.Row"
          :align="UiAlignItems.Center"
          :justify="UiJustify.Between"
          :data-testid="`migration-entity-${key}`"
        >
          <UiText :tone="job.progress[key].done ? UiTextTone.Primary : UiTextTone.Secondary" :size="UiTextSize.Md">
            {{ t(`migrationEntities.${key}`) }}
          </UiText>
          <UiText :tone="job.progress[key].done ? UiTextTone.Success : UiTextTone.Muted" :size="UiTextSize.Sm" class="font-semibold tabular-nums">
            <template v-if="job.progress[key].total > 0">
              {{ job.progress[key].imported + job.progress[key].skipped }}/{{ job.progress[key].total }}
            </template>
            <template v-else>{{ EMPTY_VALUE }}</template>
          </UiText>
        </UiStack>
      </UiStack>

      <!-- KPI tiles -->
      <UiStatRow
        :stats="COUNTER_STATS"
        :figures="counterFigures"
        :variant="UiStatRowVariant.Inline"
        :loading="job === null"
        testid-prefix="migration-stat"
      />

      <!-- Failure message the server recorded for the job -->
      <UiAlert v-if="job?.status === MigrationStatus.Failed && job.failure" :variant="UiTone.Danger" data-testid="migration-job-error">
        {{ t(migrationJobFailureLabelKey[job.failure]) }}
      </UiAlert>
      <UiStack v-if="job?.status === MigrationStatus.Failed" :direction="UiDirection.Row" :gap="UiGap.Sm">
        <UiButton :action="UiActionKind.Refresh"
          type="button"
          :variant="UiButtonVariant.Primary"
          :size="UiSize.Sm"
          :loading="starting"
          data-testid="migration-retry"
          @click="retryImport"
        >
          {{ t('shared.retry') }}
        </UiButton>
      </UiStack>

      <!-- Plugin connection + start -->
      <UiStack v-if="job !== null && isJobActive(job.status)" :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap>
        <UiBadge :tone="job.pluginConnected ? UiTone.Success : UiTone.Neutral" dot data-testid="migration-plugin-state">
          {{ job.pluginConnected ? t('migration.migrationProgressCard.pluginConnected') : t('migration.migrationProgressCard.waitingForPlugin') }}
        </UiBadge>
        <UiButton :action="UiActionKind.Test"
          type="button"
          :variant="UiButtonVariant.Secondary"
          :size="UiSize.Sm"
          :loading="checking"
          data-testid="migration-check"
          @click="checkConnection"
        >
          {{ t('shared.test') }}
        </UiButton>
        <UiBadge
          v-if="checkOutcome === CheckOutcome.Reachable || checkOutcome === CheckOutcome.Unreachable"
          :tone="checkOutcome === CheckOutcome.Reachable ? UiTone.Success : UiTone.Danger"
          data-testid="migration-check-result"
        >
          {{ checkOutcome === CheckOutcome.Reachable ? t('shared.connected') : t('migration.migrationProgressCard.notReachable') }}
        </UiBadge>
        <UiButton :action="UiActionKind.Generate"
          v-if="job.status === MigrationStatus.Pending && job.pluginConnected"
          type="button"
          :variant="UiButtonVariant.Primary"
          :size="UiSize.Sm"
          :loading="starting"
          data-testid="migration-start"
          @click="startImport"
        >
          {{ t('migration.migrationProgressCard.startImport') }}
        </UiButton>
      </UiStack>
      <UiAlert v-if="checkOutcome === CheckOutcome.Refused && checkError" :variant="UiTone.Danger" data-testid="migration-check-error">
        {{ checkError }}
      </UiAlert>
      <UiAlert v-if="startError" :variant="UiTone.Danger" data-testid="migration-start-error">
        {{ startError }}
      </UiAlert>
    </UiStack>
  </UiCard>
</template>
