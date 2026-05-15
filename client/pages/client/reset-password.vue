<template>
  <div class="min-h-screen bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">

    <!-- ── Background ─────────────────────────────────────────────────────── -->
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-secondary-500/30 rounded-full blur-[120px] animate-blob animation-delay-2000" />
      <div class="absolute bottom-0 left-1/2 w-[400px] h-[400px] bg-cyan-500/20 rounded-full blur-[120px] animate-blob animation-delay-4000" />

      <div class="absolute inset-0 opacity-[0.02]">
        <div class="absolute inset-0" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" />
      </div>

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
      <div class="text-center mb-8">
        <NuxtLink to="/" class="inline-block mb-6">
          <NuxtImg
            src="/logo.png"
            alt="Innovayse"
            width="160"
            height="48"
            loading="eager"
            class="h-12 w-auto mx-auto"
          />
        </NuxtLink>
        <h1 class="text-2xl font-bold text-white">{{ $t('client.resetPassword.title') }}</h1>
        <p class="text-gray-400 text-sm mt-1">{{ $t('client.resetPassword.subtitle') }}</p>
      </div>

      <div class="relative p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 backdrop-blur-sm">
        <!-- Card corner accents -->
        <div class="absolute top-0 left-0 w-12 h-12 border-l-2 border-t-2 border-primary-500/40 rounded-tl-2xl pointer-events-none" />
        <div class="absolute bottom-0 right-0 w-12 h-12 border-r-2 border-b-2 border-cyan-500/40 rounded-br-2xl pointer-events-none" />

        <!-- Invalid token state -->
        <div v-if="invalidParams" class="text-center py-4">
          <div class="w-16 h-16 rounded-2xl bg-red-500/10 border border-red-500/20 flex items-center justify-center mx-auto mb-5">
            <AlertCircle :size="32" :stroke-width="1.5" class="text-red-400" />
          </div>
          <h2 class="text-lg font-bold text-white mb-2">{{ $t('client.resetPassword.invalidTitle') }}</h2>
          <p class="text-gray-400 text-sm leading-relaxed">{{ $t('client.resetPassword.invalidMessage') }}</p>
          <NuxtLink
            to="/client/forgot"
            class="inline-flex items-center gap-2 mt-6 text-sm text-primary-400 hover:text-primary-300 font-medium transition-colors"
          >
            {{ $t('client.resetPassword.requestNew') }}
          </NuxtLink>
        </div>

        <!-- Success state -->
        <div v-else-if="success" class="text-center py-4">
          <div class="w-16 h-16 rounded-2xl bg-green-500/10 border border-green-500/20 flex items-center justify-center mx-auto mb-5">
            <CheckCircle :size="32" :stroke-width="1.5" class="text-green-400" />
          </div>
          <h2 class="text-lg font-bold text-white mb-2">{{ $t('client.resetPassword.successTitle') }}</h2>
          <p class="text-gray-400 text-sm leading-relaxed">{{ $t('client.resetPassword.successMessage') }}</p>
          <NuxtLink
            to="/client/login"
            class="inline-flex items-center gap-2 mt-6 text-sm text-primary-400 hover:text-primary-300 font-medium transition-colors"
          >
            <ArrowLeft :size="14" :stroke-width="2" />
            {{ $t('client.resetPassword.backToSignIn') }}
          </NuxtLink>
        </div>

        <!-- Form state -->
        <UiForm v-else :error="error" spacing="lg" @submit="handleSubmit">
          <UiInput
            v-model="newPassword"
            type="password"
            :label="$t('client.resetPassword.newPassword')"
            :placeholder="$t('client.resetPassword.newPasswordPlaceholder')"
            autocomplete="new-password"
            required
          />
          <UiInput
            v-model="confirmPassword"
            type="password"
            :label="$t('client.resetPassword.confirmPassword')"
            :placeholder="$t('client.resetPassword.confirmPasswordPlaceholder')"
            autocomplete="new-password"
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
            <KeyRound v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
            {{ loading ? $t('client.resetPassword.submitting') : $t('client.resetPassword.submit') }}
          </UiButton>
          <div class="text-center">
            <NuxtLink
              to="/client/login"
              class="inline-flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-300 transition-colors"
            >
              <ArrowLeft :size="14" :stroke-width="2" />
              {{ $t('client.resetPassword.backToSignIn') }}
            </NuxtLink>
          </div>
        </UiForm>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Password reset page — accepts token and email from URL query params,
 * lets the user set a new password.
 */
import { AlertCircle, ArrowLeft, CheckCircle, KeyRound } from 'lucide-vue-next'
import { $fetch } from 'ofetch'

definePageMeta({ layout: false })

const route = useRoute()
const { t } = useI18n()

const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))
const email = computed(() => (typeof route.query.email === 'string' ? route.query.email : ''))
const invalidParams = computed(() => !token.value || !email.value)

const newPassword = ref('')
const confirmPassword = ref('')
const loading = ref(false)
const error = ref('')
const success = ref(false)

/** Submits the new password to the backend reset endpoint. */
async function handleSubmit() {
  if (newPassword.value !== confirmPassword.value) {
    error.value = t('client.resetPassword.errorMismatch')
    return
  }

  if (newPassword.value.length < 8) {
    error.value = t('client.resetPassword.errorMinLength')
    return
  }

  loading.value = true
  error.value = ''
  try {
    await $fetch('/api/portal/auth/reset-password', {
      method: 'POST',
      body: {
        email: email.value,
        token: token.value,
        newPassword: newPassword.value,
      },
    })
    success.value = true
  } catch {
    error.value = t('client.resetPassword.errorDefault')
  } finally {
    loading.value = false
  }
}
</script>
