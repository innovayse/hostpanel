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
import { computed, onBeforeUnmount, onErrorCaptured, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { Icon } from '@/lib/icons'
import type { PluginSlotWidgetDto } from '@/types/pluginslot'
import UiCard from '@/components/ui/UiCard.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiSectionHeader from '@/components/ui/UiSectionHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UiCardPadding } from '@/utils/ui/card'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'

/** How long a body may stay pending before the widget gives up on it. */
const WIDGET_TIMEOUT_MS = 10_000

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
const testId = `${props.widget.pluginId}-${props.widget.id}`

/**
 * The widget's heading: its own title, or the plugin and widget ids when it declares none, the way the
 * plugin settings page names an untitled widget.
 *
 * @returns The text of the heading.
 */
const heading = computed<string>(() =>
  props.widget.title ?? t('plugins.pluginSlotWidgets.pluginWidget', { plugin: props.widget.pluginId, widget: props.widget.id }),
)
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" :data-testid="`plugin-widget-${testId}`">
    <UiStack>
      <UiSectionHeader :level="2" variant="card">
        {{ heading }}
        <template #actions>
          <UiIconButton
            :label="t('plugins.pluginSlot.hide')"
            :size="UiSize.Sm"
            :data-testid="`plugin-widget-hide-${testId}`"
            @click="emit('hide')"
          >
            <UiIcon :markup="Icon.EyeOff" />
          </UiIconButton>
        </template>
      </UiSectionHeader>
      <UiStatusMessage
        v-if="failure !== null"
        :message="failure"
        :tone="UiTone.Warning"
        :data-testid="`plugin-widget-note-${testId}`"
      />
      <Suspense v-else @pending="startTimer" @resolve="stopTimer">
        <slot />
        <template #fallback>
          <!-- The widget's body is drawn by the plugin, so its shape is not known: two lines stand in until it resolves -->
          <UiStack gap="sm" aria-busy="true" aria-hidden="true" :data-testid="`plugin-widget-loading-${testId}`">
            <UiSkeletonBar class="h-3.5 w-full rounded-md" />
            <UiSkeletonBar class="h-3.5 w-2/3 rounded-md" />
          </UiStack>
        </template>
      </Suspense>
    </UiStack>
  </UiCard>
</template>
