<template>
  <div
    class="inline-flex items-center gap-1 rounded-full bg-classic-surface/35 px-1 py-0.5"
    role="group"
    data-testid="carousel-dots"
  >
    <button
      v-for="index in count"
      :key="index"
      type="button"
      :aria-label="$t('common.goToSlide', { n: index })"
      :aria-current="index - 1 === active ? 'true' : undefined"
      :class="[
        'relative h-2 rounded-full transition-[width,background-color] duration-300 motion-reduce:transition-none',
        'after:absolute after:-inset-x-1 after:-inset-y-2 after:content-[\'\']',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-classic-brand focus-visible:ring-offset-2 focus-visible:ring-offset-classic-page',
        index - 1 === active ? 'w-6 bg-classic-fill' : 'w-2 bg-classic-muted/45 hover:bg-classic-muted/80',
      ]"
      :data-testid="`carousel-dot-${index - 1}`"
      @click="emit('select', index - 1)"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * UiCarouselDots: the pagination dots of a carousel.
 *
 * Small round dots in a muted token; the active one stretches to a pill in the classic fill. One colour
 * logic: a dot is the current slide or it is not (no "visited" colour). Every dot is a real button with a
 * "Go to slide N" label, `aria-current` on the active one and a focus ring. The component only draws
 * and reports: the carousel owns the position and moves when `select` fires.
 */

/** Props of UiCarouselDots. */
interface Props {
  /** How many slides there are. */
  count: number
  /** Zero-based index of the active slide. */
  active: number
}

defineProps<Props>()

/** Events of UiCarouselDots. */
const emit = defineEmits<{
  /** A dot was pressed; carries the zero-based index of its slide. */
  select: [index: number]
}>()
</script>
