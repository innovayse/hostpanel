<script setup lang="ts">
/**
 * SSO callback landing page — /auth/callback.
 *
 * SSO redirects here with `code` + `state` in the query string after the
 * user authenticates. Exchanges them for tokens, loads the session, and
 * forwards to /dashboard (or back to /login with an error on failure).
 */
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { setToken } from '../../../composables/useApi'
import { completeSsoLogin } from '../useSso'
import { useAuthStore } from '../stores/authStore'

const router = useRouter()
const authStore = useAuthStore()
const error = ref<string | null>(null)

onMounted(async () => {
  try {
    const query = new URLSearchParams(window.location.search)
    const tokens = await completeSsoLogin(query)
    setToken(tokens.access_token, tokens.refresh_token)
    await authStore.fetchMe()
    await router.replace('/dashboard')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Sign-in failed.'
  }
})
</script>

<template>
  <div class="min-h-screen bg-surface-base flex items-center justify-center">
    <div v-if="error" class="text-center max-w-sm px-6">
      <p class="text-sm text-status-red mb-4">{{ error }}</p>
      <router-link to="/login" class="text-sm text-primary-500 hover:underline">Back to sign in</router-link>
    </div>
    <div v-else class="flex items-center gap-2 text-text-secondary text-sm">
      <span class="w-1.5 h-1.5 bg-primary-500 rounded-full animate-bounce" style="animation-delay:0s"/>
      <span class="w-1.5 h-1.5 bg-primary-500 rounded-full animate-bounce" style="animation-delay:0.15s"/>
      <span class="w-1.5 h-1.5 bg-primary-500 rounded-full animate-bounce" style="animation-delay:0.3s"/>
      <span class="ml-2">Signing in…</span>
    </div>
  </div>
</template>
