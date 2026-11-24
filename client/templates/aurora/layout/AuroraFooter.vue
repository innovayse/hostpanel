<template>
  <!--
    pb-24 clears the fixed contact button, which otherwise sits on top of the
    bottom row once the page is scrolled to the end.
  -->
  <UiBlock :as="UiBlockTag.Footer" class="relative border-t border-line2 bg-[color:var(--footer)] shadow-[0_-12px_32px_-20px_rgba(20,22,42,0.22)] pb-24 pt-[clamp(40px,6vw,56px)] font-aurora">
    <UiContainer>
      <UiGrid cols="auto" gap="xl">
        <UiStack gap="none" class="min-w-0">
          <UiStack direction="row" align="center" gap="sm" class="text-tx">
            <!-- Logo image cap: intrinsically small UI, not page layout. -->
            <LogoLockup img-class="h-[34px] w-auto max-w-[140px]" mark-class="h-9 w-9" name-class="text-lg font-bold" />
          </UiStack>
          <UiText tone="inherit" class="mt-4 leading-relaxed text-mut2">
            {{ t('aurora.footer.tagline') }}
          </UiText>

          <!-- Rendered only when an external subscribe endpoint is configured. -->
          <UiForm
            v-if="newsletterUrl"
            native-submit
            spacing="none"
            :action="newsletterUrl"
            method="post"
            target="_blank"
            data-testid="newsletter-form"
            class="mt-[22px]"
          >
            <UiStack direction="row" wrap align="end" gap="xs">
              <UiInput
                id="aurora-newsletter"
                v-model="emailInput"
                name="email"
                type="email"
                required
                testid="newsletter-email"
                :label="t('aurora.footer.newsletterTitle')"
                :placeholder="t('aurora.footer.newsletterPlaceholder')"
                class="min-w-[180px] flex-1"
                @blur="emailTouched = true"
              />
              <UiButton
                type="submit"
                variant="bare"
                data-testid="newsletter-submit"
                class="whitespace-nowrap rounded-xl bg-brand px-[18px] py-3 text-sm font-bold text-on-brand hover:brightness-110"
              >
                {{ t('aurora.footer.newsletterCta') }}
              </UiButton>
            </UiStack>
            <UiText v-if="emailInvalid" size="sm" tone="danger" class="mt-2.5 min-h-[18px]" data-testid="newsletter-invalid">
              {{ t('aurora.footer.newsletterInvalid') }}
            </UiText>
          </UiForm>

          <!-- Only the profiles an operator has actually configured. -->
          <UiStack v-if="socials.length" direction="row" gap="xs" class="mt-[18px]">
            <UiButton
              v-for="social in socials"
              :key="social.tag"
              variant="bare"
              :href="social.url"
              :title="social.name"
              :aria-label="social.name"
              target="_blank"
              rel="noopener noreferrer"
              :data-testid="`footer-social-${social.tag}`"
              class="h-9 w-9 rounded-xl border border-line2 text-[13px] font-bold text-mut hover:border-ac1 hover:text-ac1"
            >
              {{ social.tag }}
            </UiButton>
          </UiStack>
        </UiStack>

        <UiStack v-for="col in columns" :key="col.title" gap="none" class="min-w-0">
          <UiHeading :level="3" size="bare" class="text-[13px] font-normal uppercase tracking-[0.1em] text-ac1">
            {{ col.title }}
          </UiHeading>
          <UiStack gap="sm" class="mt-[18px]">
            <UiFooterLink
              v-for="link in col.links"
              :key="link.label"
              testid="aurora-footer-link"
              :to="link.to"
              :variant="UiFooterLinkVariant.Aurora"
              class="text-[15px]"
            >
              {{ link.label }}
            </UiFooterLink>
          </UiStack>
        </UiStack>
      </UiGrid>

      <UiStack
        direction="row"
        wrap
        align="center"
        justify="between"
        class="mt-11 border-t border-line pt-6"
      >
        <UiText :as="UiTextTag.Span" size="sm" tone="inherit" class="text-mut2">© {{ year }} {{ siteName }}</UiText>
        <UiStack direction="row" wrap align="center" gap="none" class="gap-x-2.5 gap-y-1 text-sm text-mut2">
          <UiText v-if="taxId" :as="UiTextTag.Span" size="sm" tone="inherit">{{ t('aurora.footer.taxId') }} {{ taxId }}</UiText>
          <UiText v-if="taxId && (contactEmail || phone)" :as="UiTextTag.Span" size="sm" tone="inherit">·</UiText>
          <UiFooterLink v-if="contactEmail" testid="aurora-footer-email" :href="`mailto:${contactEmail}`" :variant="UiFooterLinkVariant.Aurora">
            {{ contactEmail }}
          </UiFooterLink>
          <UiText v-if="contactEmail && phone" :as="UiTextTag.Span" size="sm" tone="inherit">·</UiText>
          <UiFooterLink v-if="phone" testid="aurora-footer-phone" :href="`tel:${phoneLink}`" :variant="UiFooterLinkVariant.Aurora">
            {{ phone }}
          </UiFooterLink>
        </UiStack>
      </UiStack>
    </UiContainer>
  </UiBlock>
</template>

<script setup lang="ts">
/**
 * aurora template site footer.
 *
 * The newsletter block posts to an external provider's form endpoint and is
 * omitted entirely when none is configured, so an unconfigured install never
 * shows a subscribe button that goes nowhere. Live chat is not duplicated here:
 * the layout already mounts UiFloatingActions.
 */
import LogoLockup from '~/components/LogoLockup.vue'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useContactDetails } from '~/composables/useContactDetails'
import { UiBlockTag, UiFooterLinkVariant, UiTextTag } from '~/utils/ui/enums'
import {
  SITE_ACCEPTABLE_USE_PATH,
  SITE_ABOUT_PATH,
  SITE_CONTACT_PATH,
  SITE_COOKIE_POLICY_PATH,
  SITE_DOMAINS_TRANSFER_PATH,
  SITE_FAQ_PATH,
  SITE_KNOWLEDGEBASE_PATH,
  SITE_PRIVACY_PATH,
  SITE_PRODUCTS_PATH,
  SITE_REFUND_POLICY_PATH,
  SITE_TERMS_PATH,
} from '~/constants/siteRoutes'

const { t } = useI18n()
/** Prefixes a path with the active locale. */
const localePath = useLocalePath()
/** Reads an operator setting by its key. */
const { get } = usePortalSettings()
/** The operator's site name, shown in the copyright line. */
const { name: siteName } = useSiteIdentity()

/** Subscribe endpoint of the newsletter provider; empty hides the block. */
const newsletterUrl = computed(() => get(PortalSettingKey.NewsletterActionUrl, 'portalNewsletterUrl'))
/** Public contact e-mail, phone (as written and as a dialable number) and Telegram link; each empty when unset. */
const { email: contactEmail, phone, phoneLink, telegramUrl } = useContactDetails()
/** Legal tax identifier; empty hides it. */
const taxId = computed(() => get(PortalSettingKey.LegalTaxId, 'portalLegalTaxId'))
/** Current year for the copyright line. */
const year = new Date().getFullYear()

/**
 * Social profiles the operator has configured. Each is omitted when unset, so an
 * install that fills in none renders no row at all rather than a line of dead
 * icons pointing at this project's own accounts.
 */
const socials = computed(() => [
  { tag: 'fb', name: 'Facebook', url: get(PortalSettingKey.SocialFacebook, 'portalSocialFacebook') },
  { tag: 'ig', name: 'Instagram', url: get(PortalSettingKey.SocialInstagram, 'portalSocialInstagram') },
  { tag: 'in', name: 'LinkedIn', url: get(PortalSettingKey.SocialLinkedin, 'portalSocialLinkedin') },
  { tag: 'tg', name: 'Telegram', url: telegramUrl.value },
  { tag: 'yt', name: 'YouTube', url: get(PortalSettingKey.SocialYoutube, 'portalSocialYoutube') },
].filter(entry => entry.url))

/** What the visitor typed in the newsletter field. */
const emailInput = ref('')

/** Whether the newsletter field has been left once, so a half-typed address is not nagged about. */
const emailTouched = ref(false)

/**
 * Whether to warn about the address. The form posts to an external provider, so
 * success cannot be confirmed here: only an obviously wrong address can be
 * caught before the visitor is sent away.
 */
const emailInvalid = computed(() =>
  emailTouched.value && emailInput.value.length > 0 && !emailInput.value.includes('@'))

/** Link columns, in display order. */
const columns = computed(() => [
  {
    title: t('aurora.footer.company'),
    // /about tells the operator's story, or sends the visitor on to contact when none is set.
    links: [
      { label: t('aurora.footer.about'), to: localePath(SITE_ABOUT_PATH) },
      { label: t('aurora.footer.services'), to: localePath(SITE_PRODUCTS_PATH) },
      { label: t('aurora.footer.knowledgebase'), to: localePath(SITE_KNOWLEDGEBASE_PATH) },
      { label: t('aurora.footer.contact'), to: localePath(SITE_CONTACT_PATH) },
    ],
  },
  {
    title: t('aurora.footer.support'),
    links: [
      { label: t('aurora.footer.helpCenter'), to: localePath(SITE_KNOWLEDGEBASE_PATH) },
      { label: t('aurora.footer.faq'), to: localePath(SITE_FAQ_PATH) },
      { label: t('aurora.footer.domainTransfer'), to: localePath(SITE_DOMAINS_TRANSFER_PATH) },
    ],
  },
  {
    title: t('aurora.footer.legal'),
    links: [
      { label: t('aurora.footer.terms'), to: localePath(SITE_TERMS_PATH) },
      { label: t('aurora.footer.privacy'), to: localePath(SITE_PRIVACY_PATH) },
      { label: t('aurora.footer.cookies'), to: localePath(SITE_COOKIE_POLICY_PATH) },
      { label: t('aurora.footer.refund'), to: localePath(SITE_REFUND_POLICY_PATH) },
      { label: t('aurora.footer.acceptableUse'), to: localePath(SITE_ACCEPTABLE_USE_PATH) },
    ],
  },
])
</script>
