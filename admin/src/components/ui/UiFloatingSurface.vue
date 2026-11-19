<script setup lang="ts">
/**
 * The one surface every floating list of the kit is drawn on (menu, select, pickers, field selector): a popover hung from
 * its trigger from `sm` up, and a bottom sheet (`UiDrawer` with the sheet presentation) on a phone, so a component only
 * declares its content and its title and never decides the shape itself (see {@link useDrawerPresentation}).
 *
 * The popover is teleported to `<body>` and placed with fixed positioning next to the `anchor` (above it when there is
 * little room below), see {@link useFloatingPlace}; the sheet brings the grip, the safe-area inset, the drag-down close,
 * Escape and the backdrop tap of the drawer. `panel` is emitted with the element that holds the content, in both shapes (and
 * null when it goes), for the owner's outside-click test; the sheet's backdrop swallows the pointer-down, so a tap in a sheet
 * is never outside. The caller's attributes (`role`, `data-testid`, key and focus
 * handlers) land on the element that wraps the default slot in both shapes; the slot receives `sheet`, true on a phone, for
 * content that wants more room there.
 */
import { computed, ref, toRef, watch } from 'vue'
import UiDrawer from '@/components/ui/UiDrawer.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { useDrawerPresentation } from '@/composables/useDrawerPresentation'
import { useFloatingPlace } from '@/composables/useFloatingPlace'
import { UiDrawerPresentation } from '@/utils/ui/drawer'
import { FLOATING_PANEL_CLASSES, FLOATING_PLACE_CLASSES, FloatingAlign } from '@/utils/ui/floating'
import { UiTransitionKind } from '@/utils/ui/transition'

/** The attributes belong to the content wrapper, not to the Teleport or the drawer that happen to be the root. */
defineOptions({ inheritAttrs: false })

/** Props for UiFloatingSurface. */
const props = withDefaults(defineProps<{
  /** Whether the surface is shown. */
  open: boolean
  /** The control the popover hangs from; ignored by the sheet. */
  anchor: HTMLElement | null
  /** Title of the sheet (already translated); the popover has none. */
  title: string
  /** Free room below the anchor, in pixels, under which the popover opens above it. */
  minRoom: number
  /** Gap between the anchor and the popover, in pixels. */
  gap: number
  /** The anchor edge the popover lines up with. */
  align?: FloatingAlign
  /** Width of a popover that must stay inside the viewport horizontally (a wide calendar), in pixels. */
  keepInsideWidth?: number
  /** Shape of the popover (radius, width, padding); the sheet is always full width. */
  popoverClass?: string
}>(), {
  align: FloatingAlign.Start,
  keepInsideWidth: undefined,
  popoverClass: '',
})

const emit = defineEmits<{
  /** Emitted when the sheet asks to close: Escape, the backdrop, the close button or a drag down. The popover is closed by its owner. */
  close: []
  /** Emitted with the element that holds the content when it appears, and with null when it goes. */
  panel: [element: HTMLElement | null]
}>()

/** The element holding the content: the popover, or the wrapper inside the sheet. */
const content = ref<HTMLElement | null>(null)

// An event, not a two-way model: the owner only stores the element and never renders from it, so what it learns here
// cannot make it render again and rebuild this surface.
watch(content, (element) => {
  emit('panel', element)
}, { flush: 'post' })

/** The shape the screen asks for right now. */
const presentation = useDrawerPresentation()

/** Whether the surface is a bottom sheet right now. */
const sheet = computed<boolean>(() => presentation.value === UiDrawerPresentation.Sheet)

// A sheet is not placed; only an open popover follows its anchor.
useFloatingPlace(
  toRef(props, 'anchor'),
  content,
  computed<boolean>(() => props.open && !sheet.value),
  { align: props.align, minRoom: props.minRoom, gap: props.gap, keepInsideWidth: props.keepInsideWidth },
)
</script>

<template>
  <UiDrawer
    v-if="sheet"
    :open="open"
    :title="title"
    :presentation="UiDrawerPresentation.Sheet"
    flush
    @close="emit('close')"
  >
    <div ref="content" v-bind="$attrs" class="p-3">
      <slot :sheet="true" />
    </div>
  </UiDrawer>
  <Teleport v-else to="body">
    <UiTransition :kind="UiTransitionKind.Dropdown">
      <div
        v-if="open"
        ref="content"
        v-bind="$attrs"
        :class="[FLOATING_PLACE_CLASSES, FLOATING_PANEL_CLASSES, 'z-50', popoverClass]"
      >
        <slot :sheet="false" />
      </div>
    </UiTransition>
  </Teleport>
</template>
