<template>
  <UiStack gap="xs" align="center" class="text-center" data-testid="classic-section-header">
    <UiText v-if="eyebrow" as="span" tone="inherit" size="sm" weight="bold" class="uppercase tracking-widest text-classic-brand" data-testid="classic-section-header-eyebrow">
      {{ eyebrow }}
    </UiText>
    <UiHeading :level="2" size="bare" class="text-3xl md:text-4xl font-bold tracking-tight text-classic-ink" data-testid="classic-section-header-title">
      {{ title }}
    </UiHeading>
    <UiText v-if="subtitle" tone="inherit" size="lg" class="max-w-[75ch] text-classic-muted" data-testid="classic-section-header-subtitle">
      {{ subtitle }}
    </UiText>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * ClassicSectionHeader: the heading block every classic home section opens with.
 *
 * A small brand-coloured label, a solid charcoal title and a muted lead line, all centred. There
 * is no pill, no highlighted word and no motion: the classic template reads as a plain, trusted
 * hosting site. Spacing below it is the caller's (`class="mb-12"`).
 */

/** Props of ClassicSectionHeader. */
interface Props {
  /** Small uppercase label above the title. */
  eyebrow?: string
  /** The section title. */
  title: string
  /** Lead line under the title. */
  subtitle?: string
}

defineProps<Props>()
</script>
