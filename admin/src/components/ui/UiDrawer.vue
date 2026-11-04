<script setup lang="ts">
/**
 * Side panel that slides over the page edge (filters, detail peek).
 *
 * Teleported to `<body>`; closes on Escape or a backdrop click, keeps Tab inside and returns focus
 * to the opener. The default slot is the body and `footer` an optional pinned action row.
 */
import { ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import { useModalSurface } from '@/utils/ui/modalSurface'
import type { UiDrawerSide } from '@/utils/ui/drawer'

const { t } = useI18n()

/** Props for UiDrawer. */
const props = withDefaults(defineProps<{
  /** Whether the drawer is shown. */
  open: boolean
  /** Title in the header (already translated). */
  title: string
  /** Edge the drawer slides from. */
  side?: UiDrawerSide
  /** Drops the body padding, for content that draws its own (a navigation list that runs edge to edge). */
  flush?: boolean
  /** A narrower panel (20rem) that leaves a strip of the page to tap on a phone. */
  narrow?: boolean
}>(), {
  side: 'right',
  flush: false,
  narrow: false,
})

const emit = defineEmits<{
  /** Emitted when the user presses Escape, clicks the backdrop or the close button. */
  close: []
}>()

/** Id of the title element, referenced by `aria-labelledby`. */
const titleId = useId()

/** The drawer panel, for focus handling. */
const panel = ref<HTMLElement | null>(null)

/** Placement classes per edge. */
const sideClasses: Record<UiDrawerSide, string> = {
  left: 'left-0 border-r border-border-strong',
  right: 'right-0 border-l border-border-strong',
}

/** How long the panel takes to slide in or out, in milliseconds (the classes below say the same as `duration-300`). */
const SLIDE_MS = 300

/**
 * Classes of the open and close animation per edge. The overlay fades and its panel (`aside`) slides, both over
 * {@link SLIDE_MS}; the panel is reached with the `[&>aside]` variant because the transition runs on the overlay.
 * Whoever asks for reduced motion gets the drawer without the movement. While it slides out the close button is
 * hidden by `[&_[data-testid=drawer-close]]:invisible`.
 */
const slideClasses: Record<UiDrawerSide, { active: string, leaveActive: string, hidden: string }> = {
  left: {
    active: 'transition-opacity duration-300 ease-out [&>aside]:transition-transform [&>aside]:duration-300 [&>aside]:ease-out motion-reduce:transition-none motion-reduce:[&>aside]:transition-none',
    leaveActive: 'transition-opacity duration-300 ease-in [&>aside]:transition-transform [&>aside]:duration-300 [&>aside]:ease-in motion-reduce:transition-none motion-reduce:[&>aside]:transition-none [&_[data-testid=drawer-close]]:invisible',
    hidden: 'opacity-0 [&>aside]:-translate-x-full',
  },
  right: {
    active: 'transition-opacity duration-300 ease-out [&>aside]:transition-transform [&>aside]:duration-300 [&>aside]:ease-out motion-reduce:transition-none motion-reduce:[&>aside]:transition-none',
    leaveActive: 'transition-opacity duration-300 ease-in [&>aside]:transition-transform [&>aside]:duration-300 [&>aside]:ease-in motion-reduce:transition-none motion-reduce:[&>aside]:transition-none [&_[data-testid=drawer-close]]:invisible',
    hidden: 'opacity-0 [&>aside]:translate-x-full',
  },
}

/** Whether the panel has finished sliding in: the close button shows only then, and goes the moment it is asked to close. */
const settled = ref(false)

/** Marks the panel as fully open once it has slid in. */
const onOpened = (): void => {
  settled.value = true
}

// Ready for the next opening. The button of a panel that is leaving is hidden by the leave classes above (the
// leaving panel is no longer re-rendered, so its own state cannot do it).
watch(() => props.open, (isOpen) => {
  if (!isOpen) {
    settled.value = false
  }
})

/** Requests closing. */
const close = (): void => {
  emit('close')
}

/** Keyboard handler: Escape closes, Tab stays inside. */
const onKeydown = useModalSurface(panel, () => props.open, close)
</script>

<template>
  <Teleport to="body">
    <Transition
      :duration="SLIDE_MS"
      :enter-active-class="slideClasses[side].active"
      :leave-active-class="slideClasses[side].leaveActive"
      :enter-from-class="slideClasses[side].hidden"
      :leave-to-class="slideClasses[side].hidden"
      @after-enter="onOpened"
    >
    <div v-if="open" class="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm" @click.self="close">
      <aside
        ref="panel"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="titleId"
        tabindex="-1"
        class="absolute top-0 bottom-0 w-full bg-surface-card shadow-2xl flex flex-col overflow-hidden outline-none"
        :class="[narrow ? 'max-w-xs' : 'max-w-md', sideClasses[side]]"
        @keydown="onKeydown"
      >
        <div class="flex items-center justify-between gap-3 px-6 py-4 border-b border-border shrink-0">
          <UiHeading :id="titleId" :level="2" variant="card">
            {{ title }}
          </UiHeading>
          <UiIconButton
            :label="t('ui.dialog.close')"
            size="sm"
            class="transition-opacity duration-150"
            :class="settled ? 'opacity-100' : 'invisible opacity-0'"
            data-testid="drawer-close"
            @click="close"
          >
            <svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </UiIconButton>
        </div>
        <div class="flex-1 min-h-0 overflow-y-auto" :class="flush ? '' : 'p-6'" data-testid="drawer-body">
          <slot />
        </div>
        <div v-if="$slots.footer" class="flex justify-end gap-2 px-6 py-4 border-t border-border shrink-0">
          <slot name="footer" />
        </div>
      </aside>
    </div>
    </Transition>
  </Teleport>
</template>
