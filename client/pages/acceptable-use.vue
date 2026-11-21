<template>
  <LegalDocument
    :badge="t('aup.badge')"
    :title="t('aup.title')"
    :subtitle="t('aup.subtitle', { site: siteName })"
    :meta="isCustom ? undefined : lastUpdatedLine"
    :sections="sections"
    :contact-title="t('aup.contact.title')"
    :contact-description="t('aup.contact.description')"
    :contact-link="t('aup.contact.link')"
    :contact-icon="ShieldAlert"
    contact-danger
  />
</template>

<script setup lang="ts">
/**
 * Acceptable Use Policy page.
 *
 * Owns the copy keys and the SEO tags; the layout of the document is `LegalDocument`.
 */
import { ShieldAlert } from 'lucide-vue-next'
import { LEGAL_LAST_UPDATED } from '~/constants/legalUpdated'
import { LegalPage } from '~/constants/legalPage'
import { useLegalSections } from '~/composables/useLegalSections'
import type { LegalSection } from '~/types/legalsection'

/** Translates a key in the active language. */
const { t } = useI18n()

/** Operator-set site name that the SEO copy interpolates as `{site}`. */
const { name: siteName } = useSiteIdentity()

/** The sections of the policy: icon and how many paragraphs each one holds. */
const SECTION_DEFINITIONS = [
  { id: 'purpose', icon: 'mdi:target-variant', paragraphs: 1 },
  { id: 'abuse', icon: 'mdi:alert-octagon-outline', paragraphs: 5 },
  { id: 'apps', icon: 'mdi:application-cog-outline', paragraphs: 4 },
  { id: 'hosting', icon: 'mdi:server-security', paragraphs: 2 },
  { id: 'enforcement', icon: 'mdi:gavel', paragraphs: 2 }
]

/** The built-in sections with their copy resolved in the active language. */
const builtInSections = computed<LegalSection[]>(() => SECTION_DEFINITIONS.map(section => ({
  id: section.id,
  icon: section.icon,
  title: t(`aup.sections.${section.id}.title`),
  paragraphs: Array.from({ length: section.paragraphs }, (_, index) => t(`aup.sections.${section.id}.content.${index}`, { site: siteName.value }))
})))

/** The sections to draw: the operator's own text when it has stated one, otherwise the built-in sections. */
const { sections, isCustom } = useLegalSections(LegalPage.AcceptableUse, builtInSections)

/** The "last updated" line shown under the title; it dates the built-in text only. */
const lastUpdatedLine = computed(() => `${t('aup.lastUpdated')}: ${LEGAL_LAST_UPDATED}`)

useSeoMeta({
  title: t('seo.legal.aup.title', { site: siteName.value }),
  description: t('seo.legal.aup.description', { site: siteName.value }),
  robots: 'noindex, follow'
})
</script>
