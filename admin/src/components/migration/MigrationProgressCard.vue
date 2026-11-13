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
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiMetric from '@/components/ui/UiMetric.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatTile from '@/components/ui/UiStatTile.vue'
import UiText from '@/components/ui/UiText.vue'
import { NOTICE_VISIBLE_MS } from '@/lib/limits'

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

/** Stops the pending connection-check timer. */
onUnmounted(() => {
  if (checkResultTimer.value !== null) {
    clearTimeout(checkResultTimer.value)
  }
})
</script>

<template>
  <UiCard padding="md" :aria-busy="job === null ? 'true' : undefined" :data-testid="job === null ? 'migration-loading' : 'migration-progress-card'">
    <UiStack gap="md">
      <!-- Header -->
      <UiStack v-if="job === null" direction="row" align="center" justify="between" aria-hidden="true">
        <UiStack gap="xs">
          <UiSkeletonBar class="h-6 w-48 rounded-md" />
          <UiSkeletonBar class="h-3 w-20 rounded-md" />
        </UiStack>
        <UiMetric value="" loading />
      </UiStack>
      <UiStack v-else direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiStack direction="row" align="center" gap="sm">
            <UiHeading :level="3" variant="card">
              {{ job.label ?? t('migration.migrationProgressCard.migration') }}
            </UiHeading>
            <UiBadge :tone="migrationStatusTone[job.status]" small data-testid="migration-job-status">
              {{ t(migrationStatusLabelKey[job.status]) }}
            </UiBadge>
          </UiStack>
          <UiText tone="muted" size="xs">{{ t('migration.migrationProgressCard.jobId', { id: job.id }) }}</UiText>
        </UiStack>
        <UiMetric :value="`${job.overallPercent}%`" data-testid="migration-job-percent" />
      </UiStack>

      <!-- Entity checklist -->
      <UiStack v-if="job === null" gap="xs" aria-hidden="true">
        <UiStack v-for="row in PLACEHOLDER_ROWS" :key="row" direction="row" align="center" justify="between">
          <UiSkeletonBar class="h-4 w-32 rounded-md" />
          <UiSkeletonBar class="h-4 w-12 rounded-md" />
        </UiStack>
      </UiStack>
      <UiStack v-else gap="xs" data-testid="migration-entities">
        <UiStack
          v-for="key in activeKeys"
          :key="key"
          direction="row"
          align="center"
          justify="between"
          :data-testid="`migration-entity-${key}`"
        >
          <UiText :tone="job.progress[key].done ? 'primary' : 'secondary'" size="md">
            {{ t(`migrationEntities.${key}`) }}
          </UiText>
          <UiText :tone="job.progress[key].done ? 'success' : 'muted'" size="sm" class="font-semibold tabular-nums">
            <template v-if="job.progress[key].total > 0">
              {{ job.progress[key].imported + job.progress[key].skipped }}/{{ job.progress[key].total }}
            </template>
            <template v-else>—</template>
          </UiText>
        </UiStack>
      </UiStack>

      <!-- KPI tiles -->
      <UiGrid :columns="2" gap="sm">
        <UiStatTile :label="t('migration.migrationProgressCard.migrated')" :value="totalMigrated" tone="success" :loading="job === null" />
        <UiStatTile :label="t('migration.migrationProgressCard.skipped')" :value="totalSkipped" tone="warning" :loading="job === null" />
        <UiStatTile
          :label="t('migration.migrationProgressCard.failed')"
          :value="totalFailed"
          :tone="totalFailed > 0 ? 'danger' : 'neutral'"
          :loading="job === null"
        />
        <UiStatTile :label="t('shared.total')" :value="totalRecords" :loading="job === null" />
      </UiGrid>

      <!-- Failure message the server recorded for the job -->
      <UiAlert v-if="job?.status === MigrationStatus.Failed && job.failure" variant="error" data-testid="migration-job-error">
        {{ t(migrationJobFailureLabelKey[job.failure]) }}
      </UiAlert>
      <UiStack v-if="job?.status === MigrationStatus.Failed" direction="row" gap="sm">
        <UiButton
          type="button"
          variant="primary"
          size="sm"
          :loading="starting"
          data-testid="migration-retry"
          @click="retryImport"
        >
          {{ t('migration.migrationProgressCard.retryImport') }}
        </UiButton>
      </UiStack>

      <!-- Plugin connection + start -->
      <UiStack v-if="job !== null && isJobActive(job.status)" direction="row" align="center" gap="sm" wrap>
        <UiBadge :tone="job.pluginConnected ? 'success' : 'neutral'" dot data-testid="migration-plugin-state">
          {{ job.pluginConnected ? t('migration.migrationProgressCard.pluginConnected') : t('migration.migrationProgressCard.waitingForPlugin') }}
        </UiBadge>
        <UiButton
          type="button"
          variant="secondary"
          size="sm"
          :loading="checking"
          data-testid="migration-check"
          @click="checkConnection"
        >
          {{ t('migration.migrationProgressCard.check') }}
        </UiButton>
        <UiBadge
          v-if="checkOutcome === CheckOutcome.Reachable || checkOutcome === CheckOutcome.Unreachable"
          :tone="checkOutcome === CheckOutcome.Reachable ? 'success' : 'danger'"
          data-testid="migration-check-result"
        >
          {{ checkOutcome === CheckOutcome.Reachable ? t('shared.connected') : t('migration.migrationProgressCard.notReachable') }}
        </UiBadge>
        <UiButton
          v-if="job.status === MigrationStatus.Pending && job.pluginConnected"
          type="button"
          variant="primary"
          size="sm"
          :loading="starting"
          data-testid="migration-start"
          @click="startImport"
        >
          {{ t('migration.migrationProgressCard.startImport') }}
        </UiButton>
      </UiStack>
      <UiAlert v-if="checkOutcome === CheckOutcome.Refused && checkError" variant="error" data-testid="migration-check-error">
        {{ checkError }}
      </UiAlert>
      <UiAlert v-if="startError" variant="error" data-testid="migration-start-error">
        {{ startError }}
      </UiAlert>
    </UiStack>
  </UiCard>
</template>
