<script setup lang="ts">
/**
 * Migration tools page.
 *
 * Lists the migration jobs (history) in a column a third wide that scrolls inside, beside the selected job's progress card, the one-time setup
 * instructions of a pending job and the import report of a finished one, and opens a dialog that
 * creates a job. Jobs, polling, and every refusal sentence belong to the migration store. Deleting
 * a job is confirmed first; creating and deleting toast their outcome (success, or the API's
 * sentence), and a refused history load shows the sentence with a Retry.
 */
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { schemaHeader } from '@/lib/schemaHeader'
import { useMigrationStore } from '@/stores/migration'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import { isJobActive } from '@/lib/migrationStatus'
import { MIGRATION_ENTITY_KEYS, MigrationStatus } from '@/types/migration'
import type { MigrationEntityKey, MigrationExportFlags, MigrationJob } from '@/types/migration'
import MigrationHistoryItem from '@/components/migration/MigrationHistoryItem.vue'
import MigrationProgressCard from '@/components/migration/MigrationProgressCard.vue'
import MigrationReportTable from '@/components/migration/MigrationReportTable.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiSplit from '@/components/ui/UiSplit.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { API_BASE } from '@/lib/apiPaths'
import { COPIED_VISIBLE_MS } from '@/lib/limits'
import { placeholderRows } from '@/lib/placeholderRows'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiDialogSize, UiDialogVariant } from '@/utils/ui/dialog'
import { UiInputType } from '@/utils/ui/input'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant, UiLabelVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import schema from '@/views/MigrationView.schema'
import { UiAlignItems, UiDirection, UiGap, UiJustify, UiWidth } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind, actionIcons } from '@/utils/ui/actions'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

/** Translation function. */
const { t } = useI18n()

/** Opens the dialog that starts a new migration. */
const openNewJob = (): void => {
  showNewJobModal.value = true
}

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  { label: t('migration.migration.createMigration'), icon: actionIcons[UiActionKind.Add], variant: UiButtonVariant.Primary, testid: 'migration-new', onSelect: openNewJob },
])

/** Migration store: the jobs, the selected job, polling and the refusal sentence. */
const store = useMigrationStore()

/** Where the source-side plugin archive is served from. */
const PLUGIN_DOWNLOAD_URL = `${API_BASE}/migrations/plugin/download`

/** Export flag that belongs to each entity group. */
const EXPORT_FLAG: Record<MigrationEntityKey, keyof MigrationExportFlags> = {
  clients: 'exportClients',
  invoices: 'exportInvoices',
  services: 'exportServices',
  domains: 'exportDomains',
  tickets: 'exportTickets',
  products: 'exportProducts',
  orders: 'exportOrders',
  transactions: 'exportTransactions',
  quotes: 'exportQuotes',
  knowledgebase: 'exportKnowledgebase',
  contacts: 'exportContacts',
  ticketReplies: 'exportTicketReplies',
  announcements: 'exportAnnouncements',
  downloads: 'exportDownloads',
  networkIssues: 'exportNetworkIssues',
}

/** Whether the "new migration" dialog is open. */
const showNewJobModal = ref(false)

/** Label typed for the new job. */
const newJobLabel = ref('')

/** Source address typed for the new job. */
const newJobSourceUrl = ref('')

/** True while the create request is in flight. */
const creatingJob = ref(false)

/** True for a moment after the secret key was copied. */
const copiedKey = ref(false)

/** Timer that hides the "Copied" confirmation. */
const copiedTimer = ref<ReturnType<typeof setTimeout> | null>(null)

/** The job waiting for the delete confirmation, null when the prompt is closed. */
const deleteTarget = ref<MigrationJob | null>(null)

/** True while the delete is in flight. */
const deleting = ref(false)

/** The sentence of the last refused history load, shown with Retry; null when it loaded. */
const loadError = ref<string | null>(null)

/** The outcome line of the last create or delete. */
const feedback = useOutcomeToast()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/**
 * Loads the history and records whether the load was refused.
 *
 * @returns Resolves when the history has settled.
 */
const load = async (): Promise<void> => {
  await store.fetchAll()
  loadError.value = store.error
}

/** Which entity groups the new job exports; all on by default. */
const flags = reactive<MigrationExportFlags>({
  exportClients: true,
  exportInvoices: true,
  exportServices: true,
  exportDomains: true,
  exportTickets: true,
  exportProducts: true,
  exportOrders: true,
  exportTransactions: true,
  exportQuotes: true,
  exportKnowledgebase: true,
  exportContacts: true,
  exportTicketReplies: true,
  exportAnnouncements: true,
  exportDownloads: true,
  exportNetworkIssues: true,
})

/** True when every entity group is selected. */
const allSelected = computed<boolean>(() =>
  MIGRATION_ENTITY_KEYS.every(key => flags[EXPORT_FLAG[key]]),
)

/** How many placeholder rows the history draws while the jobs load. */
const HISTORY_PLACEHOLDER_ROWS = 3

/** True until the first read of the jobs has answered: the history draws its rows as placeholders. */
const historyLoading = computed<boolean>(() => store.loading && store.jobs.length === 0)

/** The rows of the history: the jobs, or null placeholders while they load. */
const historyRows = computed(() => placeholderRows(store.jobs, historyLoading.value, HISTORY_PLACEHOLDER_ROWS))

/** Whether the selected job's import report is shown (finished jobs only). */
const showReport = computed<boolean>(() =>
  store.activeJob?.status === MigrationStatus.Completed || store.activeJob?.status === MigrationStatus.Failed,
)

/** Secret key of the selected job, shown in the setup instructions. */
const activeKey = computed<string>(() => store.activeJob?.key ?? '')

/** Selects every entity group, or clears the selection when all are already selected. */
const toggleAll = (): void => {
  const next = !allSelected.value
  MIGRATION_ENTITY_KEYS.forEach((key) => {
    flags[EXPORT_FLAG[key]] = next
  })
}

/** Creates the job from the dialog, then selects it and follows its progress. */
const handleCreateJob = async (): Promise<void> => {
  creatingJob.value = true
  const job = await store.createJob({
    label: newJobLabel.value || undefined,
    sourceUrl: newJobSourceUrl.value,
    ...flags,
  })
  creatingJob.value = false
  if (job) {
    store.setActiveJob(job)
    store.startPolling(job.id)
    newJobLabel.value = ''
    newJobSourceUrl.value = ''
    showNewJobModal.value = false
    feedback.show(t('migration.migration.migrationCreated'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Closes the new-job dialog. */
const closeNewJob = (): void => {
  showNewJobModal.value = false
}

/**
 * Selects a job from the history and follows it while it can still change.
 *
 * @param job - The chosen job.
 */
const selectJob = (job: MigrationJob): void => {
  store.setActiveJob(job)
  if (isJobActive(job.status)) {
    store.startPolling(job.id)
  }
}

/**
 * Copies the job's secret key to the clipboard and confirms for a moment.
 *
 * @param key - The secret key to copy.
 */
const copyKey = async (key: string): Promise<void> => {
  await navigator.clipboard.writeText(key)
  copiedKey.value = true
  if (copiedTimer.value !== null) {
    clearTimeout(copiedTimer.value)
  }
  copiedTimer.value = setTimeout(() => {
    copiedKey.value = false
  }, COPIED_VISIBLE_MS)
}

/**
 * Deletes the job the operator confirmed and shows the outcome.
 *
 * @returns Resolves when the delete has settled.
 */
const confirmDelete = async (): Promise<void> => {
  const job = deleteTarget.value
  if (job === null) {
    return
  }
  deleting.value = true
  const deleted = await store.deleteJob(job.id)
  deleting.value = false
  deleteTarget.value = null
  if (deleted) {
    feedback.show(t('migration.migration.migrationDeleted'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTarget.value = null
  }
}

/** Loads the history. */
onMounted(() => {
  void load()
})

/** Stops following a running job and the copy confirmation timer. */
onUnmounted(() => {
  store.stopPolling()
  if (copiedTimer.value !== null) {
    clearTimeout(copiedTimer.value)
  }
})
</script>

<template>
  <UiStack :gap="UiGap.Md">
    <UiErrorState
      v-if="loadError !== null"
      :message="loadError"
      :loading="store.loading"
      testid="migration-error"
      @retry="load"
    />

    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
      <template #actions>
        <PageHelpLink :url="guideUrl" />
      </template>
    </UiPageHeader>


    <!-- Body: the history on a third and the selected job on two thirds, both as tall as the taller of the two -->
    <UiSplit :ratio="schema.ratio" :gap="UiGap.Md">
      <!-- History: fills the height of the row and scrolls inside, so a long history never stretches the page -->
      <UiStack class="relative min-h-80">
        <UiCard
          :padding="UiCardPadding.Sm"
          class="flex max-h-[28rem] flex-col lg:absolute lg:inset-0 lg:max-h-none"
          :aria-busy="historyLoading ? 'true' : undefined"
          data-testid="migration-history"
        >
          <template #header>
            <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between">
              <UiHeading :level="3" :variant="UiHeadingVariant.Card">{{ t('migration.migration.history') }}</UiHeading>
              <UiText v-if="store.jobs.length > 0" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ store.jobs.length }}</UiText>
            </UiStack>
          </template>
          <UiStack :gap="UiGap.Xs" class="mt-3 min-h-0 flex-1 overflow-y-auto">
            <UiEmptyState
              v-if="!historyLoading && store.jobs.length === 0"
              compact
              :title="t('migration.migration.noMigrationsYet')"
              data-testid="migration-history-empty"
            />

            <!-- The jobs; while the history loads the same rows are drawn with bars where a job goes -->
            <template v-for="(job, index) in historyRows" :key="job?.id ?? `placeholder-${index}`">
              <UiStack v-if="job === null" :gap="UiGap.Xs" class="px-3 py-2" aria-hidden="true">
                <UiSkeletonBar :width="UiWidth.Sm" class="h-4 rounded-md" />
                <UiSkeletonBar :width="UiWidth.Full" class="h-2 rounded-md" />
                <UiSkeletonBar :width="UiWidth.Xs" class="h-3 rounded-md" />
              </UiStack>
              <MigrationHistoryItem
                v-else
                :job="job"
                :selected="store.activeJob?.id === job.id"
                :deleting="deleting && deleteTarget?.id === job.id"
                @select="selectJob"
                @delete="deleteTarget = $event"
              />
            </template>
          </UiStack>
        </UiCard>
      </UiStack>

      <!-- Selected job -->
      <template #aside>
        <UiStack :gap="UiGap.Md" class="min-w-0">
          <UiCard v-if="!store.activeJob && !store.loading" data-testid="migration-none-selected">
            <UiEmptyState v-if="store.jobs.length > 0" :title="t('migration.migration.selectFromHistory')" />
            <UiEmptyState
              v-else
              :title="t('migration.migration.noMigrationSelected')"
              :description="t('migration.migration.createANewMigration')"
            >
              <template #actions>
                <UiButton :action="UiActionKind.Generate" :variant="UiButtonVariant.Primary" data-testid="migration-start-first" @click="showNewJobModal = true">
                  {{ t('migration.migration.createMigration') }}
                </UiButton>
              </template>
            </UiEmptyState>
          </UiCard>

          <MigrationProgressCard v-if="store.activeJob || store.loading" :job="store.activeJob" />

          <!-- Setup instructions (pending jobs only) -->
          <UiCard v-if="store.activeJob?.status === MigrationStatus.Pending" :padding="UiCardPadding.Md" data-testid="migration-setup">
            <UiStack :gap="UiGap.Md">
              <UiHeading :level="3" :variant="UiHeadingVariant.Card">{{ t('migration.migration.setupRequired') }}</UiHeading>

              <UiStack :gap="UiGap.Xs">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="font-semibold">
                  1. {{ t('migration.migration.downloadTheMigrationPlugin') }}
                </UiText>
                <UiLink :href="PLUGIN_DOWNLOAD_URL" :variant="UiButtonVariant.Link" data-testid="migration-download-plugin">
                  {{ t('migration.migration.innovayseMigrationZip') }}
                </UiLink>
              </UiStack>

              <UiStack :gap="UiGap.Xs">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="font-semibold">
                  2. {{ t('migration.migration.installOnYourSourceServer') }}
                </UiText>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                  {{ t('migration.migration.extractToYourSourceSystem') }}
                  {{ t('migration.migration.pluginPath') }}
                  {{ t('migration.migration.thenActivateInAdminAddons') }}
                </UiText>
              </UiStack>

              <UiStack :gap="UiGap.Xs">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="font-semibold">
                  3. {{ t('migration.migration.copyTheSecretKeyInto') }}
                </UiText>
                <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm">
                  <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm" truncate class="flex-1 font-mono" data-testid="migration-secret-key">
                    {{ activeKey }}
                  </UiText>
                  <UiButton :action="UiActionKind.Copy"
                    :type="UiButtonType.Button"
                    :variant="UiButtonVariant.Secondary"
                    :size="UiSize.Sm"
                    data-testid="migration-copy-key"
                    @click="copyKey(activeKey)"
                  >
                    {{ copiedKey ? t('shared.copied') : t('shared.copy') }}
                  </UiButton>
                </UiStack>
              </UiStack>

              <UiStack :gap="UiGap.Xs">
                <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="font-semibold">
                  4. {{ t('migration.migration.testConnectionAndStartImport') }}
                </UiText>
                <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
                  {{ t('migration.migration.click') }}
                  {{ t('shared.test') }}
                  {{ t('migration.migration.toVerifyConnectivityThen') }}
                  {{ t('migration.migrationProgressCard.startImport') }}
                  {{ t('migration.migration.toBegin') }}
                </UiText>
              </UiStack>
            </UiStack>
          </UiCard>

          <!-- Import report -->
          <MigrationReportTable v-if="store.activeJob && showReport" :job-id="store.activeJob.id" />
        </UiStack>
      </template>
    </UiSplit>

    <!-- New job dialog -->
    <UiDialog
      :open="showNewJobModal"
      :title="t('migration.migration.newMigration')"
      :size="UiDialogSize.Lg"
      @close="closeNewJob"
    >
      <UiStack :gap="UiGap.Md">
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ t('migration.migration.aSecretKeyWillBe') }}</UiText>

        <UiField :label="t('migration.migration.sourceUrl')" :hint="t('migration.migration.modulesAddonsInnovayseMigrationApi')" required>
          <template #default="{ id, invalid, describedby }">
            <UiInput
              :id="id"
              v-model="newJobSourceUrl"
              :type="UiInputType.Url"
              :invalid="invalid"
              :aria-describedby="describedby"
              :placeholder="t('migration.migration.urlPlaceholder')"
              data-testid="migration-source-url"
            />
          </template>
        </UiField>

        <UiField :label="`${t('migration.migration.label')} ${t('migration.migration.optional')}`">
          <template #default="{ id }">
            <UiInput
              :id="id"
              v-model="newJobLabel"
              :type="UiInputType.Text"
              :placeholder="t('migration.migration.eGProductionServer')"
              data-testid="migration-label"
              @keydown.enter="handleCreateJob"
            />
          </template>
        </UiField>

        <!-- Data to import -->
        <UiStack :gap="UiGap.Sm">
          <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :justify="UiJustify.Between">
            <UiLabel :variant="UiLabelVariant.Field">{{ t('migration.migration.dataToImport') }}</UiLabel>
            <UiButton :type="UiButtonType.Button" :variant="UiButtonVariant.Link" :size="UiSize.Sm" data-testid="migration-toggle-all" @click="toggleAll">
              {{ allSelected ? t('shared.clear') : t('shared.selectAll') }}
            </UiButton>
          </UiStack>
          <UiGrid :columns="2" :gap="UiGap.Sm" container>
            <UiLabel v-for="key in MIGRATION_ENTITY_KEYS" :key="key" :variant="UiLabelVariant.Inline">
              <UiCheckbox v-model="flags[EXPORT_FLAG[key]]" :data-testid="`migration-export-${key}`" />
              {{ t(`migrationEntities.${key}`) }}
            </UiLabel>
          </UiGrid>
        </UiStack>
      </UiStack>

      <template #footer>
        <UiButton :action="UiActionKind.Cancel" :variant="UiButtonVariant.Secondary" data-testid="migration-dialog-cancel" @click="closeNewJob">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton :action="UiActionKind.Add"
          :variant="UiButtonVariant.Primary"
          :loading="creatingJob"
          :disabled="!newJobSourceUrl.trim()"
          data-testid="migration-create"
          @click="handleCreateJob"
        >
          {{ t('migration.migration.createMigration') }}
        </UiButton>
      </template>
    </UiDialog>

    <UiDialog
      :variant="UiDialogVariant.Confirm"
      :open="deleteTarget !== null"
      :title="t('migration.migration.deleteMigration')"
      :message="t('migration.migration.confirmDeleteMigration', { id: deleteTarget?.id ?? 0 })"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      data-testid="migration-delete-dialog"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiStack>
</template>
