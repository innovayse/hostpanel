<script setup lang="ts">
/**
 * Body of the VAT card on the client summary (the card and its title are the schema's, drawn by `UiDetailCards`): shows the last check of the client's VAT number (their Tax ID): the number, its status as a
 * badge, the date of the check and the registered name and address the service returned, and the button that runs the check
 * again. A business client with a valid number in another EU country or the UK is invoiced without VAT, so staff read the
 * status here. Without a number the card says where to enter it and the button is off. The API is the only judge: its sentence
 * for a check that could not be completed (the service gave no verdict) is toasted by the screen, and the status stays as it was.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { formatDateTime } from '@/lib/format'
import { clientVatStatusTone } from '@/lib/tones'
import type { ClientVat } from '@/types/clientvat'
import { UiButtonType, UiButtonVariant } from '@/utils/ui/button'
import { UiDirection } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'

const { t } = useI18n()

/** Props for ClientVatCard. */
const props = withDefaults(defineProps<{
  /** The client's VAT number (their Tax ID), null when none is recorded. */
  taxId?: string | null
  /** The last completed check of that number, null when the API sent none. */
  vat?: ClientVat | null
  /** Whether a check is in flight. */
  checking?: boolean
}>(), {
  taxId: null,
  vat: null,
  checking: false,
})

const emit = defineEmits<{
  /** Emitted when staff ask for the check to run again. */
  check: []
}>()

/** Whether there is a number to check. */
const hasNumber = computed<boolean>(() => (props.taxId ?? '').trim() !== '')

/** The verdict, or null while the number was never checked or changed since. */
const status = computed(() => props.vat?.status ?? null)

/**
 * Asks the view to run the check.
 *
 * @returns Nothing.
 */
const handleCheck = (): void => {
  emit('check')
}
</script>

<template>
  <UiStack data-testid="client-vat">
    <UiText v-if="!hasNumber" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="client-vat-none">
      {{ t('clients.vat.none') }}
    </UiText>
    <template v-else>
      <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Md" data-testid="client-vat-number">{{ taxId }}</UiText>
      <UiStack :direction="UiDirection.Row">
        <UiBadge v-if="status" :tone="clientVatStatusTone(status)" dot data-testid="client-vat-status">
          {{ t(`clients.vat.status.${status}`) }}
        </UiBadge>
        <UiBadge v-else :tone="UiTone.Neutral" data-testid="client-vat-status">{{ t('clients.vat.status.none') }}</UiBadge>
        <UiText v-if="vat?.checkedAt" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="client-vat-date">
          {{ t('clients.vat.checkedAt', { date: formatDateTime(vat.checkedAt) }) }}
        </UiText>
      </UiStack>
      <UiText v-if="vat?.name" :tone="UiTextTone.Primary" :size="UiTextSize.Sm" data-testid="client-vat-name">{{ vat.name }}</UiText>
      <UiText v-if="vat?.address" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="client-vat-address">
        {{ vat.address }}
      </UiText>
    </template>
    <UiStack :direction="UiDirection.Row">
      <UiButton :action="UiActionKind.Test"
        :type="UiButtonType.Button"
        :variant="UiButtonVariant.Secondary"
        :loading="checking"
        :disabled="!hasNumber"
        :title="t('clients.vat.check')"
        :aria-label="t('clients.vat.check')"
        data-testid="client-vat-check"
        @click="handleCheck"
      >
        {{ t('shared.test') }}
      </UiButton>
    </UiStack>
  </UiStack>
</template>
