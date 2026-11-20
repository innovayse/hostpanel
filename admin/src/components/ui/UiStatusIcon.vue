<script setup lang="ts">
/**
 * A yes/no state drawn as one icon, for a table column that would otherwise repeat a pill ("On", "Off", "Yes", "No").
 *
 * The icon is green when the state is on and muted when it is off, and the state is named for assistive technology and
 * on hover (`aria-label` and `title` of an `img` role), never in visible text, so a column of them stays as narrow as the
 * icon. The caller passes both icons and both labels, so the one component serves two-factor sign-in, an enabled switch or any
 * other boolean.
 */
import { computed } from 'vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import { UiSize } from '@/utils/ui/size'
import { textToneClasses, UiTextTone } from '@/utils/ui/text'
import { toneTextClasses, UiTone } from '@/utils/ui/tone'

/** Props for UiStatusIcon. */
const props = withDefaults(defineProps<{
  /** Whether the state is on. */
  state: boolean
  /** Icon markup drawn when the state is on (an entry of `Icon`). */
  onIcon: string
  /** Icon markup drawn when the state is off (an entry of `Icon`). */
  offIcon: string
  /** Name of the on state: the accessible name and the tooltip. */
  onLabel: string
  /** Name of the off state: the accessible name and the tooltip. */
  offLabel: string
  /** Edge length of the icon. */
  size?: UiSize
}>(), {
  size: UiSize.Md,
})

/** Icon of the current state. */
const markup = computed<string>(() => (props.state ? props.onIcon : props.offIcon))

/** Name of the current state. */
const label = computed<string>(() => (props.state ? props.onLabel : props.offLabel))

/** Green when on, muted when off. */
const toneClasses = computed<string>(() => (props.state ? toneTextClasses[UiTone.Success] : textToneClasses[UiTextTone.Muted]))
</script>

<template>
  <span role="img" class="inline-flex align-middle" :class="toneClasses" :aria-label="label" :title="label">
    <UiIcon :markup="markup" :size="size" />
  </span>
</template>
