<template>
  <UiStack gap="none" class="min-w-0" :data-testid="`footer-column-${column}`">
    <UiHeading :level="UiHeadingLevel.H3" size="bare" class="text-sm font-semibold text-classic-ink">
      {{ title }}
    </UiHeading>
    <UiList list-style="plain" class="mt-4 space-y-2.5">
      <UiListItem v-for="link in links" :key="link.key">
        <UiFooterLink
          :to="link.to"
          :variant="UiFooterLinkVariant.Classic"
          class="min-h-[28px] text-sm"
          :testid="`footer-${column}-${link.key}`"
        >
          {{ link.label }}
        </UiFooterLink>
      </UiListItem>
    </UiList>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * LayoutFooterColumn: one titled column of links in the classic footer. Every link uses the
 * shared footer link hover, so the columns read the same in light and dark.
 */
import type { FooterLink } from '~/types/footerlink'
import { UiFooterLinkVariant, UiHeadingLevel } from '~/utils/ui/enums'

/** Props of LayoutFooterColumn. */
interface Props {
  /** Stable column key, used in test ids (`footer-<column>-<link>`). */
  column: string
  /** Translated column heading. */
  title: string
  /** The links of the column, in display order. */
  links: FooterLink[]
}

defineProps<Props>()
</script>
