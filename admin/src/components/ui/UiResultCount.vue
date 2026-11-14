<script setup lang="ts">
/**
 * The "N records found" line above or under a list.
 *
 * The count is the default slot, already written. While `loading` the line keeps its place and a bar of its length
 * stands in for the sentence, so the page never claims "0 records" before the first answer has arrived. `tone` and
 * `size` are those of `UiText`; the line is muted and body size by default.
 */
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiText from '@/components/ui/UiText.vue'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

/** Props for UiResultCount. */
withDefaults(defineProps<{
  /** Draws a bar in place of the sentence while the first answer is pending. */
  loading?: boolean
  /** Text colour of the sentence. */
  tone?: UiTextTone
  /** Type size of the sentence. */
  size?: UiTextSize
}>(), {
  loading: false,
  tone: UiTextTone.Muted,
  size: UiTextSize.Md,
})
</script>

<template>
  <UiSkeletonBar v-if="loading" class="h-3.5 w-36 rounded-md" aria-hidden="true" data-testid="ui-result-count-skeleton" />
  <UiText v-else :tone="tone" :size="size"><slot /></UiText>
</template>
