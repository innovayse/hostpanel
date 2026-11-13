<template>
  <component :is="ordered ? 'ol' : 'ul'" :class="classes">
    <slot />
  </component>
</template>

<script setup lang="ts">
/**
 * UiList: a bulleted, numbered, plain or divided list; fill it with UiListItem.
 */
import { computed } from 'vue'
import { UiListStyle } from '~/utils/ui/enums'

/** Props of UiList. */
interface Props {
  /** Numbered (ol) instead of bulleted (ul). */
  ordered?: boolean
  /** Marker style. */
  listStyle?: `${UiListStyle}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  ordered: false,
  listStyle: UiListStyle.Marked,
})

/** Class list of the root element. */
const classes = computed(() => {
  if (props.listStyle === UiListStyle.Divided) {
    return 'divide-y divide-gray-200 dark:divide-white/10'
  }
  if (props.listStyle === UiListStyle.Bare) {
    return ''
  }
  if (props.listStyle === UiListStyle.Plain) {
    return 'space-y-2'
  }
  return (props.ordered ? 'list-decimal' : 'list-disc') + ' pl-5 space-y-1.5'
})
</script>
