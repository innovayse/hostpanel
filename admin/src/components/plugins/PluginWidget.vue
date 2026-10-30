<script setup lang="ts">
/**
 * One plugin widget in a slot: its title, a Hide button and the body the parent supplies.
 *
 * The widget is its own failure domain. The body is mounted under `<Suspense>`, so it shows its own
 * skeleton while it loads; a body that throws, or does not settle within {@link WIDGET_TIMEOUT_MS},
 * is replaced by a neutral note inside this card only. The error is stopped here (`onErrorCaptured`
 * returns false), so the page and the sibling widgets keep rendering. The timer is cleared when the
 * body settles and when the widget is destroyed.
 */
import { onBeforeUnmount, onErrorCaptured, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { PluginSlotWidgetDto } from '@/types/pluginslot'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UiAlignItems, UiJustify } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'

/** How long a body may stay pending before the widget gives up on it. */
const WIDGET_TIMEOUT_MS = 10_000

/** Icon markup (shape tags) of the hide button. */
const HIDE_ICON = '<path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24" /><line x1="1" y1="1" x2="23" y2="23" />'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginWidget. */
const props = defineProps<{
  /** The widget to show. */
  widget: PluginSlotWidgetDto
}>()

/** Events emitted by PluginWidget. */
const emit = defineEmits<{
  /** Emitted when the operator asks to hide the widget. */
  hide: []
}>()

/** Sentence replacing the body after a failure, null while the body is fine. */
const failure = ref<string | null>(null)

/** Pending timeout of the body, null when none runs. */
let timer: ReturnType<typeof setTimeout> | null = null

/**
 * Stops the pending timeout, if any.
 *
 * @returns Nothing.
 */
const stopTimer = (): void => {
  if (timer !== null) {
    clearTimeout(timer)
    timer = null
  }
}

/**
 * Starts the timeout when the body begins to load; a body still pending when it fires is dropped.
 *
 * @returns Nothing.
 */
const startTimer = (): void => {
  stopTimer()
  timer = setTimeout(() => {
    timer = null
    failure.value = t('plugins.pluginSlot.timedOut')
  }, WIDGET_TIMEOUT_MS)
}

onErrorCaptured(() => {
  stopTimer()
  failure.value = t('plugins.pluginSlot.unavailable')
  // The failure ends here: the page and the other widgets never see it.
  return false
})

onBeforeUnmount(stopTimer)

/** The widget's id in test ids. */
const testId = `${props.widget.pluginId}-${props.widget.widgetId}`
</script>

<template>
  <UiCard padding="md" :data-testid="`plugin-widget-${testId}`">
    <UiStack>
      <UiStack direction="row" :align="UiAlignItems.Center" :justify="UiJustify.Between">
        <UiHeading :level="2" variant="card">{{ widget.label }}</UiHeading>
        <UiIconButton
          :label="t('plugins.pluginSlot.hide')"
          :size="UiSize.Sm"
          :data-testid="`plugin-widget-hide-${testId}`"
          @click="emit('hide')"
        >
          <UiIcon :markup="HIDE_ICON" />
        </UiIconButton>
      </UiStack>
      <UiStatusMessage
        v-if="failure !== null"
        :message="failure"
        :tone="UiTone.Warning"
        :data-testid="`plugin-widget-note-${testId}`"
      />
      <Suspense v-else @pending="startTimer" @resolve="stopTimer">
        <slot />
        <template #fallback>
          <UiSkeleton :lines="2" :data-testid="`plugin-widget-loading-${testId}`" />
        </template>
      </Suspense>
    </UiStack>
  </UiCard>
</template>
