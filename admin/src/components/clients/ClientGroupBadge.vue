<script setup lang="ts">
/**
 * A client group as a small pill: the group's colour as a dot, then its name. The one place a
 * group's name and colour are shown together, used by the groups list, the clients list and the
 * client profile.
 */
import UiBadge from '@/components/ui/UiBadge.vue'
import UiColorDot from '@/components/ui/UiColorDot.vue'
import { UiTone } from '@/utils/ui/tone'

/** Props for ClientGroupBadge. */
defineProps<{
  /** The group's display name. */
  name: string
  /** The group's colour as `#RRGGBB`. */
  color: string
}>()
</script>

<template>
  <UiBadge :tone="UiTone.Neutral" small>
    <UiColorDot :color="color" />
    {{ name }}
  </UiBadge>
</template>
