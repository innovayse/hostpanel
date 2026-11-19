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
import UiDialog from '@/components/ui/UiDialog.vue'
import { Icon } from '@/lib/icons'
import { InvoiceBulkAction } from '@/types/invoice'
import { UiButtonVariant } from '@/utils/ui/button'
import type { UiBulkAction } from '@/utils/ui/bulkBar'

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

/** The actions that wait for the operator's confirmation before they run. */
const CONFIRMED_ACTIONS: readonly InvoiceBulkAction[] = [InvoiceBulkAction.MarkCancelled, InvoiceBulkAction.Delete]

/** The actions of the bar, in display order; the first is the primary one. The key is the action itself. */
const actions = computed<UiBulkAction[]>(() => [
  { key: InvoiceBulkAction.MarkPaid, label: t('billing.invoicesList.bulkPaid'), title: t('billing.invoicesList.markPaid'), icon: Icon.Check, variant: UiButtonVariant.Primary, testid: `${props.testid}-paid` },
  { key: InvoiceBulkAction.MarkUnpaid, label: t('billing.invoicesList.bulkUnpaid'), title: t('billing.invoicesList.markUnpaid'), icon: Icon.Close, variant: UiButtonVariant.Secondary, testid: `${props.testid}-unpaid` },
  { key: InvoiceBulkAction.MarkCancelled, label: t('billing.invoicesList.bulkCancelled'), title: t('shared.markCancelled'), icon: Icon.Remove, variant: UiButtonVariant.Secondary, testid: `${props.testid}-cancelled` },
  { key: InvoiceBulkAction.Duplicate, label: t('billing.invoicesList.bulkDuplicate'), title: t('billing.invoicesList.duplicateInvoice'), icon: Icon.Copy, variant: UiButtonVariant.Secondary, testid: `${props.testid}-duplicate` },
  { key: InvoiceBulkAction.Delete, label: t('shared.delete'), title: t('shared.delete'), icon: Icon.Trash, variant: UiButtonVariant.Danger, testid: `${props.testid}-delete` },
])

/**
 * Runs an action now, or asks first when it cancels or deletes.
 *
 * @param key - The key of the chosen action, one of the `InvoiceBulkAction` values.
 * @returns Resolves when the action has settled, or at once when it waits for confirmation.
 */
const choose = async (key: string): Promise<void> => {
  const action = key as InvoiceBulkAction
  if (CONFIRMED_ACTIONS.includes(action)) {
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
  <UiBulkBar :count="count" :actions="actions" :data-testid="testid" @clear="emit('clear')" @action="choose" />

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
