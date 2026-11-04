<script setup lang="ts">
/**
 * Centered card shared by the sign-in, first-run setup and email verification screens.
 *
 * Draws the brand wordmark for the active theme, the screen title and an optional subtitle; the screen's own content is the
 * default slot. It holds no state and no decoration of its own, so the three screens cannot drift.
 */
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import BrandLogo from '@/components/layout/BrandLogo.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'

/** Props for AuthShell. */
defineProps<{
  /** Screen title (already translated). */
  title: string
  /** Line under the title (already translated). */
  subtitle?: string
}>()

</script>

<template>
  <div class="min-h-dvh bg-surface-base">
    <slot name="banner" />
    <UiStack align="center" justify="center" class="p-4">
    <UiCard padding="lg" class="w-full max-w-sm">
      <UiStack gap="lg">
        <UiStack direction="row" align="center" gap="sm">
          <!-- The wordmark for the active theme: it carries the name, so no text beside it. -->
          <BrandLogo img-class="h-9 w-auto" />
        </UiStack>
        <UiStack gap="xs">
          <UiHeading :level="1" variant="page" data-testid="auth-title">
            {{ title }}
          </UiHeading>
          <UiText v-if="subtitle !== undefined" tone="secondary" size="md">
            {{ subtitle }}
          </UiText>
        </UiStack>
        <slot />
      </UiStack>
    </UiCard>
  </UiStack>
  </div>
</template>
