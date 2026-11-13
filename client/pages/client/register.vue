<template>
  <ClientAuthShell :title="$t('client.register.title')" :subtitle="$t('client.register.subtitle')">
    <UiForm
      :error="error"
      :success="success ? $t('client.register.success') : undefined"
      spacing="md"
      data-testid="register-form"
      @submit="handleRegister"
    >
      <UiGrid cols="2" gap="md">
        <UiInput
          v-model="form.firstname"
          :label="$t('client.register.firstName')"
          :placeholder="$t('client.register.firstNamePlaceholder')"
          :error="fieldErrors.firstname"
          autocomplete="given-name"
          testid="register-firstname"
          required
        />
        <UiInput
          v-model="form.lastname"
          :label="$t('client.register.lastName')"
          :placeholder="$t('client.register.lastNamePlaceholder')"
          :error="fieldErrors.lastname"
          autocomplete="family-name"
          testid="register-lastname"
          required
        />
      </UiGrid>
      <UiInput
        v-model="form.email"
        type="email"
        :label="$t('client.register.email')"
        :placeholder="$t('client.register.emailPlaceholder')"
        :error="fieldErrors.email"
        icon="mdi:email"
        autocomplete="email"
        testid="register-email"
        required
      />
      <UiInput
        v-model="form.password"
        type="password"
        :label="$t('client.register.password')"
        :placeholder="$t('client.register.passwordPlaceholder')"
        :error="fieldErrors.password"
        autocomplete="new-password"
        testid="register-password"
        required
      />
      <UiButton type="submit" variant="primary" size="lg" full-width :loading="loading" data-testid="register-submit">
        <UserPlus v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
        {{ $t('client.register.submit') }}
      </UiButton>
    </UiForm>

    <template #footer>
      <UiText as="span" size="sm" tone="muted">
        {{ $t('client.register.haveAccount') }}
        <UiLink :to="CLIENT_LOGIN_PATH" :underline="false" testid="register-signin" class="font-medium">
          {{ $t('client.register.signIn') }}
        </UiLink>
      </UiText>
    </template>
  </ClientAuthShell>
</template>

<script setup lang="ts">
/**
 * Client registration page.
 *
 * The request, its in-flight flag, the refusal sentence and the per-field messages belong to the
 * auth store; this page renders them and sends the visitor to sign in once the account exists.
 */
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { UserPlus } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useAuthStore } from '~/stores/auth'
import { ActionOutcome } from '~/types/actionoutcome'

definePageMeta({ layout: false, middleware: 'client-auth' })

/** How long the success banner stays before the redirect to sign-in, in milliseconds. */
const REDIRECT_DELAY_MS = 1500

/** Auth store; owns the requests, in-flight flags and refusal sentences this file renders. */
const authStore = useAuthStore()

/** Whether the request is in flight, the API's sentence for a refusal and its field messages, all owned by the store. */
const { registering: loading, registerError: error, fieldErrors } = storeToRefs(authStore)

/** The registration fields as typed. */
const form = reactive({
  firstname: '',
  lastname: '',
  email: '',
  password: ''
})

/** True once the account was created and the redirect is pending. */
const success = ref(false)

/**
 * Registers the account through the auth store and, on success, sends the visitor to sign in.
 *
 * @returns Nothing; a refusal stays in the store's error state.
 */
const handleRegister = async (): Promise<void> => {
  const outcome = await authStore.register(form)
  if (outcome === ActionOutcome.Succeeded) {
    success.value = true
    setTimeout(() => navigateTo(CLIENT_LOGIN_PATH), REDIRECT_DELAY_MS)
  }
}
</script>
