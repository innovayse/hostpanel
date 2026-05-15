<template>
  <footer class="relative mt-20 overflow-hidden bg-[#050508]">
    <!-- Background Decor -->
    <div class="absolute inset-0 pointer-events-none overflow-hidden">
      <div class="absolute -top-24 -left-24 w-96 h-96 bg-primary-600/10 rounded-full blur-[120px]" />
      <div class="absolute -bottom-24 -right-24 w-96 h-96 bg-secondary-600/10 rounded-full blur-[120px]" />
      <div class="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-white/10 to-transparent" />
    </div>

    <div class="container-custom relative z-10 pt-20 pb-10">
      <!-- Main Footer Content -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-12 lg:gap-8 mb-16">
        <!-- Column 1: Logo & Vision -->
        <div>
          <div class="flex items-center mb-8 group cursor-pointer">
            <NuxtImg
              src="/logo.png"
              alt="Innovayse"
              width="240"
              height="80"
              format="webp"
              quality="90"
              loading="lazy"
              class="h-12 md:h-14 w-auto transition-transform duration-500 group-hover:scale-105"
            />
          </div>
          <p class="text-gray-400 text-base leading-relaxed mb-10 max-w-sm font-light">
            {{ $t('footer.description') }}
          </p>
          
          <!-- Premium Social icons with Magnetic effect -->
          <div class="flex items-center space-x-5">
            <UiMagnetic v-for="social in socialLinks" :key="social.name" :strength="0.4">
              <a
                :href="social.href"
                target="_blank"
                rel="noopener noreferrer"
                class="w-12 h-12 rounded-xl bg-white/[0.03] border border-white/5 flex items-center justify-center text-gray-400 hover:text-white hover:bg-primary-500/20 hover:border-primary-500/50 transition-all duration-300 group/social"
                :aria-label="social.name"
                :title="social.name"
              >
                <Icon :name="social.icon" class="text-2xl transition-transform group-hover/social:scale-110" />
              </a>
            </UiMagnetic>
          </div>
        </div>

        <!-- Column 2: Navigation -->
        <div>
        <h3 class="text-white text-lg font-bold mb-8 tracking-tight flex items-center gap-3">
          <div class="w-2 h-2 rounded-full bg-primary-500 shadow-[0_0_10px_rgba(14,165,233,0.5)]" />
          {{ $t('footer.columns.navigation') }}
        </h3>
          <ul class="grid grid-cols-1 gap-4">
            <li v-for="link in navigationLinks" :key="link.href">
              <NuxtLink
                :to="link.href"
                class="text-gray-400 text-sm hover:text-white transition-all duration-300 flex items-center group"
              >
                <span class="w-0 group-hover:w-4 h-px bg-primary-500 mr-0 group-hover:mr-3 transition-all duration-300 opacity-0 group-hover:opacity-100" />
                {{ link.label }}
              </NuxtLink>
            </li>
          </ul>
        </div>

        <!-- Column 3: Services -->
        <div>
        <h3 class="text-white text-lg font-bold mb-8 tracking-tight flex items-center gap-3">
          <div class="w-2 h-2 rounded-full bg-secondary-500 shadow-[0_0_10px_rgba(139,92,246,0.5)]" />
          {{ $t('footer.columns.services') }}
        </h3>
          <ul class="space-y-4">
            <li v-for="service in serviceLinks" :key="service.href">
              <NuxtLink
                :to="service.href"
                class="text-gray-400 text-sm hover:text-white transition-all duration-300 flex items-center group"
              >
                <span class="w-0 group-hover:w-4 h-px bg-secondary-500 mr-0 group-hover:mr-3 transition-all duration-300 opacity-0 group-hover:opacity-100" />
                {{ service.label }}
              </NuxtLink>
            </li>
          </ul>
        </div>

        <!-- Column 4: Contact & Back to Top -->
        <div class="flex flex-col">
          <h3 class="text-white text-lg font-bold mb-8 tracking-tight flex items-center gap-3">
            <div class="w-2 h-2 rounded-full bg-cyan-500 shadow-[0_0_10px_rgba(6,182,212,0.5)]" />
            {{ $t('footer.columns.contact') }}
          </h3>
          <ul class="space-y-6">
            <li class="group">
              <a href="mailto:contact@innovayse.com" class="flex items-center gap-4 text-gray-400 hover:text-white transition-all duration-300">
                <div class="w-10 h-10 rounded-lg bg-white/[0.03] border border-white/5 flex items-center justify-center group-hover:border-primary-500/30 group-hover:bg-primary-500/10">
                  <Mail :size="18" class="text-primary-400 transition-transform group-hover:scale-110" />
                </div>
                <span class="text-sm">contact@innovayse.com</span>
              </a>
            </li>
            <li class="group">
              <a href="tel:+37433731673" class="flex items-center gap-4 text-gray-400 hover:text-white transition-all duration-300">
                <div class="w-10 h-10 rounded-lg bg-white/[0.03] border border-white/5 flex items-center justify-center group-hover:border-secondary-500/30 group-hover:bg-secondary-500/10">
                  <Phone :size="18" class="text-secondary-400 transition-transform group-hover:scale-110" />
                </div>
                <span class="text-sm">+374 33 73 16 73</span>
              </a>
            </li>
            <li class="flex items-center gap-4 text-gray-400 group">
              <div class="w-10 h-10 rounded-lg bg-white/[0.03] border border-white/5 flex items-center justify-center group-hover:border-cyan-500/30 group-hover:bg-cyan-500/10">
                <MapPin :size="18" class="text-cyan-400 transition-transform group-hover:scale-110" />
              </div>
              <span class="text-sm">Yerevan, Armenia</span>
            </li>
          </ul>

          <!-- Back to top button -->
          <div class="mt-12 flex justify-end">
            <UiMagnetic :strength="0.5">
              <button
                @click="scrollToTop"
                class="w-14 h-14 rounded-full bg-white/[0.03] border border-white/10 flex items-center justify-center text-gray-400 hover:text-white hover:bg-white/[0.08] hover:border-white/20 transition-all shadow-[0_0_30px_rgba(0,0,0,0.5)] group"
              >
                <ChevronUp :size="24" class="transition-transform group-hover:-translate-y-1" />
              </button>
            </UiMagnetic>
          </div>
        </div>
      </div>

      <!-- Bottom bar Glass Layer -->
      <div 
        class="relative p-8 rounded-[2rem] bg-white/[0.02] border border-white/[0.05] backdrop-blur-xl flex flex-col md:flex-row items-center justify-between gap-6"
      >
        <p class="text-sm text-gray-500 font-medium">
          © {{ currentYear }} <span class="text-white">Innovayse</span>. {{ $t('footer.bottom.rights') }}
        </p>
        
        <div class="flex items-center flex-wrap justify-center gap-6">
          <NuxtLink 
            v-for="sublink in ['privacy', 'cookies', 'terms', 'refund']" 
            :key="sublink"
            :to="localePath(`/${sublink.replace('cookies', 'cookie-policy').replace('refund', 'refund-policy')}`)" 
            class="text-sm text-gray-500 hover:text-primary-400 transition-colors duration-300 font-medium"
          >
            {{ $t(`footer.bottom.${sublink}`) }}
          </NuxtLink>
        </div>

        <!-- Trusted Badge -->
        <div class="flex items-center gap-3 px-4 py-2 rounded-full bg-white/[0.03] border border-white/5">
          <div class="w-2 h-2 rounded-full bg-green-500 animate-pulse" />
          <span class="text-xs text-gray-400 uppercase tracking-widest font-bold">{{ $t('footer.bottom.secure') }}</span>
        </div>
      </div>
    </div>
  </footer>
</template>

<script setup lang="ts">
/**
 * Footer component with 4 columns:
 * - Logo & description with social icons
 * - Navigation links
 * - Services links
 * - Contact information
 */

import { Mail, Phone, MapPin, ChevronUp } from 'lucide-vue-next'

const { t } = useI18n()
const localePath = useLocalePath()

/** Scroll to top function */
const scrollToTop = () => {
  window.scrollTo({
    top: 0,
    behavior: 'smooth'
  })
}

/** Navigation links */
const navigationLinks = computed(() => [
  { label: t('footer.nav.home'), href: localePath('/') },
  { label: t('footer.nav.about'), href: localePath('/about') },
  { label: t('footer.nav.portfolio'), href: localePath('/portfolio') },
  { label: t('footer.nav.blog'), href: localePath('/blog') },
  { label: t('footer.nav.announcements'), href: localePath('/announcements') },
  { label: t('footer.nav.knowledgebase'), href: localePath('/knowledgebase') },
  { label: t('footer.nav.faq'), href: localePath('/faq') },
  { label: t('footer.nav.contact'), href: localePath('/contact') }
])

/** Service links */
const serviceLinks = computed(() => [
  { label: t('footer.services.webDevelopment'), href: localePath('/services#development') },
  { label: t('footer.services.seoServices'), href: localePath('/services#seo') },
  { label: t('footer.services.ppcAdvertising'), href: localePath('/services#ppc') },
  { label: t('footer.services.saasProducts'), href: localePath('/products') }
])

/** Social media links */
const socialLinks = [
  { name: 'Twitter', href: 'https://twitter.com/innovayse', icon: 'mdi:twitter' },
  { name: 'LinkedIn', href: 'https://linkedin.com/company/innovayse', icon: 'mdi:linkedin' },
  { name: 'Facebook', href: 'https://facebook.com/innovayse', icon: 'mdi:facebook' }
]

/** Current year for copyright */
const currentYear = new Date().getFullYear()
</script>
