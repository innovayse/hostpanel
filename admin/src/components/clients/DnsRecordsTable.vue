<script setup lang="ts">
/**
 * Inline CRUD table for managing DNS records on a domain.
 * Supports adding, editing, and deleting individual DNS records. Adding and editing share one
 * draft and one inline row. A delete is confirmed first, and every change reports its outcome
 * (success, or the API's sentence) under the heading.
 */
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDomainsStore } from '@/stores/domains'
import { useResultBanner } from '@/composables/useResultBanner'
import { DNS_RECORD_TYPE_OPTIONS } from '@/lib/constants'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import type { DnsRecordItem } from '@/types/domain'
import { DnsRecordType } from '@/types/domain'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Props for DnsRecordsTable. */
const props = defineProps<{
  /** Domain ID to manage DNS records for. */
  domainId: number
  /** Current list of DNS records. */
  records: DnsRecordItem[]
}>()

const emit = defineEmits<{
  /** Emitted after a successful add, edit, or delete to reload domain data. */
  refresh: []
}>()

/** DNS record types that carry a priority. */
const PRIORITY_TYPES: readonly string[] = [DnsRecordType.Mx, DnsRecordType.Srv]

/** Default record type of a new record. */
const DEFAULT_TYPE = 'A'

/** Default TTL, in seconds. */
const DEFAULT_TTL = '3600'

/** Icon markup (shape tags) of the add, save and cancel buttons. */
const ICONS = {
  add: '<line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />',
  confirm: '<polyline points="20 6 9 17 4 12" />',
  cancel: '<line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />',
} as const

/** Domains store, the only caller of the DNS endpoints. */
const store = useDomainsStore()

/** True while an API call is in flight. */
const saving = ref(false)

/** The outcome line of the last change, shown under the heading. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/**
 * Shows the outcome of one change: the success sentence, or the API's sentence of the refusal.
 *
 * @param ok - Whether the API accepted the change.
 * @param successKey - Locale key of the success sentence.
 */
const report = (ok: boolean, successKey: string): void => {
  if (ok) {
    feedback.show(t(successKey), false)
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/** Whether the inline add row is visible. */
const showAddRow = ref(false)

/** ID of the record currently being edited inline, or null. */
const editingId = ref<number | null>(null)

/** Record type of the row being added or edited. */
const draftType = ref(DEFAULT_TYPE)

/** Host of the row being added or edited. */
const draftHost = ref('')

/** Value of the row being added or edited. */
const draftValue = ref('')

/** TTL of the row being added or edited. */
const draftTtl = ref(DEFAULT_TTL)

/** Priority of the row being added or edited (MX/SRV only). */
const draftPriority = ref('')

/** Whether the draft's type requires a priority field. */
const draftNeedsPriority = computed<boolean>(() => PRIORITY_TYPES.includes(draftType.value))

/** One line of the table: a stored record or the draft row. */
interface DnsTableRow {
  /** Stable key for `v-for`. */
  key: string
  /** The stored record, absent on the add row. */
  record?: DnsRecordItem
  /** True when the draft inputs are shown in this row. */
  draft: boolean
}

/** Rows to render: the add row first, then every record (edited one as the draft row). */
const tableRows = computed<DnsTableRow[]>(() => [
  ...(showAddRow.value ? [{ key: 'new', draft: true }] : []),
  ...props.records.map(record => ({ key: String(record.id), record, draft: editingId.value === record.id })),
])

/** True when there is nothing to list and no add row open. */
const isEmpty = computed<boolean>(() => props.records.length === 0 && !showAddRow.value)

/**
 * Resets the draft to its defaults.
 *
 * @returns Nothing.
 */
const resetDraft = (): void => {
  draftType.value = DEFAULT_TYPE
  draftHost.value = ''
  draftValue.value = ''
  draftTtl.value = DEFAULT_TTL
  draftPriority.value = ''
}

/**
 * Builds the request body from the draft.
 *
 * @returns The record payload sent to the store.
 */
const draftPayload = () => ({
  type: draftType.value,
  host: draftHost.value.trim(),
  value: draftValue.value.trim(),
  ttl: parseInt(draftTtl.value) || Number(DEFAULT_TTL),
  priority: draftNeedsPriority.value && draftPriority.value ? parseInt(draftPriority.value) : null,
})

/**
 * Opens the inline add row with default values.
 *
 * @returns Nothing.
 */
const startAdd = (): void => {
  editingId.value = null
  resetDraft()
  showAddRow.value = true
}

/**
 * Opens the inline edit for a record, populating the draft.
 *
 * @param record - The DNS record to edit.
 * @returns Nothing.
 */
const startEdit = (record: DnsRecordItem): void => {
  showAddRow.value = false
  editingId.value = record.id
  draftType.value = record.type
  draftHost.value = record.host
  draftValue.value = record.value
  draftTtl.value = String(record.ttl)
  draftPriority.value = record.priority != null ? String(record.priority) : ''
}

/**
 * Closes the add or edit row without saving.
 *
 * @returns Nothing.
 */
const cancelDraft = (): void => {
  showAddRow.value = false
  editingId.value = null
}

/**
 * Saves the draft (create when adding, update when editing) and emits refresh.
 *
 * @returns Promise that resolves when the record is stored.
 */
const saveDraft = async (): Promise<void> => {
  if (!draftHost.value.trim() || !draftValue.value.trim()) {
    return
  }
  const id = editingId.value
  feedback.clear()
  saving.value = true
  const ok = id === null
    ? await store.addDnsRecord(props.domainId, draftPayload())
    : await store.updateDnsRecord(props.domainId, id, draftPayload())
  saving.value = false
  report(ok, 'clients.dnsRecordsTable.saved')
  if (ok) {
    cancelDraft()
    emit('refresh')
  }
}

/** ID of the record awaiting delete confirmation, null when nothing is staged. */
const deleteTargetId = ref<number | null>(null)

/** True while the staged record is being deleted. */
const deleting = ref(false)

/**
 * Stages a DNS record for deletion. Only opens the prompt: {@link confirmDelete} does the work,
 * so the question is asked by the app's own modal instead of the browser's blocking dialog.
 *
 * @param recordId - The ID of the record to delete.
 * @returns Nothing.
 */
const handleDelete = (recordId: number): void => {
  deleteTargetId.value = recordId
}

/**
 * Deletes the staged DNS record and clears the prompt.
 *
 * The target is cleared whether or not the store accepted the delete, so a refusal still
 * dismisses the modal and is shown as the store's `actionError`.
 *
 * @returns Promise that resolves when the record is deleted.
 */
const confirmDelete = async (): Promise<void> => {
  const recordId = deleteTargetId.value
  if (recordId === null) {
    return
  }
  feedback.clear()
  saving.value = true
  deleting.value = true
  const deleted = await store.deleteDnsRecord(props.domainId, recordId)
  saving.value = false
  deleting.value = false
  deleteTargetId.value = null
  report(deleted, 'clients.dnsRecordsTable.deleted')
  if (deleted) {
    emit('refresh')
  }
}

/** Closes the delete confirmation; ignored while the delete runs. */
const dismissDelete = (): void => {
  if (!deleting.value) {
    deleteTargetId.value = null
  }
}

/**
 * The row actions of a DNS record: edit, and delete last (confirmed first).
 *
 * @param record - The row.
 * @returns The action list passed to UiRowActions.
 */
const dnsRecordActions = (record: DnsRecordItem): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    testid: `clients-dns-edit-${record.id}`,
    /**
     * Starts editing this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      startEdit(record)
    },
  },
  {
    label: t('shared.delete'),
    testid: `clients-dns-delete-${record.id}`,
    /**
     * Asks for confirmation, then deletes this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      handleDelete(record.id)
    },
    danger: true,
  },
]
</script>

<template>
  <UiCard padding="md">
    <UiStack>
      <UiSectionHeader :level="2" variant="section">
        {{ t('clients.dnsRecordsTable.dnsRecords') }}

        <template #actions>
          <UiIconButton
            :label="t('shared.add')"
            :size="UiSize.Sm"
            data-testid="clients-dns-add"
            @click="startAdd"
          >
            <UiIcon :markup="ICONS.add" :size="UiSize.Md" />
          </UiIconButton>
        </template>
      </UiSectionHeader>

      <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="clients-dns-feedback" />

      <UiTable :density="UiTableDensity.Compact" :scrollable="false" :empty="isEmpty" data-testid="clients-dns-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.dnsRecordsTable.host') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.value') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.dnsRecordsTable.ttl') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.priority') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="row in tableRows" :key="row.key" :hoverable="!row.draft" :selected="row.draft">
            <template v-if="row.draft">
              <UiTableCell>
                <UiSelect v-model="draftType" :options="DNS_RECORD_TYPE_OPTIONS" :placeholder="t('shared.type')" data-testid="clients-dns-draft-type" />
              </UiTableCell>
              <UiTableCell>
                <UiInput
                  v-model="draftHost"
                  type="text"
                  placeholder="@"
                  data-testid="clients-dns-draft-host"
                />
              </UiTableCell>
              <UiTableCell>
                <UiInput
                  v-model="draftValue"
                  type="text"
                  :placeholder="t('shared.value')"
                  data-testid="clients-dns-draft-value"
                />
              </UiTableCell>
              <UiTableCell>
                <UiInput
                  v-model="draftTtl"
                  type="text"
                  :placeholder="DEFAULT_TTL"
                  data-testid="clients-dns-draft-ttl"
                />
              </UiTableCell>
              <UiTableCell>
                <UiNumberInput v-if="draftNeedsPriority" v-model="draftPriority" placeholder="10" data-testid="clients-dns-draft-priority" />
                <UiText v-else :tone="UiTextTone.Muted">&mdash;</UiText>
              </UiTableCell>
              <UiTableCell actions>
                <UiStack direction="row" gap="xs" :justify="UiJustify.End" :align="UiAlignItems.Center">
                  <UiIconButton
                    :label="t('shared.save')"
                    :size="UiSize.Sm"
                    :disabled="saving"
                    data-testid="clients-dns-draft-save"
                    @click="saveDraft"
                  >
                    <UiIcon :markup="ICONS.confirm" :size="UiSize.Md" />
                  </UiIconButton>
                  <UiIconButton
                    :label="t('shared.cancel')"
                    :size="UiSize.Sm"
                    data-testid="clients-dns-draft-cancel"
                    @click="cancelDraft"
                  >
                    <UiIcon :markup="ICONS.cancel" :size="UiSize.Md" />
                  </UiIconButton>
                </UiStack>
              </UiTableCell>
            </template>
            <template v-else-if="row.record">
              <UiTableCell>{{ row.record.type }}</UiTableCell>
              <UiTableCell>{{ row.record.host }}</UiTableCell>
              <UiTableCell>{{ row.record.value }}</UiTableCell>
              <UiTableCell muted>{{ row.record.ttl }}</UiTableCell>
              <UiTableCell muted>{{ row.record.priority ?? '—' }}</UiTableCell>
              <UiTableCell actions>
                <UiRowActions :actions="dnsRecordActions(row.record)" :testid="`clients-dns-actions-${row.key}`" />
              </UiTableCell>
            </template>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('clients.dnsRecordsTable.noDnsRecordsFound') }}
        </template>
      </UiTable>
    </UiStack>

    <UiDialog
      v-if="deleteTargetId !== null"
      variant="confirm"
      :title="t('clients.dnsRecordsTable.deleteDnsRecord')"
      :message="t('clients.dnsRecordsTable.deleteThisDnsRecord')"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      tone="danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiCard>
</template>
