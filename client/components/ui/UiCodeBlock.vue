<template>
  <component :is="as" class="block overflow-x-auto rounded bg-gray-900 px-3 py-2 font-mono text-xs text-gray-100">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiCodeBlock: a dark, horizontally scrolling block for a command or a snippet.
 *
 * Renders a `code` element by default; pass `as="pre"` for a multi-line snippet whose line breaks
 * must be kept. Width, flex sizing and wrapping are set by the caller through its class.
 */
import { UiCodeBlockTag } from '~/utils/ui/enums'

/** Props of UiCodeBlock. */
interface Props {
  /** Tag to render, `code` or `pre`. */
  as?: `${UiCodeBlockTag}`
}

withDefaults(defineProps<Props>(), {
  as: UiCodeBlockTag.Code,
})
</script>
