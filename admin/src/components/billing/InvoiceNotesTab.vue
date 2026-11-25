<script setup lang="ts">
/**
 * Notes tab of the invoice screen: the admin-only notes (shown read-only or edited and saved),
 * The outcome of a save (success, or the API's sentence) shows under the editor.
 */
import { onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useBillingStore } from '@/stores/billing'
import type { InvoiceDetail } from '@/types/invoice'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiCardPadding } from '@/utils/ui/card'
import { UiDirection, UiJustify } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

const props = defineProps<{
  /** The invoice whose notes are shown. */
  invoice: InvoiceDetail
  /** True to show the notes without editing controls. */
  readonly?: boolean
}>()

const store = useBillingStore()

/** The notes being edited. */
const notes = ref('')

/** True while the notes are being saved. */
const saving = ref(false)

/** The outcome line of the last save, shown under the editor. */
const feedback = useOutcomeToast()

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
      <UiAlert v-if="!invoice.notes" :variant="UiTone.Warning" data-testid="billing-invoice-notes-empty">
        {{ t('billing.invoiceNotesTab.thereAreNoNotesOn') }}
      </UiAlert>
      <UiCard v-else :padding="UiCardPadding.Md">
        <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md" class="whitespace-pre-wrap" data-testid="billing-invoice-notes-text">
          {{ invoice.notes }}
        </UiText>
      </UiCard>
    </template>

    <UiCard v-else :padding="UiCardPadding.Md">
      <UiStack>
        <UiTextarea
          v-model="notes"
          rows="8"
          :placeholder="t('shared.internalNotesVisibleOnlyTo')"
          data-testid="billing-invoice-notes-input"
        />
        <UiStack :direction="UiDirection.Row" :justify="UiJustify.Center">
          <UiButton :action="UiActionKind.Save"
            :variant="UiButtonVariant.Primary"
            :loading="saving"
            data-testid="billing-invoice-notes-save"
            @click="save"
          >
            {{ t('shared.save') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiCard>
  </UiStack>
</template>
