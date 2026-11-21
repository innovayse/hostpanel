<template>
  <LegalDocument
    :badge="t('terms.badge')"
    :title="t('terms.title')"
    :subtitle="t('terms.subtitle')"
    :meta="isCustom ? undefined : lastUpdatedLine"
    :sections="sections"
    :contact-title="t('terms.contact.title')"
    :contact-description="t('terms.contact.description')"
    :contact-link="t('terms.contact.link')"
  />
</template>

<script setup lang="ts">
/**
 * Terms of Service page.
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

/** The sections of the terms and how many paragraphs each one holds, in reading order. */
const SECTION_DEFINITIONS = [
  { id: 'acceptance', paragraphs: 2 },
  { id: 'services', paragraphs: 3 },
  { id: 'accounts', paragraphs: 2 },
  { id: 'intellectual', paragraphs: 3 },
  { id: 'payment', paragraphs: 3 },
  { id: 'termination', paragraphs: 2 },
  { id: 'liability', paragraphs: 2 },
  { id: 'changes', paragraphs: 2 }
]

/** The built-in sections with their copy resolved in the active language. */
const builtInSections = computed<LegalSection[]>(() => SECTION_DEFINITIONS.map(section => ({
  id: section.id,
  title: t(`terms.sections.${section.id}.title`),
  paragraphs: Array.from({ length: section.paragraphs }, (_, index) => t(`terms.sections.${section.id}.content.${index}`, { site: siteName.value }))
})))

/** The sections to draw: the operator's own text when it has stated one, otherwise the built-in sections. */
const { sections, isCustom } = useLegalSections(LegalPage.Terms, builtInSections)

/** The "last updated" line shown under the title; it dates the built-in text only. */
const lastUpdatedLine = computed(() => `${t('terms.lastUpdated')}: ${LEGAL_LAST_UPDATED}`)

useSeoMeta({
  title: t('seo.legal.terms.title', { site: siteName.value }),
  description: t('seo.legal.terms.description', { site: siteName.value }),
  robots: 'noindex, follow'
})
</script>
