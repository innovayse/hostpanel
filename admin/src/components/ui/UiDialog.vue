<script setup lang="ts">
/**
 * Modal dialog.
 *
 * `variant="plain"` is a titled panel around the default slot (with an optional `footer` slot).
 * `variant="confirm"` is the confirmation prompt: a `message`, a Cancel button and a confirm
 * button coloured by `tone`; it replaces the former confirmation modal. The panel is teleported to
 * `<body>`, closes on Escape or a backdrop click, keeps Tab inside and returns focus to the opener.
 * Callers mount it with `v-if`, or drive it with `open`.
 *
 * It fades and scales in and out (`UiTransition`, nothing moves under reduced motion). Focus goes to the panel
 * at once, so Escape and Tab work while it appears, and to the first control when the enter has ended. While it
 * is leaving, Escape, a backdrop click, the close button and Confirm do nothing, so a request cannot fire twice.
 * A parent that removes it with `v-if` skips the leave: the page scroll and the focus are given back at once.
 */
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { toneSolidClasses } from '@/utils/ui/tone'
import type { UiTone } from '@/utils/ui/tone'
import { useModalSurface } from '@/utils/ui/modalSurface'
import { modalPanelClasses, UiTransitionKind } from '@/utils/ui/transition'
import { UiDialogSize, UiDialogVariant } from '@/utils/ui/dialog'

/**
 * The root is a Teleport, which cannot take attributes, so a caller's `data-testid` or `aria-*`
 * would be dropped with a warning. They go onto the dialog panel instead.
 */
defineOptions({ inheritAttrs: false })

const { t } = useI18n()

/** Props for UiDialog. */
const props = withDefaults(defineProps<{
  /** Whether the dialog is shown; defaults to true so `v-if` mounting works. */
  open?: boolean
  /** Dialog kind. */
  variant?: UiDialogVariant
  /** Title in the header (already translated). */
  title?: string
  /** Prompt text of the confirm variant. */
  message?: string
  /** Label of the confirm button; defaults to a translated "Confirm". */
  confirmLabel?: string
  /** Label of the confirm button while `loading`; defaults to a translated "Processing". */
  loadingLabel?: string
  /** Whether the confirmed action is running (disables the confirm button). */
  loading?: boolean
  /** Colour of the confirm button. */
  tone?: UiTone
  /** Panel width. */
  size?: UiDialogSize
  /** Whether a click on the backdrop closes the dialog. */
  closeOnBackdrop?: boolean
}>(), {
  open: true,
  variant: UiDialogVariant.Plain,
  title: undefined,
  message: undefined,
  confirmLabel: undefined,
  loadingLabel: undefined,
  loading: false,
  tone: 'primary',
  size: UiDialogSize.Sm,
  closeOnBackdrop: true,
})

const emit = defineEmits<{
  /** Emitted when the user confirms (confirm variant). */
  confirm: []
  /** Emitted when the user cancels, presses Escape or clicks the backdrop. */
  close: []
}>()

/** Id of the title element, referenced by `aria-labelledby`. */
const titleId = useId()

/** The dialog panel, for focus handling. */
const panel = ref<HTMLElement | null>(null)

/** Panel widths. */
const sizeClasses: Record<UiDialogSize, string> = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
}

/** Classes of the confirm button's colour. */
const confirmClasses = computed<string>(() => toneSolidClasses[props.tone])

/** Visible label of the confirm button. */
const confirmText = computed<string>(() => {
  if (props.loading) {
    return props.loadingLabel ?? t('ui.dialog.processing')
  }
  return props.confirmLabel ?? t('ui.dialog.confirm')
})

/** Whether the leave animation is running; no request may be made from a dialog that is going away. */
const leaving = ref(false)

/** Requests closing, unless the dialog is already leaving. */
const close = (): void => {
  if (!leaving.value) {
    emit('close')
  }
}

/** Confirms, unless the dialog is already leaving. */
const confirm = (): void => {
  if (!leaving.value) {
    emit('confirm')
  }
}

/** Keyboard handler (Escape closes, Tab stays inside), and the focus move for the end of the enter. */
const { onKeydown, focusIn } = useModalSurface(panel, () => props.open, close)

/**
 * Moves focus to the first control once the enter has ended, unless the user already focused one inside.
 *
 * @returns Nothing; focus is moved.
 */
const onEntered = (): void => {
  focusIn()
}

/**
 * Starts the leave: requests are ignored from now on.
 *
 * @returns Nothing; the leaving state is set.
 */
const onLeaving = (): void => {
  leaving.value = true
}

/**
 * Ends the leave. With `v-if` this also runs, for the old element, when the dialog is reopened during its leave
 * (the transition ends the old element's leave and mounts a fresh one).
 *
 * @returns Nothing; the leaving state is cleared.
 */
const onLeft = (): void => {
  leaving.value = false
}

/**
 * Clears the leaving state when the very same element is shown again during its leave. Vue raises
 * `leave-cancelled` only for an element that persists (`v-show`); a `v-if` surface gets `after-leave` instead,
 * see {@link onLeft}.
 *
 * @returns Nothing; the leaving state is cleared.
 */
const onLeaveCancelled = (): void => {
  leaving.value = false
}

/** Closes on a backdrop click when allowed. */
const onBackdrop = (): void => {
  if (props.closeOnBackdrop) {
    close()
  }
}
</script>

<template>
  <Teleport to="body">
    <UiTransition
      :kind="UiTransitionKind.Dialog"
      appear
      @after-enter="onEntered"
      @before-leave="onLeaving"
      @after-leave="onLeft"
      @leave-cancelled="onLeaveCancelled"
    >
      <div
        v-if="open"
        data-testid="dialog-backdrop"
        class="group/overlay fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
        @click.self="onBackdrop"
      >
        <div
          data-testid="dialog-panel"
          v-bind="$attrs"
          ref="panel"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="title ? titleId : undefined"
          tabindex="-1"
          class="bg-surface-card border border-border-strong rounded-xl shadow-2xl w-full max-h-[calc(100dvh-2rem)] overflow-hidden flex flex-col outline-none"
          :class="[sizeClasses[size], modalPanelClasses.dialog]"
          @keydown="onKeydown"
        >
          <div v-if="title" class="flex items-center justify-between gap-3 px-6 pt-6 pb-4 shrink-0">
            <UiHeading :id="titleId" :level="2" variant="card">
              {{ title }}
            </UiHeading>
            <UiIconButton
              :label="t('ui.dialog.close')"
              size="sm"
              data-testid="dialog-close"
              @click="close"
            >
              <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </UiIconButton>
          </div>
          <div class="flex-1 min-h-0 overflow-y-auto px-6 py-2 flex flex-col gap-4" data-testid="dialog-body">
            <UiText v-if="variant === UiDialogVariant.Confirm && message" tone="secondary" size="md">
              {{ message }}
            </UiText>
            <slot />
          </div>
          <div v-if="variant === UiDialogVariant.Confirm" class="flex justify-end gap-2 px-6 pt-4 pb-6 shrink-0">
            <UiButton variant="secondary" data-testid="dialog-cancel" @click="close">
              {{ t('shared.cancel') }}
            </UiButton>
            <UiButton
              variant="plain"
              :disabled="loading"
              data-testid="dialog-confirm"
              class="px-4 py-2 text-[0.82rem] font-medium rounded-lg transition cursor-pointer outline-none focus-visible:ring-2 focus-visible:ring-primary-500/60 disabled:opacity-50 disabled:cursor-not-allowed"
              :class="confirmClasses"
              @click="confirm"
            >
              {{ confirmText }}
            </UiButton>
          </div>
          <div v-else-if="$slots.footer" class="flex justify-end gap-2 px-6 pt-4 pb-6 shrink-0">
            <slot name="footer" />
          </div>
        </div>
      </div>
    </UiTransition>
  </Teleport>
</template>
