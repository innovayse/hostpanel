<script setup lang="ts">
/**
 * A row of small chips that always stays on one line: as many chips as fit are drawn and a "+N" chip stands for the
 * rest, its hint listing them. A chip never wraps inside itself and the chips keep one gap between them, so the row
 * never overflows its container, whatever the length of the labels.
 *
 * The fit is arithmetic over widths measured once: a hidden copy of every chip (and of the widest "+N" chip) is
 * measured when the labels change, and a resize only compares the row's width with those numbers, so no layout is read
 * while the row is being resized. The row's width comes from a `ResizeObserver`, which reports at most once per frame
 * and also when a hidden row (a background tab) becomes visible, which is when the chips are measured if they could not be before.
 * Until the first measurement the row is invisible, so it never flashes a wrong count.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { fitChipCount } from '@/utils/ui/chipRow'
import { UiTone } from '@/utils/ui/tone'

/** Props for UiChipRow. */
const props = defineProps<{
  /** The chip labels, already translated, in order. */
  chips: string[]
}>()

/** The row, whose width is the room the chips share. */
const row = ref<HTMLElement | null>(null)

/** The hidden copy of the chips the widths are measured from. */
const ruler = ref<HTMLElement | null>(null)

/** Natural width of each chip, in the order of `chips`. */
const widths = ref<number[]>([])

/** Natural width of the "+N" chip, measured for the largest N so it never grows past what was reserved. */
const moreWidth = ref(0)

/** Space between two chips, read from the hidden copy. */
const gap = ref(0)

/** Width of the row. */
const available = ref(0)

/** True once the chips have been measured. */
const measured = ref(false)

/** How many leading chips are drawn. */
const visibleCount = computed<number>(() =>
  measured.value ? fitChipCount(widths.value, moreWidth.value, available.value, gap.value) : props.chips.length,
)

/** The chips that are drawn. */
const shownChips = computed<string[]>(() => props.chips.slice(0, visibleCount.value))

/** The chips the "+N" chip stands for. */
const hiddenChips = computed<string[]>(() => props.chips.slice(visibleCount.value))

/** Reads the width of every chip of the hidden copy, and the width of its last child, the "+N" chip; does nothing while the row is hidden. */
const measure = (): void => {
  const copy = ruler.value
  if (copy === null) {
    return
  }
  const children = Array.from(copy.children) as HTMLElement[]
  const more = children.at(-1)?.offsetWidth ?? 0
  // A hidden row (a background tab, a closed panel) lays nothing out, so every width reads 0: keep waiting for the
  // row to show, the observer measures then.
  if (more === 0) {
    return
  }
  widths.value = children.slice(0, -1).map(child => child.offsetWidth)
  moreWidth.value = more
  gap.value = parseFloat(getComputedStyle(copy).columnGap) || 0
  measured.value = true
}

/** Follows the width of the row. */
let observer: ResizeObserver | null = null

/** Measures the chips once the hidden copy exists and starts following the width of the row. */
onMounted(() => {
  measure()
  if (row.value !== null) {
    available.value = row.value.clientWidth
    observer = new ResizeObserver((entries) => {
      available.value = entries[0]?.contentRect.width ?? available.value
      // The row showing up after being hidden is a resize from 0: that is the moment the chips can be measured.
      if (!measured.value) {
        measure()
      }
    })
    observer.observe(row.value)
  }
})

/** Stops following the row. */
onBeforeUnmount(() => {
  observer?.disconnect()
})

/** New labels mean new widths: measure again once the hidden copy has been redrawn. */
watch(() => props.chips.join('\u0000'), async () => {
  await nextTick()
  measure()
})
</script>

<template>
  <div ref="row" class="relative flex w-full min-w-0 items-center gap-1" :class="measured ? '' : 'invisible'">
    <UiBadge v-for="chip in shownChips" :key="chip" :tone="UiTone.Neutral" small data-testid="ui-chip-row-chip">
      {{ chip }}
    </UiBadge>
    <UiTooltip v-if="hiddenChips.length > 0" :text="hiddenChips.join(', ')">
      <UiBadge :tone="UiTone.Neutral" small tabindex="0" data-testid="ui-chip-row-more">+{{ hiddenChips.length }}</UiBadge>
    </UiTooltip>
    <span aria-hidden="true" class="pointer-events-none invisible absolute left-0 top-0 h-0 w-0 overflow-hidden">
      <span ref="ruler" class="flex gap-1">
        <UiBadge v-for="chip in chips" :key="chip" :tone="UiTone.Neutral" small>{{ chip }}</UiBadge>
        <UiBadge :tone="UiTone.Neutral" small>+{{ chips.length }}</UiBadge>
      </span>
    </span>
  </div>
</template>
