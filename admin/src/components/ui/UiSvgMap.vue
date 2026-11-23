<script setup lang="ts">
/**
 * An interactive map drawn from SVG outlines (a world map of countries).
 *
 * Each shape is one outline filled with its own Tailwind class. A shape with a `label` is a control: it can be focused,
 * is named for assistive technology, reports the pointer or focus entering and leaving it (`activate`, `deactivate`) and
 * a click, Enter or Space on it (`select`). A shape without a label is decoration and is hidden from assistive technology.
 * The map owns no data and no highlight: the screen decides each fill and what a choice does.
 */
import { mapShapeFocusClasses } from '@/utils/ui/svgMap'
import type { UiMapShape } from '@/utils/ui/svgMap'

/** Props for UiSvgMap. */
defineProps<{
  /** The SVG `viewBox` the outlines are drawn in, e.g. `0 0 1010 666`. */
  viewBox: string
  /** Name of the whole map for assistive technology (already translated). */
  label: string
  /** The outlines, drawn in order (later ones over earlier ones). */
  shapes: readonly UiMapShape[]
}>()

/** Events emitted by UiSvgMap. */
const emit = defineEmits<{
  /** The pointer or focus entered a labelled outline; carries its id. */
  activate: [id: string]
  /** The pointer or focus left the outline. */
  deactivate: []
  /** A labelled outline was chosen; carries its id. */
  select: [id: string]
}>()
</script>

<template>
  <svg :viewBox="viewBox" class="w-full" role="group" :aria-label="label">
    <template v-for="shape in shapes" :key="shape.id">
      <path
        v-if="shape.label === undefined"
        :d="shape.path"
        :class="[shape.fill, 'stroke-surface-card']"
        stroke-width="0.3"
        aria-hidden="true"
        :data-testid="shape.testid"
      />
      <path
        v-else
        :d="shape.path"
        :class="[shape.fill, 'stroke-surface-card cursor-pointer', mapShapeFocusClasses]"
        stroke-width="0.3"
        role="button"
        tabindex="0"
        :aria-label="shape.label"
        :data-testid="shape.testid"
        @mouseenter="emit('activate', shape.id)"
        @mouseleave="emit('deactivate')"
        @focus="emit('activate', shape.id)"
        @blur="emit('deactivate')"
        @click="emit('select', shape.id)"
        @keydown.enter.prevent="emit('select', shape.id)"
        @keydown.space.prevent="emit('select', shape.id)"
      />
    </template>
  </svg>
</template>
