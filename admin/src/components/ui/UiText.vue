<script setup lang="ts">
/**
 * Paragraph of text, or another text element through `as`.
 *
 * `tone` colours it from the theme tokens and `size` sets the type size; both default to `plain`
 * (no classes), so a screen that still passes its own typography renders unchanged.
 */
import { computed } from 'vue'
import { textSizeClasses, textToneClasses } from '@/utils/ui/text'
import { UiTextTag } from '@/utils/ui/text'
import type { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for UiText. */
const props = withDefaults(defineProps<{
  /** Text colour. */
  tone?: UiTextTone
  /** Type size. */
  size?: UiTextSize
  /** Element the text is drawn as; a paragraph by default, a `span` where only phrasing content is allowed (inside a button). */
  as?: UiTextTag
}>(), {
  tone: 'plain',
  size: 'plain',
  as: UiTextTag.P,
})

/** Classes of the chosen tone and size. */
const classes = computed<string[]>(() => [textToneClasses[props.tone], textSizeClasses[props.size]])
</script>

<template>
  <component :is="as" :class="classes"><slot /></component>
</template>
