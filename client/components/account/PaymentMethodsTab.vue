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
          <UiButton v-if="mayAddCard" size="sm" data-testid="account-payment-add" @click="openAddCard">
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
          <UiListItem v-for="method in paymentMethods" :key="`${method.gatewayModuleId}:${method.id}`">
            <AccountPaymentMethodRow
              :method="method"
              :setting-default="settingDefaultId === method.id"
              :removing="removingId === method.id"
              @set-default="onSetDefault"
              @remove="pendingRemoval = $event"
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
        <UiSelect
          v-if="savedMethodModules.length > 1"
          v-model="selectedModule"
          :label="$t('client.payment.module')"
          :placeholder="$t('ui.selectPlaceholder')"
          :options="moduleOptions"
          testid="account-payment-module"
        />
        <!-- The portal embeds only the built-in card form; any other module saves through its own page. -->
        <CheckoutStripeCardForm v-if="showCardForm" ref="cardFormRef" />
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
        <UiButton type="submit" form="add-card-form" :loading="addingCard" :disabled="showCardForm && !cardFieldReady" data-testid="account-payment-add-submit">
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
      :open="pendingRemoval !== null"
      :title="$t('client.payment.removeConfirm')"
      :description="$t('client.payment.removeConfirmDesc')"
      :confirm-label="$t('client.payment.remove')"
      :loading="removingId !== null"
      variant="danger"
      @confirm="onConfirmRemoval"
      @cancel="pendingRemoval = null"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * PaymentMethodsTab - the "Payment Methods" tab: the saved cards, add card, set default, remove and
 * turning automatic charges off.
 *
 * "Add card" saves a card without a payment through a gateway module the API offers; the button is
 * absent when no module offers it, and the dialog lets the payer choose when there are several. The
 * payer must tick the agreement to automatic renewal charges first. The built-in card module is
 * confirmed by the payment provider's own form in this page; a module with a hosted page sends the
 * payer there and the payer comes back to this tab with `?module=` (and `?session=` when the gateway
 * adds it) in the address, which the tab completes and then removes. The backend counts a card as
 * saved only after reading the result back from the gateway.
 *
 * A card charged automatically is marked, and one button, after a confirmation, turns automatic
 * charges off without removing the cards.
 *
 * Every refusal is the store's sentence. A member of the account without the permission to view and
 * pay invoices (the flag the payment-method actions need) sees the tab disabled with the reason.
 *
 * @module components/account/PaymentMethodsTab
 */

import { CreditCard, Plus } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { CLIENT_INVOICES_PATH } from '~/constants/clientRoutes'
import { usePendingCardSave } from '~/composables/usePendingCardSave'
import { Permission } from '~/composables/usePermissions'
import { useAccountStore } from '~/stores/account'
import { useClientStore } from '~/stores/client'
import { ActionOutcome } from '~/types/actionoutcome'
import type { CardFormHandle } from '~/types/cardformhandle'
import { CardSaveOutcome } from '~/types/cardsaveoutcome'
import type { PaymentMethod } from '~/types/payment'
import { PaymentModule } from '~/types/paymentmodule'
import { redirectToExternal } from '~/utils/externalRedirect'

/** Translator for the success sentences. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The current route; a payer back from a gateway carries `?module=` here. */
const route = useRoute()

/** The router that removes the return parameters once they are handled. */
const router = useRouter()

/** The account store that holds and changes the saved methods. */
const account = useAccountStore()

/** The client store, which knows what the signed-in person may do. */
const client = useClientStore()

/** The session a payer left for a gateway with, remembered by the store. */
const { recall: recallPendingCardSave } = usePendingCardSave()

/** Whether the person may see and change saved payment methods. */
const maySeePaymentMethods = computed(() => client.mayDo(Permission.ViewPayInvoices))

/** The methods, their loading and error state, and the per-action progress and refusal. */
const {
  paymentMethods, paymentLoading, paymentLoadError, paymentActionError, settingDefaultId, removingId,
  addingCard, turningOffAutoCharge, savedMethodModules
} = storeToRefs(account)

/** Whether any saved card is charged for renewals by the client's agreement. */
const hasAutoCharge = computed(() => paymentMethods.value.some(method => method.autoCharge === true))

/** Whether some gateway module can save a card, which is what "Add card" needs. */
const mayAddCard = computed(() => savedMethodModules.value.length > 0)

/** The modules as the choice in the dialog offers them. */
const moduleOptions = computed(() => savedMethodModules.value.map(module => ({ value: module.module, label: module.displayName })))

/** The gateway module the card is saved with; the first one each time the dialog opens. */
const selectedModule = ref('')

/** Whether the chosen module is the built-in card module, whose form this page embeds. */
const showCardForm = computed(() => selectedModule.value === PaymentModule.Stripe)

/** The card form in the add-card dialog; absent for a module that saves through its own page. */
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

/** The method the removal confirmation is about, or null when it is closed. */
const pendingRemoval = ref<PaymentMethod | null>(null)

/**
 * Makes a method the default and reads the list again when it worked.
 *
 * @param method - The method.
 */
const onSetDefault = async (method: PaymentMethod): Promise<void> => {
  success.value = ''
  if (await account.setDefaultPaymentMethod(method) === ActionOutcome.Succeeded) {
    success.value = t('client.payment.defaultSet')
    await account.loadPaymentMethods()
  }
}

/** Opens the add-card dialog with the agreement unticked and the first module chosen. */
const openAddCard = (): void => {
  success.value = ''
  account.clearPaymentActionError()
  consentAccepted.value = false
  consentMissing.value = false
  selectedModule.value = savedMethodModules.value[0]?.module ?? ''
  addCardOpen.value = true
}

/**
 * Saves the card with the chosen module; without the agreement nothing is sent. A hosted page takes
 * the browser away, and the save is completed when the payer returns.
 */
const onAddCard = async (): Promise<void> => {
  consentMissing.value = !consentAccepted.value
  if (consentMissing.value || (showCardForm.value && !cardFieldReady.value)) {
    return
  }
  const form = cardFormRef.value
  const confirmEmbedded = (clientToken: string): Promise<unknown> =>
    form ? form.confirmSetup(clientToken) : Promise.reject(new Error(t('client.payment.embeddedUnsupported')))
  const result = await account.addPaymentMethod(selectedModule.value, true, confirmEmbedded)
  if (result.outcome === CardSaveOutcome.Saved) {
    addCardOpen.value = false
    success.value = t('client.payment.cardSaved')
    await account.loadPaymentMethods()
  } else if (result.outcome === CardSaveOutcome.Redirect && result.redirectUrl) {
    redirectToExternal(result.redirectUrl)
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
  const method = pendingRemoval.value
  if (method === null) {
    return
  }
  success.value = ''
  if (await account.removePaymentMethod(method) === ActionOutcome.Succeeded) {
    success.value = t('client.payment.removeSuccess')
    await account.loadPaymentMethods()
  }
  pendingRemoval.value = null
}

/**
 * Reads one return parameter of the address.
 *
 * @param value - The query value, which may be repeated.
 * @returns The first value, or undefined when absent.
 */
const firstQueryValue = (value: unknown): string | undefined => {
  const first = Array.isArray(value) ? value[0] : value
  return typeof first === 'string' && first !== '' ? first : undefined
}

/**
 * Completes the save of a payer who is back from a gateway's page, then removes the return
 * parameters so a reload does not complete it again.
 */
const completeReturnedSave = async (): Promise<void> => {
  const gatewayModuleId = firstQueryValue(route.query.module)
  if (!gatewayModuleId || !maySeePaymentMethods.value) {
    return
  }
  const sessionId = firstQueryValue(route.query.session) ?? recallPendingCardSave(gatewayModuleId)
  await router.replace({ query: { ...route.query, module: undefined, session: undefined } })
  if (!sessionId) {
    return
  }
  success.value = ''
  if (await account.completeCardSave(gatewayModuleId, sessionId) === ActionOutcome.Succeeded) {
    success.value = t('client.payment.cardSaved')
    await account.loadPaymentMethods()
  }
}

onMounted(completeReturnedSave)
</script>
