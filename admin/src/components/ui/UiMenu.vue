<script setup lang="ts">
/**
 * Dropdown menu of named actions behind one trigger button.
 *
 * The trigger shows the `default` slot (a kebab ⋮ icon when the slot is empty) and carries an accessible label
 * (`ariaLabel`, "Actions" by default). Destructive actions move to the end of the list in the danger tone; each still
 * asks for confirmation in its own `onSelect`. The menu closes on Escape, on a click outside the component, and after
 * any action is activated. The list is drawn in the page body (a teleport), because a frosted card around it would
 * otherwise become the containing block of a fixed box. Disabled actions are shown faded and cannot be selected. An
 * action with a `to` route is a real link (an anchor via RouterLink), so middle click, Ctrl/Cmd click and "open in new
 * tab" work and assistive technology announces a link. No actions render nothing.
 *
 * An action with `checked` set is one choice of a group: its row is a `menuitemradio` with `aria-checked`, and a check
 * mark is drawn while it is the current choice.
 *
 * Keyboard behaviour follows the WAI-ARIA APG "Menu Button" pattern:
 * - Enter / click on the trigger opens the menu and moves focus to the first enabled item.
 * - ArrowDown / ArrowUp cycle focus through the enabled items.
 * - Escape closes the menu and returns focus to the trigger button.
 * - Tab moving focus out of the component closes the menu without taking focus back.
 */
import { computed, nextTick, watch } from 'vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'
import { useI18n } from 'vue-i18n'
import { RouterLink } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import { Icon } from '@/lib/icons'
import { useDropdown } from '@/composables/useDropdown'
import { useFloatingPlace } from '@/composables/useFloatingPlace'
import { FLOATING_PANEL_CLASSES, FLOATING_PLACE_CLASSES, FloatingAlign } from '@/utils/ui/floating'
import { Key } from '@/utils/ui/keys'
import { elementAttrs, isLink, itemRole, toneClasses } from '@/utils/ui/menu'
import type { UiMenuAction } from '@/utils/ui/menu'

/** Props for UiMenu. */
const props = defineProps<{
  /** The actions to show in the menu. */
  actions: UiMenuAction[]
  /** Accessible label of the trigger; the shared "Actions" text when not given. */
  ariaLabel?: string
  /** `data-testid` of the trigger; `ui-menu-trigger` when not given. */
  testid?: string
  /** `data-testid` of the open menu panel; `ui-menu-panel` when not given. */
  menuTestid?: string
}>()

const { t } = useI18n()

/** The actions in display order: the destructive ones last, each group keeping the order it was given in. */
const ordered = computed<UiMenuAction[]>(() => [
  ...props.actions.filter((action) => !action.danger),
  ...props.actions.filter((action) => action.danger),
])

/** The accessible label of the trigger: the caller's, or the shared "Actions" text. */
const triggerLabel = computed<string>(() => props.ariaLabel ?? t('shared.actions'))

/** The `data-testid` of the trigger: the caller's id, or the shared default. */
const triggerTestid = computed<string>(() => props.testid ?? 'ui-menu-trigger')

/** The `data-testid` of the open panel: the caller's id, or the shared default. */
const panelTestid = computed<string>(() => props.menuTestid ?? 'ui-menu-panel')

/** Look of one row of the open menu. */
const MENU_ITEM_CLASSES = 'flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors hover:bg-surface-hover focus-visible:outline-none focus-visible:bg-surface-hover disabled:cursor-not-allowed disabled:opacity-40'

/** Selector of the focusable, enabled menu items, buttons and link anchors alike. */
const ITEM_SELECTOR = '[role^="menuitem"]:not([disabled])'

/** Open flag, outside-pointer and Escape handling shared with the other floating lists; `panel` is the teleported menu. */
const { open, root, trigger, panel, isInside, hide, toggle, onEscape } = useDropdown()

/** Gap between the trigger and the menu, in pixels. */
const MENU_GAP_PX = 4

/** Free room below the trigger, in pixels, under which the menu opens above it. */
const MENU_MIN_ROOM_PX = 160

// The menu is `fixed` and in the page body, so a table that scrolls inside its card (`overflow-x-auto`) cannot clip
// it or grow a scrollbar for it; it opens above the trigger when there is little room below and follows the trigger
// while the page scrolls.
useFloatingPlace(trigger, panel, open, { align: FloatingAlign.End, minRoom: MENU_MIN_ROOM_PX, gap: MENU_GAP_PX })

/**
 * Activates an action and closes the menu.
 *
 * @param action - The action the user picked.
 */
const select = (action: UiMenuAction): void => {
  if (action.disabled) { return }
  hide()
  action.onSelect?.()
  if (!isLink(action)) {
    trigger.value?.focus()
  }
}

/**
 * Closes the menu when focus moves to something outside the component (Tab out of the menu), leaving focus where
 * the user sent it. Focus moving between the trigger and the items stays inside the component and keeps the menu open.
 *
 * @param e - The focusout event; `relatedTarget` is the element receiving focus, or null when focus leaves the page.
 */
const onFocusOut = (e: FocusEvent): void => {
  if (open.value && e.relatedTarget && !isInside(e.relatedTarget)) {
    hide()
  }
}

/**
 * Handles keyboard navigation on the trigger and inside the open menu.
 * - Escape: close and return focus to trigger.
 * - ArrowDown / ArrowUp: move focus between enabled items.
 *
 * @param e - The keyboard event.
 */
const onKeydown = (e: KeyboardEvent): void => {
  onEscape(e)
  if (!open.value) { return }

  if (e.key === Key.ArrowDown || e.key === Key.ArrowUp) {
    e.preventDefault()
    const items = Array.from(panel.value?.querySelectorAll<HTMLElement>(ITEM_SELECTOR) ?? [])
    if (!items.length) { return }
    const idx = items.findIndex((item) => item === document.activeElement)
    const next =
      e.key === Key.ArrowDown
        ? items[(idx + 1) % items.length]
        : items[(idx - 1 + items.length) % items.length]
    next?.focus()
  }
}

// Focus moves to the first enabled item once the menu is rendered.
watch(open, async (val) => {
  if (!val) { return }
  await nextTick()
  // Closed again before the render finished: focus nothing.
  if (!open.value) { return }
  panel.value?.querySelector<HTMLElement>(ITEM_SELECTOR)?.focus()
})
</script>

<template>
  <div v-if="ordered.length > 0" ref="root" class="relative inline-block" @focusout="onFocusOut">
    <!-- Trigger: the slot content, a kebab by default -->
    <button
      ref="trigger"
      type="button"
      :aria-label="triggerLabel"
      aria-haspopup="menu"
      :aria-expanded="open"
      class="group inline-flex items-center justify-center rounded-control focus-visible:outline-2 focus-visible:outline-primary-500"
      :data-testid="triggerTestid"
      @click="toggle"
      @keydown="onKeydown"
    >
      <slot>
        <span class="inline-flex size-8 items-center justify-center rounded-control text-text-muted transition-colors group-hover:bg-surface-hover group-hover:text-text-primary">
          <UiIcon :markup="Icon.MoreVertical" size="sm" />
        </span>
      </slot>
    </button>

    <!-- Dropdown: in the page body, so no card around the trigger can clip it or re-anchor its fixed place -->
    <Teleport to="body">
      <UiTransition :kind="UiTransitionKind.Dropdown">
        <div
          v-if="open"
          ref="panel"
          role="menu"
          :class="[FLOATING_PLACE_CLASSES, FLOATING_PANEL_CLASSES, 'z-50 min-w-[12rem] rounded-control p-1.5']"
          :data-testid="panelTestid"
          @focusout="onFocusOut"
          @keydown="onKeydown"
        >
          <component
            :is="isLink(action) ? RouterLink : 'button'"
            v-for="action in ordered"
            :key="action.testid ?? action.label"
            v-bind="elementAttrs(action)"
            :role="itemRole(action)"
            :aria-checked="action.checked"
            :data-testid="action.testid"
            class="rounded-lg"
            :class="[MENU_ITEM_CLASSES, toneClasses(action)]"
            @click="select(action)"
          >
            <UiIcon v-if="action.icon" :markup="action.icon" size="sm" />
            {{ action.label }}
            <UiIcon v-if="action.checked" :markup="Icon.Check" size="sm" class="ml-auto" />
          </component>
        </div>
      </UiTransition>
    </Teleport>
  </div>
</template>
