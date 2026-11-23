<template>
  <UiSection padding="lg" class="border-b border-nova-border" data-testid="nova-support">
    <NovaSectionHeader
      :eyebrow="t('nova.support.eyebrow')"
      :title="t('nova.support.title')"
      :lead="t('nova.support.lead')"
    />

    <UiBalancedGrid :as="UiGridTag.Ul" :count="channels.length" class="mt-10" data-testid="nova-support-grid">
      <UiStack v-for="item in channels" :key="item.channel" :as="UiStackTag.Li" gap="none" class="h-full" :data-testid="`nova-support-${item.channel}`">
        <UiCard padding="lg" class="flex h-full flex-col !border-nova-border !bg-nova-surface">
          <UiIconTile :icon="SUPPORT_CHANNEL_CARDS[item.channel].icon" size="lg" tone="nova" />
          <UiHeading :level="3" size="bare" class="mt-5 text-[17px] font-bold text-nova-ink">
            {{ t(SUPPORT_CHANNEL_CARDS[item.channel].titleKey) }}
          </UiHeading>
          <UiText size="inherit" tone="inherit" class="mt-2.5 text-[15px] leading-relaxed text-nova-muted">
            {{ t(SUPPORT_CHANNEL_CARDS[item.channel].bodyKey ?? '') }}
          </UiText>
          <UiText
            v-if="item.responseTime"
            :as="UiTextTag.P"
            size="inherit"
            tone="inherit"
            class="mt-3 text-[14px] font-semibold text-nova-brand"
            data-testid="nova-support-response"
          >
            {{ t('nova.support.responseTime', { time: item.responseTime }) }}
          </UiText>
          <NovaCta
            v-if="item.href"
            :href="item.href"
            tone="outline"
            size="card"
            class="mt-5 self-start"
            data-testid="nova-support-link"
          >
            {{ item.value ?? t('nova.support.open') }}
          </NovaCta>
        </UiCard>
      </UiStack>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * nova support section: one card per available channel (chat, ticket, email, phone) with its
 * typical response time and, where the channel has an address, a link to it. The channels come
 * from `useSupportChannels()`; the ticket channel is always present.
 */
import NovaCta from '~/templates/nova/sections/NovaCta.vue'
import NovaSectionHeader from '~/templates/nova/sections/NovaSectionHeader.vue'
import { SUPPORT_CHANNEL_CARDS } from '~/templates/nova/content'
import { UiGridTag, UiStackTag, UiTextTag } from '~/utils/ui/enums'

/** Translator for the copy. */
const { t } = useI18n()
/** The channels this install offers. */
const channels = useSupportChannels()
</script>
