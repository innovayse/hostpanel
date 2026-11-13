<template>
  <ClientServicesSetupStepCard
    :icon="Lock"
    :title="$t('client.setup.credentialsTitle')"
    :subtitle="$t('client.setup.credentialsSubtitle')"
    data-testid="setup-step-credentials"
  >
    <UiInput
      :model-value="username"
      :label="$t('client.setup.usernameLabel')"
      :placeholder="$t('client.setup.usernamePlaceholder')"
      :hint="$t('client.setup.usernameHint')"
      :error="usernameError"
      autocomplete="username"
      testid="setup-username"
      required
      @update:model-value="username = sanitizeUsername(String($event))"
    />
    <UiInput
      v-model="password"
      type="password"
      :label="$t('client.setup.passwordLabel')"
      :placeholder="$t('client.setup.passwordPlaceholder')"
      :hint="$t('client.setup.passwordHint')"
      :error="passwordError"
      autocomplete="new-password"
      testid="setup-password"
      required
    />
    <UiStack direction="row" gap="md" class="pt-2">
      <UiButton variant="subtle" full-width data-testid="setup-credentials-back" @click="emit('back')">
        {{ $t('client.setup.back') }}
      </UiButton>
      <UiButton full-width :disabled="!canContinue" data-testid="setup-credentials-next" @click="emit('next')">
        {{ $t('client.setup.confirmDetails') }}
      </UiButton>
    </UiStack>
  </ClientServicesSetupStepCard>
</template>

<script setup lang="ts">
/**
 * SetupCredentialsStep. The second step of the setup wizard: the control-panel username and
 * password. The username is cleaned as it is typed (lowercase letters and digits only).
 */
import { Lock } from 'lucide-vue-next'
import { sanitizeUsername } from '~/utils/hostingSetup'

/** The control-panel username as typed (v-model). */
const username = defineModel<string>('username', { required: true })

/** The control-panel password as typed (v-model). */
const password = defineModel<string>('password', { required: true })

/** Props of SetupCredentialsStep. */
interface Props {
  /** Why the typed username is not acceptable; empty when it is. */
  usernameError: string
  /** Why the typed password is not acceptable; empty when it is. */
  passwordError: string
  /** Whether both values are filled in and acceptable. */
  canContinue: boolean
}

defineProps<Props>()

/** Events the component raises. */
const emit = defineEmits<{
  /** The reader asked to go back a step. */
  back: []
  /** The reader asked to go to the next step. */
  next: []
}>()
</script>
