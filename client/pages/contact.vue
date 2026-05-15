<template>
  <div>
    <!-- Hero Section -->
    <section class="relative py-12 md:py-32 bg-[#0a0a0f] overflow-hidden">
      <!-- Background -->
      <div class="absolute inset-0 pointer-events-none">
        <div class="absolute inset-0 bg-gradient-to-br from-primary-500/10 via-transparent to-secondary-500/10" />
        <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/20 rounded-full blur-[150px] animate-blob" />
        <div class="absolute bottom-0 right-1/4 w-[400px] h-[400px] bg-secondary-500/20 rounded-full blur-[140px] animate-blob animation-delay-2000" />

        <!-- Grid pattern -->
        <div class="absolute inset-0 opacity-[0.02]">
          <div style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 60px 60px;" class="w-full h-full" />
        </div>

        <!-- Floating particles -->
        <div class="absolute top-1/4 left-1/3 w-2 h-2 bg-primary-400/70 rounded-full animate-float" style="animation-delay: 0.5s;" />
        <div class="absolute bottom-1/3 right-1/4 w-3 h-3 bg-secondary-400/60 rounded-full animate-float" style="animation-delay: 1.5s;" />
      </div>

      <div class="container-custom relative z-10 text-center">
        <span
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :enter="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          class="inline-block px-4 py-1.5 bg-primary-500/20 text-primary-400 text-sm font-medium rounded-full mb-6"
        >
          {{ $t('contact.hero.badge') }}
        </span>
        <h1 class="text-4xl md:text-6xl lg:text-7xl font-bold text-white mb-6 leading-tight">
          {{ $t('contact.hero.title') }}
          <span class="bg-gradient-to-r from-primary-400 to-secondary-400 bg-clip-text text-transparent animate-gradient bg-300">
            {{ $t('contact.hero.titleHighlight') }}
          </span>
        </h1>
        <p class="text-lg md:text-xl text-gray-400 max-w-3xl mx-auto">
          {{ $t('contact.hero.subtitle') }}
        </p>
      </div>

      <!-- Corner decorations -->
      <div class="absolute top-0 right-0 w-48 h-48 border-r-2 border-t-2 border-secondary-500/30 pointer-events-none" />
      <div class="absolute bottom-0 left-0 w-48 h-48 border-l-2 border-b-2 border-primary-500/30 pointer-events-none" />
    </section>

    <!-- Contact Section -->
    <section class="py-8 md:py-20 bg-[#0a0a0f] relative overflow-hidden">
      <div class="container-custom">
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 sm:gap-8 md:gap-12 max-w-6xl mx-auto w-full">
          <!-- Left: Contact Form -->
          <div
            v-motion
            :initial="{ opacity: 0, x: -40 }"
            :visibleOnce="{ opacity: 1, x: 0, transition: { duration: 600 } }"
            class="order-2 lg:order-1"
          >
            <div class="relative p-8 rounded-2xl bg-gradient-to-br from-white/5 to-white/[0.02] border border-white/10 backdrop-blur-sm">
              <!-- Decorative elements -->
              <div class="absolute top-0 right-0 w-48 h-48 bg-secondary-500/10 rounded-full blur-3xl" />
              <div class="absolute bottom-0 left-0 w-32 h-32 bg-primary-500/10 rounded-full blur-2xl" />

              <div class="relative">
                <h2 class="text-2xl font-bold text-white mb-6">{{ $t('contact.form.title') }}</h2>

                <UiForm spacing="lg" @submit="handleSubmit">
                  <!-- Name -->
                  <UiInput
                    v-model="formData.name"
                    :label="$t('contact.form.name')"
                    :placeholder="$t('contact.form.placeholders.name')"
                    icon="mdi:account"
                    required
                  />

                  <!-- Email -->
                  <UiInput
                    v-model="formData.email"
                    type="email"
                    :label="$t('contact.form.email')"
                    :placeholder="$t('contact.form.placeholders.email')"
                    icon="mdi:email"
                    required
                  />

                  <!-- Phone -->
                  <UiInput
                    v-model="formData.phone"
                    type="tel"
                    :label="$t('contact.form.phone')"
                    :placeholder="$t('contact.form.placeholders.phone')"
                    icon="mdi:phone"
                  />

                  <!-- Service Type -->
                  <UiDropdown
                    v-model="formData.serviceType"
                    :label="$t('contact.form.service')"
                    :placeholder="$t('contact.form.placeholders.service')"
                    :options="serviceOptions"
                    icon="mdi:format-list-bulleted"
                    required
                  />

                  <!-- Message -->
                  <UiTextarea
                    v-model="formData.message"
                    :label="$t('contact.form.message')"
                    :placeholder="$t('contact.form.placeholders.message')"
                    :rows="6"
                    required
                  />

                  <!-- Consent -->
                  <UiCheckbox
                    v-model="formData.consent"
                    required
                  >
                    <span class="text-sm text-gray-400">
                      {{ $t('contact.form.consentPrefix') }}
                      <NuxtLink to="/privacy" class="text-primary-400 hover:underline">
                        {{ $t('contact.form.consentLink') }}
                      </NuxtLink>
                      {{ $t('contact.form.consentSuffix') }}
                    </span>
                  </UiCheckbox>

                  <!-- Submit Button -->
                  <UiButton
                    type="submit"
                    variant="primary"
                    size="lg"
                    full-width
                    :loading="isSubmitting"
                    class="hover:shadow-xl hover:shadow-primary-500/50"
                  >
                    <Send v-if="!isSubmitting" :size="18" :stroke-width="2" class="mr-2" />
                    {{ $t('contact.form.submit') }}
                  </UiButton>

                  <!-- Success Message -->
                  <Transition name="fade">
                    <div v-if="showSuccess" class="p-4 bg-green-500/10 border border-green-500/20 rounded-xl">
                      <div class="flex items-center gap-3">
                        <CheckCircle :size="24" :stroke-width="2" class="text-green-400" />
                        <div>
                          <p class="text-sm font-semibold text-green-400">{{ $t('contact.form.success.title') }}</p>
                          <p class="text-xs text-gray-400 mt-1">{{ $t('contact.form.success.subtitle') }}</p>
                        </div>
                      </div>
                    </div>
                  </Transition>
                </UiForm>
              </div>

              <!-- Corner accents -->
              <div class="absolute top-0 left-0 w-16 h-16 border-l-2 border-t-2 border-primary-500/40 rounded-tl-2xl" />
              <div class="absolute bottom-0 right-0 w-16 h-16 border-r-2 border-b-2 border-secondary-500/40 rounded-br-2xl" />
            </div>
          </div>

          <!-- Right: Contact Info -->
          <div
            v-motion
            :initial="{ opacity: 0, x: 40 }"
            :visibleOnce="{ opacity: 1, x: 0, transition: { duration: 600 } }"
            class="order-1 lg:order-2 space-y-4 sm:space-y-6 w-full min-w-0"
          >
            <!-- Office Info Card -->
            <div class="p-6 rounded-2xl bg-white/5 border border-white/10 hover:border-primary-500/30 transition-all duration-300 group">
              <div class="flex items-start gap-4 mb-6">
                <div class="w-12 h-12 rounded-xl bg-gradient-to-br from-primary-500/20 to-cyan-500/10 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <Building2 :size="24" :stroke-width="2" class="text-primary-400" />
                </div>
                <div>
                  <h3 class="text-lg font-bold text-white mb-1">{{ $t('contact.info.officeAddress.title') }}</h3>
                  <p class="text-sm text-gray-400 leading-relaxed">
                    {{ $t('contact.info.officeAddress.street') }}<br>
                    {{ $t('contact.info.officeAddress.city') }}
                  </p>
                </div>
              </div>
            </div>

            <!-- Contact Methods -->
            <div class="p-6 rounded-2xl bg-white/5 border border-white/10 space-y-4">
              <h3 class="text-lg font-bold text-white mb-4">{{ $t('contact.info.contactMethods.title') }}</h3>

              <!-- Email -->
              <a href="mailto:contact@innovayse.com" class="flex items-center gap-3 p-3 rounded-lg hover:bg-white/5 transition-colors group">
                <div class="w-10 h-10 rounded-lg bg-primary-500/20 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <Mail :size="20" :stroke-width="2" class="text-primary-400" />
                </div>
                <div>
                  <div class="text-xs text-gray-500">{{ $t('contact.info.contactMethods.email') }}</div>
                  <div class="text-sm font-semibold text-white group-hover:text-primary-300 transition-colors">contact@innovayse.com</div>
                </div>
              </a>

              <!-- Phone -->
              <a href="tel:+37433731673" class="flex items-center gap-3 p-3 rounded-lg hover:bg-white/5 transition-colors group">
                <div class="w-10 h-10 rounded-lg bg-secondary-500/20 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <Phone :size="20" :stroke-width="2" class="text-secondary-400" />
                </div>
                <div>
                  <div class="text-xs text-gray-500">{{ $t('contact.info.contactMethods.phone') }}</div>
                  <div class="text-sm font-semibold text-white group-hover:text-secondary-300 transition-colors">+374 33 73 16 73</div>
                </div>
              </a>

              <!-- Working Hours -->
              <div class="flex items-center gap-3 p-3 rounded-lg">
                <div class="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
                  <Clock :size="20" :stroke-width="2" class="text-cyan-400" />
                </div>
                <div>
                  <div class="text-xs text-gray-500">{{ $t('contact.info.contactMethods.workingHours') }}</div>
                  <div class="text-sm font-semibold text-white">{{ $t('contact.info.contactMethods.workingHoursValue') }}</div>
                </div>
              </div>
            </div>

            <!-- Social/Messengers -->
            <div class="p-6 rounded-2xl bg-white/5 border border-white/10">
              <h3 class="text-lg font-bold text-white mb-4">{{ $t('contact.info.quickConnect.title') }}</h3>

              <div class="grid grid-cols-2 gap-3">
                <a
                  href="https://t.me/innovayse"
                  target="_blank"
                  class="flex items-center justify-center gap-2 p-4 rounded-xl bg-gradient-to-br from-cyan-500/20 to-cyan-500/10 border border-cyan-500/30 hover:border-cyan-500/50 transition-all duration-300 hover:scale-105 group"
                >
                  <Send :size="24" :stroke-width="2" class="text-cyan-400 group-hover:scale-110 transition-transform" />
                  <span class="text-sm font-semibold text-white">{{ $t('contact.info.quickConnect.telegram') }}</span>
                </a>

                <a
                  href="https://wa.me/37433731673"
                  target="_blank"
                  class="flex items-center justify-center gap-2 p-4 rounded-xl bg-gradient-to-br from-green-500/20 to-green-500/10 border border-green-500/30 hover:border-green-500/50 transition-all duration-300 hover:scale-105 group"
                >
                  <MessageCircle :size="24" :stroke-width="2" class="text-green-400 group-hover:scale-110 transition-transform" />
                  <span class="text-sm font-semibold text-white">{{ $t('contact.info.quickConnect.whatsapp') }}</span>
                </a>
              </div>
            </div>

            <!-- Yandex Map Widget -->
            <div class="relative h-48 sm:h-56 md:h-64 rounded-xl sm:rounded-2xl overflow-hidden border border-white/10 w-full min-w-0">
              <iframe
                :src="mapUrl"
                width="100%"
                height="100%"
                frameborder="0"
                allowfullscreen
                class="w-full h-full"
              />
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- FAQ Preview -->
    <SectionsFAQ :limit="4" :show-categories="false" />
  </div>
</template>

<script setup lang="ts">
/**
 * Contact page with form and contact information
 */

import { $fetch } from 'ofetch'
import { Send, CheckCircle, Building2, Mail, Phone, Clock, MessageCircle } from 'lucide-vue-next'

const { t } = useI18n()

// SEO setup with canonical, hreflang, OG, Twitter tags
const { baseUrl } = useSeo({
  title: t('seo.contact.title'),
  description: t('seo.contact.description'),
  keywords: t('seo.contact.keywords'),
  type: 'website',
  path: '/contact'
})

// Schema.org
const { organizationSchema, injectSchema } = useSchemaOrg()
injectSchema([
  organizationSchema(),
  {
    '@context': 'https://schema.org',
    '@type': 'ContactPage',
    '@id': `${baseUrl}/contact#contactpage`,
    url: `${baseUrl}/contact`,
    name: 'Contact Innovayse',
    description: t('seo.contact.description'),
    inLanguage: ['en', 'ru', 'hy'],
    mainEntity: { '@id': `${baseUrl}/#organization` }
  }
])

// Yandex Map Widget URL
const latitude = 40.175837
const longitude = 44.512906
const theme = 'dark'

const mapUrl = computed(() => {
  return `https://yandex.com/map-widget/v1/?ll=${longitude},${latitude}&z=17&l=map&pt=${longitude},${latitude},pm2rdm&theme=${theme}`
})

// Form data
const formData = ref({
  name: '',
  email: '',
  phone: '',
  serviceType: '',
  message: '',
  consent: false
})

// Service options with i18n
const serviceOptions = computed(() => [
  { value: 'web-development', label: t('contact.form.serviceOptions.webDevelopment') },
  { value: 'mobile-development', label: t('contact.form.serviceOptions.mobileDevelopment') },
  { value: 'seo', label: t('contact.form.serviceOptions.seo') },
  { value: 'ppc', label: t('contact.form.serviceOptions.ppc') },
  { value: 'saas-product', label: t('contact.form.serviceOptions.saasProduct') },
  { value: 'consulting', label: t('contact.form.serviceOptions.consulting') },
  { value: 'other', label: t('contact.form.serviceOptions.other') }
])

const isSubmitting = ref(false)
const showSuccess = ref(false)

const handleSubmit = async () => {
  if (!formData.value.consent) {
    alert(t('contact.form.error.consentRequired'))
    return
  }

  isSubmitting.value = true

  try {
    await $fetch('/api/contact', {
      method: 'POST',
      body: {
        name: formData.value.name,
        email: formData.value.email,
        phone: formData.value.phone,
        service: formData.value.serviceType,
        message: formData.value.message,
        timestamp: new Date().toLocaleString()
      }
    })

    showSuccess.value = true
    formData.value = {
      name: '',
      email: '',
      phone: '',
      serviceType: '',
      message: '',
      consent: false
    }

    // Hide success message after 5 seconds
    setTimeout(() => {
      showSuccess.value = false
    }, 5000)
  } catch (error) {
    console.error('Failed to send message:', error)
    alert(t('contact.form.error.failed'))
  } finally {
    isSubmitting.value = false
  }
}

</script>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: all 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}
</style>
