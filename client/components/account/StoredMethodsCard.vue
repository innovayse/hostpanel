<template>
  <UiCard data-testid="stored-methods-card">
    <UiCardHeader :title="$t('client.storedMethods.title')" :description="$t('client.storedMethods.subtitle')" />

    <UiStack gap="md">
      <UiSkeleton v-if="completing || (loading && !methods.length)" shape="block" class="h-12" data-testid="stored-methods-loading" />
      <UiAlert v-if="loadError" variant="error" data-testid="stored-methods-load-error">{{ loadError }}</UiAlert>

      <UiList v-if="methods.length" list-style="divided">
        <UiListItem v-for="method in methods" :key="method.consentId">
          <UiStack direction="row" align="center" justify="between" gap="sm" wrap class="py-2" :data-testid="`stored-method-${method.consentId}`">
            <UiStack gap="none">
              <UiStack direction="row" align="center" gap="xs" wrap>
                <UiText v-if="method.detailsAvailable" size="sm" weight="medium">
                  {{ method.brand || $t('client.storedMethods.card') }}
                  <span v-if="method.last4">•••• {{ method.last4 }}</span>
                </UiText>
                <UiText v-else size="sm" tone="subtle" :data-testid="`stored-method-unavailable-${method.consentId}`">
                  {{ $t('client.storedMethods.detailsUnavailable') }}
                </UiText>
                <UiBadge v-if="method.isDefault" variant="primary" size="sm" rounded :data-testid="`stored-method-default-badge-${method.consentId}`">
                  {{ $t('client.storedMethods.defaultBadge') }}
                </UiBadge>
              </UiStack>
              <UiText v-if="method.detailsAvailable && method.expiryMonth > 0 && method.expiryYear > 0" size="xs" tone="subtle">
                {{ $t('client.payment.expires') }} {{ expiry(method) }}
              </UiText>
            </UiStack>
            <UiStack direction="row" align="center" gap="xs">
              <UiButton
                v-if="!method.isDefault"
                size="sm"
                variant="outline"
                :loading="settingDefaultId === method.consentId"
                :disabled="busy"
                :aria-label="$t('client.storedMethods.makeDefaultLabel', { method: methodName(method) })"
                :data-testid="`stored-method-default-${method.consentId}`"
                @click="onMakeDefault(method.consentId)"
              >
                <Star :size="13" :stroke-width="2" class="mr-1" />
                {{ $t('client.storedMethods.makeDefault') }}
              </UiButton>
              <UiButton
                size="sm"
                variant="danger"
                :loading="revokingId === method.consentId"
                :disabled="busy"
                :aria-label="$t('client.storedMethods.revokeLabel', { method: methodName(method) })"
                :data-testid="`stored-method-revoke-${method.consentId}`"
                @click="pendingRevokeId = method.consentId"
              >
                <Trash2 :size="13" :stroke-width="2" class="mr-1" />
                {{ $t('client.storedMethods.revoke') }}
              </UiButton>
            </UiStack>
          </UiStack>
        </UiListItem>
      </UiList>
      <UiText v-else-if="!loading && !loadError" size="sm" tone="subtle" data-testid="stored-methods-empty">
        {{ $t('client.storedMethods.empty') }}
      </UiText>

      <UiDivider />

      <UiText v-if="!gatewayOptions.length" size="sm" tone="subtle" data-testid="stored-methods-no-gateways">
        {{ $t('client.storedMethods.noGateways') }}
      </UiText>
      <UiSelect
        v-else
        v-model="selectedModule"
        :label="$t('client.storedMethods.gateway')"
        :placeholder="$t('client.storedMethods.gatewayPlaceholder')"
        :options="gatewayOptions"
        :disabled="starting"
        testid="stored-methods-gateway"
      />
      <UiCheckbox
        v-if="gatewayOptions.length"
        v-model="consent"
        :label="$t('client.storedMethods.consent')"
        :disabled="starting"
        testid="stored-methods-consent"
      />
      <UiAlert v-if="error" variant="error" data-testid="stored-methods-error">{{ error }}</UiAlert>
      <UiAlert v-if="success" variant="success" data-testid="stored-methods-success">{{ success }}</UiAlert>
      <UiStack direction="row" gap="none">
        <UiButton
          :loading="starting"
          :disabled="!selectedModule || !consent || !gatewayOptions.length"
          data-testid="stored-methods-start"
          @click="onStart"
        >
          <Plus :size="14" :stroke-width="2" class="mr-1" />
          {{ $t('client.storedMethods.start') }}
        </UiButton>
      </UiStack>
    </UiStack>

    <UiConfirmDialog
      :open="pendingRevokeId !== null"
      :title="$t('client.storedMethods.revokeConfirm')"
      :description="$t('client.storedMethods.revokeConfirmDesc')"
      :confirm-label="$t('client.storedMethods.revoke')"
      :loading="revokingId !== null"
      variant="danger"
      @confirm="onConfirmRevoke"
      @cancel="pendingRevokeId = null"
    />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * StoredMethodsCard: the "Saved payment methods" section of the payment tab.
 *
 * The payer picks a gateway, ticks the consent to later charges and is sent to the gateway's
 * page; on return (`?storedReturn=1`) the card completes the session. The list is read on mount
 * and after every change; each method can be made the default or revoked (behind a confirmation).
 * Every refusal is the store's sentence.
 *
 * @module components/account/StoredMethodsCard
 */

import { Plus, Star, Trash2 } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { STORED_METHOD_RETURN_QUERY } from '~/constants/storedMethodReturn'
import { useStoredMethodStart } from '~/composables/useStoredMethodStart'
import { ConsentTextVersion } from '~/types/consenttextversion'
import { useAccountStore } from '~/stores/account'
import { useStoredMethodsStore } from '~/stores/storedmethods'
import { ActionOutcome } from '~/types/actionoutcome'
import { PaymentModule } from '~/types/paymentmodule'
import type { StoredMethod } from '~/types/storedmethod'

/** Translator for the success sentence. */
const { t } = useI18n()

/** The current route; the return from the gateway is marked in its query. */
const route = useRoute()

/** The router that clears the return marker once handled. */
const router = useRouter()

/** The stored-method store. */
const storedStore = useStoredMethodsStore()

/** The stored methods, progress flags and the last refusal. */
const { methods, starting, completing, revokingId, settingDefaultId, loading, loadError, error } = storeToRefs(storedStore)

/** Whether a default change or a revoke is running; the row buttons wait for it. */
const busy = computed(() => revokingId.value !== null || settingDefaultId.value !== null)

/** The account store that already holds the gateways of the deployment. */
const { gatewayMethods } = storeToRefs(useAccountStore())

/** The gateways a method may be stored at: every one except the built-in manual transfer. */
const gatewayOptions = computed(() =>
  (gatewayMethods.value ?? [])
    .filter(gateway => gateway.module !== PaymentModule.BankTransfer)
    .map(gateway => ({ value: gateway.module, label: gateway.displayName }))
)

/** The gateway module picked in the form. */
const selectedModule = ref<string | number | null>(null)

/** Whether the consent box is ticked. */
const consent = ref(false)

/** Success sentence after a change; empty otherwise. */
const success = ref('')

/** Consent id the revoke confirmation is about, or null when it is closed. */
const pendingRevokeId = ref<number | null>(null)

/**
 * Formats a method's expiry as MM/YYYY.
 *
 * @param method - The stored method.
 * @returns The expiry text.
 */
const expiry = (method: StoredMethod): string => `${String(method.expiryMonth).padStart(2, '0')}/${method.expiryYear}`

/**
 * Names a method for assistive technology: brand and last digits, or the generic card word.
 *
 * @param method - The stored method.
 * @returns The label text without markup.
 */
const methodName = (method: StoredMethod): string => {
  if (!method.detailsAvailable) {
    return t('client.storedMethods.card')
  }
  return [method.brand || t('client.storedMethods.card'), method.last4].filter(Boolean).join(' ')
}

/** Starts a save session and follows its redirect; shared with the post-checkout save. */
const { start } = useStoredMethodStart()

/** Opens a save session and hands the browser to the gateway's page. */
const onStart = async (): Promise<void> => {
  success.value = ''
  if (typeof selectedModule.value !== 'string') {
    return
  }
  if (await start(selectedModule.value, consent.value, ConsentTextVersion.AccountCard)) {
    // The consent covered this one session; a later visit asks again.
    consent.value = false
  }
}

/** Revokes the confirmed consent. */
const onConfirmRevoke = async (): Promise<void> => {
  const id = pendingRevokeId.value
  if (id === null) {
    return
  }
  success.value = ''
  if (await storedStore.revoke(id) === ActionOutcome.Succeeded) {
    success.value = t('client.storedMethods.revokeSuccess')
  }
  pendingRevokeId.value = null
}

/**
 * Makes a method the default.
 *
 * @param consentId - The consent id.
 */
const onMakeDefault = async (consentId: number): Promise<void> => {
  success.value = ''
  if (await storedStore.makeDefault(consentId) === ActionOutcome.Succeeded) {
    success.value = t('client.storedMethods.defaultSet')
  }
}

onMounted(async () => {
  if (!route.query[STORED_METHOD_RETURN_QUERY]) {
    await storedStore.load()
    return
  }
  // Completing the pending session re-reads the list itself; with no session to complete
  // (a reload, another tab) nothing read it, so read it here.
  const outcome = await storedStore.completePending()
  if (outcome === null) {
    await storedStore.load()
  } else if (outcome === ActionOutcome.Succeeded) {
    success.value = t('client.storedMethods.saved')
  }
  // The marker has done its job; a reload must not try to complete again.
  const rest = Object.fromEntries(Object.entries(route.query).filter(([key]) => key !== STORED_METHOD_RETURN_QUERY))
  await router.replace({ query: rest })
})
</script>
