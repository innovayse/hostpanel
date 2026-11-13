<template>
  <div
    aria-hidden="true"
    :data-variant="variant"
    data-testid="ui-backdrop"
    class="pointer-events-none absolute inset-0 overflow-hidden"
    :class="isNova ? 'bg-gradient-to-b from-nova-surface-2 to-nova-bg' : ''"
  >
    <template v-if="isAurora">
      <div class="ui-backdrop-glow absolute -top-40 -left-40 h-[32rem] w-[32rem] rounded-full bg-glow1 opacity-70 blur-3xl" />
      <div class="ui-backdrop-glow absolute -bottom-40 -right-40 h-[32rem] w-[32rem] rounded-full bg-glow2 opacity-70 blur-3xl" />
    </template>
    <div v-else-if="isGrid" class="absolute inset-0 pattern-grid-50" />
    <div v-else-if="isDots" class="ui-backdrop-dots absolute inset-0 pattern-dots-30" />
  </div>
</template>

<script setup lang="ts">
/**
 * UiBackdrop: a decorative background for marketing sections (aurora glows, nova wash, grid,
 * dots). It fills its nearest positioned ancestor, never takes pointer events and is hidden from
 * assistive technology. Colours come from the template tokens, so it follows light and dark.
 */
import { computed } from 'vue'
import { UiBackdropVariant } from '~/utils/ui/enums'

/** Props of UiBackdrop. */
interface Props {
  /** Which decoration to draw. */
  variant?: `${UiBackdropVariant}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  variant: UiBackdropVariant.Aurora,
})

/** Whether the aurora glows are drawn. */
const isAurora = computed(() => props.variant === UiBackdropVariant.Aurora)

/** Whether the nova wash is drawn. */
const isNova = computed(() => props.variant === UiBackdropVariant.Nova)

/** Whether the grid lines are drawn. */
const isGrid = computed(() => props.variant === UiBackdropVariant.Grid)

/** Whether the dot matrix is drawn. */
const isDots = computed(() => props.variant === UiBackdropVariant.Dots)
</script>
