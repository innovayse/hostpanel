<script setup lang="ts">
/**
 * Admin login page — full-screen dark with brand gradient accents.
 *
 * Redirects to /dashboard on successful authentication.
 */
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/authStore'

const router = useRouter()
const authStore = useAuthStore()

/** Email field value. */
const email = ref('')

/** Password field value. */
const password = ref('')

/** True while the login request is in flight. */
const loading = ref(false)

/** Error message shown when login fails. */
const error = ref<string | null>(null)

/**
 * Submits the login form.
 *
 * @returns Promise that resolves after login attempt.
 */
async function handleLogin(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    await authStore.login(email.value, password.value)
    await router.push('/dashboard')
  } catch {
    error.value = 'Invalid email or password.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <!-- Root: dark bg + ambient orbs -->
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
        <p class="text-sm text-text-secondary">Sign in to manage your platform</p>
      </div>

      <!-- Form -->
      <form @submit.prevent="handleLogin" class="flex flex-col gap-4">

        <!-- Email -->
        <div class="flex flex-col gap-1.5">
          <label class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Email address</label>
          <div class="relative">
            <svg class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
              <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/>
              <polyline points="22,6 12,13 2,6"/>
            </svg>
            <input
              v-model="email"
              type="email"
              required
              placeholder="admin@innovayse.com"
              autocomplete="email"
              class="w-full bg-white/[0.04] border border-white/[0.08] rounded-[10px] pl-9 pr-3 py-2.5 text-sm text-text-primary placeholder:text-text-muted outline-none transition-all duration-200 focus:border-primary-500/50 focus:bg-primary-500/[0.04] focus:ring-2 focus:ring-primary-500/10"
            />
          </div>
        </div>

        <!-- Password -->
        <div class="flex flex-col gap-1.5">
          <label class="text-[0.72rem] font-semibold uppercase tracking-[0.08em] text-text-muted">Password</label>
          <div class="relative">
            <svg class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2"/>
              <path d="M7 11V7a5 5 0 0 1 10 0v4"/>
            </svg>
            <input
              v-model="password"
              type="password"
              required
              placeholder="••••••••••"
              autocomplete="current-password"
              class="w-full bg-white/[0.04] border border-white/[0.08] rounded-[10px] pl-9 pr-3 py-2.5 text-sm text-text-primary placeholder:text-text-muted outline-none transition-all duration-200 focus:border-primary-500/50 focus:bg-primary-500/[0.04] focus:ring-2 focus:ring-primary-500/10"
            />
          </div>
        </div>

        <!-- Error -->
        <div v-if="error" class="flex items-center gap-2 text-[0.8rem] text-status-red bg-status-red/8 border border-status-red/20 rounded-lg px-3 py-2.5">
          <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
          {{ error }}
        </div>

        <!-- Submit -->
        <button
          type="submit"
          :disabled="loading"
          class="mt-1 w-full py-3 rounded-[10px] font-display font-semibold text-[0.95rem] text-white gradient-brand border-none cursor-pointer transition-all duration-200 hover:-translate-y-px disabled:opacity-50 disabled:cursor-not-allowed disabled:translate-y-0"
          style="box-shadow: 0 4px 20px rgba(14,165,233,0.25);"
        >
          <span v-if="!loading">Sign in</span>
          <span v-else class="flex items-center justify-center gap-1.5">
            <span class="w-1.5 h-1.5 bg-white rounded-full animate-bounce" style="animation-delay:0s"/>
            <span class="w-1.5 h-1.5 bg-white rounded-full animate-bounce" style="animation-delay:0.15s"/>
            <span class="w-1.5 h-1.5 bg-white rounded-full animate-bounce" style="animation-delay:0.3s"/>
          </span>
        </button>

      </form>
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
