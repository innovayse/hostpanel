<template>
  <UiSection :contained="false" padding="lg" class="bg-classic-surface-2 border-y border-classic-border" data-testid="sections-cta">
    <UiContainer>
      <UiStack align="center" gap="none" class="text-center animate-reveal-up motion-reduce:animate-none">
        <UiHeading :level="2" class="text-3xl md:text-4xl mb-4 text-classic-ink">{{ $t('cta.title') }}</UiHeading>

        <UiText size="lg" class="mb-8 max-w-[75ch] mx-auto text-classic-muted">{{ $t('cta.subtitle') }}</UiText>

        <UiStack align="center" justify="center" class="sm:flex-row">
          <UiButton :to="localePath(SITE_HOSTING_PATH)" variant="primary" size="lg" data-testid="cta-plans">
            {{ $t('cta.button') }}
            <ArrowRight :size="18" :stroke-width="2" class="ml-2" />
          </UiButton>
          <UiButton :to="localePath(SITE_CONTACT_PATH)" variant="outline" size="lg" data-testid="cta-contact">
            {{ $t('common.contactUs') }}
          </UiButton>
        </UiStack>
      </UiStack>
    </UiContainer>
  </UiSection>
</template>

<script setup lang="ts">
/**
 * Call-to-action band that closes a marketing page: a hosting headline, "See hosting plans" as the
 * main action and a "Talk to us" contact link (the footer band already carries the domain search), on a plain classic surface. The block enters with a CSS keyframe, not
 * a scroll-triggered directive, so it is visible in server HTML and in both themes.
 */
import { ArrowRight } from 'lucide-vue-next'
import { SITE_CONTACT_PATH, SITE_HOSTING_PATH } from '~/constants/siteRoutes'

/** Prefixes a path with the active language. */
const localePath = useLocalePath()
</script>
