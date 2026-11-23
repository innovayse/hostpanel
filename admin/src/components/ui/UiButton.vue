<script setup lang="ts">
/**
 * Button with a named look.
 *
 * `variant` picks the look (primary gradient, secondary, ghost, danger, link) and `size` the
 * padding; both read theme tokens so the button follows light/dark. A button with no `variant` is a
 * `secondary` button, so it is never raw. `loading` shows a
 * spinner beside the label (the label stays), marks the button busy and blocks clicks; `block` stretches it to the container.
 * An action names its kind with `action` (`UiActionKind`, drawn from the one map in `utils/ui/actions`) or passes its own
 * `icon` (an `Icon` entry); the icon sits before the label and gives way to the spinner while `loading`. The `link` look
 * is the text look of a link inside prose; a link-look button that acts may still name its action.
 * `hint` is the full phrase of an action whose visible label is shortened because the icon already says the
 * word (`PDF` for "Download PDF"): it becomes the accessible name (`aria-label`) and the native tooltip (`title`).
 * The hint must contain the visible text (WCAG label in name), so a voice user saying the label still hits the button.
 * `data-testid` and listeners fall through to the native `<button>`.
 */
import { computed } from 'vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { resolveActionIcon } from '@/utils/ui/actions'
import type { UiActionKind } from '@/utils/ui/actions'
import { buttonBaseClasses, buttonRadiusClasses, buttonSpinnerClasses, buttonVariantClasses, UiButtonVariant } from '@/utils/ui/button'
import type { UiButtonType } from '@/utils/ui/button'
import { buttonLinkSizeClasses, buttonSizeClasses } from '@/utils/ui/size'
import type { UiSize } from '@/utils/ui/size'

/** Props for UiButton. */
const props = withDefaults(defineProps<{
  /** Visual look of the button; `secondary` when omitted. */
  variant?: UiButtonVariant
  /** Padding and type size. */
  size?: UiSize
  /** Native button type; omitted so a button inside a form keeps the browser default. */
  type?: UiButtonType
  /** Shows a spinner beside the label, marks the button busy and blocks clicks. */
  loading?: boolean
  /** Blocks clicks without showing progress. */
  disabled?: boolean
  /** Stretches the button to the full width of its container. */
  block?: boolean
  /** Kind of the action; the icon of the kind is drawn before the label. */
  action?: UiActionKind
  /** Own icon (an `Icon` entry) for an action with no kind; wins over the icon of `action`. */
  icon?: string
  /** Draws a button-like look as a pill (fully rounded ends), whatever its size. */
  pill?: boolean
  /** Full phrase for a shortened visible label; sets `aria-label` and `title`, and must contain the visible text. */
  hint?: string
}>(), {
  variant: UiButtonVariant.Secondary,
  size: 'md',
  type: undefined,
  loading: false,
  disabled: false,
  block: false,
  action: undefined,
  icon: undefined,
  pill: false,
  hint: undefined,
})

/** Markup of the icon drawn before the label; undefined for a button that names neither an action nor an icon. */
const glyph = computed<string | undefined>(() => resolveActionIcon(props.icon, props.action))

/** Class list for the chosen look and size; the link look keeps the type scale but has no padding. */
const classes = computed<string[]>(() => {
  const width = props.block ? 'w-full' : ''
  const sizing = props.variant === UiButtonVariant.Link ? buttonLinkSizeClasses[props.size] : buttonSizeClasses[props.size]
  return [buttonBaseClasses, buttonVariantClasses[props.variant], sizing, buttonRadiusClasses(props.variant, props.size, props.pill), width]
})
</script>

<template>
  <button
    :type="type"
    :disabled="disabled || loading"
    :aria-busy="loading || undefined"
    :aria-label="hint"
    :title="hint"
    :class="classes"
  >
    <span v-if="loading" :class="buttonSpinnerClasses" aria-hidden="true" data-testid="ui-button-spinner" />
    <UiIcon v-else-if="glyph" :markup="glyph" size="sm" />
    <slot />
  </button>
</template>
