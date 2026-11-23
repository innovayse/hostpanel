<template>
  <UiSection :id="HomeSectionId.Services" padding="lg" :contained="false" :class="[HOME_SECTION_SCROLL_MARGIN_CLASS, 'relative border-t border-line']">
    <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac2']">{{ t('aurora.services.eyebrow') }}</UiText>
    <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
      {{ t('aurora.services.title') }}
    </UiHeading>

    <UiGrid cols="auto" gap="lg" class="mt-8">
      <UiCard
        v-for="service in services"
        :key="service.key"
        tone="bare"
        hover
        :data-testid="`service-card-${service.key}`"
        class="border-line bg-surf shadow-card transition-[border-color,transform] duration-200 hover:-translate-y-0.5 hover:border-ac1"
      >
        <UiStack gap="sm">
          <UiTile size="lg" :icon="service.icon" :class="[service.tint, 'text-white']" :data-testid="`service-icon-${service.key}`" />
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" weight="bold" class="text-[18px] text-tx">{{ service.title }}</UiText>
          <UiText :as="UiTextTag.Span" size="inherit" tone="inherit" class="text-[15px] leading-relaxed text-mut">{{ service.body }}</UiText>
        </UiStack>
      </UiCard>
    </UiGrid>

    <!-- The header's Services entry lands here, so the full catalogue stays one click away. -->
    <UiStack direction="row" class="mt-8">
      <UiButton
        variant="bare"
        :to="localePath(SITE_PRODUCTS_PATH)"
        data-testid="services-view-all"
        :class="[LINE_BUTTON, 'px-6 py-3 text-[15px]']"
      >
        {{ t('aurora.services.viewAll') }}
      </UiButton>
    </UiStack>
  </UiSection>
</template>

<script setup lang="ts">
/** aurora services grid: four editorial cards (hosting, servers, domains, billing), each with an icon on a gradient tile, no data dependency. */
import { Cpu, Globe, Receipt, Server } from 'lucide-vue-next'
import { HOME_SECTION_SCROLL_MARGIN_CLASS } from '~/constants/homeSections'
import { HomeSectionId } from '~/types/homesectionid'
import { LINE_BUTTON } from '~/templates/aurora/buttons'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import { SITE_PRODUCTS_PATH } from '~/constants/siteRoutes'
import { UiTextTag } from '~/utils/ui/enums'

/** Translator for the active locale. */
const { t } = useI18n()

/** Prefixes a path with the active locale. */
const localePath = useLocalePath()

/** The four services: stable key, tile icon (white on the gradient), copy, and a named token class for the tile gradient. */
const services = computed(() => [
  { key: 'hosting', icon: Server, title: t('aurora.services.hostingTitle'), body: t('aurora.services.hostingBody'), tint: 'bg-tile-sky' },
  { key: 'servers', icon: Cpu, title: t('aurora.services.serversTitle'), body: t('aurora.services.serversBody'), tint: 'bg-tile-lilac' },
  { key: 'domains', icon: Globe, title: t('aurora.services.domainsTitle'), body: t('aurora.services.domainsBody'), tint: 'bg-tile-mint' },
  { key: 'billing', icon: Receipt, title: t('aurora.services.billingTitle'), body: t('aurora.services.billingBody'), tint: 'bg-tile-pink' },
])
</script>
