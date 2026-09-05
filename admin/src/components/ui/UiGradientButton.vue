<script setup lang="ts">
/**
 * @description
 * The panel's primary brand-gradient action button, with a built-in loading state.
 *
 * The three bouncing dots and the gradient shadow were pasted into the login and
 * setup screens separately; drawing them twice meant fixing them twice. The shadow is
 * a Tailwind arbitrary value rather than a `style` attribute so nothing about this
 * button's appearance lives outside its own class list.
 */

const props = withDefaults(defineProps<{
  /** Native button type — 'submit' inside a form, 'button' otherwise. */
  type?: 'button' | 'submit'
  /** Replaces the label with a progress indicator and blocks clicks. */
  loading?: boolean
  /** Blocks clicks without showing progress. */
  disabled?: boolean
  /** Renders the muted, secondary treatment instead of the brand gradient. */
  variant?: 'brand' | 'quiet'
}>(), {
  type: 'button',
  loading: false,
  disabled: false,
  variant: 'brand',
})

const emit = defineEmits<{
  /** Fired on a real click, i.e. not while loading or disabled. */
  click: []
}>()

/** Class list for the chosen variant. */
const variantClasses: Record<string, string> = {
  brand: 'text-white gradient-brand shadow-[0_4px_20px_rgba(14,165,233,0.25)]',
  quiet: 'text-text-secondary bg-white/[0.04] border border-white/[0.08] hover:bg-white/[0.07]',
}

/**
 * Forwards the click, unless the button is busy or disabled.
 *
 * @returns Nothing.
 */
const handleClick = (): void => {
  if (props.loading || props.disabled) return
  emit('click')
}
</script>

<template>
  <button
    :type="props.type"
    :disabled="props.loading || props.disabled"
    class="w-full py-3 rounded-[10px] font-display font-semibold text-[0.95rem] border-none cursor-pointer transition-all duration-200 hover:-translate-y-px disabled:opacity-50 disabled:cursor-not-allowed disabled:translate-y-0"
    :class="variantClasses[props.variant]"
    @click="handleClick"
  >
    <span v-if="!props.loading">
      <slot />
    </span>
    <span v-else class="flex items-center justify-center gap-1.5">
      <span class="w-1.5 h-1.5 bg-current rounded-full animate-bounce [animation-delay:0s]" />
      <span class="w-1.5 h-1.5 bg-current rounded-full animate-bounce [animation-delay:0.15s]" />
      <span class="w-1.5 h-1.5 bg-current rounded-full animate-bounce [animation-delay:0.3s]" />
    </span>
  </button>
</template>
