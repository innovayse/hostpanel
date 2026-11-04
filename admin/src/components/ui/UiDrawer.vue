<script setup lang="ts">
/**
 * Side panel that slides over the page edge (filters, detail peek, the menu on a phone).
 *
 * Teleported to `<body>`; closes on Escape or a backdrop click, keeps Tab inside and returns focus
 * to the opener. The default slot is the body and `footer` an optional pinned action row. It slides in from its
 * edge (`UiTransition`, nothing moves under reduced motion); focus goes to the first control when the enter has
 * ended, the close button shows only once the panel is in, and a request made while it is leaving is ignored.
 */
import { ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { useModalSurface } from '@/utils/ui/modalSurface'
import { modalPanelClasses, UiTransitionKind } from '@/utils/ui/transition'
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

/** Panel motion per edge. */
const slideClasses: Record<UiDrawerSide, string> = {
  left: modalPanelClasses.drawerLeft,
  right: modalPanelClasses.drawerRight,
}

/** Whether the leave animation is running; no request may be made from a drawer that is going away. */
const leaving = ref(false)

/** Whether the panel has finished sliding in: the close button shows only then, so it never slides across the page. */
const settled = ref(false)

/** Requests closing, unless the drawer is already leaving. */
const close = (): void => {
  if (!leaving.value) {
    emit('close')
  }
}

/** Keyboard handler (Escape closes, Tab stays inside), and the focus move for the end of the enter. */
const { onKeydown, focusIn } = useModalSurface(panel, () => props.open, close)

/**
 * Marks the panel as in and moves focus to its first control, once the enter has ended.
 *
 * @returns Nothing; the settled state is set and focus is moved.
 */
const onEntered = (): void => {
  settled.value = true
  focusIn()
}

/**
 * Starts the leave: requests are ignored from now on and the close button goes at once.
 *
 * @returns Nothing; the leaving and settled states are set.
 */
const onLeaving = (): void => {
  leaving.value = true
  settled.value = false
}
</script>

<template>
  <Teleport to="body">
    <UiTransition
      :kind="UiTransitionKind.Drawer"
      appear
      @after-enter="onEntered"
      @before-leave="onLeaving"
      @after-leave="leaving = false"
    >
      <div v-if="open" class="group/overlay fixed inset-0 z-50 bg-black/60 backdrop-blur-sm" @click.self="close">
        <aside
          ref="panel"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="titleId"
          tabindex="-1"
          class="absolute top-0 bottom-0 w-full bg-surface-card shadow-2xl flex flex-col overflow-hidden outline-none"
          :class="[narrow ? 'max-w-xs' : 'max-w-md', sideClasses[side], slideClasses[side]]"
          @keydown="onKeydown"
        >
          <div class="flex items-center justify-between gap-3 px-6 py-4 border-b border-border shrink-0">
            <UiHeading :id="titleId" :level="2" variant="card">
              {{ title }}
            </UiHeading>
            <UiIconButton
              :label="t('ui.dialog.close')"
              size="sm"
              class="transition-opacity duration-(--duration-fast) motion-reduce:transition-none"
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
    </UiTransition>
  </Teleport>
</template>
