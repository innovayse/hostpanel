<template>
  <div data-testid="account-payment-tab">
    <!-- The API refuses every payment-method request of a member without the invoices permission, so
         the tab offers nothing and says why; the store does not load the list either. -->
    <UiAlert v-if="!maySeePaymentMethods" variant="warning" data-testid="account-payment-denied">
      {{ $t('client.payment.permissionDenied') }}
    </UiAlert>

    <UiStack v-else-if="paymentLoading" gap="sm" data-testid="account-payment-loading">
      <UiSkeleton v-for="index in 3" :key="index" shape="block" class="h-16" />
    </UiStack>

    <UiStack v-else gap="md">
      <UiCard>
        <UiCardHeader :title="$t('client.payment.title')" />

        <!-- A failed read is checked before the empty state, which it is otherwise
             indistinguishable from: the list is empty either way. -->
        <UiAlert v-if="paymentLoadError" variant="error" data-testid="account-payment-load-error">
          {{ paymentLoadError }}
        </UiAlert>

        <UiEmptyState
          v-else-if="!paymentMethods.length"
          :icon="CreditCard"
          :title="$t('client.payment.empty')"
          :description="$t('client.payment.emptyDesc')"
        >
          <UiButton :to="localePath(CLIENT_INVOICES_PATH)" variant="subtle" size="sm" data-testid="account-payment-invoices">
            {{ $t('client.payment.viewInvoices') }}
          </UiButton>
        </UiEmptyState>

        <UiList v-else list-style="divided">
          <UiListItem v-for="method in paymentMethods" :key="method.id">
            <AccountPaymentMethodRow
              :method="method"
              :setting-default="settingDefaultId === method.id"
              :removing="removingId === method.id"
              @set-default="onSetDefault"
              @remove="pendingRemovalId = $event"
            />
          </UiListItem>
        </UiList>
      </UiCard>

      <UiAlert v-if="paymentActionError" variant="error" data-testid="account-payment-action-error">
        {{ paymentActionError }}
      </UiAlert>
      <UiAlert v-if="success" variant="success" data-testid="account-payment-success">{{ success }}</UiAlert>
    </UiStack>

    <UiConfirmDialog
      :open="pendingRemovalId !== null"
      :title="$t('client.payment.removeConfirm')"
      :description="$t('client.payment.removeConfirmDesc')"
      :confirm-label="$t('client.payment.remove')"
      :loading="removingId !== null"
      variant="danger"
      @confirm="onConfirmRemoval"
      @cancel="pendingRemovalId = null"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * PaymentMethodsTab - the "Payment Methods" tab: the saved methods, set-default and remove.
 *
 * The API has no way to add a card here, so the tab offers no add action: a customer without a
 * saved card is told to pay an invoice through the hosted gateway and is linked to the invoices.
 * Every refusal is the store's sentence. A member of the account without the permission to view and
 * pay invoices (the flag the payment-method actions need) sees the tab disabled with the reason.
 *
 * @module components/account/PaymentMethodsTab
 */

import { CreditCard } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { CLIENT_INVOICES_PATH } from '~/constants/clientRoutes'
import { Permission } from '~/composables/usePermissions'
import { useAccountStore } from '~/stores/account'
import { useClientStore } from '~/stores/client'
import { ActionOutcome } from '~/types/actionoutcome'

/** Translator for the success sentences. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The account store that holds and changes the saved methods. */
const account = useAccountStore()

/** The client store, which knows what the signed-in person may do. */
const client = useClientStore()

/** Whether the person may see and change saved payment methods. */
const maySeePaymentMethods = computed(() => client.mayDo(Permission.ViewPayInvoices))

/** The methods, their loading and error state, and the per-action progress and refusal. */
const {
  paymentMethods, paymentLoading, paymentLoadError, paymentActionError, settingDefaultId, removingId
} = storeToRefs(account)

/** Success sentence after a change; empty otherwise. */
const success = ref('')

/** Id of the method the removal confirmation is about, or null when it is closed. */
const pendingRemovalId = ref<string | null>(null)

/**
 * Makes a method the default and reads the list again when it worked.
 *
 * @param id - Id of the method.
 */
const onSetDefault = async (id: string): Promise<void> => {
  success.value = ''
  if (await account.setDefaultPaymentMethod(id) === ActionOutcome.Succeeded) {
    success.value = t('client.payment.defaultSet')
    await account.loadPaymentMethods()
  }
}

/** Removes the confirmed method and reads the list again when it worked. */
const onConfirmRemoval = async (): Promise<void> => {
  const id = pendingRemovalId.value
  if (id === null) {
    return
  }
  success.value = ''
  if (await account.removePaymentMethod(id) === ActionOutcome.Succeeded) {
    success.value = t('client.payment.removeSuccess')
    await account.loadPaymentMethods()
  }
  pendingRemovalId.value = null
}
</script>
