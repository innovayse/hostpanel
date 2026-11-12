<template>
  <UiCheckbox
    :model-value="modelValue"
    :label="$t('checkout.saveForRenewals')"
    :description="$t('checkout.saveForRenewalsAgreement')"
    :disabled="disabled"
    :testid="testid"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>

<script setup lang="ts">
/**
 * SaveOption - the checkout choice "Save this payment method for automatic renewals".
 *
 * Off by default (the parent owns the value). The text under the label is the agreement to charge
 * later renewal invoices, so ticking the box is the payer's consent; the server records it from the
 * start-payment request that carries the box, and the gateway saves the method within that payment.
 */

/** Props of SaveOption. */
interface Props {
  /** Whether the box is ticked (v-model). */
  modelValue?: boolean
  /** True while the box cannot be changed, for instance during the hand-off to the bank. */
  disabled?: boolean
  /** `data-testid` of the box. */
  testid?: string
}

withDefaults(defineProps<Props>(), { modelValue: false, disabled: false, testid: undefined })

/** Emits the component raises. */
const emit = defineEmits<{
  /** The box was ticked or cleared. */
  'update:modelValue': [value: boolean]
}>()
</script>
