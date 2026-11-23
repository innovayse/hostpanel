<template>
  <UiStack gap="none">
    <UiStack direction="row" wrap align="center" gap="sm">
      <UiText
        :as="UiTextTag.Span"
        size="inherit"
        tone="inherit"
        weight="semibold"
        class="text-[13px] uppercase tracking-[0.1em] text-nova-brand"
      >
        {{ eyebrow }}
      </UiText>
      <slot name="badge" />
    </UiStack>
    <UiHeading
      :level="2"
      size="bare"
      class="mt-3 text-[clamp(1.6rem,3.4vw,2.4rem)] font-bold tracking-tight text-nova-ink"
    >
      {{ title }}
    </UiHeading>
    <UiText
      v-if="lead"
      size="inherit"
      tone="inherit"
      class="mt-3 max-w-[75ch] text-[16px] leading-[1.7] text-nova-muted"
    >
      {{ lead }}
    </UiText>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The eyebrow, h2 and optional lead every nova section opens with.
 *
 * One component instead of eight copies of the same three lines, so the type scale of a section
 * heading changes in one place. The `badge` slot sits beside the eyebrow (the migration
 * section's "free" marker). The heading is always an h2: the hero owns the page's only h1.
 */
import { UiTextTag } from '~/utils/ui/enums'

/** Props of NovaSectionHeader. */
interface Props {
  /** Small uppercase line above the heading (already translated). */
  eyebrow: string
  /** The heading (already translated). */
  title: string
  /** Supporting paragraph under the heading (already translated); omitted renders none. */
  lead?: string
}

withDefaults(defineProps<Props>(), {
  lead: undefined,
})
</script>
