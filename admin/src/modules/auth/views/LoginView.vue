<script setup lang="ts">
/**
 * Admin login page — full-screen dark with brand gradient accents.
 *
 * Authentication is delegated entirely to Innovayse SSO: this page just
 * kicks off the redirect. The actual token exchange happens on
 * /auth/callback once SSO sends the browser back.
 */
import { ref } from 'vue'
import { useAuthStore } from '../stores/authStore'

const authStore = useAuthStore()

/** True while the SSO redirect is being prepared (PKCE pair generation). */
const loading = ref(false)

/** Error message shown if starting the SSO flow itself fails. */
const error = ref<string | null>(null)

/**
 * Starts the SSO login flow — navigates the browser away from this app.
 */
async function handleLogin(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    await authStore.login()
  } catch {
    error.value = 'Could not start sign-in. Please try again.'
    loading.value = false
  }
}
</script>

<template>
  <!-- Root: dark bg + ambient orbs -->
  <div class="relative min-h-dvh bg-surface-base flex items-center justify-center overflow-hidden">

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

    <!-- Dot-grid overlay -->
    <div
      class="pointer-events-none absolute inset-0"
      style="
        background-image: linear-gradient(rgba(14,165,233,0.04) 1px, transparent 1px),
                          linear-gradient(90deg, rgba(14,165,233,0.04) 1px, transparent 1px);
        background-size: 48px 48px;
        mask-image: radial-gradient(ellipse 80% 80% at 50% 50%, black 40%, transparent 100%);
      "
    />

    <!-- Card -->
    <div
      class="relative z-10 w-full max-w-sm mx-4 bg-surface-card/85 backdrop-blur-2xl border border-white/[0.06] rounded-2xl p-10 shadow-2xl"
      style="animation: card-in 0.5s cubic-bezier(0.16,1,0.3,1) both;"
    >
      <!-- Logo -->
      <div class="flex items-center gap-2.5 mb-8">
        <div class="w-9 h-9 flex items-center justify-center rounded-[10px] bg-primary-500/10 border border-primary-500/20 shrink-0">
          <svg width="18" height="18" viewBox="0 0 22 22" fill="none">
            <path d="M11 2L20 7V15L11 20L2 15V7L11 2Z" stroke="url(#lg1)" stroke-width="1.5" fill="none"/>
            <path d="M11 7L16 10V14L11 17L6 14V10L11 7Z" fill="url(#lg1)" opacity="0.7"/>
            <defs>
              <linearGradient id="lg1" x1="2" y1="2" x2="20" y2="20">
                <stop offset="0%" stop-color="#0ea5e9"/>
                <stop offset="100%" stop-color="#a855f7"/>
              </linearGradient>
            </defs>
          </svg>
        </div>
        <span class="font-display font-bold text-[1.05rem] gradient-brand-text">Innovayse</span>
      </div>

      <!-- Heading -->
      <div class="mb-7">
        <h1 class="font-display text-[1.6rem] font-bold text-text-primary tracking-tight leading-none mb-1.5">Admin Panel</h1>
        <p class="text-sm text-text-secondary">Sign in with your Innovayse account</p>
      </div>

      <!-- Error -->
      <div v-if="error" class="mb-4 flex items-center gap-2 text-[0.8rem] text-status-red bg-status-red/8 border border-status-red/20 rounded-lg px-3 py-2.5">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
        </svg>
        {{ error }}
      </div>

      <!-- SSO sign-in -->
      <button
        type="button"
        :disabled="loading"
        @click="handleLogin"
        class="w-full py-3 rounded-[10px] font-display font-semibold text-[0.95rem] text-white gradient-brand border-none cursor-pointer transition-all duration-200 hover:-translate-y-px disabled:opacity-50 disabled:cursor-not-allowed disabled:translate-y-0"
        style="box-shadow: 0 4px 20px rgba(14,165,233,0.25);"
      >
        <span v-if="!loading">Sign in with Innovayse SSO</span>
        <span v-else class="flex items-center justify-center gap-1.5">
          <span class="w-1.5 h-1.5 bg-white rounded-full animate-bounce" style="animation-delay:0s"/>
          <span class="w-1.5 h-1.5 bg-white rounded-full animate-bounce" style="animation-delay:0.15s"/>
          <span class="w-1.5 h-1.5 bg-white rounded-full animate-bounce" style="animation-delay:0.3s"/>
        </span>
      </button>
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
