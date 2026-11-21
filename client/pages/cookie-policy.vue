<template>
  <LegalDocument
    :title="t('cookiePolicy.title')"
    :meta="isCustom ? undefined : lastUpdatedLine"
    :sections="sections"
  />
</template>

<script setup lang="ts">
/**
 * Cookie Policy page.
 *
 * Explains how the portal uses cookies. Owns the copy keys and the SEO tags; the layout of the
 * document is `LegalDocument`.
 */
import { LEGAL_LAST_UPDATED } from '~/constants/legalUpdated'
import type { LegalCard } from '~/types/legalcard'
import { LegalPage } from '~/constants/legalPage'
import { useLegalSections } from '~/composables/useLegalSections'
import type { LegalSection } from '~/types/legalsection'

/** Translates a key in the active language; `tm` reads a raw message tree. */
const { t, tm } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()

/** The operator's contact email; the contact section links to it only when it is set. */
const { email: contactEmail } = useContactDetails()

/** The types of cookie, each with a name and what it is for. */
const cookieTypes = computed(() => tm('cookiePolicy.sections.typesOfCookies.types') as LegalCard[])

/** What the cookies are used for, one line each. */
const cookieUses = computed(() => tm('cookiePolicy.sections.howWeUseCookies.uses') as string[])

/** The built-in sections with their copy resolved in the active language. */
const builtInSections = computed<LegalSection[]>(() => [
  {
    id: 'introduction',
    title: t('cookiePolicy.sections.introduction.title'),
    paragraphs: [t('cookiePolicy.sections.introduction.content', { site: siteName.value })]
  },
  {
    id: 'whatAreCookies',
    title: t('cookiePolicy.sections.whatAreCookies.title'),
    paragraphs: [t('cookiePolicy.sections.whatAreCookies.content')]
  },
  {
    id: 'typesOfCookies',
    title: t('cookiePolicy.sections.typesOfCookies.title'),
    cards: cookieTypes.value.map(type => ({ title: type.title, description: type.description }))
  },
  {
    id: 'howWeUseCookies',
    title: t('cookiePolicy.sections.howWeUseCookies.title'),
    items: cookieUses.value,
    checklist: true
  },
  {
    id: 'thirdPartyCookies',
    title: t('cookiePolicy.sections.thirdPartyCookies.title'),
    paragraphs: [t('cookiePolicy.sections.thirdPartyCookies.content')]
  },
  {
    id: 'managingCookies',
    title: t('cookiePolicy.sections.managingCookies.title'),
    paragraphs: [t('cookiePolicy.sections.managingCookies.content')],
    note: t('cookiePolicy.sections.managingCookies.note')
  },
  {
    id: 'contact',
    title: t('cookiePolicy.sections.contact.title'),
    paragraphs: [t('cookiePolicy.sections.contact.content')],
    link: contactEmail.value ? { label: contactEmail.value, href: `mailto:${contactEmail.value}` } : undefined
  }
])

/** The sections to draw: the operator's own text when it has stated one, otherwise the built-in sections. */
const { sections, isCustom } = useLegalSections(LegalPage.Cookies, builtInSections)

/** The "last updated" line shown under the title; it dates the built-in text only. */
const lastUpdatedLine = computed(() => `${t('cookiePolicy.lastUpdated')}: ${LEGAL_LAST_UPDATED}`)

useSeoMeta({
  title: t('seo.legal.cookies.title', { site: siteName.value }),
  description: t('seo.legal.cookies.description', { site: siteName.value }),
  robots: 'noindex, follow',

  // Open Graph
  ogTitle: t('seo.legal.cookies.title', { site: siteName.value }),
  ogDescription: t('seo.legal.cookies.ogDescription', { site: siteName.value }),
  ogType: 'website',

  // Twitter Card
  twitterCard: 'summary',
  twitterTitle: t('seo.legal.cookies.title', { site: siteName.value }),
  twitterDescription: t('seo.legal.cookies.twitterDescription', { site: siteName.value })
})
</script>
