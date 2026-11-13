<script setup lang="ts">
/**
 * Bulk action bar of an invoice list: mark paid (the primary action), mark unpaid, mark cancelled, duplicate and delete,
 * on a `UiBulkBar`. Cancelling and deleting are confirmed first in a danger prompt that names how many invoices they
 * touch; the other actions run at once. The bar owns only the choice and the prompt: the screen's `perform` runs the action
 * (and reloads), and the prompt stays open, with its button busy, until `perform` settles. Used by every invoice list that has
 * the bulk endpoint, so the actions, their labels and the prompts are written once.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBulkBar from '@/components/ui/UiBulkBar.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import { InvoiceBulkAction } from '@/types/invoice'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'

const { t } = useI18n()

/** Props for InvoiceBulkBar. */
const props = defineProps<{
  /** How many invoices are selected. */
  count: number
  /** Prefix of the `data-testid` of the bar, its buttons and the prompt. */
  testid: string
  /** Runs one bulk action on the selected invoices and resolves when it (and the reload) has settled. */
  perform: (action: InvoiceBulkAction) => Promise<void>
}>()

const emit = defineEmits<{
  /** Emitted when the user clears the selection. */
  clear: []
}>()

/** The destructive action waiting for confirmation, null when none. */
const pending = ref<InvoiceBulkAction | null>(null)

/** True while an action is running. */
const processing = ref(false)

/** True when the pending action is the delete (otherwise it is the cancel). */
const pendingIsDelete = computed<boolean>(() => pending.value === InvoiceBulkAction.Delete)

/** The buttons of the bar, in display order; the first is the primary action. `suffix` ends the button's test id. */
const actions = computed(() => [
  { action: InvoiceBulkAction.MarkPaid, suffix: 'paid', label: t('billing.invoicesList.markPaid'), variant: UiButtonVariant.Primary, confirmed: false },
  { action: InvoiceBulkAction.MarkUnpaid, suffix: 'unpaid', label: t('billing.invoicesList.markUnpaid'), variant: UiButtonVariant.Secondary, confirmed: false },
  { action: InvoiceBulkAction.MarkCancelled, suffix: 'cancelled', label: t('shared.markCancelled'), variant: UiButtonVariant.Secondary, confirmed: true },
  { action: InvoiceBulkAction.Duplicate, suffix: 'duplicate', label: t('billing.invoicesList.duplicateInvoice'), variant: UiButtonVariant.Secondary, confirmed: false },
  { action: InvoiceBulkAction.Delete, suffix: 'delete', label: t('shared.delete'), variant: UiButtonVariant.Danger, confirmed: true },
])

/**
 * Runs an action now, or asks first when it cancels or deletes.
 *
 * @param action - The chosen action.
 * @param confirmed - True when the action needs the operator's confirmation.
 * @returns Resolves when the action has settled, or at once when it waits for confirmation.
 */
const choose = async (action: InvoiceBulkAction, confirmed: boolean): Promise<void> => {
  if (confirmed) {
    pending.value = action
    return
  }
  await props.perform(action)
}

/**
 * Runs the confirmed action and closes the prompt.
 *
 * @returns Resolves when the action has settled.
 */
const confirm = async (): Promise<void> => {
  const action = pending.value
  if (action === null) {
    return
  }
  processing.value = true
  await props.perform(action)
  processing.value = false
  pending.value = null
}

/** Closes the prompt; ignored while the action runs. */
const dismiss = (): void => {
  if (!processing.value) {
    pending.value = null
  }
}
</script>

<template>
  <UiBulkBar :count="count" :data-testid="testid" @clear="emit('clear')">
    <UiButton
      v-for="entry in actions"
      :key="entry.action"
      :variant="entry.variant"
      :size="UiSize.Sm"
      :data-testid="`${testid}-${entry.suffix}`"
      @click="choose(entry.action, entry.confirmed)"
    >
      {{ entry.label }}
    </UiButton>
  </UiBulkBar>

  <UiDialog
    variant="confirm"
    :open="pending !== null"
    :title="pendingIsDelete ? t('billing.invoicesList.deleteInvoices') : t('billing.invoicesList.cancelInvoices')"
    :message="pendingIsDelete
      ? t('billing.invoicesList.deleteInvoicesMessage', { count }, count)
      : t('billing.invoicesList.cancelInvoicesMessage', { count }, count)"
    :confirm-label="pendingIsDelete ? t('shared.delete') : t('shared.markCancelled')"
    :loading="processing"
    tone="danger"
    :data-testid="`${testid}-confirm`"
    @confirm="confirm"
    @close="dismiss"
  />
</template>
