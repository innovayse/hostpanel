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
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import type { DnsRecordItem } from '@/types/domain'
import { DnsRecordType } from '@/types/domain'

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

/** Icon markup (shape tags) of the row buttons. */
const ICONS = {
  add: '<line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />',
  edit: '<path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" /><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />',
  remove: '<polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />',
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
</script>

<template>
  <UiCard padding="md">
    <UiStack>
      <UiStack direction="row" :justify="UiJustify.Between" :align="UiAlignItems.Center">
        <UiHeading :level="2" variant="section">
          {{ t('clients.dnsRecordsTable.dnsRecords') }}
        </UiHeading>
        <UiIconButton
          :label="t('shared.add')"
          :size="UiSize.Sm"
          data-testid="clients-dns-add"
          @click="startAdd"
        >
          <UiIcon :markup="ICONS.add" :size="UiSize.Md" />
        </UiIconButton>
      </UiStack>

      <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="clients-dns-feedback" />

      <UiTable density="compact" :scrollable="false" :empty="isEmpty" :colspan="6" data-testid="clients-dns-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.type') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.dnsRecordsTable.host') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.value') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.dnsRecordsTable.ttl') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.priority') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.actions') }}</UiTableHeader>
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
              <UiTableCell>
                <UiStack direction="row" gap="xs" :align="UiAlignItems.Center">
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
              <UiTableCell>
                <UiStack direction="row" gap="xs" :align="UiAlignItems.Center">
                  <UiIconButton
                    :label="t('shared.edit')"
                    :size="UiSize.Sm"
                    :data-testid="`clients-dns-edit-${row.record.id}`"
                    @click="startEdit(row.record)"
                  >
                    <UiIcon :markup="ICONS.edit" :size="UiSize.Sm" />
                  </UiIconButton>
                  <UiIconButton
                    :label="t('shared.delete')"
                    :variant="UiButtonVariant.Ghost"
                    :size="UiSize.Sm"
                    :data-testid="`clients-dns-delete-${row.record.id}`"
                    @click="handleDelete(row.record.id)"
                  >
                    <UiIcon :markup="ICONS.remove" :size="UiSize.Sm" />
                  </UiIconButton>
                </UiStack>
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
      :loading-label="t('shared.deleting')"
      :loading="deleting"
      tone="danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiCard>
</template>
