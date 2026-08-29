<template>
  <div class="min-h-dvh bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute inset-0" style="background: radial-gradient(circle at center, transparent 40%, #0a0a0f 100%)" />
    </div>

    <div class="w-full max-w-md relative z-10">
      <div class="text-center mb-8">
        <h1 class="text-2xl font-bold text-white">Reset password</h1>
        <p class="text-gray-400 text-sm mt-1">We'll email you a link to reset your password</p>
      </div>

      <div class="p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10">
        <div v-if="success" class="text-center text-green-400 text-sm">
          If an account exists for that email, a reset link has been sent.
        </div>
        <form v-else class="space-y-4" @submit.prevent="handleForgot">
          <div v-if="error" class="text-red-400 text-sm">{{ error }}</div>
          <div>
            <label class="block text-xs font-medium text-gray-400 uppercase tracking-wide mb-1.5">Email</label>
            <input v-model="email" type="email" required placeholder="you@example.com" class="w-full bg-white/5 border border-white/10 focus:border-primary-500 text-white placeholder-gray-500 rounded-lg px-3 py-2.5 text-sm outline-none transition-all" />
          </div>
          <button type="submit" :disabled="loading" class="w-full py-3 rounded-xl bg-gradient-to-r from-primary-600 to-primary-500 text-white font-semibold disabled:opacity-50">
            {{ loading ? 'Sending...' : 'Send reset link' }}
          </button>
        </form>
      </div>

      <p class="text-center text-sm mt-6">
        <NuxtLink to="/client/login" class="text-primary-400 hover:text-primary-300 transition-colors">Back to sign in</NuxtLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Password reset request page — takes an email address and asks the backend to send a
 * reset link.
 */
import { useAuthApi } from '~/composables/apis/useAuthApi'
import { apiErrorMessage } from '~/utils/apiError'

definePageMeta({ layout: false })

const { requestPasswordReset } = useAuthApi()

/** Address the visitor typed. */
const email = ref('')

/** True once the backend has accepted the request. */
const success = ref(false)

/** True while the request is in flight. */
const loading = ref(false)

/** Failure sentence as the API worded it, empty when there is none. */
const error = ref('')

/**
 * Asks for a reset mail and switches the page to its confirmation state.
 *
 * The confirmation is deliberately vague about whether the address exists — that is what
 * stops the page being an account-enumeration oracle — but it is only shown when the request
 * actually succeeded. It used to be set from inside an empty `catch` as well, so a backend
 * that was down still told the visitor the mail was on its way — a failed request reported
 * as a success.
 *
 * @returns Nothing; a failure sets {@link error} rather than throwing.
 */
const handleForgot = async (): Promise<void> => {
  loading.value = true
  error.value = ''
  try {
    await requestPasswordReset(email.value)
    success.value = true
  } catch (err: unknown) {
    error.value = apiErrorMessage(err)
  } finally {
    loading.value = false
  }
}
</script>
