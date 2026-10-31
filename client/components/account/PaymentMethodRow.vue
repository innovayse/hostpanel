<template>
  <UiStack direction="row" align="center" justify="between" gap="sm" wrap class="py-2" :data-testid="`account-payment-${method.id}`">
    <UiStack direction="row" align="center" gap="sm">
      <CreditCard :size="22" :stroke-width="1.5" class="text-gray-500 dark:text-gray-400 flex-shrink-0" />
      <UiStack gap="none">
        <UiStack direction="row" align="center" gap="xs" wrap>
          <UiText size="sm" weight="medium">{{ method.description || method.gatewayName }}</UiText>
          <UiBadge variant="success" size="sm" rounded>{{ $t('client.payment.statusActive') }}</UiBadge>
          <UiBadge v-if="method.isDefault" variant="primary" size="sm" rounded data-testid="account-payment-default-badge">
            {{ $t('client.payment.defaultBadge') }}
          </UiBadge>
          <UiBadge v-if="method.autoCharge" variant="info" size="sm" rounded data-testid="account-payment-auto-badge">
            {{ $t('client.payment.autoChargeBadge') }}
          </UiBadge>
        </UiStack>
        <UiText size="xs" tone="subtle">
          <span data-testid="account-payment-type">{{ typeLabel }}</span>
          <span v-if="method.cardExpiry"> · {{ $t('client.payment.expires') }} {{ method.cardExpiry }}</span>
        </UiText>
      </UiStack>
    </UiStack>

    <UiStack direction="row" align="center" gap="xs">
      <UiButton
        v-if="!method.isDefault"
        size="sm"
        variant="outline"
        :loading="settingDefault"
        :disabled="busy"
        :data-testid="`account-payment-default-${method.id}`"
        @click="emit('setDefault', method.id)"
      >
        <Star :size="13" :stroke-width="2" class="mr-1" />
        {{ $t('client.payment.setDefault') }}
      </UiButton>
      <UiButton
        size="sm"
        variant="danger"
        :loading="removing"
        :disabled="busy"
        :data-testid="`account-payment-remove-${method.id}`"
        @click="emit('remove', method.id)"
      >
        <Trash2 :size="13" :stroke-width="2" class="mr-1" />
        {{ $t('client.payment.remove') }}
      </UiButton>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PaymentMethodRow - one saved card or bank account with its "set default" and "remove" actions.
 *
 * Pure presentation: it announces what was asked and the tab decides what to do.
 *
 * @module components/account/PaymentMethodRow
 */

import { CreditCard, Star, Trash2 } from 'lucide-vue-next'
import { PaymentMethodType } from '~/types/payment'
import type { PaymentMethod } from '~/types/payment'

/** Props of {@link PaymentMethodRow}. */
interface Props {
  /** The saved method. */
  method: PaymentMethod
  /** Whether this method is being made the default. */
  settingDefault?: boolean
  /** Whether this method is being removed. */
  removing?: boolean
}

/** The props of the component. */
const props = withDefaults(defineProps<Props>(), {
  settingDefault: false,
  removing: false
})

/** Whether either action is running on this method; both buttons wait for it to end. */
const busy = computed<boolean>(() => props.settingDefault || props.removing)

/** Translator for the method type label. */
const { t } = useI18n()

/** Locale key of the label for each method type. */
const TYPE_LABEL_KEYS: Record<PaymentMethodType, string> = {
  [PaymentMethodType.Card]: 'client.payment.type.card',
  [PaymentMethodType.BankAccount]: 'client.payment.type.bankAccount',
  [PaymentMethodType.Other]: 'client.payment.type.other'
}

/** The translated name of the method's type. */
const typeLabel = computed<string>(() => t(TYPE_LABEL_KEYS[props.method.type]))

/** Events of {@link PaymentMethodRow}. */
const emit = defineEmits<{
  /** The client asked to make this method the default; carries its id. */
  setDefault: [id: string]
  /** The client asked to remove this method; carries its id. */
  remove: [id: string]
}>()
</script>
