<template>
  <LegalDocument
    :badge="t('refundPolicy.badge')"
    :title="t('refundPolicy.title')"
    :subtitle="t('refundPolicy.subtitle', { site: siteName })"
    :meta="isCustom ? undefined : lastUpdatedLine"
    :sections="sections"
    :contact-title="t('refundPolicy.contact.title')"
    :contact-description="t('refundPolicy.contact.description')"
    :contact-link="t('refundPolicy.contact.link')"
  />
</template>

<script setup lang="ts">
/**
 * Refund Policy page.
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

/** The sections of the policy: icon and how many paragraphs each one holds. */
const SECTION_DEFINITIONS = [
  { id: 'general', icon: 'mdi:information-outline', paragraphs: 1 },
  { id: 'subscriptions', icon: 'mdi:sync', paragraphs: 3 },
  { id: 'domains', icon: 'mdi:web', paragraphs: 4 },
  { id: 'hosting', icon: 'mdi:server', paragraphs: 2 },
  { id: 'servers', icon: 'mdi:server-network', paragraphs: 2 },
  { id: 'process', icon: 'mdi:help-circle-outline', paragraphs: 2 }
]

/** The built-in sections with their copy resolved in the active language. */
const builtInSections = computed<LegalSection[]>(() => SECTION_DEFINITIONS.map(section => ({
  id: section.id,
  icon: section.icon,
  title: t(`refundPolicy.sections.${section.id}.title`),
  paragraphs: Array.from({ length: section.paragraphs }, (_, index) => t(`refundPolicy.sections.${section.id}.content.${index}`, { site: siteName.value }))
})))

/** The sections to draw: the operator's own text when it has stated one, otherwise the built-in sections. */
const { sections, isCustom } = useLegalSections(LegalPage.Refund, builtInSections)

/** The "last updated" line shown under the title; it dates the built-in text only. */
const lastUpdatedLine = computed(() => `${t('refundPolicy.lastUpdated')}: ${LEGAL_LAST_UPDATED}`)

useSeoMeta({
  title: t('seo.legal.refund.title', { site: siteName.value }),
  description: t('seo.legal.refund.description', { site: siteName.value }),
  robots: 'noindex, follow'
})
</script>
