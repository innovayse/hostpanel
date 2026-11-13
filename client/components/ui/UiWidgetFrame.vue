<template>
  <div ref="el" :class="classes" data-testid="ui-widget-frame" />
</template>

<script setup lang="ts">
/**
 * UiWidgetFrame: a framed, empty box a third-party widget mounts itself into.
 *
 * A payment card element or a captcha draws into a DOM node it is given, so the screen needs a
 * node it can hold a reference to without writing a styled `div` of its own. The node is exposed
 * as `el`; the frame only owns the look: neutral, "ready" once the widget has drawn, "invalid"
 * when the widget reports a problem.
 */
import { computed, ref } from 'vue'

/** Props of UiWidgetFrame. */
interface Props {
  /** The widget has drawn itself and can take input. */
  ready?: boolean
  /** The widget reports a problem with what was entered. */
  invalid?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  ready: false,
  invalid: false,
})

/** The box the widget mounts into. */
const el = ref<HTMLDivElement | null>(null)

/** Frame colour for the current state; an error outranks readiness. */
const stateClasses = computed(() => {
  if (props.invalid) {
    return 'border-red-500 dark:border-red-400/60'
  }
  return props.ready ? 'border-cyan-600/50 dark:border-cyan-400/40' : 'border-gray-300 dark:border-white/10'
})

/** Class list of the box. */
const classes = computed(() =>
  `min-h-[44px] rounded-2xl border bg-ui-surface p-4 transition-colors ${stateClasses.value}`)

defineExpose({ el })
</script>
