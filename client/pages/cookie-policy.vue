<template>
  <div class="min-h-screen bg-[#0a0a0f]">
    <!-- Hero Section -->
    <section class="py-10 md:py-24 bg-[#0a0a0f] relative overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[450px] h-[450px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 70px 70px;" class="w-full h-full" />
        </div>
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto text-center">
          <h1
            v-motion
            :initial="{ opacity: 0, y: 30 }"
            :enter="{ opacity: 1, y: 0, transition: { duration: 600 } }"
            class="text-4xl md:text-5xl lg:text-6xl font-bold text-white mb-6"
          >
            {{ $t('cookiePolicy.title') }}
          </h1>
          <p
            v-motion
            :initial="{ opacity: 0, y: 20 }"
            :enter="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
            class="text-xl text-gray-400"
          >
            {{ $t('cookiePolicy.lastUpdated') }}: {{ lastUpdated }}
          </p>
        </div>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Content Section -->
    <section class="py-8 md:py-20 bg-[#0d0d12] relative overflow-hidden">
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-r from-primary-500/5 via-transparent to-secondary-500/5" />
      </div>

      <div class="container-custom relative z-10">
        <div class="max-w-4xl mx-auto">
          <div class="prose prose-invert prose-lg max-w-none">
            <!-- Introduction -->
            <div
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { duration: 500 } }"
              class="mb-12"
            >
              <h2 class="text-2xl font-bold text-white mb-4">{{ $t('cookiePolicy.sections.introduction.title') }}</h2>
              <p class="text-gray-400 leading-relaxed">{{ $t('cookiePolicy.sections.introduction.content') }}</p>
            </div>

            <!-- What Are Cookies -->
            <div
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 100, duration: 500 } }"
              class="mb-12"
            >
              <h2 class="text-2xl font-bold text-white mb-4">{{ $t('cookiePolicy.sections.whatAreCookies.title') }}</h2>
              <p class="text-gray-400 leading-relaxed mb-4">{{ $t('cookiePolicy.sections.whatAreCookies.content') }}</p>
            </div>

            <!-- Types of Cookies -->
            <div
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 200, duration: 500 } }"
              class="mb-12"
            >
              <h2 class="text-2xl font-bold text-white mb-4">{{ $t('cookiePolicy.sections.typesOfCookies.title') }}</h2>

              <div class="space-y-6">
                <div v-for="(type, index) in cookieTypes" :key="index" class="p-6 rounded-xl bg-white/5 border border-white/10">
                  <h3 class="text-xl font-semibold text-white mb-2">{{ type.title }}</h3>
                  <p class="text-gray-400">{{ type.description }}</p>
                </div>
              </div>
            </div>

            <!-- How We Use Cookies -->
            <div
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 300, duration: 500 } }"
              class="mb-12"
            >
              <h2 class="text-2xl font-bold text-white mb-4">{{ $t('cookiePolicy.sections.howWeUseCookies.title') }}</h2>
              <ul class="space-y-3">
                <li v-for="(use, index) in cookieUses" :key="index" class="flex items-start gap-3">
                  <CheckCircle :size="20" :stroke-width="2" class="text-primary-400 flex-shrink-0 mt-1" />
                  <span class="text-gray-400">{{ use }}</span>
                </li>
              </ul>
            </div>

            <!-- Third Party Cookies -->
            <div
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 400, duration: 500 } }"
              class="mb-12"
            >
              <h2 class="text-2xl font-bold text-white mb-4">{{ $t('cookiePolicy.sections.thirdPartyCookies.title') }}</h2>
              <p class="text-gray-400 leading-relaxed mb-4">{{ $t('cookiePolicy.sections.thirdPartyCookies.content') }}</p>
            </div>

            <!-- Managing Cookies -->
            <div
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 500, duration: 500 } }"
              class="mb-12"
            >
              <h2 class="text-2xl font-bold text-white mb-4">{{ $t('cookiePolicy.sections.managingCookies.title') }}</h2>
              <p class="text-gray-400 leading-relaxed mb-4">{{ $t('cookiePolicy.sections.managingCookies.content') }}</p>
              <div class="p-6 rounded-xl bg-primary-500/10 border border-primary-500/20">
                <p class="text-gray-300 flex items-start gap-2">
                  <Info :size="20" :stroke-width="2" class="text-primary-400 flex-shrink-0" />
                  <span>{{ $t('cookiePolicy.sections.managingCookies.note') }}</span>
                </p>
              </div>
            </div>

            <!-- Contact -->
            <div
              v-motion
              :initial="{ opacity: 0, y: 30 }"
              :visibleOnce="{ opacity: 1, y: 0, transition: { delay: 600, duration: 500 } }"
              class="mb-12"
            >
              <h2 class="text-2xl font-bold text-white mb-4">{{ $t('cookiePolicy.sections.contact.title') }}</h2>
              <p class="text-gray-400 leading-relaxed">
                {{ $t('cookiePolicy.sections.contact.content') }}
                <a href="mailto:contact@innovayse.com" class="text-primary-400 hover:text-primary-300 transition-colors">contact@innovayse.com</a>
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * Cookie Policy page
 * Explains how Innovayse uses cookies
 */

import { CheckCircle, Info } from 'lucide-vue-next'

const { t, tm } = useI18n()

const lastUpdated = '2026-03-12'

const cookieTypes = computed(() => tm('cookiePolicy.sections.typesOfCookies.types') as Array<{ title: string; description: string }>)
const cookieUses = computed(() => tm('cookiePolicy.sections.howWeUseCookies.uses') as string[])

// SEO
useSeoMeta({
  title: 'Cookie Policy | Innovayse',
  description: 'Learn how Innovayse uses cookies to enhance your browsing experience. Understand what cookies are, how we use them, and how you can manage your cookie preferences.',
  robots: 'noindex, follow',

  // Open Graph
  ogTitle: 'Cookie Policy | Innovayse',
  ogDescription: 'Learn about how Innovayse uses cookies on our website and how you can manage your preferences.',
  ogType: 'website',

  // Twitter Card
  twitterCard: 'summary',
  twitterTitle: 'Cookie Policy | Innovayse',
  twitterDescription: 'Understand how we use cookies on our website'
})
</script>
