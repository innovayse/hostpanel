<script setup lang="ts">
/**
 * Inline CRUD table for managing email forwarding rules on a domain.
 * Supports adding, editing, and deleting individual forwarding rules. Adding and editing share
 * one draft and one inline row. A delete is confirmed first, and every change reports its
 * outcome (success, or the API's sentence) under the heading.
 */
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDomainsStore } from '@/stores/domains'
import { useResultBanner } from '@/composables/useResultBanner'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
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
import UiToggle from '@/components/ui/UiToggle.vue'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import type { EmailForwardingRuleItem } from '@/types/domain'
import { UiTableDensity } from '@/utils/ui/injection'

const { t } = useI18n()

/** Props for EmailForwardingTable. */
const props = defineProps<{
  /** Domain ID to manage email forwarding for. */
  domainId: number
  /** Current list of email forwarding rules. */
  rules: EmailForwardingRuleItem[]
}>()

const emit = defineEmits<{
  /** Emitted after a successful add, edit, or delete to reload domain data. */
  refresh: []
}>()

/** Icon markup (shape tags) of the add, save and cancel buttons. */
const ICONS = {
  add: '<line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />',
  confirm: '<polyline points="20 6 9 17 4 12" />',
  cancel: '<line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />',
} as const

/** Domains store, the only caller of the forwarding endpoints. */
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

/** ID of the rule currently being edited inline, or null. */
const editingId = ref<number | null>(null)

/** Source alias of the row being added or edited. */
const draftSource = ref('')

/** Destination email of the row being added or edited. */
const draftDestination = ref('')

/** One line of the table: a stored rule or the draft row. */
interface ForwardingTableRow {
  /** Stable key for `v-for`. */
  key: string
  /** The stored rule, absent on the add row. */
  rule?: EmailForwardingRuleItem
  /** True when the draft inputs are shown in this row. */
  draft: boolean
}

/** Rows to render: the add row first, then every rule (edited one as the draft row). */
const tableRows = computed<ForwardingTableRow[]>(() => [
  ...(showAddRow.value ? [{ key: 'new', draft: true }] : []),
  ...props.rules.map(rule => ({ key: String(rule.id), rule, draft: editingId.value === rule.id })),
])

/** True when there is nothing to list and no add row open. */
const isEmpty = computed<boolean>(() => props.rules.length === 0 && !showAddRow.value)

/**
 * Opens the inline add row with empty values.
 *
 * @returns Nothing.
 */
const startAdd = (): void => {
  editingId.value = null
  draftSource.value = ''
  draftDestination.value = ''
  showAddRow.value = true
}

/**
 * Opens inline edit for a rule, populating the draft.
 *
 * @param rule - The email forwarding rule to edit.
 * @returns Nothing.
 */
const startEdit = (rule: EmailForwardingRuleItem): void => {
  showAddRow.value = false
  editingId.value = rule.id
  draftSource.value = rule.source
  draftDestination.value = rule.destination
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
 * @returns Promise that resolves when the rule is stored.
 */
const saveDraft = async (): Promise<void> => {
  const source = draftSource.value.trim()
  const destination = draftDestination.value.trim()
  if (!source || !destination) {
    return
  }
  const id = editingId.value
  feedback.clear()
  saving.value = true
  const ok = id === null
    ? await store.addForwardingRule(props.domainId, source, destination)
    : await store.updateForwardingRule(props.domainId, id, { source, destination })
  saving.value = false
  report(ok, 'clients.emailForwardingTable.saved')
  if (ok) {
    cancelDraft()
    emit('refresh')
  }
}

/**
 * Toggles the active state of a rule via the API.
 *
 * @param rule - The rule to toggle.
 * @returns Promise that resolves when the toggle completes.
 */
const toggleActive = async (rule: EmailForwardingRuleItem): Promise<void> => {
  feedback.clear()
  saving.value = true
  const updated = await store.updateForwardingRule(props.domainId, rule.id, {
    source: rule.source,
    destination: rule.destination,
    isActive: !rule.isActive,
  })
  saving.value = false
  report(updated, 'clients.emailForwardingTable.saved')
  if (updated) {
    emit('refresh')
  }
}

/** ID of the rule awaiting delete confirmation, null when nothing is staged. */
const deleteTargetId = ref<number | null>(null)

/** True while the staged rule is being deleted. */
const deleting = ref(false)

/**
 * Stages a forwarding rule for deletion. Only opens the prompt: {@link confirmDelete} does the
 * work, so the question is asked by the app's own modal instead of the browser's blocking dialog.
 *
 * @param ruleId - The ID of the rule to delete.
 * @returns Nothing.
 */
const handleDelete = (ruleId: number): void => {
  deleteTargetId.value = ruleId
}

/**
 * Deletes the staged forwarding rule and clears the prompt.
 *
 * The target is cleared whether or not the store accepted the delete, so a refusal still
 * dismisses the modal and is shown as the store's `actionError`.
 *
 * @returns Promise that resolves when the rule is deleted.
 */
const confirmDelete = async (): Promise<void> => {
  const ruleId = deleteTargetId.value
  if (ruleId === null) {
    return
  }
  feedback.clear()
  saving.value = true
  deleting.value = true
  const deleted = await store.deleteForwardingRule(props.domainId, ruleId)
  saving.value = false
  deleting.value = false
  deleteTargetId.value = null
  report(deleted, 'clients.emailForwardingTable.deleted')
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
 * The row actions of a forwarding rule: edit, and delete last (confirmed first).
 *
 * @param rule - The row.
 * @returns The action list passed to UiRowActions.
 */
const forwardingRuleActions = (rule: EmailForwardingRuleItem): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    testid: `clients-forwarding-edit-${rule.id}`,
    /**
     * Starts editing this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      startEdit(rule)
    },
  },
  {
    label: t('shared.delete'),
    testid: `clients-forwarding-delete-${rule.id}`,
    /**
     * Asks for confirmation, then deletes this row.
     *
     * @returns Nothing.
     */
    onSelect: (): void => {
      handleDelete(rule.id)
    },
    danger: true,
  },
]
</script>

<template>
  <UiCard padding="md">
    <UiStack>
      <UiSectionHeader :level="2" variant="section">
        {{ t('clients.emailForwardingTable.emailForwardingRules') }}

        <template #actions>
          <UiIconButton
            :label="t('shared.add')"
            :size="UiSize.Sm"
            data-testid="clients-forwarding-add"
            @click="startAdd"
          >
            <UiIcon :markup="ICONS.add" :size="UiSize.Md" />
          </UiIconButton>
        </template>
      </UiSectionHeader>

      <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="clients-forwarding-feedback" />

      <UiTable :density="UiTableDensity.Compact" :empty="isEmpty" data-testid="clients-forwarding-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('clients.emailForwardingTable.source') }}</UiTableHeader>
            <UiTableHeader>{{ t('clients.emailForwardingTable.destination') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.active') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="row in tableRows" :key="row.key" :hoverable="!row.draft" :selected="row.draft">
            <template v-if="row.draft">
              <UiTableCell>
                <UiInput
                  v-model="draftSource"
                  type="text"
                  :placeholder="t('shared.examples.emailLocalPart')"
                  data-testid="clients-forwarding-draft-source"
                />
              </UiTableCell>
              <UiTableCell>
                <UiInput
                  v-model="draftDestination"
                  type="text"
                  :placeholder="t('shared.examples.email')"
                  data-testid="clients-forwarding-draft-destination"
                />
              </UiTableCell>
              <UiTableCell>
                <UiText :tone="UiTextTone.Muted">&mdash;</UiText>
              </UiTableCell>
              <UiTableCell actions>
                <UiStack direction="row" gap="xs" :justify="UiJustify.End" :align="UiAlignItems.Center">
                  <UiIconButton
                    :label="t('shared.save')"
                    :size="UiSize.Sm"
                    :disabled="saving"
                    data-testid="clients-forwarding-draft-save"
                    @click="saveDraft"
                  >
                    <UiIcon :markup="ICONS.confirm" :size="UiSize.Md" />
                  </UiIconButton>
                  <UiIconButton
                    :label="t('shared.cancel')"
                    :size="UiSize.Sm"
                    data-testid="clients-forwarding-draft-cancel"
                    @click="cancelDraft"
                  >
                    <UiIcon :markup="ICONS.cancel" :size="UiSize.Md" />
                  </UiIconButton>
                </UiStack>
              </UiTableCell>
            </template>
            <template v-else-if="row.rule">
              <UiTableCell>{{ row.rule.source }}</UiTableCell>
              <UiTableCell>{{ row.rule.destination }}</UiTableCell>
              <UiTableCell>
                <UiToggle
                  :model-value="row.rule.isActive"
                  :data-testid="`clients-forwarding-toggle-${row.rule.id}`"
                  @update:model-value="toggleActive(row.rule)"
                />
              </UiTableCell>
              <UiTableCell actions>
                <UiRowActions :actions="forwardingRuleActions(row.rule)" :testid="`clients-forwarding-actions-${row.key}`" />
              </UiTableCell>
            </template>
          </UiTableRow>
        </UiTableBody>
        <template #empty>
          {{ t('clients.emailForwardingTable.noEmailForwardingRulesFound') }}
        </template>
      </UiTable>
    </UiStack>

    <UiDialog
      v-if="deleteTargetId !== null"
      variant="confirm"
      :title="t('clients.emailForwardingTable.deleteForwardingRule')"
      :message="t('clients.emailForwardingTable.deleteThisEmailForwardingRule')"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      tone="danger"
      @confirm="confirmDelete"
      @close="dismissDelete"
    />
  </UiCard>
</template>
