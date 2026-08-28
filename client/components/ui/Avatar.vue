<template>
  <span
    :class="sizeClasses"
    class="grid flex-shrink-0 place-items-center rounded-full bg-primary-500/20 font-bold uppercase leading-none text-primary-400 select-none"
    :title="label || undefined"
    aria-hidden="true"
  >{{ initials }}</span>
</template>

<script setup lang="ts">
/**
 * Circular identity avatar showing a person's initials.
 *
 * Purely presentational — it takes an already-resolved name and e-mail rather
 * than reading the session itself, so the same component serves the public
 * header, the client-area shell and anywhere a person has to be shown. Marked
 * `aria-hidden` because it never carries information the surrounding control
 * does not already name; the control it sits inside supplies the accessible
 * label.
 *
 * Written because the initials circle was being hand-drawn in the header with
 * its own colour, radius and size — exactly what `ui-components.md` calls out:
 * "If it appears once but has a colour, a radius, a size or a shadow in it, it
 * is still a component."
 */

const props = withDefaults(defineProps<{
  /** Given name, used for the first initial. */
  firstName?: string
  /** Family name, used for the second initial when present. */
  lastName?: string
  /** E-mail address, used only when no name is known yet. */
  email?: string
  /** Rendered diameter. `sm` suits a dense list, `md` a header control. */
  size?: 'sm' | 'md'
}>(), {
  firstName: '',
  lastName: '',
  email: '',
  size: 'md',
})

/** Full name when one is known, otherwise the e-mail — used as the hover title. */
const label = computed(() =>
  [props.firstName, props.lastName].filter(Boolean).join(' ') || props.email)

/**
 * One or two initials.
 *
 * Falls back through name → e-mail → `?` rather than rendering an empty circle:
 * the avatar appears the moment the session is known, which is before the
 * profile request that carries the name has come back.
 */
const initials = computed(() => {
  const first = props.firstName.trim().charAt(0)
  const last = props.lastName.trim().charAt(0)

  if (first) return `${first}${last}`
  return props.email.trim().charAt(0) || '?'
})

/** Diameter and type size for the chosen `size`. */
const sizeClasses = computed(() =>
  props.size === 'sm' ? 'h-6 w-6 text-[11px]' : 'h-9 w-9 text-[13px]')
</script>
