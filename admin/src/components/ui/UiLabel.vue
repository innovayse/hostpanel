<script setup lang="ts">
/**
 * Form label.
 *
 * `variant` picks the look: `field` is the 13px muted sentence-case caption above a control, `inline`
 * wraps a checkbox/radio with its text, `plain` (default) adds nothing. `as` picks the element: a native
 * `label` (default) for a control, a `span` for a caption tied to no control. Attributes (`for`, classes,
 * listeners) land on that element.
 */
import { computed } from 'vue'
import { labelVariantClasses } from '@/utils/ui/text'
import type { UiLabelTag, UiLabelVariant } from '@/utils/ui/text'

/** Props for UiLabel. */
const props = withDefaults(defineProps<{
  /** Visual look of the label. */
  variant?: UiLabelVariant
  /** Element to draw: `label` for a control's caption, `span` for a caption tied to no control. */
  as?: UiLabelTag
}>(), {
  variant: 'plain',
  as: 'label',
})

/** Classes of the chosen look. */
const classes = computed<string>(() => labelVariantClasses[props.variant])
</script>

<template>
  <component :is="as" :class="classes"><slot /></component>
</template>
