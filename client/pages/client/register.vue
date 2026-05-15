<template>
  <div class="min-h-screen bg-[#0a0a0f] flex items-center justify-center px-4 py-16 relative overflow-hidden">

    <!-- Background -->
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <div class="absolute top-0 right-1/4 w-[500px] h-[500px] bg-secondary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute top-1/3 left-1/4 w-[600px] h-[600px] bg-primary-500/30 rounded-full blur-[120px] animate-blob animation-delay-2000" />
      <div class="absolute bottom-0 right-1/2 w-[400px] h-[400px] bg-cyan-500/20 rounded-full blur-[120px] animate-blob animation-delay-4000" />
      <div class="absolute inset-0 opacity-[0.02]">
        <div class="absolute inset-0" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" />
      </div>
      <div class="absolute inset-0" style="background: radial-gradient(circle at center, transparent 40%, #0a0a0f 100%)" />
    </div>

    <div class="absolute top-0 right-0 w-32 h-32 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-32 h-32 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute top-1/4 left-1/4 w-2 h-2 bg-secondary-400 rounded-full animate-float" style="animation-delay: 0.5s;" />
      <div class="absolute top-2/3 right-1/3 w-3 h-3 bg-primary-400 rounded-full animate-float" style="animation-delay: 1.5s;" />
      <div class="absolute bottom-1/4 left-1/2 w-2 h-2 bg-cyan-400 rounded-full animate-float" style="animation-delay: 2.5s;" />
    </div>

    <!-- Card -->
    <div class="w-full max-w-md relative z-10">
      <div class="text-center mb-8">
        <NuxtLink to="/" class="inline-block mb-6">
          <NuxtImg src="/logo.png" alt="Innovayse" width="160" height="48" loading="eager" class="h-12 w-auto mx-auto" />
        </NuxtLink>
        <h1 class="text-2xl font-bold text-white">{{ $t('client.register.title') }}</h1>
        <p class="text-gray-400 text-sm mt-1">{{ $t('client.register.subtitle') }}</p>
      </div>

      <div class="relative p-6 sm:p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 backdrop-blur-sm">
        <div class="absolute top-0 left-0 w-12 h-12 border-l-2 border-t-2 border-primary-500/40 rounded-tl-2xl pointer-events-none" />
        <div class="absolute bottom-0 right-0 w-12 h-12 border-r-2 border-b-2 border-secondary-500/40 rounded-br-2xl pointer-events-none" />

        <UiForm
          :error="error"
          :success="success ? $t('client.register.success') : undefined"
          spacing="md"
          @submit="handleRegister"
        >
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <UiInput v-model="form.firstname" :label="$t('client.register.firstName')" :placeholder="$t('client.register.firstNamePlaceholder')" required />
            <UiInput v-model="form.lastname" :label="$t('client.register.lastName')" :placeholder="$t('client.register.lastNamePlaceholder')" required />
          </div>
          <UiInput v-model="form.email" type="email" :label="$t('client.register.email')" :placeholder="$t('client.register.emailPlaceholder')" icon="mdi:email" autocomplete="email" required />
          <UiInput v-model="form.password" type="password" :label="$t('client.register.password')" :placeholder="$t('client.register.passwordPlaceholder')" autocomplete="new-password" required />

          <UiButton type="submit" variant="primary" size="lg" :full-width="true" :loading="loading" class="hover:shadow-xl hover:shadow-primary-500/30">
            <UserPlus v-if="!loading" :size="18" :stroke-width="2" class="mr-2" />
            {{ loading ? $t('client.register.submitting') : $t('client.register.submit') }}
          </UiButton>
        </UiForm>

        <div class="mt-6 text-center">
          <p class="text-gray-500 text-sm">
            {{ $t('client.register.haveAccount') }}
            <NuxtLink to="/client/login" class="text-primary-400 hover:text-primary-300 font-medium transition-colors">{{ $t('client.register.signIn') }}</NuxtLink>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { UserPlus } from 'lucide-vue-next'

definePageMeta({ layout: false, middleware: 'client-auth' })

const { t } = useI18n()
const { register } = useClientAuth()

const form = reactive({
  firstname: '',
  lastname: '',
  email: '',
  password: ''
})

const loading = ref(false)
const error = ref('')
const success = ref(false)

async function handleRegister() {
  loading.value = true
  error.value = ''
  try {
    await register(form)
    success.value = true
    setTimeout(() => navigateTo('/client/login'), 1500)
  } catch (err) {
    error.value = (err as { data?: { statusMessage?: string } })?.data?.statusMessage || t('client.register.errorDefault')
  } finally {
    loading.value = false
  }
}
</script>
