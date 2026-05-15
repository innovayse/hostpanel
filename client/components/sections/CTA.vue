<template>
  <section class="py-24 relative overflow-hidden">
    <!-- Background cover -->
    <div class="absolute inset-0">
      <!-- Dark gradient base -->
      <div class="absolute inset-0 bg-gradient-to-br from-gray-900 via-[#0a0a0f] to-gray-900" />

      <!-- Accent gradient overlay -->
      <div class="absolute inset-0 bg-gradient-to-r from-primary-600/20 via-transparent to-secondary-600/20" />

      <!-- Glowing orbs -->
      <div class="absolute top-0 left-1/4 w-96 h-96 bg-primary-500/30 rounded-full blur-[150px] animate-blob" />
      <div class="absolute bottom-0 right-1/4 w-80 h-80 bg-secondary-500/30 rounded-full blur-[150px] animate-blob animation-delay-2000" />

      <!-- Floating particles -->
      <div class="absolute top-1/3 left-1/4 w-2 h-2 bg-primary-400/70 rounded-full animate-float" style="animation-delay: 0.5s;" />
      <div class="absolute top-1/2 right-1/3 w-3 h-3 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 1.2s;" />
      <div class="absolute bottom-1/3 left-1/3 w-2 h-2 bg-cyan-400/70 rounded-full animate-float" style="animation-delay: 2s;" />

      <!-- Border lines -->
      <div class="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-primary-500/50 to-transparent" />
      <div class="absolute bottom-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-secondary-500/50 to-transparent" />

      <!-- Grid overlay -->
      <div class="absolute inset-0 opacity-[0.02]">
        <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" class="w-full h-full" />
      </div>
    </div>

    <div class="container-custom relative z-10">
      <div
        v-motion
        :initial="{ opacity: 0, y: 40 }"
        :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 600 } }"
        class="max-w-3xl mx-auto text-center"
      >
        <!-- Badge -->
        <span class="inline-block px-4 py-2 bg-white/5 backdrop-blur-sm border border-white/10 rounded-full text-sm text-gray-300 mb-6">
          {{ $t('cta.badge') }}
        </span>

        <!-- Headline -->
        <h2 class="text-4xl md:text-5xl lg:text-6xl font-bold text-white mb-4">
          {{ $t('cta.title') }}
        </h2>

        <p class="text-lg text-gray-400 mb-10 max-w-xl mx-auto">
          {{ $t('cta.subtitle') }}
        </p>

        <!-- Email form -->
        <form class="flex flex-col sm:flex-row gap-4 max-w-xl mx-auto relative" @submit.prevent="handleSubmit">
          <!-- Glow effect on focus -->
          <div class="absolute -inset-1 bg-gradient-to-r from-primary-500/20 to-secondary-500/20 rounded-2xl opacity-0 group-focus-within:opacity-100 blur transition-opacity duration-500" />

          <UiInput
            v-model="email"
            type="email"
            :placeholder="$t('cta.placeholder')"
            required
            class="flex-1 relative"
            size="lg"
          />

          <UiButton
            type="submit"
            variant="primary"
            size="lg"
            :loading="isSubmitting"
            class="relative hover:shadow-lg hover:shadow-primary-500/50 transition-all duration-300"
          >
            <span>{{ $t('cta.button') }}</span>
            <ArrowRight v-if="!isSubmitting" :size="18" :stroke-width="2" class="ml-2" />
          </UiButton>
        </form>

        <!-- Success message -->
        <Transition name="fade">
          <div v-if="showSuccess" class="mt-6 p-4 bg-green-500/10 border border-green-500/20 backdrop-blur-sm rounded-xl">
            <div class="flex items-center justify-center gap-2">
              <CheckCircle :size="20" :stroke-width="2" class="text-green-400" />
              <p class="text-sm text-green-400">
                {{ $t('cta.success') }}
              </p>
            </div>
          </div>
        </Transition>
      </div>
    </div>

    <!-- Decorative corner accents -->
    <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
    <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
  </section>
</template>

<script setup lang="ts">
/**
 * CTA section with dark theme cover
 */

import { ArrowRight, CheckCircle } from 'lucide-vue-next'

const email = ref('')
const isSubmitting = ref(false)
const showSuccess = ref(false)

const handleSubmit = async () => {
  if (!email.value) return

  isSubmitting.value = true
  await new Promise(resolve => setTimeout(resolve, 1000))

  showSuccess.value = true
  email.value = ''
  isSubmitting.value = false

  setTimeout(() => {
    showSuccess.value = false
  }, 3000)
}
</script>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
