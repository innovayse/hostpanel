<template>
  <!-- pb-24 clears the fixed contact button the layout mounts over this corner. -->
  <UiText :as="UiTextTag.Footer" :size="UiTextSize.Inherit" :tone="UiTextTone.Inherit" class="tpl-nova border-t border-nova-border bg-[color:var(--n-footer)] shadow-[0_-12px_32px_-20px_rgba(10,32,41,0.22)] pb-24 pt-14 font-nova text-nova-ink">
    <UiContainer>
      <UiGrid :cols="UiGridColumns.One" :gap="UiGap.Xl" class="lg:grid-cols-[minmax(0,1.4fr)_repeat(3,minmax(0,1fr))]">
        <UiStack :gap="UiGap.None">
          <UiStack :direction="UiDirection.Row" :align="UiCrossAlign.Center" :gap="UiGap.Sm">
            <!-- Logo image cap: intrinsically small UI, not page layout. -->
            <LogoLockup img-class="h-8 w-auto max-w-[120px]" mark-class="h-8 w-8" name-class="text-base font-bold" />
          </UiStack>
          <UiText :size="UiTextSize.Inherit" :tone="UiTextTone.Inherit" class="mt-4 text-[15px] leading-relaxed text-nova-muted">
            {{ t('nova.footer.tagline') }}
          </UiText>

          <!-- Only the profiles the operator configured; an unset one is not a dead link. -->
          <UiStack v-if="socials.length" :gap="UiGap.None" class="mt-6">
            <UiHeading
              :level="UiHeadingLevel.H2"
              :size="UiHeadingSize.Bare"
              class="text-[13px] font-semibold uppercase tracking-[0.08em] text-nova-muted"
            >
              {{ t('nova.footer.social') }}
            </UiHeading>
            <UiList :list-style="UiListStyle.Bare" class="mt-3 flex flex-wrap gap-2">
              <UiStack v-for="social in socials" :key="social.key" :as="UiStackTag.Li" :gap="UiGap.None">
                <UiLink
                  :href="social.url"
                  external
                  :tone="UiLinkTone.Inherit"
                  :underline="false"
                  :testid="`nova-footer-social-${social.key}`"
                  class="grid h-11 w-11 place-items-center rounded-xl border border-nova-border text-nova-muted hover:border-nova-brand hover:text-nova-ink"
                >
                  <Icon :name="social.icon" class="h-[18px] w-[18px]" aria-hidden="true" />
                  <UiText :as="UiTextTag.Span" :size="UiTextSize.Inherit" :tone="UiTextTone.Inherit" class="sr-only">{{ social.name }}</UiText>
                </UiLink>
              </UiStack>
            </UiList>
          </UiStack>
        </UiStack>

        <UiStack v-for="column in FOOTER_COLUMNS" :key="column.key" :gap="UiGap.None" class="min-w-0">
          <UiHeading
            :level="UiHeadingLevel.H2"
            :size="UiHeadingSize.Bare"
            class="text-[13px] font-semibold uppercase tracking-[0.08em] text-nova-muted"
          >
            {{ t(column.titleKey) }}
          </UiHeading>
          <UiList :list-style="UiListStyle.Bare" class="mt-4 flex flex-col gap-1">
            <UiStack v-for="link in column.links" :key="link.key" :as="UiStackTag.Li" :gap="UiGap.None">
              <UiFooterLink
                :to="localePath(link.to)"
                :variant="UiFooterLinkVariant.Nova"
                :testid="`nova-footer-${column.key}-${link.key}`"
                class="min-h-[40px] text-[15px]"
              >
                {{ t(link.labelKey) }}
              </UiFooterLink>
            </UiStack>
          </UiList>
        </UiStack>
      </UiGrid>

      <UiStack
        :direction="UiDirection.Row"
        wrap
        :align="UiCrossAlign.Center"
        :justify="UiJustify.Between"
        :gap="UiGap.Md"
        class="mt-12 border-t border-nova-border pt-6 text-sm text-nova-muted"
      >
        <UiText :size="UiTextSize.Inherit" :tone="UiTextTone.Inherit">© {{ year }} {{ BRAND_NAME }}. {{ t('nova.footer.rights') }}</UiText>

        <!-- Each channel is hidden when the operator has not filled it in. -->
        <UiList :list-style="UiListStyle.Bare" class="flex flex-wrap items-center gap-x-4 gap-y-1">
          <UiText v-if="taxId" :as="UiTextTag.Li" :size="UiTextSize.Inherit" :tone="UiTextTone.Inherit">{{ t('nova.footer.taxId') }} {{ taxId }}</UiText>
          <UiStack v-if="contactEmail" :as="UiStackTag.Li" :gap="UiGap.None">
            <UiFooterLink
              :href="`mailto:${contactEmail}`"
              :variant="UiFooterLinkVariant.Nova"
              testid="nova-footer-email"
            >
              {{ contactEmail }}
            </UiFooterLink>
          </UiStack>
          <UiStack v-if="phone" :as="UiStackTag.Li" :gap="UiGap.None">
            <UiFooterLink
              :href="`tel:${phoneLink}`"
              :variant="UiFooterLinkVariant.Nova"
              testid="nova-footer-phone"
            >
              {{ phone }}
            </UiFooterLink>
          </UiStack>
        </UiList>
      </UiStack>
    </UiContainer>
  </UiText>
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
import LogoLockup from '~/components/LogoLockup.vue'
import { UiCrossAlign, UiDirection, UiFooterLinkVariant, UiGap, UiGridColumns, UiHeadingLevel, UiHeadingSize, UiJustify, UiLinkTone, UiListStyle, UiStackTag, UiTextSize, UiTextTag, UiTextTone } from '~/utils/ui/enums'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { useContactDetails } from '~/composables/useContactDetails'
import { FOOTER_COLUMNS } from '~/templates/nova/content'

/** Name the footer signs with: the operator's site name. */
const { name: BRAND_NAME } = useSiteIdentity()

/** Translator for the copy. */
const { t } = useI18n()
/** Prefixes a route with the active locale. */
const localePath = useLocalePath()
/** Reads the operator settings. */
const { get } = usePortalSettings()

/** Current year, for the copyright line. */
const year = new Date().getFullYear()

/** Operator contact email, phone (as written and as a dialable number) and Telegram link; each empty when unset. */
const { email: contactEmail, phone, phoneLink, telegramUrl } = useContactDetails()
/** Operator tax id; empty hides the entry. */
const taxId = computed(() => get(PortalSettingKey.LegalTaxId, 'portalLegalTaxId'))

/** The social profiles the operator configured, in display order. */
const socials = computed(() => [
  { key: 'facebook', icon: 'lucide:facebook', name: 'Facebook', url: get(PortalSettingKey.SocialFacebook, 'portalSocialFacebook') },
  { key: 'instagram', icon: 'lucide:instagram', name: 'Instagram', url: get(PortalSettingKey.SocialInstagram, 'portalSocialInstagram') },
  { key: 'linkedin', icon: 'lucide:linkedin', name: 'LinkedIn', url: get(PortalSettingKey.SocialLinkedin, 'portalSocialLinkedin') },
  { key: 'telegram', icon: 'lucide:send', name: 'Telegram', url: telegramUrl.value },
  { key: 'youtube', icon: 'lucide:youtube', name: 'YouTube', url: get(PortalSettingKey.SocialYoutube, 'portalSocialYoutube') },
].filter(entry => entry.url))
</script>
