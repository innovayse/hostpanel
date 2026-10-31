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
        <UiCardHeader :title="$t('client.payment.title')">
          <UiButton size="sm" data-testid="account-payment-add" @click="openAddCard">
            <Plus :size="14" :stroke-width="2" class="mr-1" />
            {{ $t('client.payment.addCard') }}
          </UiButton>
        </UiCardHeader>

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

        <!-- Shown only while some card is charged by the client's agreement; turning it off keeps the cards. -->
        <UiStack v-if="hasAutoCharge" direction="row" justify="end" class="mt-3">
          <UiButton
            size="sm"
            variant="outline"
            :loading="turningOffAutoCharge"
            data-testid="account-payment-auto-off"
            @click="onTurnOffAutoCharge"
          >
            {{ $t('client.payment.turnOffAutoCharge') }}
          </UiButton>
        </UiStack>
      </UiCard>

      <UiAlert v-if="paymentActionError" variant="error" data-testid="account-payment-action-error">
        {{ paymentActionError }}
      </UiAlert>
      <UiAlert v-if="success" variant="success" data-testid="account-payment-success">{{ success }}</UiAlert>
    </UiStack>

    <UiDialog v-model="addCardOpen" :title="$t('client.payment.addCardTitle')" size="md">
      <UiForm id="add-card-form" data-testid="account-payment-add-form" spacing="sm" @submit="onAddCard">
        <UiText size="sm" tone="muted">{{ $t('client.payment.addCardDesc') }}</UiText>
        <CheckoutStripeCardForm ref="cardFormRef" />
        <UiCheckbox
          v-model="consentAccepted"
          :label="$t('client.payment.consent')"
          testid="account-payment-consent"
        />
        <UiAlert v-if="consentMissing" variant="warning" data-testid="account-payment-consent-required">
          {{ $t('client.payment.consentRequired') }}
        </UiAlert>
        <UiAlert v-if="paymentActionError" variant="error" data-testid="account-payment-add-error">
          {{ paymentActionError }}
        </UiAlert>
      </UiForm>
      <template #footer>
        <UiButton type="submit" form="add-card-form" :loading="addingCard" :disabled="!cardFieldReady" data-testid="account-payment-add-submit">
          {{ $t('client.payment.saveCard') }}
        </UiButton>
      </template>
    </UiDialog>

    <UiConfirmDialog
      :open="pendingAutoChargeOff"
      :title="$t('client.payment.turnOffConfirm')"
      :description="$t('client.payment.turnOffConfirmDesc')"
      :confirm-label="$t('client.payment.turnOffAutoCharge')"
      :loading="turningOffAutoCharge"
      @confirm="onConfirmAutoChargeOff"
      @cancel="pendingAutoChargeOff = false"
    />

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
 * "Add card" saves a card without a payment: the payer must tick the agreement to automatic renewal
 * charges, the card is confirmed by the payment provider's own form, and the backend counts it as saved only
 * after reading the result back from the provider. A card charged automatically is marked, and one button
 * turns automatic charges off without removing the cards. Every refusal is the store's sentence. A member of the account without the permission to view and
 * pay invoices (the flag the payment-method actions need) sees the tab disabled with the reason.
 *
 * @module components/account/PaymentMethodsTab
 */

import { CreditCard, Plus } from 'lucide-vue-next'
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
  paymentMethods, paymentLoading, paymentLoadError, paymentActionError, settingDefaultId, removingId,
  addingCard, turningOffAutoCharge
} = storeToRefs(account)

/** Whether any saved card is charged for renewals by the client's agreement. */
const hasAutoCharge = computed(() => paymentMethods.value.some(method => method.autoCharge === true))

/** What the card form exposes: it confirms the setup intent with the mounted card element. */
interface CardFormHandle {
  /** Confirms saving the card with the setup intent's client secret. */
  confirmSetup: (clientSecret: string) => Promise<{ id: string }>
  /** Whether the provider's card field is mounted and ready for input. */
  ready: boolean
}

/** The card form in the add-card dialog. */
const cardFormRef = ref<CardFormHandle | null>(null)

/** Whether the card field is ready; without it (no provider key, still loading) there is nothing to save. */
const cardFieldReady = computed(() => cardFormRef.value?.ready === true)

/** Whether the add-card dialog is open. */
const addCardOpen = ref(false)

/** Whether the payer ticked the agreement to automatic renewal charges; off each time the dialog opens. */
const consentAccepted = ref(false)

/** Whether the payer tried to save without the agreement, which the dialog then says. */
const consentMissing = ref(false)

/** Ticking the agreement withdraws the warning that it was missing. */
watch(consentAccepted, (ticked) => {
  if (ticked) {
    consentMissing.value = false
  }
})

/** Whether the confirmation to turn automatic charges off is open. */
const pendingAutoChargeOff = ref(false)

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

/** Opens the add-card dialog with the agreement unticked. */
const openAddCard = (): void => {
  success.value = ''
  account.paymentActionError = ''
  consentAccepted.value = false
  consentMissing.value = false
  addCardOpen.value = true
}

/** Saves the card typed into the dialog; without the agreement nothing is sent. */
const onAddCard = async (): Promise<void> => {
  consentMissing.value = !consentAccepted.value
  if (consentMissing.value || !cardFieldReady.value || !cardFormRef.value) {
    return
  }
  const confirmSetup = cardFormRef.value.confirmSetup
  if (await account.addPaymentMethod(true, confirmSetup) === ActionOutcome.Succeeded) {
    addCardOpen.value = false
    success.value = t('client.payment.cardSaved')
    await account.loadPaymentMethods()
  }
}

/** Turns off automatic charges once the payer confirmed, and reads the list again when it worked. */
const onConfirmAutoChargeOff = async (): Promise<void> => {
  success.value = ''
  if (await account.turnOffAutoCharge() === ActionOutcome.Succeeded) {
    success.value = t('client.payment.turnOffDone')
    await account.loadPaymentMethods()
  }
  pendingAutoChargeOff.value = false
}

/** Asks for confirmation before turning automatic charges off. */
const onTurnOffAutoCharge = (): void => {
  pendingAutoChargeOff.value = true
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
