<script setup lang="ts">
/**
 * The state of a credit note as a coloured badge: Draft is neutral, Issued is informational, Applied is a success and Voided is
 * a warning, each with its translated name.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import { CreditNoteStatus } from '@/types/creditnote'
import { UiTone } from '@/utils/ui/tone'
import type { UiTone as UiToneValue } from '@/utils/ui/tone'

const { t } = useI18n()

const props = defineProps<{
  /** The state to show. */
  status: CreditNoteStatus
}>()

/** Tone of each state. */
const TONES: Record<CreditNoteStatus, UiToneValue> = {
  [CreditNoteStatus.Draft]: UiTone.Neutral,
  [CreditNoteStatus.Issued]: UiTone.Info,
  [CreditNoteStatus.Applied]: UiTone.Success,
  [CreditNoteStatus.Voided]: UiTone.Warning,
}

/** Tone of the state on show. */
const tone = computed<UiToneValue>(() => TONES[props.status])
</script>

<template>
  <UiBadge :tone="tone" dot small :data-testid="`billing-credit-note-status-${status}`">
    {{ t(`billing.creditNotes.status.${status}`) }}
  </UiBadge>
</template>
