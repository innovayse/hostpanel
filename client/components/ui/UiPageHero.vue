<template>
  <section class="relative overflow-hidden py-12 md:py-24 [.tpl-classic_&]:border-b [.tpl-classic_&]:border-classic-border [.tpl-classic_&]:bg-classic-surface-2 [.tpl-classic_&]:py-10 md:[.tpl-classic_&]:py-14" data-testid="ui-page-hero">
    <UiBackdrop variant="aurora" class="[.tpl-classic_&]:hidden" />
    <div
      aria-hidden="true"
      class="ui-hero-corner pointer-events-none absolute right-0 top-0 h-48 w-48 border-r-2 border-t-2 border-secondary-500/30"
    />
    <div
      aria-hidden="true"
      class="ui-hero-corner pointer-events-none absolute bottom-0 left-0 h-48 w-48 border-b-2 border-l-2 border-secondary-500/30"
    />

    <UiContainer class="relative z-10 text-center [.tpl-classic_&]:text-left">
      <!--
        The header slides in but is never transparent: a fade from opacity 0 left the title, badge
        and subtitle invisible whenever the animation was held back (a background tab, a slow first
        paint, a capture). Classic is flat and does not animate the band at all.
      -->
      <div class="animate-slide-up motion-reduce:animate-none [.tpl-classic_&]:animate-none">
        <slot name="breadcrumbs" />
        <UiBadge
          v-if="badge"
          variant="primary"
          rounded
          class="mb-6 [.tpl-classic_&]:rounded-md [.tpl-classic_&]:border-0 [.tpl-classic_&]:bg-classic-brand-soft [.tpl-classic_&]:text-classic-brand"
          data-testid="ui-page-hero-badge"
        >
          {{ badge }}
        </UiBadge>
        <UiHeading
          :level="1"
          size="display"
          class="mb-6 break-words [.tpl-classic_&]:mb-3 [.tpl-classic_&]:text-3xl [.tpl-classic_&]:font-semibold [.tpl-classic_&]:text-classic-ink md:[.tpl-classic_&]:text-4xl"
          data-testid="ui-page-hero-title"
        >
          {{ title }}
          <UiGradientText
            v-if="highlight"
            class="bg-gradient-to-r from-primary-600 to-secondary-600 dark:from-primary-400 dark:to-secondary-400 [.tpl-classic_&]:bg-none [.tpl-classic_&]:text-classic-brand"
          >
            {{ highlight }}
          </UiGradientText>
        </UiHeading>
        <UiText
          v-if="subtitle"
          size="lg"
          tone="muted"
          class="mx-auto max-w-[75ch] [.tpl-classic_&]:mx-0 [.tpl-classic_&]:text-classic-muted"
          data-testid="ui-page-hero-subtitle"
        >
          {{ subtitle }}
        </UiText>
        <slot />
      </div>
    </UiContainer>
  </section>
</template>

<script setup lang="ts">
/**
 * UiPageHero. The opening band of a public page: aurora backdrop, corner accents, an optional
 * badge, the h1 title (with an optional gradient-highlighted word), a lead sentence, and a
 * default slot for what belongs under them (category links, a "last updated" line).
 *
 * The `breadcrumbs` slot sits above the badge. The whole block slides up on first paint without
 * ever dropping below full opacity (classic does not animate it).
 *
 * Under the classic template (the `tpl-classic` root class) the same markup is a flat, left-aligned
 * band: no backdrop, no corner frames, no gradient word, a solid brand badge and a hairline below.
 */

/** Props of UiPageHero. */
interface Props {
  /** Small pill above the title. */
  badge?: string
  /** The page title, rendered as the h1. */
  title: string
  /** Word or phrase painted with the brand gradient after the title. */
  highlight?: string
  /** One lead sentence under the title. */
  subtitle?: string
}

withDefaults(defineProps<Props>(), {
  badge: undefined,
  highlight: undefined,
  subtitle: undefined,
})
</script>
