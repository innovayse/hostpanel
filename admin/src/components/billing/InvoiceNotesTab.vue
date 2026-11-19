<script setup lang="ts">
/**
 * Notes tab of the invoice screen: the admin-only notes (shown read-only or edited and saved),
 * with the line items and the transaction ledgers below. The outcome of a save (success, or the
 * API's sentence) shows under the editor.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import InvoiceItemsSection from '@/components/billing/InvoiceItemsSection.vue'
import InvoiceTransactionsSection from '@/components/billing/InvoiceTransactionsSection.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useResultBanner } from '@/composables/useResultBanner'
import { useBillingStore } from '@/stores/billing'
import type { InvoiceDetail } from '@/types/invoice'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice whose notes are shown. */
  invoice: InvoiceDetail
  /** True to show the notes and the items without editing controls. */
  readonly?: boolean
}>()

const store = useBillingStore()

/** The notes being edited. */
const notes = ref('')

/** True while the notes are being saved. */
const saving = ref(false)

/** The outcome line of the last save, shown under the editor. */
const feedback = useResultBanner()

/** Tone of the outcome line: danger for a refusal, success otherwise. */
const feedbackTone = computed<UiTone>(() => (feedback.isError.value ? UiTone.Danger : UiTone.Success))

/** Resets the editor to the notes the invoice carries. */
const populate = (): void => {
  notes.value = props.invoice.notes ?? ''
}

/**
 * Saves the notes and shows the outcome.
 *
 * @returns Resolves when the request has settled.
 */
const save = async (): Promise<void> => {
  feedback.clear()
  saving.value = true
  const ok = await store.updateNotes(props.invoice.id, notes.value)
  saving.value = false
  if (ok) {
    feedback.show(t('shared.changesSavedSuccessfully'), false)
  } else {
    feedback.show(store.error ?? t('errors.noAnswer'), true)
  }
}

onMounted(populate)
watch(() => props.invoice.notes, populate)
</script>

<template>
  <UiStack data-testid="billing-invoice-notes">
    <template v-if="readonly">
      <UiAlert v-if="!invoice.notes" variant="warning" data-testid="billing-invoice-notes-empty">
        {{ t('billing.invoiceNotesTab.thereAreNoNotesOn') }}
      </UiAlert>
      <UiCard v-else padding="md">
        <UiText tone="primary" size="md" class="whitespace-pre-wrap" data-testid="billing-invoice-notes-text">
          {{ invoice.notes }}
        </UiText>
      </UiCard>
    </template>

    <UiCard v-else padding="md">
      <UiStack>
        <UiTextarea
          v-model="notes"
          rows="8"
          :placeholder="t('shared.internalNotesVisibleOnlyTo')"
          data-testid="billing-invoice-notes-input"
        />
        <UiStack direction="row" justify="center">
          <UiButton :action="UiActionKind.Save"
            variant="primary"
            :loading="saving"
            data-testid="billing-invoice-notes-save"
            @click="save"
          >
            {{ t('shared.saveChanges') }}
          </UiButton>
        </UiStack>
        <UiStatusMessage :message="feedback.message.value" :tone="feedbackTone" data-testid="billing-invoice-notes-feedback" />
      </UiStack>
    </UiCard>

    <InvoiceItemsSection :invoice="invoice" :readonly="readonly" />
    <InvoiceTransactionsSection :invoice="invoice" />
  </UiStack>
</template>
