<script setup lang="ts">
/**
 * Two-column layout (main area and aside) that stacks on narrow screens.
 *
 * Put exactly two children in the default slot; `ratio` decides how the width is shared. For an aside that should
 * not pile up under the main area on a phone, pass `asideTitle` and the aside as the `aside` slot instead: from the
 * `lg` breakpoint up it is the second column, a card headed by the title, below it it leaves the page and opens in
 * a bottom sheet (`UiDrawer`, same title) that the screen controls with `v-model:open`, from a button of its own.
 */
import { computed } from 'vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { gapClasses, splitRatioClasses, UI_SPLIT_COMPACT_QUERY } from '@/utils/ui/layout'
import type { UiGap, UiSplitRatio } from '@/utils/ui/layout'

/** Props for UiSplit. */
const props = withDefaults(defineProps<{
  /** Width share of main : aside. */
  ratio?: UiSplitRatio
  /** Space between the columns. */
  gap?: UiGap
  /** Title of the aside (already translated); giving it turns the `aside` slot into a sheet below `lg`. */
  asideTitle?: string
}>(), {
  ratio: 'two-thirds',
  gap: 'lg',
  asideTitle: undefined,
})

/** Whether the sheet is open, two-way bound with `v-model:open`. */
const open = defineModel<boolean>('open', { default: false })

/** Whether the screen is below `lg`. */
const compact = useMediaQuery(UI_SPLIT_COMPACT_QUERY)

/** Whether the aside currently lives in the sheet instead of the second column. */
const sheeted = computed<boolean>(() => props.asideTitle !== undefined && compact.value)

/** Layout classes. */
const classes = computed<string[]>(() => [splitRatioClasses[props.ratio], gapClasses[props.gap]])
</script>

<template>
  <div class="grid" :class="classes">
    <slot />
    <template v-if="!sheeted">
      <UiCard v-if="asideTitle !== undefined" padding="md" class="self-start">
        <UiStack>
          <UiHeading :level="2" variant="card">{{ asideTitle }}</UiHeading>
          <slot name="aside" />
        </UiStack>
      </UiCard>
      <slot v-else name="aside" />
    </template>
    <UiDrawer v-else sheet :open="open" :title="asideTitle ?? ''" @close="open = false">
      <slot name="aside" />
    </UiDrawer>
  </div>
</template>
