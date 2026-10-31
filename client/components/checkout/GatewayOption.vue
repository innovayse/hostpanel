<template>
  <UiOptionCard
    :name="name"
    :value="method.module"
    :model-value="modelValue"
    :label="method.displayName || method.module"
    :description="description"
    :icon="PAYMENT_GATEWAY_KIND_ICON[kind]"
    :disabled="disabled"
    @update:model-value="emit('update:modelValue', String($event))"
  />
</template>

<script setup lang="ts">
/**
 * GatewayOption - one payment gateway as a selectable row of a checkout picker.
 *
 * Picks the icon and the short description ("Pay by card") from the gateway's kind, so every
 * picker that lists gateways shows them the same way. Attributes it does not declare, such as
 * `testid` and the palette classes of a storefront template, go to the {@link UiOptionCard}.
 */
import { computed } from 'vue'
import { PAYMENT_GATEWAY_KIND_DESCRIPTION_KEY, PAYMENT_GATEWAY_KIND_ICON, paymentGatewayKind } from '~/constants/paymentGatewayKind'
import type { GatewayMethod } from '~/types/gatewaymethod'

/** Props of GatewayOption. */
interface Props {
  /** The gateway this row stands for. */
  method: GatewayMethod
  /** Group name shared by the rows of one picker. */
  name: string
  /** Module name of the chosen gateway (v-model). */
  modelValue?: string | null
  /** True when the row cannot be chosen, for instance because the person may not place orders. */
  disabled?: boolean
}

/** The props of the component. */
const props = withDefaults(defineProps<Props>(), { modelValue: undefined, disabled: false })

/** Emits the component raises. */
const emit = defineEmits<{
  /** This gateway was chosen. */
  'update:modelValue': [module: string]
}>()

/** The translator. */
const { t } = useI18n()

/** The kind of the gateway: its icon and description follow from it. */
const kind = computed(() => paymentGatewayKind(props.method.module))

/** The short description of the kind, or nothing for a gateway of an unknown kind. */
const description = computed(() => {
  const key = PAYMENT_GATEWAY_KIND_DESCRIPTION_KEY[kind.value]
  return key ? t(key) : undefined
})
</script>
