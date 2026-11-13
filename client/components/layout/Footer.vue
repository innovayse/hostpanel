<template>
  <footer
    class="relative border-t border-classic-border bg-classic-footer shadow-[0_-12px_32px_-20px_rgba(17,24,39,0.18)] dark:shadow-none"
    data-testid="layout-footer"
  >
    <!-- Top band: one line of copy and the two ways in. -->
    <UiStack class="border-b border-classic-border" gap="none" data-testid="footer-cta">
      <UiContainer class="py-8">
        <UiStack gap="md" align="start" class="md:flex-row md:items-center md:justify-between">
          <UiStack gap="xs" class="min-w-0">
            <UiHeading :level="UiHeadingLevel.H2" size="bare" class="text-xl font-semibold text-classic-ink">
              {{ $t('footer.cta.title') }}
            </UiHeading>
            <UiText tone="inherit" size="sm" class="text-classic-muted">
              {{ $t('footer.cta.text') }}
            </UiText>
          </UiStack>
          <UiStack gap="sm" align="start" class="w-full sm:flex-row sm:items-center md:w-auto">
            <SectionsDomainSearch size="md" class="w-full sm:min-w-[24rem] sm:flex-1 md:w-[28rem] md:flex-none" />
            <UiButton :to="localePath('/products')" :variant="UiButtonVariant.Outline" size="md" data-testid="footer-cta-plans">
              {{ $t('footer.cta.plans') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiContainer>
    </UiStack>

    <UiContainer class="py-12">
      <UiGrid cols="1" gap="xl" class="sm:grid-cols-3 lg:grid-cols-7">
        <!-- Brand block -->
        <UiStack gap="none" class="min-w-0 sm:col-span-3 lg:col-span-2" data-testid="footer-brand">
          <UiStack direction="row" align="center" gap="sm" class="text-classic-ink">
            <!-- Logo image cap: intrinsically small UI, not page layout. -->
            <LogoLockup img-class="h-9 w-auto max-w-[180px] object-contain" mark-class="h-9 w-9" name-class="text-lg font-bold" />
          </UiStack>
          <UiText tone="inherit" size="sm" class="mt-4 leading-relaxed text-classic-muted">
            {{ $t('footer.tagline') }}
          </UiText>

          <UiStack gap="xs" class="mt-5">
            <UiFooterLink v-if="contactEmail" :href="`mailto:${contactEmail}`" :variant="UiFooterLinkVariant.Classic" class="text-sm" testid="footer-email">
              {{ contactEmail }}
            </UiFooterLink>
            <UiFooterLink v-if="contactPhone" :href="`tel:${contactPhoneLink}`" :variant="UiFooterLinkVariant.Classic" class="text-sm" testid="footer-phone">
              {{ contactPhone }}
            </UiFooterLink>
            <UiText v-if="contactAddress" tone="inherit" size="sm" class="text-classic-muted" data-testid="footer-address">
              {{ contactAddress }}
            </UiText>
          </UiStack>

          <!-- Generic trust marks only: icons and plain words, no third party logos. -->
          <UiList list-style="plain" class="mt-6 space-y-2.5" data-testid="footer-trust">
            <UiListItem v-for="mark in trustMarks" :key="mark.key">
              <UiStack direction="row" align="center" gap="xs" class="text-classic-muted">
                <component :is="mark.icon" :size="16" class="shrink-0" aria-hidden="true" />
                <UiText as="span" tone="inherit" size="xs">{{ mark.label }}</UiText>
              </UiStack>
            </UiListItem>
          </UiList>
        </UiStack>

        <LayoutFooterColumn
          v-for="column in columns"
          :key="column.key"
          :column="column.key"
          :title="column.title"
          :links="column.links"
        />
      </UiGrid>
    </UiContainer>

    <!-- Bottom row: copyright on the left, pickers and theme on the right. -->
    <UiStack gap="none" class="border-t border-classic-border" data-testid="footer-bottom">
      <UiContainer class="py-5">
        <UiStack gap="md" align="start" class="sm:flex-row sm:items-center sm:justify-between">
          <UiText tone="inherit" size="sm" class="text-classic-muted">
            © {{ currentYear }} {{ BRAND_NAME }}. {{ $t('footer.bottom.rights') }}
          </UiText>
          <UiStack direction="row" wrap align="center" gap="sm">
            <UiLanguageSwitcher :placement="UiPlacement.Top" :align="UiMenuAlign.Start" />
            <UiCurrencySwitcher :placement="UiPlacement.Top" :align="UiMenuAlign.Start" />
            <UiThemeToggle />
          </UiStack>
        </UiStack>
      </UiContainer>
    </UiStack>
  </footer>
</template>

<script setup lang="ts">
/**
 * LayoutFooter. The classic storefront footer: a call-to-action band, a brand block with the
 * operator's contact details, five link columns (hosting plan groups from the catalogue, domains,
 * company, support, legal) and a bottom row with the copyright, language and currency pickers and
 * the theme toggle. It sits on the classic footer surface with a visible top border so it reads
 * as its own block in both colour modes.
 */
import { CreditCard, Lock, ShieldCheck } from 'lucide-vue-next'
import type { Component } from 'vue'
import LogoLockup from '~/components/LogoLockup.vue'
import LayoutFooterColumn from '~/components/layout/FooterColumn.vue'
import { useCatalogStore } from '~/stores/catalog'
import type { FooterLink } from '~/types/footerlink'
import { familyKey } from '~/utils/catalogGroups'
import { UiButtonVariant, UiFooterLinkVariant, UiHeadingLevel, UiMenuAlign, UiPlacement } from '~/utils/ui/enums'

/** One trust mark: a generic icon and its label. */
interface TrustMark {
  /** Stable key. */
  key: string
  /** Generic icon component. */
  icon: Component
  /** Translated label. */
  label: string
}

/** One link column of the footer. */
interface FooterColumn {
  /** Stable key, used in test ids. */
  key: string
  /** Translated heading. */
  title: string
  /** The column's links. */
  links: FooterLink[]
}

/** Translate function. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** Company name shown in the copyright line: the operator's site name. */
const { name: BRAND_NAME } = useSiteIdentity()

/** Operator contact channels: email, phone as typed, phone for a `tel:` link, and address. */
const { email: contactEmail, phone: contactPhone, phoneLink: contactPhoneLink, address: contactAddress } = useContactDetails()

/** The public catalogue, source of the hosting plan groups. */
const catalog = useCatalogStore()

/** Generic payment and trust marks; icons only, never a third party logo. */
const trustMarks = computed<TrustMark[]>(() => [
  { key: 'secure', icon: Lock, label: t('footer.trust.secure') },
  { key: 'payments', icon: CreditCard, label: t('footer.trust.payments') },
  { key: 'guarantee', icon: ShieldCheck, label: t('footer.trust.guarantee') },
])

/** Hosting column: the all-plans entry, then every plan group the storefront shows. */
const hostingLinks = computed<FooterLink[]>(() => [
  { key: 'all', label: t('footer.hosting.all'), to: localePath('/products') },
  ...catalog.familyGroups.map((group) => ({
    key: `group-${group.id}`,
    label: group.name,
    to: localePath(`/products/${familyKey(group)}`),
  })),
])

/** The link columns, in display order. */
const columns = computed<FooterColumn[]>(() => [
  { key: 'hosting', title: t('footer.columns.hosting'), links: hostingLinks.value },
  {
    key: 'domains',
    title: t('footer.columns.domains'),
    links: [
      { key: 'register', label: t('footer.domains.register'), to: localePath('/domains') },
      { key: 'transfer', label: t('footer.domains.transfer'), to: localePath('/domains/transfer') },
      { key: 'pricing', label: t('footer.domains.pricing'), to: localePath('/domains') },
    ],
  },
  {
    key: 'company',
    title: t('footer.columns.company'),
    links: [
      { key: 'about', label: t('footer.company.about'), to: localePath('/about') },
      { key: 'contact', label: t('footer.company.contact'), to: localePath('/contact') },
      { key: 'announcements', label: t('footer.company.announcements'), to: localePath('/announcements') },
    ],
  },
  {
    key: 'support',
    title: t('footer.columns.support'),
    links: [
      { key: 'help', label: t('footer.support.help'), to: localePath('/knowledgebase') },
      { key: 'faq', label: t('footer.support.faq'), to: localePath('/faq') },
      { key: 'ticket', label: t('footer.support.ticket'), to: localePath('/contact') },
    ],
  },
  {
    key: 'legal',
    title: t('footer.columns.legal'),
    links: [
      { key: 'privacy', label: t('footer.bottom.privacy'), to: localePath('/privacy') },
      { key: 'cookies', label: t('footer.bottom.cookies'), to: localePath('/cookie-policy') },
      { key: 'terms', label: t('footer.bottom.terms'), to: localePath('/terms') },
      { key: 'refund', label: t('footer.bottom.refund'), to: localePath('/refund-policy') },
    ],
  },
])

/** Current year for the copyright line. */
const currentYear = new Date().getFullYear()

/** Reads the plan groups once in the browser when no page has loaded them yet. */
onMounted(async () => {
  if (catalog.productGroups.length === 0) {
    await catalog.loadProductGroups()
  }
})
</script>
