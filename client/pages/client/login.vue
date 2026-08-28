<template>
  <div class="min-h-dvh bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">

    <!-- ── Background ─────────────────────────────────────────────────── -->
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-secondary-500/30 rounded-full blur-[120px] animate-blob animation-delay-2000" />
      <div class="absolute bottom-0 left-1/2 w-[400px] h-[400px] bg-cyan-500/20 rounded-full blur-[120px] animate-blob animation-delay-4000" />
      <div class="absolute inset-0 opacity-[0.02]">
        <div class="absolute inset-0" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" />
      </div>
      <div class="absolute inset-0" style="background: radial-gradient(circle at center, transparent 40%, #0a0a0f 100%)" />
    </div>

    <div class="absolute top-0 right-0 w-32 h-32 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-32 h-32 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />

    <!-- ── Card ───────────────────────────────────────────────────────── -->
    <div class="w-full max-w-md relative z-10">
      <div class="text-center mb-8">
        <NuxtLink to="/" class="inline-block mb-6">
          <NuxtImg src="/logo.svg" alt="Innovayse" width="160" height="48" loading="eager" class="h-12 w-auto mx-auto" />
        </NuxtLink>
        <h1 class="text-2xl font-bold text-white">{{ $t('client.login.title') }}</h1>
        <p class="text-gray-400 text-sm mt-1">{{ $t('client.login.subtitle') }}</p>
      </div>

      <!-- Error -->
      <div v-if="error" class="mb-4 px-4 py-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm text-center">
        {{ error }}
      </div>

      <div class="relative p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 backdrop-blur-sm">
        <div class="absolute top-0 left-0 w-12 h-12 border-l-2 border-t-2 border-primary-500/40 rounded-tl-2xl pointer-events-none" />
        <div class="absolute bottom-0 right-0 w-12 h-12 border-r-2 border-b-2 border-cyan-500/40 rounded-br-2xl pointer-events-none" />

        <!-- SSO Mode -->
        <a
          v-if="authMode === 'sso'"
          href="/api/portal/auth/sso/authorize"
          class="flex items-center justify-center gap-3 w-full py-3 px-6 rounded-xl bg-gradient-to-r from-primary-600 to-primary-500 text-white font-semibold text-base hover:from-primary-500 hover:to-primary-400 transition-all duration-200 shadow-lg shadow-primary-500/25"
        >
          <svg width="20" height="20" viewBox="0 0 22 22" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M11 2L20 7V15L11 20L2 15V7L11 2Z" stroke="currentColor" stroke-width="1.5" fill="none"/>
            <path d="M11 7L16 10V14L11 17L6 14V10L11 7Z" fill="currentColor" opacity="0.7"/>
          </svg>
          {{ $t('client.login.continueWithInnovayse') }}
        </a>

        <!-- Local Mode -->
        <form v-else class="space-y-4" @submit.prevent="handleLogin">
          <div>
            <label class="block text-xs font-medium text-gray-400 uppercase tracking-wide mb-1.5">{{ $t('client.login.email', 'Email') }}</label>
            <input
              v-model="email"
              type="email"
              required
              autocomplete="email"
              placeholder="you@example.com"
              class="w-full bg-white/5 border border-white/10 focus:border-primary-500 focus:ring-1 focus:ring-primary-500 text-white placeholder-gray-500 rounded-lg px-3 py-2.5 text-sm outline-none transition-all"
            />
          </div>
          <div>
            <label class="block text-xs font-medium text-gray-400 uppercase tracking-wide mb-1.5">{{ $t('client.login.password', 'Password') }}</label>
            <input
              v-model="password"
              type="password"
              required
              autocomplete="current-password"
              placeholder="••••••••"
              class="w-full bg-white/5 border border-white/10 focus:border-primary-500 focus:ring-1 focus:ring-primary-500 text-white placeholder-gray-500 rounded-lg px-3 py-2.5 text-sm outline-none transition-all"
            />
          </div>
          <div class="flex justify-end">
            <NuxtLink to="/client/forgot-password" class="text-xs text-primary-400 hover:text-primary-300 transition-colors">
              {{ $t('client.login.forgotPassword', 'Forgot password?') }}
            </NuxtLink>
          </div>
          <button
            type="submit"
            :disabled="loading"
            class="w-full py-3 px-6 rounded-xl bg-gradient-to-r from-primary-600 to-primary-500 text-white font-semibold text-base hover:from-primary-500 hover:to-primary-400 transition-all duration-200 shadow-lg shadow-primary-500/25 disabled:opacity-50"
          >
            {{ loading ? $t('client.login.signingIn', 'Signing in...') : $t('client.login.signIn', 'Sign in') }}
          </button>
        </form>
      </div>

      <!-- Footer (local mode only) -->
      <p v-if="authMode === 'local'" class="text-center text-gray-400 text-sm mt-6">
        {{ $t('client.login.noAccount', "Don't have an account?") }}
        <NuxtLink to="/client/register" class="text-primary-400 hover:text-primary-300 transition-colors font-medium">
          {{ $t('client.login.createAccount', 'Create account') }}
        </NuxtLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false, middleware: 'client-auth' })

const { login } = useAuthStore()
const config = useRuntimeConfig()
const router = useRouter()
const route = useRoute()

const authMode = config.public.authMode as string
const email = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)

async function handleLogin() {
  error.value = ''
  loading.value = true
  try {
    const result = await login(email.value, password.value)
    if ('twoFactorRequired' in result && result.twoFactorRequired) {
      // TODO: navigate to 2FA page with pendingToken
      error.value = 'Two-factor authentication required.'
      return
    }
    const redirect = (route.query.redirect as string) || '/client/dashboard'
    await router.push(redirect)
  } catch (e: any) {
    error.value = e?.data?.error || e?.message || 'Invalid email or password.'
  } finally {
    loading.value = false
  }
}
</script>
