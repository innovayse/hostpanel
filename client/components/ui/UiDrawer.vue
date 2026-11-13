<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      leave-active-class="transition-opacity duration-200 ease-in"
      enter-from-class="opacity-0"
      leave-to-class="opacity-0"
    >
      <div
        v-if="modelValue"
        class="fixed inset-0 z-50"
        data-testid="ui-drawer"
      >
        <!-- Backdrop -->
        <div
          class="absolute inset-0 bg-black/50 backdrop-blur-sm"
          data-testid="ui-drawer-backdrop"
          @click="onBackdropClick"
        />

        <!-- Panel -->
        <aside
          ref="panelRef"
          role="dialog"
          aria-modal="true"
          tabindex="-1"
          :aria-label="title"
          :class="panelClasses"
        >
          <div class="flex shrink-0 items-center justify-between gap-3 p-5 border-b border-gray-200 dark:border-white/10">
            <slot name="header">
              <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
                {{ title }}
              </h2>
            </slot>
            <UiIconButton
              :icon="X"
              :label="$t('ui.close')"
              variant="ghost"
              testid="ui-drawer-close"
              @click="close"
            />
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto p-5" data-testid="ui-drawer-body">
            <slot />
          </div>

          <div v-if="$slots.footer" class="shrink-0 p-5 border-t border-gray-200 dark:border-white/10">
            <slot name="footer" />
          </div>
        </aside>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
/**
 * UiDrawer: a side panel that overlays the page (cart, filters, mobile menu).
 *
 * Teleports to body, locks page scrolling while open (shared counter, see utils/ui/overlay.ts),
 * closes on Escape, the close button, or a backdrop click. The default slot scrolls; `header`
 * replaces the title; `footer` is pinned at the bottom.
 */
import { computed, ref } from 'vue'
import { X } from 'lucide-vue-next'
import { UiDrawerSide, UiPanelSize } from '~/utils/ui/enums'
import { useOverlay } from '~/utils/ui/overlay'

/** Props of UiDrawer. */
interface Props {
  /** Whether the drawer is open (v-model). */
  modelValue: boolean
  /** Title in the header and the accessible name. */
  title?: string
  /** Edge the panel is attached to. */
  side?: `${UiDrawerSide}`
  /** Panel width. */
  size?: `${UiPanelSize}`
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  title: undefined,
  side: UiDrawerSide.Right,
  size: UiPanelSize.Md,
})

/** The panel element; the overlay helper traps focus inside it. */
const panelRef = ref<HTMLElement | null>(null)

/** Emits the component raises. */
const emit = defineEmits<{
  /** The drawer asked to open or close. */
  'update:modelValue': [value: boolean]
  /** The drawer was closed by the reader. */
  close: []
}>()

/** Panel width utility per size. */
const SIZE_CLASSES: Record<UiPanelSize, string> = {
  [UiPanelSize.Sm]: 'max-w-xs',
  [UiPanelSize.Md]: 'max-w-md',
  [UiPanelSize.Lg]: 'max-w-lg',
  [UiPanelSize.Xl]: 'max-w-2xl',
  [UiPanelSize.Full]: 'max-w-full',
}

/** Class list of the panel. */
const panelClasses = computed(() => [
  'absolute inset-y-0 flex w-full flex-col overflow-hidden bg-ui-surface shadow-2xl',
  SIZE_CLASSES[props.size as UiPanelSize],
  props.side === UiDrawerSide.Left ? 'left-0 border-r' : 'right-0 border-l',
  'border-gray-200 dark:border-white/15',
].join(' '))

/** Closes the drawer and tells the parent. */
const close = (): void => {
  emit('update:modelValue', false)
  emit('close')
}

/** Closes on a backdrop click. */
const onBackdropClick = (): void => {
  close()
}

useOverlay(() => props.modelValue, close, () => panelRef.value)
</script>
