<script setup lang="ts">
/**
 * Email verification landing page.
 *
 * Receives token and email from URL query params, calls the verify API,
 * and shows success or failure message.
 */
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useApi, clearToken } from '../../../composables/useApi'
import { useAuthStore } from '../stores/authStore'

const route = useRoute()
const { request } = useApi()

/** True while the verification request is in flight. */
const loading = ref(true)

/** True when verification succeeded. */
const success = ref(false)

/** Error message shown when verification fails. */
const error = ref<string | null>(null)

onMounted(async () => {
  const token = route.query.token as string | undefined
  const email = route.query.email as string | undefined

  if (!token || !email) {
    error.value = 'Invalid verification link. Missing token or email.'
    loading.value = false
    return
  }

  try {
    await request<{ success: boolean }>(
      `/auth/verify-email?token=${encodeURIComponent(token)}&email=${encodeURIComponent(email)}`,
    )
    success.value = true
    // Clear session so "Go to Admin Panel" takes user to login, not dashboard
    clearToken()
    const auth = useAuthStore()
    auth.user = null
    auth.emailVerified = null
  } catch {
    error.value = 'Verification failed. The link may be expired or invalid.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="relative min-h-screen bg-surface-base flex items-center justify-center overflow-hidden">

    <!-- Orb blue (top-left) -->
    <div
      class="pointer-events-none absolute -top-32 -left-24 w-[480px] h-[480px] rounded-full opacity-20 blur-[90px]"
      style="background: radial-gradient(circle, #0ea5e9 0%, transparent 70%); animation: drift-a 12s ease-in-out infinite alternate;"
    />
    <!-- Orb purple (bottom-right) -->
    <div
      class="pointer-events-none absolute -bottom-24 -right-20 w-[420px] h-[420px] rounded-full opacity-20 blur-[90px]"
      style="background: radial-gradient(circle, #8b5cf6 0%, transparent 70%); animation: drift-a 15s ease-in-out infinite alternate-reverse;"
    />

    <!-- Card -->
    <div
      class="relative z-10 w-full max-w-sm mx-4 bg-surface-card/85 backdrop-blur-2xl border border-white/[0.06] rounded-2xl p-10 shadow-2xl text-center"
      style="animation: card-in 0.5s cubic-bezier(0.16,1,0.3,1) both;"
    >
      <!-- Loading -->
      <div v-if="loading" class="flex flex-col items-center gap-4">
        <div class="w-12 h-12 border-3 border-primary-500/30 border-t-primary-500 rounded-full animate-spin" />
        <p class="text-sm text-text-secondary">Verifying your email...</p>
      </div>

      <!-- Success -->
      <div v-else-if="success" class="flex flex-col items-center gap-5">
        <div class="w-16 h-16 rounded-full bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2">
            <path d="M20 6L9 17l-5-5" />
          </svg>
        </div>
        <div>
          <h1 class="font-display text-xl font-bold text-text-primary mb-1.5">Email Verified!</h1>
          <p class="text-sm text-text-secondary">Your email has been successfully verified. You now have full access to the admin panel.</p>
        </div>
        <router-link
          to="/login"
          class="w-full py-3 rounded-[10px] font-display font-semibold text-[0.95rem] text-white gradient-brand border-none cursor-pointer transition-all duration-200 hover:-translate-y-px inline-block text-center"
          style="box-shadow: 0 4px 20px rgba(14,165,233,0.25);"
        >
          Go to Admin Panel
        </router-link>
      </div>

      <!-- Error -->
      <div v-else class="flex flex-col items-center gap-5">
        <div class="w-16 h-16 rounded-full bg-red-500/10 border border-red-500/20 flex items-center justify-center">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="#ef4444" stroke-width="2">
            <circle cx="12" cy="12" r="10" />
            <line x1="15" y1="9" x2="9" y2="15" />
            <line x1="9" y1="9" x2="15" y2="15" />
          </svg>
        </div>
        <div>
          <h1 class="font-display text-xl font-bold text-text-primary mb-1.5">Verification Failed</h1>
          <p class="text-sm text-text-secondary">{{ error }}</p>
        </div>
        <router-link
          to="/login"
          class="text-sm text-primary-400 hover:text-primary-300 transition-colors"
        >
          Back to login
        </router-link>
      </div>
    </div>
  </div>
</template>

<style>
@keyframes drift-a {
  from { transform: translate(0, 0); }
  to   { transform: translate(40px, 30px); }
}
@keyframes card-in {
  from { opacity: 0; transform: translateY(18px) scale(0.98); }
  to   { opacity: 1; transform: translateY(0) scale(1); }
}
</style>
