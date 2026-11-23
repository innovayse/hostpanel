<template>
  <UiStack
    direction="row"
    align="center"
    justify="center"
    gap="none"
    class="relative min-h-dvh overflow-hidden bg-gray-50 px-4 py-16 dark:bg-ui-surface"
    data-testid="auth-shell"
  >
    <UiBackdrop variant="aurora" />

    <!-- Demo notice: auth pages use no layout, so the shell carries the banner itself. -->
    <LayoutDemoBanner class="absolute inset-x-0 top-0 z-20" />

    <!-- Auth card width: an intrinsically small card, not page layout. -->
    <UiStack gap="none" class="relative z-10 w-full max-w-md">
      <UiStack align="center" gap="none" class="mb-8 text-center">
        <ClientAuthLogo v-if="showLogo" />
        <UiHeading v-if="title" :level="1" size="section" data-testid="auth-shell-title">{{ title }}</UiHeading>
        <UiText v-if="subtitle" tone="muted" size="sm" class="mt-1" data-testid="auth-shell-subtitle">{{ subtitle }}</UiText>
      </UiStack>

      <UiCard padding="xl">
        <slot />
      </UiCard>

      <UiStack v-if="$slots.footer" align="center" gap="none" class="mt-6 text-center">
        <slot name="footer" />
      </UiStack>
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * The page frame every signed-out client page shares: the aurora backdrop, the logo, a title
 * and subtitle, and the form card.
 *
 * The sign-in, registration, password-reset, confirmation and invitation pages each drew this
 * frame themselves, with a glow layer, corner accents and a card of their own. One frame means
 * one place to restyle them and one light/dark behaviour, through the kit's backdrop and card.
 */

/** Props of ClientAuthShell. */
interface Props {
  /** Heading above the card; omitted by pages whose card carries the whole message. */
  title?: string
  /** Line under the heading. */
  subtitle?: string
  /** Whether the logo is shown above the heading. */
  showLogo?: boolean
}

withDefaults(defineProps<Props>(), {
  title: undefined,
  subtitle: undefined,
  showLogo: true
})
</script>
