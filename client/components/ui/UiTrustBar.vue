<template>
  <div :class="['rounded-2xl px-6 py-5', surfaceClass]" data-testid="trust-bar">
    <ul :class="['grid grid-cols-1 justify-center gap-4 sm:gap-x-10', columnClass]">
      <li
        v-for="item in items"
        :key="item.key"
        class="flex items-center gap-3"
        :class="textClass"
        data-testid="trust-item"
      >
        <UiTile size="lg" :tone="item.tone" :class="item.tone ? '' : chipClass" data-testid="trust-chip">
          <Icon :name="item.icon" class="h-5 w-5" aria-hidden="true" />
        </UiTile>
        <span class="text-sm font-semibold">
          <strong v-if="item.value" class="font-bold">{{ item.value }}</strong>
          {{ item.label }}
        </span>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
/**
 * UiTrustBar: a card of short guarantees, each with an icon chip. The items are centred with
 * even gaps instead of stretched over the container, and stack on a phone. The card has a
 * single hairline border and no drop shadow, so nothing shows under its bottom edge.
 */
import { computed } from 'vue'
import type { UiTone } from '~/utils/ui/enums'

/** One guarantee of the bar. */
interface TrustItem {
  /** Stable key. */
  key: string
  /** Icon name for the chip. */
  icon: string
  /** The guarantee. */
  label: string
  /** Optional figure printed in bold before the label (an uptime number). */
  value?: string
  /** Tone of the chip; when omitted `chipClass` paints it. */
  tone?: `${UiTone}`
}

/** Props of UiTrustBar. */
interface Props {
  /** The guarantees. */
  items: TrustItem[]
  /** Surface classes: background and border; empty draws no card. */
  surfaceClass?: string
  /** Text colour classes of each item. */
  textClass?: string
  /** Chip colour classes for items without a tone. */
  chipClass?: string
}

/** Props with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  surfaceClass: 'border border-gray-200 bg-ui-surface dark:border-white/10',
  textClass: 'text-gray-700 dark:text-gray-200',
  chipClass: 'bg-primary-500/10 text-primary-600 dark:text-primary-400',
})

/**
 * Column tracks per item count (literal classes so Tailwind emits them). Tracks are as wide as
 * their content and the grid is centred, so rows are balanced (3 + 3 for six, never 5 + 1) and
 * the items are not stretched over the container.
 */
const COLUMN_CLASSES: Record<number, string> = {
  1: 'sm:grid-cols-[repeat(1,max-content)]',
  2: 'sm:grid-cols-[repeat(2,max-content)]',
  3: 'sm:grid-cols-[repeat(3,max-content)]',
  4: 'sm:grid-cols-[repeat(2,max-content)] lg:grid-cols-[repeat(4,max-content)]',
  5: 'sm:grid-cols-[repeat(2,max-content)] lg:grid-cols-[repeat(3,max-content)]',
  6: 'sm:grid-cols-[repeat(2,max-content)] lg:grid-cols-[repeat(3,max-content)]',
  7: 'sm:grid-cols-[repeat(2,max-content)] lg:grid-cols-[repeat(4,max-content)]',
  8: 'sm:grid-cols-[repeat(2,max-content)] lg:grid-cols-[repeat(4,max-content)]',
}

/** Column classes for the current item count; larger sets use the eight-item layout. */
const columnClass = computed(() => COLUMN_CLASSES[Math.min(Math.max(props.items.length, 1), 8)])
</script>

