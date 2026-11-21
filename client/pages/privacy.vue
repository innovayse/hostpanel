<template>
  <LegalDocument
    :badge="t('privacy.badge')"
    :title="t('privacy.title')"
    :subtitle="t('privacy.subtitle', { site: siteName })"
    :meta="isCustom ? undefined : lastUpdatedLine"
    :sections="sections"
    :contact-title="t('privacy.contact.title')"
    :contact-description="t('privacy.contact.description', { site: siteName })"
    :contact-link="t('privacy.contact.link')"
  />
</template>

<script setup lang="ts">
/**
 * Privacy Policy page.
 *
 * Owns the copy keys and the SEO tags; the layout of the document is `LegalDocument`.
 */
import { LEGAL_LAST_UPDATED } from '~/constants/legalUpdated'
import { LegalPage } from '~/constants/legalPage'
import { useLegalSections } from '~/composables/useLegalSections'
import type { LegalSection } from '~/types/legalsection'

/** Translates a key in the active language. */
const { t } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()

/** The sections of the policy: icon, paragraph count and how many bullet entries each one holds. */
const SECTION_DEFINITIONS = [
  { id: 'collection', icon: 'mdi:database', paragraphs: 1, listCount: 5 },
  { id: 'usage', icon: 'mdi:chart-bar', paragraphs: 1, listCount: 5 },
  { id: 'sharing', icon: 'mdi:share-variant', paragraphs: 2, listCount: 0 },
  { id: 'cookies', icon: 'mdi:cookie', paragraphs: 2, listCount: 0 },
  { id: 'security', icon: 'mdi:shield-check', paragraphs: 2, listCount: 0 },
  { id: 'rights', icon: 'mdi:account-check', paragraphs: 1, listCount: 4 },
  { id: 'retention', icon: 'mdi:clock-outline', paragraphs: 2, listCount: 0 },
  { id: 'children', icon: 'mdi:baby-face', paragraphs: 1, listCount: 0 },
  { id: 'changes', icon: 'mdi:pencil', paragraphs: 2, listCount: 0 }
]

/** The built-in sections with their copy resolved in the active language. */
const builtInSections = computed<LegalSection[]>(() => SECTION_DEFINITIONS.map(section => ({
  id: section.id,
  icon: section.icon,
  title: t(`privacy.sections.${section.id}.title`),
  paragraphs: Array.from({ length: section.paragraphs }, (_, index) => t(`privacy.sections.${section.id}.content.${index}`, { site: siteName.value })),
  items: Array.from({ length: section.listCount }, (_, index) => t(`privacy.sections.${section.id}.list.${index}`))
})))

/** The sections to draw: the operator's own text when it has stated one, otherwise the built-in sections. */
const { sections, isCustom } = useLegalSections(LegalPage.Privacy, builtInSections)

/** The "last updated" line shown under the title; it dates the built-in text only. */
const lastUpdatedLine = computed(() => `${t('privacy.lastUpdated')}: ${LEGAL_LAST_UPDATED}`)

useSeoMeta({
  title: t('seo.legal.privacy.title', { site: siteName.value }),
  description: t('seo.legal.privacy.description', { site: siteName.value }),
  robots: 'noindex, follow'
})
</script>
