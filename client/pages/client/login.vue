<template>
  <div class="min-h-screen bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">

    <!-- ── Background (matches Hero section) ─────────────────────────────── -->
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <!-- Animated blobs -->
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-secondary-500/30 rounded-full blur-[120px] animate-blob animation-delay-2000" />
      <div class="absolute bottom-0 left-1/2 w-[400px] h-[400px] bg-cyan-500/20 rounded-full blur-[120px] animate-blob animation-delay-4000" />

      <!-- Grid pattern -->
      <div class="absolute inset-0 opacity-[0.02]">
        <div class="absolute inset-0" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" />
      </div>

      <!-- Radial vignette -->
      <div class="absolute inset-0" style="background: radial-gradient(circle at center, transparent 40%, #0a0a0f 100%)" />
    </div>

    <!-- Page corner accents -->
    <div class="absolute top-0 right-0 w-32 h-32 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-32 h-32 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />

    <!-- Floating particles -->
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute top-1/4 right-1/4 w-2 h-2 bg-primary-400 rounded-full animate-float" style="animation-delay: 0.5s;" />
      <div class="absolute top-1/3 left-1/3 w-3 h-3 bg-secondary-400 rounded-full animate-float" style="animation-delay: 1.5s;" />
      <div class="absolute bottom-1/3 right-1/3 w-2 h-2 bg-cyan-400 rounded-full animate-float" style="animation-delay: 2.5s;" />
    </div>

    <!-- ── Card ───────────────────────────────────────────────────────────── -->
    <div class="w-full max-w-md relative z-10">
      <!-- Logo -->
      <div class="text-center mb-8">
        <NuxtLink to="/" class="inline-block mb-6">
          <NuxtImg
            src="/logo.svg"
            alt="Innovayse"
            width="160"
            height="48"
            loading="eager"
            class="h-12 w-auto mx-auto"
          />
        </NuxtLink>
        <h1 class="text-2xl font-bold text-white">{{ $t('client.login.title') }}</h1>
        <p class="text-gray-400 text-sm mt-1">{{ $t('client.login.subtitle') }}</p>
      </div>

      <div class="relative p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 backdrop-blur-sm">
        <!-- Card corner accents -->
        <div class="absolute top-0 left-0 w-12 h-12 border-l-2 border-t-2 border-primary-500/40 rounded-tl-2xl pointer-events-none" />
        <div class="absolute bottom-0 right-0 w-12 h-12 border-r-2 border-b-2 border-cyan-500/40 rounded-br-2xl pointer-events-none" />

        <!-- ── Email + Password form ──────────────────────────── -->
        <template v-if="!twoFactorRequired">
          <UiForm :error="error" spacing="lg" @submit="handleLogin">
            <UiInput
              v-model="form.email"
              type="email"
              :label="$t('client.login.email')"
              :placeholder="$t('client.login.emailPlaceholder')"
              icon="mdi:email"
              autocomplete="email"
              required
            />
            <UiInput
              v-model="form.password"
              type="password"
              :label="$t('client.login.password')"
              placeholder="••••••••"
              autocomplete="current-password"
              required
            />
            <UiButton
              type="submit"
              variant="primary"
              size="lg"
              :full-width="true"
              :loading="loading"
              class="hover:shadow-xl hover:shadow-primary-500/30"
            >
              <LogIn v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
              {{ loading ? $t('client.login.submitting') : $t('client.login.submit') }}
            </UiButton>
          </UiForm>

          <div class="mt-6 text-center">
            <p class="text-gray-500 text-sm">
              {{ $t('client.login.noAccount') }}
              <NuxtLink to="/client/register" class="text-primary-400 hover:text-primary-300 font-medium transition-colors">
                {{ $t('client.login.createOne') }}
              </NuxtLink>
            </p>
          </div>

          <div class="mt-3 text-center">
            <NuxtLink to="/client/forgot" class="text-gray-500 hover:text-gray-300 text-xs transition-colors">
              {{ $t('client.login.forgotPassword') }}
            </NuxtLink>
          </div>
        </template>

        <!-- ── 2FA step ──────────────────────────────────────── -->
        <template v-else>
          <div class="text-center mb-6">
            <div class="w-14 h-14 rounded-2xl bg-gradient-to-br from-primary-500/20 to-cyan-500/20 border border-primary-500/30 flex items-center justify-center mx-auto mb-4">
              <ShieldCheck :size="28" :stroke-width="1.5" class="text-primary-400" />
            </div>
            <h3 class="text-lg font-bold text-white">{{ $t('client.twoFactor.stepTitle') }}</h3>
            <p class="text-gray-400 text-sm mt-1">{{ $t('client.twoFactor.stepSubtitle') }}</p>
          </div>

          <UiForm :error="error" spacing="lg" @submit="handleTwoFactor">
            <UiOtpInput
              v-model="twoFactorCode"
              :length="6"
              :error="error ? ' ' : ''"
              class="py-2"
            />
            <UiButton
              type="submit"
              variant="primary"
              size="lg"
              :full-width="true"
              :loading="loading"
              class="hover:shadow-xl hover:shadow-primary-500/30"
            >
              <ShieldCheck v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
              {{ loading ? $t('client.login.submitting') : $t('client.twoFactor.verify') }}
            </UiButton>
          </UiForm>

          <div class="mt-4 text-center">
            <button
              type="button"
              class="text-gray-500 hover:text-gray-300 text-xs transition-colors"
              @click="twoFactorRequired = false; error = ''"
            >
              {{ $t('client.twoFactor.back') }}
            </button>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { LogIn, ShieldCheck } from 'lucide-vue-next'

definePageMeta({ layout: false, middleware: 'client-auth' })

const { login, loginWithTwoFactor } = useClientAuth()
const route = useRoute()
const { t } = useI18n()

const form = reactive({ email: '', password: '' })
const loading = ref(false)
const error = ref('')

// 2FA step
const twoFactorRequired = ref(false)
const pendingToken = ref('')
const twoFactorCode = ref('')

async function handleLogin() {
  loading.value = true
  error.value = ''
  try {
    const result = await login(form.email, form.password)
    if ('twoFactorRequired' in result && result.twoFactorRequired) {
      pendingToken.value = result.pendingToken
      twoFactorRequired.value = true
      return
    }
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/client/dashboard'
    window.location.href = redirect
  } catch (err: any) {
    error.value = err?.data?.statusMessage || t('client.login.errorInvalid')
  } finally {
    loading.value = false
  }
}

async function handleTwoFactor() {
  loading.value = true
  error.value = ''
  try {
    await loginWithTwoFactor(pendingToken.value, twoFactorCode.value)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/client/dashboard'
    window.location.href = redirect
  } catch (err: any) {
    error.value = err?.data?.statusMessage || t('client.twoFactor.errorInvalid')
  } finally {
    loading.value = false
  }
}
</script>
