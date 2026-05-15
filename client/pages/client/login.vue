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
            src="/logo.png"
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
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { LogIn } from 'lucide-vue-next'

definePageMeta({ layout: false, middleware: 'client-auth' })

const { login } = useClientAuth()
const route = useRoute()

const form = reactive({ email: '', password: '' })
const loading = ref(false)
const error = ref('')

async function handleLogin() {
  loading.value = true
  error.value = ''
  try {
    await login(form.email, form.password)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/client/dashboard'
    // Use location.href (full reload) so the new authed cookie is picked up by middleware
    window.location.href = redirect
  } catch (err: any) {
    error.value = err?.data?.statusMessage || useI18n().t('client.login.errorInvalid')
  } finally {
    loading.value = false
  }
}
</script>
