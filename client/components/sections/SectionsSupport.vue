<template>
  <section class="py-14 md:py-20 bg-classic-page" data-testid="sections-support">
    <UiContainer>
      <ClassicSectionHeader
        class="mb-12"
        :eyebrow="$t('hostingSections.support.badge')"
        :title="$t('hostingSections.support.title')"
        :subtitle="$t('hostingSections.support.subtitle')"
      />

      <UiBalancedGrid :count="channels.length" data-testid="sections-support-grid">
        <ClassicCard
          v-for="item in channels"
          :key="item.channel"
          hover
          :data-testid="`support-${item.channel}`"
        >
          <UiStack gap="sm">
            <ClassicIconTile :icon="CHANNEL_ICONS[item.channel]" />
            <UiHeading :level="3" size="bare" class="text-lg font-semibold text-classic-ink">{{ $t(`hostingSections.support.channels.${item.channel}.title`) }}</UiHeading>
            <UiText tone="inherit" size="sm" class="text-classic-muted">{{ $t(`hostingSections.support.channels.${item.channel}.description`) }}</UiText>
            <UiBadge v-if="item.responseTime" variant="success" rounded :data-testid="`support-response-${item.channel}`">
              {{ $t('hostingSections.support.responseTime', { time: item.responseTime }) }}
            </UiBadge>
            <UiLink v-if="item.href" :href="item.href" :underline="false" :testid="`support-link-${item.channel}`">
              {{ item.value ?? $t(`hostingSections.support.channels.${item.channel}.action`) }}
            </UiLink>
          </UiStack>
        </ClassicCard>
      </UiBalancedGrid>
    </UiContainer>
  </section>
</template>

<script setup lang="ts">
/**
 * Support section: the ways to reach the team (live chat, ticket, email, phone) with the reply
 * time the operator promises for each. Only configured channels show.
 */
import { SupportChannel } from '~/constants/supportChannel'

/** The support channels on offer, in display order. */
const channels = useSupportChannels()

/** Icon of each channel. */
const CHANNEL_ICONS: Record<SupportChannel, string> = {
  [SupportChannel.Chat]: 'mdi:chat-processing-outline',
  [SupportChannel.Ticket]: 'mdi:ticket-outline',
  [SupportChannel.Email]: 'mdi:email-outline',
  [SupportChannel.Phone]: 'mdi:phone-outline',
}
</script>
