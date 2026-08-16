<template>
  <!--
    pb-24 clears the fixed contact button, which otherwise sits on top of the
    bottom row once the page is scrolled to the end.
  -->
  <footer
    class="relative border-t border-line px-[clamp(20px,5vw,48px)] pb-24 pt-[clamp(40px,6vw,56px)] font-aurora"
  >
    <div class="grid gap-x-10 gap-y-11 [grid-template-columns:repeat(auto-fit,minmax(210px,1fr))]">
      <div class="min-w-0">
        <div class="flex items-center gap-3">
          <span
            class="grid h-[30px] w-[30px] place-items-center rounded-[9px] bg-brand text-[15px] font-extrabold text-[#08090F]"
          >i</span>
          <span class="text-[17px] font-bold text-tx">Innovayse</span>
        </div>
        <p class="mt-4 max-w-[300px] text-[15px] leading-relaxed text-mut2">
          {{ t('aurora.footer.tagline') }}
        </p>

        <!-- Rendered only when an external subscribe endpoint is configured. -->
        <form
          v-if="newsletterUrl"
          :action="newsletterUrl"
          method="post"
          target="_blank"
          class="mt-[22px] max-w-[340px]"
        >
          <label class="text-sm font-semibold text-tx2" for="aurora-newsletter">
            {{ t('aurora.footer.newsletterTitle') }}
          </label>
          <div class="mt-2.5 flex flex-wrap gap-2">
            <input
              id="aurora-newsletter"
              v-model="emailInput"
              type="email"
              name="email"
              required
              :placeholder="t('aurora.footer.newsletterPlaceholder')"
              class="min-w-[180px] flex-1 rounded-[10px] border border-line2 bg-input px-3.5 py-3 text-[15px] text-tx outline-none focus:border-ac1"
              @blur="emailTouched = true"
            >
            <button
              type="submit"
              class="whitespace-nowrap rounded-[10px] bg-brand px-[18px] py-3 text-sm font-bold text-[#08090F] hover:brightness-110"
            >{{ t('aurora.footer.newsletterCta') }}</button>
          </div>
          <p v-if="emailInvalid" class="mt-2.5 min-h-[18px] text-[13px] text-danger">
            {{ t('aurora.footer.newsletterInvalid') }}
          </p>
        </form>

        <!-- Only the profiles an operator has actually configured. -->
        <div v-if="socials.length" class="mt-[18px] flex gap-2.5">
          <a
            v-for="social in socials"
            :key="social.tag"
            :href="social.url"
            :title="social.name"
            target="_blank"
            rel="noopener noreferrer"
            class="grid h-9 w-9 place-items-center rounded-[10px] border border-line2 text-[13px] font-bold text-mut hover:border-ac1 hover:text-ac1"
          >{{ social.tag }}</a>
        </div>
      </div>

      <div v-for="col in columns" :key="col.title" class="min-w-0">
        <div class="text-[13px] uppercase tracking-[0.1em] text-ac1">{{ col.title }}</div>
        <div class="mt-[18px] flex flex-col gap-3">
          <NuxtLink
            v-for="link in col.links"
            :key="link.label"
            :to="link.to"
            class="text-[15px] text-mut hover:text-tx"
          >{{ link.label }}</NuxtLink>
        </div>
      </div>
    </div>

    <div class="mt-11 flex flex-wrap items-center justify-between gap-6 border-t border-line pt-6">
      <span class="text-sm text-mut2">© {{ year }} Innovayse</span>
      <div class="flex flex-wrap items-center gap-x-2.5 gap-y-1 text-sm text-mut2">
        <span v-if="taxId">{{ t('aurora.footer.taxId') }} {{ taxId }}</span>
        <span v-if="taxId && (contactEmail || phone)">·</span>
        <a v-if="contactEmail" :href="`mailto:${contactEmail}`" class="hover:text-tx">{{ contactEmail }}</a>
        <span v-if="contactEmail && phone">·</span>
        <a v-if="phone" :href="`tel:${phone.replace(/[^0-9+]/g, '')}`" class="hover:text-tx">{{ phone }}</a>
      </div>
    </div>
  </footer>
</template>

<script setup lang="ts">
/**
 * aurora template site footer.
 *
 * The newsletter block posts to an external provider's form endpoint and is
 * omitted entirely when none is configured, so an unconfigured install never
 * shows a subscribe button that goes nowhere. Live chat is not duplicated here —
 * the layout already mounts UiFloatingActions.
 */
const { t } = useI18n()
const localePath = useLocalePath()
const { get } = usePortalSettings()

const newsletterUrl = computed(() => get('portal.newsletter.action_url', 'portalNewsletterUrl'))
// Was a hard-coded support@innovayse.com — every self-hosted install published
// this operator's address as its own. Hidden when unset, like the other channels.
const contactEmail = computed(() => get('portal.contact.email', 'portalContactEmail'))
const phone = computed(() => get('portal.contact.phone', 'portalContactPhone'))
const taxId = computed(() => get('portal.legal.tax_id', 'portalLegalTaxId'))
const year = new Date().getFullYear()

/**
 * Social profiles the operator has configured. Each is omitted when unset, so an
 * install that fills in none renders no row at all rather than a line of dead
 * icons pointing at this project's own accounts.
 */
const socials = computed(() => [
  { tag: 'fb', name: 'Facebook', url: get('portal.social.facebook', 'portalSocialFacebook') },
  { tag: 'ig', name: 'Instagram', url: get('portal.social.instagram', 'portalSocialInstagram') },
  { tag: 'in', name: 'LinkedIn', url: get('portal.social.linkedin', 'portalSocialLinkedin') },
  { tag: 'tg', name: 'Telegram', url: telegramUrl.value },
  { tag: 'yt', name: 'YouTube', url: get('portal.social.youtube', 'portalSocialYoutube') },
].filter(entry => entry.url))

/** Telegram is stored as a handle, not a URL, because the chat actions need it that way. */
const telegramUrl = computed(() => {
  const handle = get('portal.contact.telegram', 'portalTelegram')
  return handle ? `https://t.me/${handle}` : ''
})

const emailInput = ref('')
const emailTouched = ref(false)

/**
 * Whether to warn about the address. The form posts to an external provider, so
 * success cannot be confirmed here — only an obviously wrong address can be
 * caught before the visitor is sent away.
 */
const emailInvalid = computed(() =>
  emailTouched.value && emailInput.value.length > 0 && !emailInput.value.includes('@'))

const columns = computed(() => [
  {
    title: t('aurora.footer.company'),
    // No /about route exists in this portal — the company link points at contact.
    links: [
      { label: t('aurora.footer.about'), to: localePath('/contact') },
      { label: t('aurora.footer.blog'), to: localePath('/knowledgebase') },
      { label: t('aurora.footer.contact'), to: localePath('/contact') },
    ],
  },
  {
    title: t('aurora.footer.support'),
    links: [
      { label: t('aurora.footer.helpCenter'), to: localePath('/knowledgebase') },
      { label: t('aurora.footer.faq'), to: localePath('/faq') },
      { label: t('aurora.footer.domainTransfer'), to: localePath('/domains/transfer') },
    ],
  },
  {
    title: t('aurora.footer.legal'),
    links: [
      { label: t('aurora.footer.terms'), to: localePath('/terms') },
      { label: t('aurora.footer.privacy'), to: localePath('/privacy') },
      { label: t('aurora.footer.cookies'), to: localePath('/cookie-policy') },
      { label: t('aurora.footer.refund'), to: localePath('/refund-policy') },
    ],
  },
])
</script>
