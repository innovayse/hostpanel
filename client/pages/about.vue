<template>
  <LegalDocument
    :title="t('about.title')"
    :subtitle="tagline || undefined"
    :sections="sections"
    :contact-title="t('about.contactTitle')"
    :contact-description="t('about.contactDescription')"
    :contact-link="t('about.contactLink')"
  />
</template>

<script setup lang="ts">
/**
 * About page.
 *
 * Tells the operator's company story, read from the `portal.about.text` settings in the visitor's
 * language. The page writes no story of its own: an install with none set sends the visitor to the
 * contact page instead.
 */
import type { LegalSection } from '~/types/legalsection'
import { SITE_CONTACT_PATH } from '~/constants/siteRoutes'

/** Translates a key in the active language. */
const { t } = useI18n()

/** Builds a path in the active language. */
const localePath = useLocalePath()

/** Operator-set site name and tagline. */
const { name: siteName, tagline } = useSiteIdentity()

/** The operator's story, one entry per paragraph. */
const { paragraphs } = useAboutText()

if (paragraphs.value.length === 0) {
  await navigateTo(localePath(SITE_CONTACT_PATH), { redirectCode: 302 })
}

/** The one section the page shows: the story. */
const sections = computed<LegalSection[]>(() => [
  {
    id: 'story',
    icon: 'mdi:domain',
    title: t('about.storyTitle', { site: siteName.value }),
    paragraphs: paragraphs.value
  }
])

useSeoMeta({
  title: t('seo.about.title', { site: siteName.value }),
  description: t('seo.about.description', { site: siteName.value }),
  ogTitle: t('seo.about.title', { site: siteName.value }),
  ogDescription: t('seo.about.description', { site: siteName.value }),
  ogType: 'website',
  twitterCard: 'summary',
  twitterTitle: t('seo.about.title', { site: siteName.value }),
  twitterDescription: t('seo.about.description', { site: siteName.value })
})
</script>
