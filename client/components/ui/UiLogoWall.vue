<template>
  <UiBalancedGrid v-if="layout === UiLogoWallLayout.Grid" :count="items.length" class="!gap-x-8 !gap-y-6">
    <UiLogoWallItem v-for="item in items" :key="item.id" :item="item" :testid-prefix="testidPrefix" />
  </UiBalancedGrid>

  <div v-else-if="layout === UiLogoWallLayout.Row" class="flex flex-wrap items-center justify-center gap-x-6 gap-y-4">
    <UiLogoWallItem
      v-for="item in items"
      :key="item.id"
      :item="item"
      :testid-prefix="testidPrefix"
      class="!w-auto"
    />
  </div>

  <!-- The edges fade through a mask, so the strip needs no knowledge of the page colour. -->
  <div
    v-else
    class="relative overflow-hidden [mask-image:linear-gradient(to_right,transparent,black_8%,black_92%,transparent)]"
  >
    <!-- Two copies side by side loop without a gap; with reduced motion the first copy simply wraps and the second is hidden. -->
    <div class="flex w-max animate-marquee py-2 hover:[animation-play-state:paused] motion-reduce:w-auto motion-reduce:animate-none motion-reduce:flex-wrap motion-reduce:justify-center">
      <div
        v-for="(item, index) in looped"
        :key="index"
        class="mx-4 flex-shrink-0 sm:mx-6"
        :class="{ 'motion-reduce:hidden': index >= items.length }"
        :aria-hidden="index >= items.length ? 'true' : undefined"
      >
        <UiLogoWallItem :item="item" :testid-prefix="testidPrefix" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiLogoWall. The partner and "trusted by" strips: logos in one consistent muted style, with a
 * clean wordmark for a partner that has no logo. No cards or pills.
 *
 * Three layouts: a balanced grid for a few partners, a compact row for a strip, and a looping
 * marquee for many. The colour of the wordmarks is the text colour of the parent.
 */
import { computed } from 'vue'
import UiBalancedGrid from '~/components/ui/UiBalancedGrid.vue'
import UiLogoWallItem from '~/components/ui/UiLogoWallItem.vue'
import { UiLogoWallLayout } from '~/utils/ui/logoWall'
import type { LogoWallItem } from '~/utils/ui/logoWall'

/** Props of UiLogoWall. */
interface Props {
  /** The logos to draw, in display order. */
  items: readonly LogoWallItem[]
  /** Prefix of the cells' test ids (`partner` gives `partner-1` and `partner-logo-1`). */
  testidPrefix: string
  /** How the logos are laid out. */
  layout?: `${UiLogoWallLayout}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), { layout: UiLogoWallLayout.Grid })

/** The logos twice over, so the marquee loops without a gap. */
const looped = computed(() => [...props.items, ...props.items])
</script>
