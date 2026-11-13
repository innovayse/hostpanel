<template>
  <UiLink :to="to" tone="muted" :underline="false" :testid="testid" :class="linkClasses">
    <ArrowLeft :size="compact ? 14 : 16" :stroke-width="2" />
    <slot>{{ label }}</slot>
  </UiLink>
</template>

<script setup lang="ts">
/**
 * UiBackLink. The "back" link above a detail page or under an auth form: an arrow and a label.
 *
 * The label comes from the `label` prop or, for a link that holds richer content, the default
 * slot. `compact` is the tighter variant the auth screens use.
 */
import { computed } from 'vue'
import { ArrowLeft } from 'lucide-vue-next'

/** Props of UiBackLink. */
interface Props {
  /** Route the link returns to. */
  to: string
  /** Text of the link; the default slot replaces it. */
  label?: string
  /** `data-testid` of the link. */
  testid?: string
  /** A smaller arrow and a tighter gap, as under an auth form. */
  compact?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  label: undefined,
  testid: undefined,
  compact: false,
})

/** Class list of the link: the gap follows the arrow size. */
const linkClasses = computed(() => `inline-flex items-center text-sm ${props.compact ? 'gap-1.5' : 'gap-2'}`)
</script>
