<template>
  <UiStack gap="none" data-testid="contact-page">
    <!-- Hero Section -->
    <UiPageHero
      :badge="t('contact.hero.badge')"
      :title="t('contact.hero.title')"
      :highlight="t('contact.hero.titleHighlight')"
      :subtitle="t('contact.hero.subtitle')"
    />

    <!-- Contact Section -->
    <UiSection padding="md">
      <!-- The info column comes first in the DOM so it leads on phones; `reverse` puts the form on the left from `lg` up. -->
      <UiSplit reverse gap="xl" class="w-full">
        <!-- Right: Contact Info -->
        <template #start>
          <UiStack
            gap="lg"
            class="w-full min-w-0 animate-reveal-up motion-reduce:animate-none [.tpl-classic_&]:animate-none"
          >
            <!-- Office Info Card -->
            <UiCard v-if="address" hover data-testid="contact-office">
              <UiStack direction="row" align="start" gap="md">
                <UiTile
                  :icon="Building2"
                  size="xl"
                  class="border border-primary-500/20 bg-primary-500/10 text-primary-700 dark:text-primary-300 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand"
                />
                <UiStack gap="none">
                  <UiHeading :level="3" class="mb-1">
                    {{ t('contact.info.officeAddress.title') }}
                  </UiHeading>
                  <UiText size="sm" tone="muted" class="whitespace-pre-line leading-relaxed" data-testid="contact-address">
                    {{ address }}
                  </UiText>
                </UiStack>
              </UiStack>
            </UiCard>

            <!-- Contact Methods; the whole card goes when the operator set none of its rows. -->
            <UiCard v-if="contactEmail || contactPhone || workingHours" data-testid="contact-methods">
              <UiHeading :level="3" class="mb-4">
                {{ t('contact.info.contactMethods.title') }}
              </UiHeading>

              <UiStack gap="sm">
                <!-- Email -->
                <UiLink
                  v-if="contactEmail"
                  :href="`mailto:${contactEmail}`"
                  tone="inherit"
                  :underline="false"
                  class="group flex items-center gap-3 rounded-lg p-3 hover:bg-gray-50 dark:hover:bg-white/5 hover:[.tpl-classic_&]:bg-classic-surface-2"
                  testid="contact-email-link"
                >
                  <UiTile
                    :icon="Mail"
                    size="xl"
                    class="bg-primary-500/20 text-primary-700 dark:text-primary-300 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand"
                  />
                  <UiStack gap="none">
                    <UiText :as="UiTextTag.Span" size="xs" tone="muted">
                      {{ t('contact.info.contactMethods.email') }}
                    </UiText>
                    <UiText
                      :as="UiTextTag.Span"
                      size="sm"
                      weight="semibold"
                      class="transition-colors group-hover:text-primary-700 dark:group-hover:text-primary-300 group-hover:[.tpl-classic_&]:text-classic-brand"
                    >
                      {{ contactEmail }}
                    </UiText>
                  </UiStack>
                </UiLink>

                <!-- Phone -->
                <UiLink
                  v-if="contactPhone"
                  :href="`tel:${contactPhoneLink}`"
                  tone="inherit"
                  :underline="false"
                  class="group flex items-center gap-3 rounded-lg p-3 hover:bg-gray-50 dark:hover:bg-white/5 hover:[.tpl-classic_&]:bg-classic-surface-2"
                  testid="contact-phone-link"
                >
                  <UiTile
                    :icon="Phone"
                    size="xl"
                    class="bg-secondary-500/20 text-secondary-700 dark:text-secondary-400 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand"
                  />
                  <UiStack gap="none">
                    <UiText :as="UiTextTag.Span" size="xs" tone="muted">
                      {{ t('contact.info.contactMethods.phone') }}
                    </UiText>
                    <UiText
                      :as="UiTextTag.Span"
                      size="sm"
                      weight="semibold"
                      class="transition-colors group-hover:text-secondary-700 dark:group-hover:text-secondary-300 group-hover:[.tpl-classic_&]:text-classic-brand"
                    >
                      {{ contactPhone }}
                    </UiText>
                  </UiStack>
                </UiLink>

                <!-- Working Hours -->
                <UiStack
                  v-if="workingHours"
                  direction="row"
                  align="center"
                  gap="sm"
                  class="p-3"
                  data-testid="contact-hours"
                >
                  <UiTile
                    :icon="Clock"
                    size="xl"
                    class="bg-cyan-500/20 text-cyan-700 dark:text-cyan-400 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand"
                  />
                  <UiStack gap="none">
                    <UiText :as="UiTextTag.Span" size="xs" tone="muted">
                      {{ t('contact.info.contactMethods.workingHours') }}
                    </UiText>
                    <UiText :as="UiTextTag.Span" size="sm" weight="semibold">
                      {{ workingHours }}
                    </UiText>
                  </UiStack>
                </UiStack>
              </UiStack>
            </UiCard>

            <!-- Social/Messengers -->
            <UiCard v-if="telegramUrl || whatsappUrl">
              <UiHeading :level="3" class="mb-4">
                {{ t('contact.info.quickConnect.title') }}
              </UiHeading>

              <UiGrid cols="2" gap="sm" class="grid-cols-2">
                <UiLink
                  v-if="telegramUrl"
                  :href="telegramUrl"
                  external
                  tone="inherit"
                  :underline="false"
                  class="group flex items-center justify-center gap-2 rounded-xl border border-cyan-500/30 bg-cyan-500/10 p-4 hover:border-cyan-500/50 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2 hover:[.tpl-classic_&]:border-classic-brand"
                  testid="contact-telegram"
                >
                  <Send :size="24" :stroke-width="2" class="text-cyan-700 dark:text-cyan-400 [.tpl-classic_&]:text-classic-brand" />
                  <UiText :as="UiTextTag.Span" size="sm" weight="semibold">
                    {{ t('contact.info.quickConnect.telegram') }}
                  </UiText>
                </UiLink>

                <UiLink
                  v-if="whatsappUrl"
                  :href="whatsappUrl"
                  external
                  tone="inherit"
                  :underline="false"
                  class="group flex items-center justify-center gap-2 rounded-xl border border-green-500/30 bg-green-500/10 p-4 hover:border-green-500/50 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2 hover:[.tpl-classic_&]:border-classic-brand"
                  testid="contact-whatsapp"
                >
                  <MessageCircle :size="24" :stroke-width="2" class="text-green-600 dark:text-green-400 [.tpl-classic_&]:text-classic-brand" />
                  <UiText :as="UiTextTag.Span" size="sm" weight="semibold">
                    {{ t('contact.info.quickConnect.whatsapp') }}
                  </UiText>
                </UiLink>
              </UiGrid>
            </UiCard>

            <!-- Map widget -->
            <UiEmbed v-if="mapSrc" :src="mapSrc" :title="t('contact.map.title')" data-testid="contact-map" />
          </UiStack>
        </template>

        <!-- Left: Contact Form -->
        <template #end>
          <UiCard
            padding="xl"
            class="relative overflow-hidden animate-reveal-up motion-reduce:animate-none [.tpl-classic_&]:animate-none [.tpl-classic_&]:rounded-lg"
          >
            <UiHeading :level="2" class="mb-6">
              {{ t('contact.form.title') }}
            </UiHeading>

            <UiForm spacing="lg" data-testid="contact-form" @submit="handleSubmit">
              <!-- Name -->
              <UiInput
                v-model="formData.name"
                data-testid="contact-name"
                :label="t('contact.form.name')"
                :placeholder="t('contact.form.placeholders.name')"
                :error="fieldErrors.name"
                icon="mdi:account"
                required
              />

              <!-- Email -->
              <UiInput
                v-model="formData.email"
                data-testid="contact-email"
                type="email"
                :label="t('contact.form.email')"
                :placeholder="t('contact.form.placeholders.email')"
                :error="fieldErrors.email"
                icon="mdi:email"
                required
              />

              <!-- Phone -->
              <UiInput
                v-model="formData.phone"
                data-testid="contact-phone"
                type="tel"
                :label="t('contact.form.phone')"
                :placeholder="t('contact.form.placeholders.phone')"
                :error="fieldErrors.phone"
                icon="mdi:phone"
              />

              <!-- Service Type -->
              <UiDropdown
                v-model="formData.serviceType"
                data-testid="contact-service"
                :label="t('contact.form.service')"
                :placeholder="t('contact.form.placeholders.service')"
                :options="serviceOptions"
                :error="fieldErrors.service"
                icon="mdi:format-list-bulleted"
                required
              />

              <!-- Message -->
              <UiTextarea
                v-model="formData.message"
                data-testid="contact-message"
                :label="t('contact.form.message')"
                :placeholder="t('contact.form.placeholders.message')"
                :error="fieldErrors.message"
                :rows="6"
                required
              />

              <!-- Consent -->
              <UiCheckbox
                v-model="formData.consent"
                data-testid="contact-consent"
                required
              >
                <UiText :as="UiTextTag.Span" size="sm" tone="muted">
                  {{ t('contact.form.consentPrefix') }}
                  <UiLink tone="primary" :to="localePath(SITE_PRIVACY_PATH)" :underline="false" testid="contact-privacy-link">
                    {{ t('contact.form.consentLink') }}
                  </UiLink>
                  {{ t('contact.form.consentSuffix') }}
                </UiText>
              </UiCheckbox>

              <!-- Submit Button -->
              <UiButton
                type="submit"
                :title="t('contact.form.submit')"
                :aria-label="t('contact.form.submit')"
                data-testid="contact-submit"
                variant="primary"
                size="lg"
                full-width
                :loading="contactLoading"
                class="hover:shadow-xl hover:shadow-primary-500/50 hover:[.tpl-classic_&]:shadow-none"
              >
                <Send v-if="!contactLoading" :size="18" :stroke-width="2" class="mr-2" />
                {{ t('common.send') }}
              </UiButton>

              <!-- Failure Message. Rendered from the API's answer, never from a sentence
                   written here: see handleSubmit. -->
              <Transition
                enter-active-class="transition duration-300 ease-out"
                leave-active-class="transition duration-300 ease-in"
                enter-from-class="opacity-0 -translate-y-2.5"
                leave-to-class="opacity-0 -translate-y-2.5"
              >
                <UiAlert v-if="errorMessage" variant="error" data-testid="contact-error">
                  {{ errorMessage }}
                </UiAlert>
              </Transition>

              <!-- Success Message -->
              <Transition
                enter-active-class="transition duration-300 ease-out"
                leave-active-class="transition duration-300 ease-in"
                enter-from-class="opacity-0 -translate-y-2.5"
                leave-to-class="opacity-0 -translate-y-2.5"
              >
                <UiAlert
                  v-if="showSuccess"
                  variant="success"
                  :title="t('contact.form.success.title')"
                  data-testid="contact-success"
                >
                  {{ t('contact.form.success.subtitle') }}
                </UiAlert>
              </Transition>
            </UiForm>
          </UiCard>
        </template>
      </UiSplit>
    </UiSection>

    <!-- FAQ Preview -->
    <SectionsFAQ :limit="4" :show-categories="false" />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * Contact page with form and contact information
 */

import { Send, Building2, Mail, Phone, Clock, MessageCircle } from 'lucide-vue-next'
import { ColorMode } from '~/types/colormode'
import { APP_LOCALES } from '~/constants/appLocales'
import { SCHEMA_ORG_CONTEXT } from '~/constants/schemaOrg'
import { SUCCESS_BANNER_MS } from '~/constants/uiDefaults'
import { useSupportStore } from '~/stores/support'
import { ContactService } from '~/types/contactservice'
import { SubmitOutcome } from '~/types/submitoutcome'
import { SeoPageType } from '~/types/seopagetype'
import { SITE_CONTACT_PATH, SITE_PRIVACY_PATH } from '~/constants/siteRoutes'
import { UiTextTag } from '~/utils/ui/enums'

/** Operator-set working hours with their time zone; empty when unset. */
const { workingHours } = useContactHours()

/** Translates a key in the active language. */
const { t } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()

/** Localised path builder from the i18n module. */
const localePath = useLocalePath()

// SEO setup with canonical, hreflang, OG, Twitter tags
/** Sets the canonical, hreflang and social tags of the page; `baseUrl` is the site origin. */
const { baseUrl } = useSeo({
  title: t('seo.contact.title', { site: siteName.value }),
  description: t('seo.contact.description', { site: siteName.value }),
  keywords: t('seo.contact.keywords', { site: siteName.value }),
  type: SeoPageType.Website,
  path: SITE_CONTACT_PATH
})

// Schema.org
/** Builders and the publisher of the schema.org structured data. */
const { organizationSchema, injectSchema } = useSchemaOrg()
injectSchema([
  organizationSchema(),
  {
    '@context': SCHEMA_ORG_CONTEXT,
    '@type': 'ContactPage',
    '@id': `${baseUrl}/contact#contactpage`,
    url: `${baseUrl}/contact`,
    name: t('seo.contact.title'),
    description: t('seo.contact.description', { site: siteName.value }),
    inLanguage: [...APP_LOCALES],
    mainEntity: { '@id': `${baseUrl}/#organization` }
  }
])

/** The operator's contact details; each row is hidden when its setting is empty. */
const {
  email: contactEmail,
  phone: contactPhone,
  phoneLink: contactPhoneLink,
  telegramUrl,
  whatsappUrl,
  address,
  mapUrl,
} = useContactDetails()

/** Whether the page is in dark mode; flips when the visitor toggles the colour mode. */
const { isDark } = useAppColorMode()

/** The operator's map address with the theme that follows the colour mode; empty hides the map. */
const mapSrc = computed(() => {
  if (!mapUrl.value) {
    return ''
  }
  const url = new URL(mapUrl.value)
  url.searchParams.set('theme', isDark.value ? ColorMode.Dark : ColorMode.Light)
  return url.toString()
})

/** What the visitor typed into the contact form. */
interface ContactFormState {
  /** Full name. */
  name: string
  /** Reply address. */
  email: string
  /** Optional phone number. */
  phone: string
  /** The service the enquiry is about (a {@link ContactService} value); empty until one is picked. */
  serviceType: string
  /** The message body. */
  message: string
  /** Whether the privacy policy box is ticked. */
  consent: boolean
}

/**
 * Builds an empty form.
 *
 * @returns A form with every field blank and the consent box unticked.
 */
const emptyForm = (): ContactFormState => ({
  name: '',
  email: '',
  phone: '',
  serviceType: '',
  message: '',
  consent: false
})

/** The form's current values. */
const formData = ref<ContactFormState>(emptyForm())

/** Locale key that labels each service. */
const SERVICE_LABEL_KEYS: Record<ContactService, string> = {
  [ContactService.Hosting]: 'contact.form.serviceOptions.hosting',
  [ContactService.Domains]: 'contact.form.serviceOptions.domains',
  [ContactService.Servers]: 'contact.form.serviceOptions.servers',
  [ContactService.Billing]: 'contact.form.serviceOptions.billing',
  [ContactService.Support]: 'contact.form.serviceOptions.support',
  [ContactService.Other]: 'contact.form.serviceOptions.other'
}

/** The dropdown's choices, labelled in the active language. */
const serviceOptions = computed(() => Object.values(ContactService).map(service => ({
  value: service,
  label: t(SERVICE_LABEL_KEYS[service])
})))

/** Submission state of the contact form; the store owns the loading flag, the refusal and its field messages. */
const { contactLoading, contactError, fieldErrors } = storeToRefs(useSupportStore())
/** Sends the contact form through the support store. */
const { sendContactMessage } = useSupportStore()

/** True for a few seconds after the message was accepted. */
const showSuccess = ref(false)

/** A client-side refusal (missing consent); the API's own refusal lives in the store. */
const localError = ref('')

/** The failure currently shown under the form, or `''` when the last attempt did not fail. */
const errorMessage = computed(() => localError.value || contactError.value)

/**
 * Submits the contact form.
 *
 * The success state is set **only** after the POST resolves. The route behind it answers 204
 * once the backend's SMTP relay has accepted the message and rejects otherwise, so the visitor
 * is never told a message was sent that was not.
 *
 * Failure wording comes from the response, whole. The API resolves it from its own resources
 * in the language `X-Language` asked for, so CONTACT_SEND_FAILED and CONTACT_NOT_CONFIGURED
 * arrive in ru or hy like every other refusal.
 * The recovery action still differs between them, and that difference is carried by the wording
 * the API chose: the unconfigured-inbox sentence sends the visitor to the phone number and email
 * beside the form rather than inviting a retry. No sentence is written here.
 */
const handleSubmit = async (): Promise<void> => {
  localError.value = ''

  // A guard before any request is possible, which `api-driven-frontend.md` allows as one of its
  // two exceptions. The consent box is not part of the endpoint's contract, so there is no
  // server-side rule whose wording this could borrow.
  if (!formData.value.consent) {
    localError.value = t('contact.form.error.consentRequired')
    return
  }

  const outcome = await sendContactMessage({
    name: formData.value.name,
    email: formData.value.email,
    phone: formData.value.phone,
    service: formData.value.serviceType,
    message: formData.value.message,
    timestamp: new Date().toLocaleString()
  })

  if (outcome === SubmitOutcome.Sent) {
    showSuccess.value = true
    formData.value = emptyForm()

    // Hide the success message once the banner time has passed
    setTimeout(() => {
      showSuccess.value = false
    }, SUCCESS_BANNER_MS)
  }
}
</script>
