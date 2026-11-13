<template>
  <UiCard padding="xl" class="text-center" data-testid="setup-state">
    <UiStack align="center" gap="md">
      <UiTile v-if="icon" :icon="icon" :tone="tone" size="xl" />
      <UiHeading :level="2" size="section">{{ title }}</UiHeading>
      <UiText tone="muted">{{ description }}</UiText>
      <slot />
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * SetupStatePanel. The centred card the setup page shows instead of the wizard: the
 * service cannot be set up, or it is already set up. An icon, a headline, a
 * sentence and an optional action in the default slot.
 */
import type { Component } from 'vue'
import { UiTone } from '~/utils/ui/enums'

/** Props of SetupStatePanel. */
interface Props {
  /** Headline. */
  title: string
  /** One supporting sentence. */
  description: string
  /** A lucide icon component. */
  icon?: Component
  /** Colour of the icon tile. */
  tone?: `${UiTone}`
}

withDefaults(defineProps<Props>(), {
  icon: undefined,
  tone: UiTone.Primary,
})
</script>
