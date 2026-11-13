<script setup lang="ts">
/**
 * A spinning ring that says something is loading.
 *
 * A busy status region whose label defaults to the shared "Loading" (`ui.loading`), so a screen reader announces
 * it once. A caller whose server tells what is going on passes that text as `label` and `showLabel` to write it
 * beside the ring; otherwise the label is only spoken. The ring stands still for readers who ask for reduced motion.
 */
import { useI18n } from 'vue-i18n'
import { loaderSizeClasses } from '@/utils/ui/loader'
import { UiSize } from '@/utils/ui/size'

/** Props for UiLoader. */
const props = withDefaults(defineProps<{
  /** What is going on (already translated, from the server); defaults to the shared "Loading". */
  label?: string
  /** Diameter of the ring. */
  size?: UiSize
  /** Writes the label beside the ring instead of leaving it to screen readers. */
  showLabel?: boolean
}>(), {
  label: undefined,
  size: UiSize.Sm,
  showLabel: false,
})

/** Translation function. */
const { t } = useI18n()
</script>

<template>
  <span class="inline-flex items-center gap-2" role="status" aria-busy="true" data-testid="ui-loader">
    <span
      class="shrink-0 animate-spin rounded-full border-2 border-text-accent/20 border-t-text-accent motion-reduce:animate-none"
      :class="loaderSizeClasses[size]"
      aria-hidden="true"
      data-testid="ui-loader-ring"
    />
    <span :class="showLabel ? undefined : 'sr-only'" data-testid="ui-loader-label">{{ props.label ?? t('ui.loading') }}</span>
  </span>
</template>
