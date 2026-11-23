<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      leave-active-class="transition-opacity duration-150 ease-in"
      enter-from-class="opacity-0"
      leave-to-class="opacity-0"
    >
      <div
        v-if="modelValue"
        class="fixed inset-0 z-50 flex items-center justify-center"
        :class="sheet ? 'sm:p-4' : 'p-4'"
        data-testid="ui-dialog"
      >
        <!-- Backdrop -->
        <div
          class="absolute inset-0 bg-black/50 backdrop-blur-sm"
          data-testid="ui-dialog-backdrop"
          @click="onBackdropClick"
        />

        <!-- Panel -->
        <div
          ref="panelRef"
          role="dialog"
          aria-modal="true"
          :aria-label="$slots.header ? undefined : title"
          :aria-labelledby="$slots.header ? headerId : undefined"
          tabindex="-1"
          :class="panelClasses"
        >
          <div v-if="title || $slots.header" :id="headerId" class="flex shrink-0 items-center justify-between gap-3 p-4 sm:p-6 border-b border-gray-200 dark:border-white/10">
            <slot name="header">
              <h3 class="min-w-0 break-words text-xl font-semibold text-gray-900 dark:text-white">
                {{ title }}
              </h3>
            </slot>

            <UiIconButton
              v-if="closable"
              :icon="X"
              :label="$t('ui.close')"
              testid="ui-dialog-close"
              @click="close"
            />
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto p-4 sm:p-6" data-testid="ui-dialog-body">
            <slot />
          </div>

          <div v-if="$slots.footer" class="flex shrink-0 flex-wrap items-center justify-end gap-3 p-4 sm:p-6 border-t border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5">
            <slot name="footer" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
/**
 * UiDialog: a centred modal with a backdrop, an optional header, a body and a footer.
 *
 * Teleports to `<body>` so it stacks above everything, locks page scrolling while open (shared
 * counter, see `utils/ui/overlay.ts`), closes on Escape, and closes on a backdrop click when
 * `closeOnBackdrop` is set. Closing emits `update:modelValue(false)` and `close`.
 *
 * Below `sm` the panel is a full-screen sheet (full width and height, the body scrolling inside it and
 * the header and footer staying pinned, so the actions are always in reach); from `sm` it is the centred
 * card of the chosen size. `sheet` set to false keeps the centred card on a phone too, for a short
 * confirmation that would be lost in a full screen.
 */
import { computed, ref, useId } from 'vue'
import { X } from 'lucide-vue-next'
import { UiPanelSize } from '~/utils/ui/enums'
import { useOverlay } from '~/utils/ui/overlay'

/** Props of UiDialog. */
interface Props {
  /** Whether the dialog is open (v-model). */
  modelValue: boolean
  /** Header title; without a title or a `header` slot there is no header bar. */
  title?: string
  /** Panel width. */
  size?: `${UiPanelSize}`
  /** Shows the close button in the header. */
  closable?: boolean
  /** Closes when the backdrop is clicked. */
  closeOnBackdrop?: boolean
  /** Whether the panel is a full-screen sheet on a phone; false keeps the centred card there. */
  sheet?: boolean
}

/** Props of the component with their defaults applied. */
const props = withDefaults(defineProps<Props>(), {
  title: undefined,
  size: UiPanelSize.Md,
  closable: true,
  closeOnBackdrop: true,
  sheet: true,
})

/** Id of the header, referenced by `aria-labelledby` when a custom header is slotted. */
const headerId = useId()

/** The panel element; the overlay helper traps focus inside it. */
const panelRef = ref<HTMLElement | null>(null)

/** Emits the component raises. */
const emit = defineEmits<{
  /** The dialog asked to open or close. */
  'update:modelValue': [value: boolean]
  /** The dialog was closed by the reader. */
  close: []
}>()

/** Panel width utility per size; a sheet is full width below `sm`, so the cap starts there. */
const SIZE_CLASSES: Record<UiPanelSize, string> = {
  [UiPanelSize.Sm]: 'sm:max-w-sm',
  [UiPanelSize.Md]: 'sm:max-w-md',
  [UiPanelSize.Lg]: 'sm:max-w-lg',
  [UiPanelSize.Xl]: 'sm:max-w-2xl',
  [UiPanelSize.Full]: 'sm:max-w-6xl',
}

/** Class list of the panel. */
const panelClasses = computed(() => [
  'relative w-full bg-ui-surface border-gray-200 dark:border-white/15 shadow-2xl flex flex-col overflow-hidden',
  SIZE_CLASSES[props.size as UiPanelSize],
  props.sheet
    ? 'h-dvh rounded-none border-0 sm:h-auto sm:max-h-[90dvh] sm:rounded-xl sm:border'
    : 'max-h-[90dvh] rounded-xl border',
].join(' '))

/** Closes the dialog and tells the parent. */
const close = (): void => {
  emit('update:modelValue', false)
  emit('close')
}

/** Closes on a backdrop click when allowed. */
const onBackdropClick = (): void => {
  if (props.closeOnBackdrop) {
    close()
  }
}

useOverlay(() => props.modelValue, close, () => panelRef.value)
</script>
