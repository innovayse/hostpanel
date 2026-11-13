<template>
  <UiCard data-testid="service-password">
    <UiCardHeader :title="$t('client.services.passwordTitle')" :icon="KeyRound" />

    <UiAlert v-if="passwordSuccess" variant="success" data-testid="service-password-success">
      {{ $t('client.services.passwordSuccess') }}
    </UiAlert>

    <UiForm v-else data-testid="service-password-form" :error="formError" spacing="md" @submit="changePassword">
      <UiInput
        v-model="newPassword"
        type="password"
        :label="$t('client.services.newPassword')"
        :placeholder="$t('client.services.newPasswordPlaceholder')"
        autocomplete="new-password"
        testid="service-new-password"
        required
      />
      <UiInput
        v-model="confirmPassword"
        type="password"
        :label="$t('client.services.confirmPassword')"
        :placeholder="$t('client.services.confirmPasswordPlaceholder')"
        autocomplete="new-password"
        testid="service-confirm-password"
        required
      />

      <template #actions>
        <UiButton type="submit" :loading="passwordChanging" data-testid="service-password-submit">
          <KeyRound v-if="!passwordChanging" :size="14" :stroke-width="2" class="mr-1.5" />
          {{ $t('client.services.passwordBtn') }}
        </UiButton>
      </template>
    </UiForm>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ServicePasswordForm: changes the control-panel password of one service. The two typed values
 * are checked here for length and match before anything is sent; the request, its in-flight flag
 * and the API's refusal belong to the service management store.
 */
import { computed, ref } from 'vue'
import { KeyRound } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useServiceManagementStore } from '~/stores/servicemanagement'

/** Shortest password the control panel accepts. */
const MIN_PASSWORD_LENGTH = 8

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of ServicePasswordForm. */
interface Props {
  /** The id of the service whose password is changed. */
  serviceId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The service management store: the request flags and the API's refusal sentence. */
const store = useServiceManagementStore()
/** Reactive state of the store this component renders. */
const { passwordChanging, passwordSuccess } = storeToRefs(store)

/** The new password being typed. */
const newPassword = ref('')

/** The confirmation being typed. */
const confirmPassword = ref('')

/** Why the typed password was rejected before it was sent; empty when it passed. */
const validationError = ref('')

/** The sentence above the form: the local validation message first, else the API's refusal. */
const formError = computed(() => validationError.value || store.passwordError)

/**
 * Validates the typed password and hands it to the store.
 *
 * @returns Resolves when the attempt has ended; the outcome is rendered from the store.
 */
const changePassword = async (): Promise<void> => {
  validationError.value = ''

  if (newPassword.value.length < MIN_PASSWORD_LENGTH) {
    validationError.value = t('client.services.passwordTooShort')
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    validationError.value = t('client.services.passwordMismatch')
    return
  }

  await store.changePassword(props.serviceId, newPassword.value)
}
</script>
