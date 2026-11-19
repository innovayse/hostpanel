<script setup lang="ts">
/**
 * Panel that slides over the page: a side panel (filters, detail peek, the notification feed) or, with
 * `presentation="sheet"`, a bottom sheet (the navigation menu and every popup of a phone, see {@link useDrawerPresentation}).
 *
 * Teleported to `<body>`; closes on Escape or a backdrop click, keeps Tab inside and returns focus
 * to the opener. The panel is an opaque elevated surface (nothing shows through it). A sheet is full width along the
 * bottom edge with rounded top corners, a grip and room for the home indicator (the safe-area inset), and follows
 * touch gestures ({@link useSheetDrag}): dragged down by the grip or the header (or by the body of a touch, while it is
 * scrolled to its top) it closes past a distance or on a flick, a short drag springs back, dragged up from a partial
 * height it opens to full height. The default slot is the body and `footer` an optional pinned action row. It slides
 * in from its edge (`UiTransition`, nothing moves under reduced motion); focus goes to the first control when the enter
 * has ended, the close button shows only once the panel is in, and a request made while it is leaving is ignored.
 * `dismissible="false"` makes it a surface the reader cannot leave (no close button; Escape, the backdrop and a drag
 * do nothing) while work runs. A parent that removes it with `v-if` while open gets the leave played on an inert copy
 * of the backdrop (`playGhostLeave`). The caller's attributes (a `data-testid`, `aria-*`) land on the panel.
 */
import { computed, nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { useSheetDrag } from '@/composables/useSheetDrag'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import { ACTION_ROW_PHONE_CLASSES } from '@/utils/ui/actions'
import { UiSize } from '@/utils/ui/size'
import { useModalSurface } from '@/utils/ui/modalSurface'
import { modalPanelClasses, playGhostLeave, UiTransitionKind } from '@/utils/ui/transition'
import { UiDrawerPresentation, UiDrawerSide } from '@/utils/ui/drawer'

/**
 * The root is a Teleport, which cannot take attributes, so a caller's `data-testid` or `aria-*`
 * would be dropped with a warning. They go onto the panel instead.
 */
defineOptions({ inheritAttrs: false })

const { t } = useI18n()

/** Props for UiDrawer. */
const props = withDefaults(defineProps<{
  /** Whether the drawer is shown. */
  open: boolean
  /** Title in the header (already translated). */
  title: string
  /** Shape of the drawer: a panel on a page edge, or a bottom sheet. */
  presentation?: UiDrawerPresentation
  /** Edge a side panel slides from. */
  side?: UiDrawerSide
  /** Drops the body padding, for content that draws its own (a navigation list that runs edge to edge). */
  flush?: boolean
  /** A narrower side panel (20rem). */
  narrow?: boolean
  /** Whether the reader can close it: false hides the close button and ignores Escape, the backdrop and a drag (work is running). */
  dismissible?: boolean
  /** Whether a click on the backdrop closes it. */
  closeOnBackdrop?: boolean
}>(), {
  presentation: UiDrawerPresentation.Side,
  side: UiDrawerSide.Right,
  flush: false,
  narrow: false,
  dismissible: true,
  closeOnBackdrop: true,
})

const emit = defineEmits<{
  /** Emitted when the user presses Escape, clicks the backdrop or the close button, or drags a sheet away. */
  close: []
}>()

/** Id of the title element, referenced by `aria-labelledby`. */
const titleId = useId()

/** The backdrop element, which a parent's `v-if` removal copies for the leave. */
const backdrop = ref<HTMLElement | null>(null)

/** The drawer panel, for focus handling. */
const panel = ref<HTMLElement | null>(null)

/** Placement classes per edge. */
const sideClasses: Record<UiDrawerSide, string> = {
  [UiDrawerSide.Left]: 'top-0 bottom-0 left-0 border-r border-border-strong',
  [UiDrawerSide.Right]: 'top-0 bottom-0 right-0 border-l border-border-strong',
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
  [UiDrawerSide.Left]: modalPanelClasses.drawerLeft,
  [UiDrawerSide.Right]: modalPanelClasses.drawerRight,
}

/** The scrolling body, whose scroll position decides whether a drag from it moves the sheet. */
const body = ref<HTMLElement | null>(null)

/** Whether the drawer is a bottom sheet. */
const sheetActive = computed<boolean>(() => props.presentation === UiDrawerPresentation.Sheet)

/** Placement of the panel: the chosen edge, or the bottom of the screen for a sheet. */
const placementClasses = computed<string>(() => {
  if (!sheetActive.value) {
    return sideClasses[props.side]
  }
  return `${sheetPlacement} ${expanded.value ? sheetHeights.expanded : sheetHeights.partial}`
})

/** Motion of the panel: a sheet rises from the bottom. */
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

/** Requests closing, unless the drawer is already leaving or may not be dismissed. */
const close = (): void => {
  if (!leaving.value && props.dismissible) {
    emit('close')
  }
}

/** Closes on a backdrop click when allowed. */
const onBackdrop = (): void => {
  if (props.closeOnBackdrop) {
    close()
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

onBeforeUnmount(() => {
  // Still in the page here means no leave has run for it: the parent removed the component while it was open.
  if (backdrop.value !== null && !leaving.value) {
    playGhostLeave(backdrop.value, UiTransitionKind.Drawer)
  }
})
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
      <!-- The backdrop swallows the pointer-down: a surface opened from inside another floating panel (a select in a picker) is not "outside" it. -->
      <div
        v-if="open"
        ref="backdrop"
        class="group/overlay fixed inset-0 z-50 bg-(--overlay-backdrop) backdrop-blur-sm"
        @mousedown.stop
        @click.self="onBackdrop"
      >
        <aside
          ref="panel"
          role="dialog"
          aria-modal="true"
          data-testid="drawer"
          v-bind="$attrs"
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
          <div v-if="sheetActive" class="flex shrink-0 touch-none justify-center pt-2" aria-hidden="true" data-sheet-handle data-testid="drawer-grip">
            <span class="h-1 w-10 rounded-pill bg-border-strong" />
          </div>
          <div class="flex h-(--shell-header-h) items-center justify-between gap-3 border-b border-border-subtle px-6 shrink-0" :class="{ 'touch-none': sheetActive }" data-sheet-handle>
            <UiHeading :id="titleId" :level="2" variant="card">
              {{ title }}
            </UiHeading>
            <UiIconButton
              v-if="dismissible"
              :label="t('ui.dialog.close')"
              :size="UiSize.Sm"
              class="transition-opacity duration-(--duration-fast) motion-reduce:transition-none"
              :class="settled ? 'opacity-100' : 'invisible opacity-0'"
              data-testid="drawer-close"
              @click="close"
            >
              <UiIcon :markup="Icon.Close" :size="UiSize.Sm" />
            </UiIconButton>
          </div>
          <div ref="body" class="flex-1 min-h-0 overflow-y-auto" :class="[flush ? '' : 'p-6', sheetActive ? 'overscroll-y-contain' : '']" data-testid="drawer-body">
            <slot />
          </div>
          <div v-if="$slots.footer" class="flex justify-end gap-2 px-6 py-4 border-t border-border-subtle shrink-0" :class="ACTION_ROW_PHONE_CLASSES" data-testid="ui-drawer-footer">
            <slot name="footer" />
          </div>
        </aside>
      </div>
    </UiTransition>
  </Teleport>
</template>
