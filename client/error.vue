<template>
  <div class="min-h-screen bg-[#0a0a0f] flex items-center justify-center relative overflow-hidden">
    <!-- Background -->
    <div class="absolute inset-0 pointer-events-none">
      <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
      <div class="absolute top-0 left-1/4 w-[600px] h-[600px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
      <div class="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

      <!-- Grid pattern -->
      <div class="absolute inset-0 opacity-[0.02]">
        <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 80px 80px;" class="w-full h-full" />
      </div>

      <!-- Floating particles -->
      <div class="absolute top-1/4 left-1/3 w-3 h-3 bg-primary-400/60 rounded-full animate-float" style="animation-delay: 0.5s;" />
      <div class="absolute bottom-1/3 right-1/4 w-2 h-2 bg-secondary-400/70 rounded-full animate-float" style="animation-delay: 1.5s;" />
    </div>

    <!-- Error Content -->
    <div class="container-custom relative z-10">
      <div class="max-w-2xl mx-auto text-center">
      <!-- Error Code with glitch effect -->
      <div
        v-motion
        :initial="{ opacity: 0, scale: 0.5 }"
        :enter="{ opacity: 1, scale: 1, transition: { type: 'spring', stiffness: 100, damping: 10 } }"
        class="relative mb-8"
      >
        <h1 class="text-[150px] md:text-[200px] font-bold leading-none">
          <span class="bg-gradient-to-r from-primary-400 via-secondary-400 to-primary-400 bg-clip-text text-transparent animate-gradient bg-300">
            {{ error.statusCode }}
          </span>
        </h1>
      </div>

      <!-- Error Message Card -->
      <div
        v-motion
        :initial="{ opacity: 0, y: 50 }"
        :enter="{ opacity: 1, y: 0, transition: { delay: 200 } }"
        class="relative group mb-8"
      >
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/20 to-secondary-500/20 rounded-2xl blur-xl group-hover:blur-2xl transition-all duration-500" />

        <div class="relative bg-[#0d0d12]/80 backdrop-blur-xl border border-white/10 rounded-2xl p-8 md:p-12">
          <!-- Icon -->
          <div class="mb-6 flex justify-center">
            <component
              :is="errorIconComponent"
              :size="64"
              :stroke-width="2"
              class="text-primary-400 animate-bounce"
            />
          </div>

          <!-- Title -->
          <h2 class="text-3xl md:text-4xl font-bold text-white mb-4">
            {{ errorTitle }}
          </h2>

          <!-- Description -->
          <p class="text-lg text-gray-400 mb-8">
            {{ errorMessage }}
          </p>

          <!-- Actions -->
          <div class="flex flex-col sm:flex-row gap-4 justify-center">
            <!-- Go Back Button -->
            <button
              @click="handleGoBack"
              class="group relative px-8 py-4 bg-gradient-to-r from-primary-500 to-primary-600 text-white rounded-xl font-semibold hover:shadow-lg hover:shadow-primary-500/50 transition-all duration-300 hover:-translate-y-1 overflow-hidden"
            >
              <div class="absolute inset-0 bg-gradient-to-r from-primary-600 to-primary-700 opacity-0 group-hover:opacity-100 transition-opacity duration-300" />
              <div class="relative flex items-center gap-2 justify-center">
                <ArrowLeft :size="20" :stroke-width="2" class="group-hover:-translate-x-1 transition-transform" />
                <span>{{ $t('error.goBack') }}</span>
              </div>
            </button>

            <!-- Go Home Button -->
            <NuxtLink
              :to="localePath('/')"
              class="group relative px-8 py-4 bg-white/5 backdrop-blur border border-white/10 text-white rounded-xl font-semibold hover:bg-white/10 hover:border-white/20 transition-all duration-300 hover:-translate-y-1"
            >
              <div class="flex items-center gap-2 justify-center">
                <Home :size="20" :stroke-width="2" class="group-hover:scale-110 transition-transform" />
                <span>{{ $t('error.goHome') }}</span>
              </div>
            </NuxtLink>
          </div>
        </div>
      </div>

      <!-- Help Text -->
      <p
        v-motion
        :initial="{ opacity: 0 }"
        :enter="{ opacity: 1, transition: { delay: 400 } }"
        class="text-sm text-gray-500"
      >
        {{ $t('error.helpText') }}
        <NuxtLink :to="localePath('/contact')" class="text-primary-400 hover:text-primary-300 transition-colors underline">
          {{ $t('error.contactUs') }}
        </NuxtLink>
      </p>
      </div>
    </div>

    <!-- Corner decorations -->
    <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, Home, Search, AlertTriangle, Lock, XCircle } from 'lucide-vue-next'

const props = defineProps({
  error: {
    type: Object,
    required: true
  }
})

const { t } = useI18n()
const localePath = useLocalePath()

// Icon components mapping
const iconComponents = {
  Search,
  AlertTriangle,
  Lock,
  XCircle
}

// Determine error icon based on status code
const errorIcon = computed(() => {
  const code = props.error.statusCode
  if (code === 404) return 'Search'
  if (code === 500) return 'AlertTriangle'
  if (code === 403) return 'Lock'
  return 'XCircle'
})

// Get the icon component
const errorIconComponent = computed(() => iconComponents[errorIcon.value as keyof typeof iconComponents])

// Get error title based on status code
const errorTitle = computed(() => {
  const code = props.error.statusCode
  return t(`error.${code}.title`, t('error.default.title'))
})

// Get error message based on status code
const errorMessage = computed(() => {
  const code = props.error.statusCode
  return t(`error.${code}.message`, props.error.message || t('error.default.message'))
})

// Handle go back action
const handleGoBack = () => {
  if (window.history.length > 1) {
    window.history.back()
  } else {
    navigateTo(localePath('/'))
  }
}

// Clear error on component unmount
const handleClearError = () => {
  clearError({ redirect: '/' })
}
</script>
