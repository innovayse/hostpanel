<template>
  <UiCard data-testid="account-2fa-card">
    <UiStack direction="row" align="center" justify="between" gap="md">
      <UiStack direction="row" align="center" gap="sm" class="min-w-0">
        <ShieldCheck v-if="enabled" :size="22" :stroke-width="1.5" class="text-green-600 dark:text-green-400 flex-shrink-0" />
        <ShieldOff v-else :size="22" :stroke-width="1.5" class="text-gray-500 dark:text-gray-400 flex-shrink-0" />
        <UiStack gap="none" class="min-w-0">
          <UiText size="sm" weight="semibold">{{ $t('client.twoFactor.title') }}</UiText>
          <UiText size="xs" tone="muted">
            {{ enabled ? $t('client.twoFactor.descEnabled') : $t('client.twoFactor.descDisabled') }}
          </UiText>
        </UiStack>
      </UiStack>
      <UiButton
        v-if="!enabled"
        size="sm"
        variant="subtle"
        :loading="tfaLoading"
        data-testid="account-2fa-enable"
        @click="startEnable"
      >
        {{ $t('client.twoFactor.enable') }}
      </UiButton>
      <UiButton
        v-else
        size="sm"
        variant="outline"
        data-testid="account-2fa-disable"
        @click="startDisable"
      >
        {{ $t('client.twoFactor.disable') }}
      </UiButton>
    </UiStack>

    <AccountTwoFactorDialog
      v-model="dialogOpen"
      :mode="mode"
      :secret="secret"
      :qr-data-url="qrDataUrl"
    />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * TwoFactorCard: shows whether two-factor is on and starts enabling or disabling it.
 *
 * Enabling asks the store for a fresh secret and QR first, and opens the dialog only when that
 * worked (a failure stays in the store). Disabling opens the same dialog to collect a code.
 *
 * @module components/account/TwoFactorCard
 */

import { ShieldCheck, ShieldOff } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAccountStore } from '~/stores/account'
import { TwoFactorMode } from '~/types/twofactormode'

/** Props of {@link TwoFactorCard}. */
interface Props {
  /** Whether two-factor is switched on for the account. */
  enabled: boolean
}

defineProps<Props>()

/** The account store that starts enrolment. */
const account = useAccountStore()
/** Whether enrolment is being started. */
const { tfaLoading } = storeToRefs(account)

/** Whether the code dialog is open. */
const dialogOpen = ref(false)

/** The direction the dialog was opened in. */
const mode = ref<TwoFactorMode>(TwoFactorMode.Enable)

/** The manual-entry secret of the pending enrolment; empty when disabling. */
const secret = ref('')

/** The QR of the pending enrolment as a local data: URL; empty when disabling. */
const qrDataUrl = ref('')

/** Starts enrolment and opens the dialog with the secret and QR the store returned. */
const startEnable = async (): Promise<void> => {
  const setup = await account.startTwoFactorSetup()
  // A failure stays in the store, worded by the API.
  if (setup) {
    mode.value = TwoFactorMode.Enable
    secret.value = setup.secret
    qrDataUrl.value = setup.qrDataUrl
    dialogOpen.value = true
  }
}

/**
 * Asks for a code before switching two-factor off.
 *
 * A signed-in session alone is not enough: removing the second factor is the first thing
 * someone who had taken over a session would do, so the API requires a current code and the
 * same dialog is reused to collect one.
 */
const startDisable = (): void => {
  mode.value = TwoFactorMode.Disable
  secret.value = ''
  qrDataUrl.value = ''
  dialogOpen.value = true
}
</script>
