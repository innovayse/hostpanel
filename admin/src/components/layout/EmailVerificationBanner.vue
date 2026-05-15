<script setup lang="ts">
/**
 * Banner overlay shown when the current admin's email is not yet verified.
 *
 * Displays over page content (not the sidebar) with a message to check inbox.
 * Automatically hides when emailVerified becomes true.
 */
import { useAuthStore } from '../../modules/auth/stores/authStore'

const authStore = useAuthStore()
</script>

<template>
  <div
    v-if="authStore.emailVerified === false"
    class="absolute inset-0 z-20 flex items-center justify-center bg-surface-base/80 backdrop-blur-sm"
  >
    <div class="max-w-md mx-4 bg-surface-card border border-white/[0.06] rounded-2xl p-8 shadow-2xl text-center">
      <div class="w-14 h-14 mx-auto mb-5 rounded-full bg-amber-500/10 border border-amber-500/20 flex items-center justify-center">
        <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" stroke-width="1.5">
          <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/>
          <polyline points="22,6 12,13 2,6"/>
        </svg>
      </div>
      <h2 class="font-display text-lg font-bold text-text-primary mb-2">Verify Your Email</h2>
      <p class="text-sm text-text-secondary mb-4">
        Please check your inbox and click the verification link to activate your admin account. You won't be able to use the admin panel until your email is verified.
      </p>
      <p class="text-xs text-text-muted">
        Didn't receive the email? Check your spam folder.
      </p>
    </div>
  </div>
</template>
