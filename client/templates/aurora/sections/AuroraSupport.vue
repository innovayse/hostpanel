<template>
  <UiSection padding="lg" :contained="false" class="relative border-t border-line" data-testid="aurora-support">
    <UiText tone="inherit" size="inherit" :class="[EYEBROW_CLASS, 'text-ac3']">{{ t('aurora.support.eyebrow') }}</UiText>
    <UiHeading :level="2" size="bare" :class="[SECTION_TITLE_CLASS, 'mt-3']">
      {{ t('aurora.support.title') }}
    </UiHeading>

    <UiBalancedGrid :count="channels.length" class="mt-8" data-testid="aurora-support-grid">
      <component
        :is="channel.href ? UiLink : UiStack"
        v-for="channel in channels"
        :key="channel.channel"
        v-bind="channel.href ? { href: channel.href, tone: 'inherit', underline: false } : {}"
        :data-testid="`support-${channel.channel}`"
      >
        <UiCard
          tone="bare"
          hover
          class="h-full border-line bg-surf shadow-card transition-[border-color,transform] duration-200 hover:-translate-y-0.5 hover:border-ac1"
        >
          <UiStack gap="sm">
            <UiTile size="lg" :icon="ICONS[channel.channel]" class="bg-tile-sky text-white" />
            <UiText as="span" size="inherit" tone="inherit" weight="bold" class="text-[18px] text-tx">{{ t(`aurora.support.${channel.channel}`) }}</UiText>
            <UiText v-if="channel.value" as="span" size="inherit" tone="inherit" class="text-[15px] text-tx2" :data-testid="`support-value-${channel.channel}`">{{ channel.value }}</UiText>
            <UiText v-if="channel.responseTime" as="span" size="inherit" tone="inherit" class="text-[14px] text-mut" :data-testid="`support-response-${channel.channel}`">
              {{ t('aurora.support.response', { time: channel.responseTime }) }}
            </UiText>
          </UiStack>
        </UiCard>
      </component>
    </UiBalancedGrid>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * aurora support channels: chat, ticket, e-mail and phone, each a card with its typical
 * response time when the operator set one. A card with a link opens it; the section hides when
 * no channel is available.
 */
import type { Component } from 'vue'
import { LifeBuoy, Mail, MessageCircle, Phone } from 'lucide-vue-next'
import { SupportChannel } from '~/constants/supportChannel'
import { useSupportChannels } from '~/composables/useSupportChannels'
import { EYEBROW_CLASS, SECTION_TITLE_CLASS } from '~/templates/aurora/typography'
import UiLink from '~/components/ui/UiLink.vue'
import UiStack from '~/components/ui/UiStack.vue'

/** Translator for the active locale. */
const { t } = useI18n()

/** The channels on offer, in display order. */
const channels = useSupportChannels()

/** The tile icon of each channel. */
const ICONS: Record<SupportChannel, Component> = {
  [SupportChannel.Chat]: MessageCircle,
  [SupportChannel.Ticket]: LifeBuoy,
  [SupportChannel.Email]: Mail,
  [SupportChannel.Phone]: Phone,
}
</script>
