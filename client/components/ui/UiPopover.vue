<template>
  <div ref="rootEl" class="relative">
    <slot name="trigger" :open="modelValue" :toggle="toggle" :close="close" />

    <div
      v-if="modelValue"
      ref="panelEl"
      role="menu"
      data-testid="ui-popover-panel"
      :class="panelClasses"
    >
      <slot :close="close" />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UiPopover: a floating panel opened from a trigger, for menus and tile launchers.
 *
 * The trigger lives in the `trigger` slot and receives `open`, `toggle` and `close`; the default
 * slot is the panel body and receives `close`. The panel closes on Escape and on a click anywhere
 * outside the component. The panel always hangs from the trigger: it opens right under it and
 * lines up with the trigger's right edge by default (`align="end"`, the right end of a header) or
 * its left edge (`align="start"`), and is capped to the viewport width, so it stays in the header
 * and on screen at every width. When the preferred edge would push the panel past the viewport
 * and the other edge would not, the panel opens from the other edge. The screen only chooses how wide the panel is, through `panelClass`.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { UiMenuAlign } from '~/utils/ui/enums'
import { menuAlignClasses, resolveMenuAlign } from '~/utils/ui/menuAlign'

/** Props of UiPopover. */
interface Props {
  /** Whether the panel is open (v-model). */
  modelValue?: boolean
  /** Edge of the trigger the panel lines up with. */
  align?: `${UiMenuAlign}`
  /** Width utilities of the panel (`w-72`); placement belongs to the component. */
  panelClass?: string
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  align: UiMenuAlign.End,
  panelClass: '',
})

/** Events this component raises. */
const emit = defineEmits<{
  /** Fires when the panel opens or closes. */
  'update:modelValue': [value: boolean]
}>()

/** Root element, used to tell an inside click from an outside one. */
const rootEl = ref<HTMLElement | null>(null)

/** Panel element, measured once open to keep it inside the viewport. */
const panelEl = ref<HTMLElement | null>(null)

/** Edge the panel is anchored to now: the preferred one unless it would leave the viewport. */
const activeAlign = ref<UiMenuAlign>(props.align as UiMenuAlign)

/** Class list of the panel: the anchoring under the trigger, the surface, then the screen's width. */
const panelClasses = computed(() => [
  menuAlignClasses(activeAlign.value),
  'top-full mt-2 z-40 flex flex-col rounded-2xl border border-gray-200 bg-ui-popover p-2.5 shadow-2xl dark:border-white/15',
  props.panelClass,
].filter(Boolean).join(' '))

/** Re-anchors the open panel: starts from the preferred edge, then flips when only the other one fits. */
const placePanel = async (): Promise<void> => {
  activeAlign.value = props.align as UiMenuAlign
  await nextTick()
  if (!rootEl.value || !panelEl.value) {
    return
  }
  const trigger = rootEl.value.getBoundingClientRect()
  activeAlign.value = resolveMenuAlign(
    props.align,
    trigger.left,
    trigger.right,
    panelEl.value.offsetWidth,
    document.documentElement.clientWidth,
  )
}

watch(
  () => [props.modelValue, props.align] as const,
  ([open]) => {
    if (open) {
      void placePanel()
    }
  },
)

/** Opens a closed panel and closes an open one. */
const toggle = (): void => {
  emit('update:modelValue', !props.modelValue)
}

/** Closes the panel. */
const close = (): void => {
  emit('update:modelValue', false)
}

/**
 * Closes the panel when the click landed outside this component.
 *
 * @param event The document click.
 */
const onClickOutside = (event: MouseEvent): void => {
  if (props.modelValue && rootEl.value && !rootEl.value.contains(event.target as Node)) { // target is always a Node for a document click
    close()
  }
}

/**
 * Closes the panel on Escape.
 *
 * @param event The document key press.
 */
const onEscape = (event: KeyboardEvent): void => {
  if (event.key === 'Escape') {
    close()
  }
}

onMounted(() => {
  document.addEventListener('click', onClickOutside)
  document.addEventListener('keydown', onEscape)
  if (props.modelValue) {
    void placePanel()
  }
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onClickOutside)
  document.removeEventListener('keydown', onEscape)
})
</script>
