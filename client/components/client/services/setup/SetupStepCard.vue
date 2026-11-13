<template>
  <UiCard padding="xl">
    <UiStack direction="row" align="center" class="mb-8">
      <UiTile :icon="icon" :tone="tone" size="xl" />
      <UiStack gap="none">
        <UiHeading :level="2" size="subsection">{{ title }}</UiHeading>
        <UiText size="sm" tone="subtle">{{ subtitle }}</UiText>
      </UiStack>
    </UiStack>
    <UiStack gap="lg">
      <slot />
    </UiStack>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * SetupStepCard. The frame of a setup wizard step: an icon tile, the step's title and subtitle
 * and the step's fields in the default slot.
 */
import type { Component } from 'vue'
import { UiTone } from '~/utils/ui/enums'

/** Props of SetupStepCard. */
interface Props {
  /** A lucide icon component. */
  icon: Component
  /** Title of the step. */
  title: string
  /** One sentence under the title. */
  subtitle: string
  /** Colour of the icon tile. */
  tone?: `${UiTone}`
}

withDefaults(defineProps<Props>(), {
  tone: UiTone.Primary,
})
</script>
