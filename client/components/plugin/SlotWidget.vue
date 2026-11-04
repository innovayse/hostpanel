<script setup lang="ts">
/**
 * PluginSlotWidget - one plugin widget on a host page, isolated from the page and from the other
 * widgets. It reads its own view after it mounts and shows its own loading state; an error while
 * it renders is caught here and goes no further; a view that cannot be read, or a view and first
 * data that have not all arrived within the time limit, give way to a small neutral note. Either
 * way the host page and the other widgets stay as they are.
 *
 * Late arrival, decided: once the note is shown it stays for this mount. A view or data that turns
 * up after the limit is dropped, so a widget never flickers from the note back to content; a new
 * context (another service) starts a fresh attempt.
 */
import { onErrorCaptured } from 'vue'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginSlotId } from '~/types/pluginslotid'
import type { PluginSlotWidget } from '~/types/pluginslotwidget'
import type { PluginSlotWidgetView } from '~/types/pluginslotwidgetview'

/** Props of PluginSlotWidget. */
interface Props {
  /** The slot the widget sits in. */
  slotId: PluginSlotId
  /** The widget, as the slot index lists it. */
  widget: PluginSlotWidget
  /** The slot's context, e.g. `{ serviceId: '4' }`: sent with the view read, and what the widget's routes are filled from. */
  context?: Record<string, string>
  /** How long the view and the widget's first data may take together, in milliseconds. */
  timeoutMs?: number
}

/** The props. */
const props = withDefaults(defineProps<Props>(), { context: () => ({}), timeoutMs: 5000 })

/** The store the view is read through. */
const store = usePluginsStore()

/** The widget's view; null until it arrived. */
const view = ref<PluginSlotWidgetView | null>(null)
/** Whether the widget failed, could not be read or was too slow; final for the current attempt. */
const unavailable = ref(false)
/** The first block reads still running. */
const pending = ref(0)
/** The time limit of the current attempt; cleared once everything arrived, on a new attempt and on unmount. */
let deadline: ReturnType<typeof setTimeout> | undefined
/** The number of the current attempt; a reply to an older one is dropped. */
let attempt = 0

/** A `data-testid` suffix naming the plugin and the widget. */
const testid = computed(() => `${props.widget.pluginId}-${props.widget.id}`)

/**
 * Stops the time limit.
 *
 * @returns Nothing.
 */
const stopDeadline = (): void => {
  clearTimeout(deadline)
  deadline = undefined
}

/**
 * Counts a block's first read while it runs, so the time limit can tell a slow widget from a ready one.
 *
 * @param read - The read.
 * @returns Nothing.
 */
const track = (read: Promise<void>): void => {
  pending.value += 1
  void read.finally(() => {
    pending.value -= 1
  })
}

/**
 * Starts an attempt: clears what the last one showed, arms the time limit and reads the view. A
 * failed read is the neutral note, never an error on the page.
 *
 * @returns Resolves when the read has ended.
 */
const start = async (): Promise<void> => {
  attempt += 1
  const current = attempt
  stopDeadline()
  view.value = null
  unavailable.value = false
  pending.value = 0
  deadline = setTimeout(() => {
    unavailable.value = true
    deadline = undefined
  }, props.timeoutMs)

  const read = await store.loadSlotWidget(props.slotId, props.widget.pluginId, props.widget.id, props.context)
  // A newer attempt started, or the note is already shown: this reply is dropped (see the late-arrival rule above).
  if (current !== attempt || unavailable.value) {
    return
  }
  if (read) {
    view.value = read
    // After the render the blocks have started their first reads (they track them as they mount); with none running
    // the widget is ready and the limit has nothing left to guard.
    await nextTick()
    if (current === attempt && pending.value === 0) {
      stopDeadline()
    }
  } else {
    stopDeadline()
    unavailable.value = true
  }
}

// The limit only guards the wait: when the last first read ends with the view already here, it is cleared.
watch(pending, (running) => {
  if (view.value && running === 0) {
    stopDeadline()
  }
})

onErrorCaptured(() => {
  stopDeadline()
  unavailable.value = true
  // Stops the error here: the page and the other widgets never see it.
  return false
})

onMounted(() => {
  void start()
})

watch(() => props.context, () => {
  void start()
}, { deep: true })

onBeforeUnmount(stopDeadline)
</script>

<template>
  <UiText v-if="unavailable" size="sm" tone="muted" :data-testid="`plugin-slot-unavailable-${testid}`">{{ $t('plugins.unavailable') }}</UiText>
  <UiSkeleton v-else-if="!view" :data-testid="`plugin-slot-loading-${testid}`" />
  <PluginView
    v-else
    :plugin-id="widget.pluginId"
    :view="view.view"
    :values="context"
    :external-origins="view.externalOrigins"
    :track="track"
    :data-testid="`plugin-slot-widget-${testid}`"
  />
</template>
