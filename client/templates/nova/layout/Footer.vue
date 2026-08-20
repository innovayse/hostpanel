<template>
  <!-- pb-24 clears the fixed contact button the layout mounts over this corner. -->
  <footer class="tpl-nova border-t border-nova-border bg-nova-bg pb-24 pt-14 font-nova text-nova-ink">
    <div class="mx-auto max-w-[1240px] px-4 sm:px-6 lg:px-8">
      <div class="grid gap-10 lg:grid-cols-[minmax(0,1.4fr)_repeat(3,minmax(0,1fr))]">
        <div>
          <div class="flex items-center gap-2.5">
            <span
              class="grid h-8 w-8 place-items-center rounded-lg bg-[linear-gradient(135deg,var(--n-brand),var(--n-accent))] text-[15px] font-extrabold text-[#08191f]"
              aria-hidden="true"
            >i</span>
            <span class="text-base font-bold">Innovayse</span>
          </div>
          <p class="mt-4 max-w-[320px] text-[15px] leading-relaxed text-nova-muted">
            {{ t('nova.footer.tagline') }}
          </p>

          <!-- Only the profiles the operator configured; an unset one is not a dead link. -->
          <div v-if="socials.length" class="mt-6">
            <h2 class="text-[13px] font-semibold uppercase tracking-[0.08em] text-nova-muted">
              {{ t('nova.footer.social') }}
            </h2>
            <ul class="mt-3 flex flex-wrap gap-2">
              <li v-for="social in socials" :key="social.key">
                <a
                  :href="social.url"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="grid h-11 w-11 place-items-center rounded-xl border border-nova-border text-nova-muted transition-colors hover:border-nova-brand hover:text-nova-ink"
                >
                  <Icon :name="social.icon" class="h-[18px] w-[18px]" aria-hidden="true" />
                  <span class="sr-only">{{ social.name }}</span>
                </a>
              </li>
            </ul>
          </div>
        </div>

        <div v-for="column in FOOTER_COLUMNS" :key="column.key" class="min-w-0">
          <h2 class="text-[13px] font-semibold uppercase tracking-[0.08em] text-nova-muted">
            {{ t(column.titleKey) }}
          </h2>
          <ul class="mt-4 flex flex-col gap-1">
            <li v-for="link in column.links" :key="link.key">
              <NuxtLink
                :to="localePath(link.to)"
                class="flex min-h-[40px] items-center text-[15px] text-nova-muted transition-colors hover:text-nova-ink"
              >{{ t(link.labelKey) }}</NuxtLink>
            </li>
          </ul>
        </div>
      </div>

      <div class="mt-12 flex flex-wrap items-center justify-between gap-4 border-t border-nova-border pt-6 text-sm text-nova-muted">
        <p>© {{ year }} Innovayse. {{ t('nova.footer.rights') }}</p>

        <!-- Each channel is hidden when the operator has not filled it in. -->
        <ul class="flex flex-wrap items-center gap-x-4 gap-y-1">
          <li v-if="taxId">{{ t('nova.footer.taxId') }} {{ taxId }}</li>
          <li v-if="contactEmail">
            <a :href="`mailto:${contactEmail}`" class="hover:text-nova-ink">{{ contactEmail }}</a>
          </li>
          <li v-if="phone">
            <a :href="`tel:${phoneHref}`" class="hover:text-nova-ink">{{ phone }}</a>
          </li>
        </ul>
      </div>
    </div>
  </footer>
</template>

<script setup lang="ts">
/**
 * nova template site footer.
 *
 * The columns come from `content.ts` and point only at routes this portal has.
 * The contact line and the social row are read from operator settings and each
 * entry disappears when unset, so a fresh install publishes neither an address
 * it does not own nor a link to somebody else's account.
 *
 * Live chat is not repeated here: the layout already mounts UiFloatingActions.
 */
import { FOOTER_COLUMNS } from '~/templates/nova/content'

const { t } = useI18n()
const localePath = useLocalePath()
const { get } = usePortalSettings()

const year = new Date().getFullYear()

const contactEmail = computed(() => get('portal.contact.email', 'portalContactEmail'))
const phone = computed(() => get('portal.contact.phone', 'portalContactPhone'))
const taxId = computed(() => get('portal.legal.tax_id', 'portalLegalTaxId'))

/** `tel:` accepts digits and a leading plus; everything else is presentation. */
const phoneHref = computed(() => phone.value.replace(/[^0-9+]/g, ''))

/** Telegram is stored as a handle rather than a URL — the chat actions need it that way. */
const telegramUrl = computed(() => {
  const handle = get('portal.contact.telegram', 'portalTelegram')
  return handle ? `https://t.me/${handle}` : ''
})

const socials = computed(() => [
  { key: 'facebook', icon: 'lucide:facebook', name: 'Facebook', url: get('portal.social.facebook', 'portalSocialFacebook') },
  { key: 'instagram', icon: 'lucide:instagram', name: 'Instagram', url: get('portal.social.instagram', 'portalSocialInstagram') },
  { key: 'linkedin', icon: 'lucide:linkedin', name: 'LinkedIn', url: get('portal.social.linkedin', 'portalSocialLinkedin') },
  { key: 'telegram', icon: 'lucide:send', name: 'Telegram', url: telegramUrl.value },
  { key: 'youtube', icon: 'lucide:youtube', name: 'YouTube', url: get('portal.social.youtube', 'portalSocialYoutube') },
].filter(entry => entry.url))
</script>
