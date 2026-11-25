<script setup lang="ts">
/**
 * A yes/no state of a record drawn as a labelled badge: the shared cell of every boolean status column of a list.
 *
 * The state is named in text, never by an icon or a colour alone: a green badge with the `onLabel` (Published, Active,
 * Enabled, Yes) when the state is on and a neutral one with the `offLabel` (Draft, Inactive, Disabled, No) when it is
 * off. It is the same one line `UiBadge` as the other status columns of a list, so a screen passes the state and the two
 * texts its domain needs and draws nothing itself.
 */
import { computed } from 'vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import { UiTone } from '@/utils/ui/tone'

/** Props for UiStatusBadge. */
const props = defineProps<{
  /** Whether the state is on. */
  state: boolean
  /** Text of the on state (already translated). */
  onLabel: string
  /** Text of the off state (already translated). */
  offLabel: string
}>()

/** Green when on, neutral when off. */
const tone = computed<UiTone>(() => (props.state ? UiTone.Success : UiTone.Neutral))

/** Text of the current state. */
const label = computed<string>(() => (props.state ? props.onLabel : props.offLabel))
</script>

<template>
  <UiBadge :tone="tone" small>{{ label }}</UiBadge>
</template>
