<template>
  <UiSection :contained="false" padding="lg" class="bg-classic-surface border-y border-classic-border" data-testid="sections-why-choose-us">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-12"
        :eyebrow="$t('whyChooseUs.badge')"
        :title="$t('whyChooseUs.title')"
        :subtitle="$t('whyChooseUs.subtitle')"
      />

      <!-- Advantages -->
      <UiGrid cols="2" gap="lg" class="lg:grid-cols-4">
        <ClassicCard
          v-for="advantage in advantages"
          :key="advantage.key"
          hover
          class="h-full"
          :data-testid="`advantage-${advantage.key}`"
        >
          <UiStack gap="sm">
            <ClassicIconTile :icon="advantage.icon" />
            <UiHeading :level="3" size="bare" class="text-lg font-semibold text-classic-ink">{{ advantage.title }}</UiHeading>
            <UiText tone="inherit" size="sm" class="leading-relaxed text-classic-muted">{{ advantage.description }}</UiText>
          </UiStack>
        </ClassicCard>
      </UiGrid>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Why Choose Us section: the four reasons to host with us (fast servers, reliable uptime,
 * round-the-clock support, trusted by customers) as flat cards with a soft brand icon tile.
 */

const { t } = useI18n()

/** The four advantages, each with the icon that stands for it and its translated copy. */
const advantages = computed(() => [
  {
    key: 'fastDelivery',
    icon: 'mdi:rocket-launch',
    title: t('whyChooseUs.advantages.fastDelivery.title'),
    description: t('whyChooseUs.advantages.fastDelivery.description'),
  },
  {
    key: 'qualityGuaranteed',
    icon: 'mdi:shield-check',
    title: t('whyChooseUs.advantages.qualityGuaranteed.title'),
    description: t('whyChooseUs.advantages.qualityGuaranteed.description'),
  },
  {
    key: 'support',
    icon: 'mdi:headset',
    title: t('whyChooseUs.advantages.support.title'),
    description: t('whyChooseUs.advantages.support.description'),
  },
  {
    key: 'provenResults',
    icon: 'mdi:account-group',
    title: t('whyChooseUs.advantages.provenResults.title'),
    description: t('whyChooseUs.advantages.provenResults.description'),
  },
])
</script>
