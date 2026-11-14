<script setup lang="ts">
/**
 * Side panel that slides over the page edge (filters, detail peek, the menu on a phone).
 *
 * Teleported to `<body>`; closes on Escape or a backdrop click, keeps Tab inside and returns focus
 * to the opener. The panel is an opaque elevated surface (nothing shows through it). With `sheet` it is a bottom
 * sheet below the `sm` breakpoint (below `lg` with `sheetBelow`; rounded top corners, a grip, room for the home
 * indicator through the safe-area inset) and the usual side panel from there up. A sheet follows touch gestures
 * ({@link useSheetDrag}): dragged down it closes past a distance or on a flick, a short drag springs back, dragged
 * up from a partial height it opens to full height. The default slot is the body and `footer` an optional pinned action row. It slides in from its
 * edge (`UiTransition`, nothing moves under reduced motion); focus goes to the first control when the enter has
 * ended, the close button shows only once the panel is in, and a request made while it is leaving is ignored.
 */
import { computed, nextTick, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { useSheetDrag } from '@/composables/useSheetDrag'
import { useModalSurface } from '@/utils/ui/modalSurface'
import { modalPanelClasses, UiTransitionKind } from '@/utils/ui/transition'
import { sheetQueries, UiDrawerSheetBelow } from '@/utils/ui/drawer'
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
  /** Becomes a bottom sheet on a phone (below `sm`); a side panel from there up. Off keeps the side panel everywhere. */
  sheet?: boolean
  /** The breakpoint under which a `sheet` is a bottom sheet: a phone (`sm`, the default) or anything below `lg` (the menu). Read once at setup. */
  sheetBelow?: UiDrawerSheetBelow
}>(), {
  side: 'right',
  flush: false,
  narrow: false,
  sheet: false,
  sheetBelow: UiDrawerSheetBelow.Sm,
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
  left: 'top-0 bottom-0 left-0 border-r border-border-strong',
  right: 'top-0 bottom-0 right-0 border-l border-border-strong',
}

/** Placement of a bottom sheet: along the bottom with rounded top corners and room for the home indicator. */
const sheetPlacement = 'bottom-0 left-0 right-0 rounded-t-panel border-t border-border-strong pb-[env(safe-area-inset-bottom)]'

/** Height limit of a bottom sheet: partial (content height up to this maximum) or opened to full height. */
const sheetHeights = {
  partial: 'max-h-[85dvh]',
  expanded: 'h-[95dvh] max-h-[95dvh]',
} as const

/** Panel motion per edge. */
const slideClasses: Record<UiDrawerSide, string> = {
  left: modalPanelClasses.drawerLeft,
  right: modalPanelClasses.drawerRight,
}

/** The scrolling body, whose scroll position decides whether a drag from it moves the sheet. */
const body = ref<HTMLElement | null>(null)

/** Whether the screen is narrow enough for a `sheet` to be a bottom sheet right now. */
const underSheetBreakpoint = useMediaQuery(sheetQueries[props.sheetBelow])

/** Whether the drawer is a bottom sheet at this moment: asked for with `sheet` and on a narrow enough screen. */
const sheetActive = computed<boolean>(() => props.sheet && underSheetBreakpoint.value)

/** Placement of the panel for the chosen edge. */
const placementClasses = computed<string>(() => {
  if (!sheetActive.value) {
    return sideClasses[props.side]
  }
  return `${sheetPlacement} ${expanded.value ? sheetHeights.expanded : sheetHeights.partial}`
})

/** Motion of the panel: a sheet rises from the bottom on a phone. */
const motionClasses = computed<string>(() => (sheetActive.value ? modalPanelClasses.sheet : slideClasses[props.side]))

/** Maximum width; a bottom sheet is full width, so only a side panel is limited. */
const widthClasses = computed<string>(() => {
  if (sheetActive.value) {
    return ''
  }
  return props.narrow ? 'max-w-xs' : 'max-w-md'
})

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

/** Touch gestures of the sheet: the finger moves it, a drag past a distance or a flick closes it, up opens it fully. */
const { dragging, expanded, onPointerdown, onPointermove, onPointerup, onPointercancel, reset } = useSheetDrag({
  panel,
  body,
  enabled: sheetActive,
  close,
})

// Every opening starts at the resting place and the partial height, whatever the last gesture left behind.
watch(() => props.open, (open) => {
  if (open) {
    reset()
  }
})

/** Keyboard handler (Escape closes, Tab stays inside), and the focus move for the end of the enter. */
const { onKeydown, focusIn } = useModalSurface(panel, () => props.open, close)

/**
 * Marks the panel as in and moves focus to its first control, once the enter has ended.
 *
 * The close button turns visible with the `settled` state and a hidden control cannot take focus, so the focus
 * move waits for the DOM update that shows it. A control the user already focused is left alone.
 *
 * @returns A promise that settles once focus has been moved (or left where it was).
 */
const onEntered = async (): Promise<void> => {
  settled.value = true
  await nextTick()
  focusIn()
}

/**
 * Ends the leave: the old panel is gone, so requests are accepted again.
 *
 * With `v-if` this also runs, for the old element, when the drawer is reopened during its leave (the transition
 * ends the old element's leave and mounts a fresh one); the fresh panel then settles through the enter hook.
 *
 * @returns Nothing; the leaving state is cleared.
 */
const onLeft = (): void => {
  leaving.value = false
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

/**
 * Undoes the start of a leave when the very same element is shown again while it was leaving. Vue raises
 * `leave-cancelled` only for an element that persists (`v-show`); a `v-if` surface gets `after-leave` on the old
 * element and a new element instead, see {@link onLeft}. Kept so a persisted panel is usable again: requests work
 * and the close button shows.
 *
 * @returns Nothing; the leaving and settled states are restored.
 */
const onLeaveCancelled = (): void => {
  leaving.value = false
  settled.value = true
}
</script>

<template>
  <Teleport to="body">
    <UiTransition
      :kind="UiTransitionKind.Drawer"
      appear
      @after-enter="onEntered"
      @before-leave="onLeaving"
      @after-leave="onLeft"
      @leave-cancelled="onLeaveCancelled"
    >
      <div v-if="open" class="group/overlay fixed inset-0 z-50 bg-(--overlay-backdrop) backdrop-blur-sm" @click.self="close">
        <aside
          ref="panel"
          role="dialog"
          aria-modal="true"
          :aria-labelledby="titleId"
          tabindex="-1"
          class="glass-strong absolute w-full shadow-overlay flex flex-col overflow-hidden outline-none"
          :class="[widthClasses, placementClasses, motionClasses, sheetActive ? 'translate-y-[var(--sheet-drag,0px)] data-dragging:transition-none' : '']"
          :data-dragging="dragging ? '' : undefined"
          @keydown="onKeydown"
          @pointerdown="onPointerdown"
          @pointermove="onPointermove"
          @pointerup="onPointerup"
          @pointercancel="onPointercancel"
        >
          <div v-if="sheetActive" class="flex shrink-0 touch-none justify-center pt-2" aria-hidden="true" data-testid="drawer-grip">
            <span class="h-1 w-10 rounded-pill bg-border-strong" />
          </div>
          <div class="flex h-(--shell-header-h) items-center justify-between gap-3 border-b border-border-subtle px-6 shrink-0" :class="{ 'touch-none': sheetActive }">
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
          <div ref="body" class="flex-1 min-h-0 overflow-y-auto" :class="[flush ? '' : 'p-6', sheetActive ? 'overscroll-y-contain' : '']" data-testid="drawer-body">
            <slot />
          </div>
          <div v-if="$slots.footer" class="flex justify-end gap-2 px-6 py-4 border-t border-border-subtle shrink-0 max-sm:flex-col-reverse max-sm:items-stretch" data-testid="ui-drawer-footer">
            <slot name="footer" />
          </div>
        </aside>
      </div>
    </UiTransition>
  </Teleport>
</template>
