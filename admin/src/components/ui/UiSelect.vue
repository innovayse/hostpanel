<script setup lang="ts" generic="T extends string | number | null">
/**
 * Themed listbox: the kit's one select, and the base every picker is built on.
 *
 * Replaces the native `<select>`, whose options cannot be themed across browsers. It is a WAI-ARIA
 * combobox with a listbox popup. Without `searchable` the trigger itself is the combobox
 * (select-only); with `searchable` or `remote` a filter field in the popup is the combobox and
 * the trigger opens it. The keyboard works the same either way: ArrowDown/ArrowUp/Home/End move
 * the highlight (`aria-activedescendant`), Enter picks it, Escape closes and hands the focus back,
 * Tab leaves, and typing filters (searchable) or jumps to a label (plain).
 *
 * A list that lives behind the API sets `remote`: the component then filters nothing itself, asks
 * the owner for matches through `search` (the empty term on open, then the typed term once typing
 * pauses) and shows `loading` while they are on the way. A picked option is remembered, so the
 * trigger keeps its label when the owner replaces the options with the next search.
 * The list is drawn in the page body (a teleport) and placed under the trigger with fixed positioning, because the
 * frosted cards of a page create stacking contexts and would paint a list rendered in place under the next card.
 * `data-testid` and classes land on the root element.
 */
import { ref, shallowRef, computed, watch, nextTick, onUnmounted, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import UiSkeleton from '@/components/ui/UiSkeleton.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { useDropdown } from '@/composables/useDropdown'
import { useFloatingPlace } from '@/composables/useFloatingPlace'
import { FLOATING_PLACE_CLASSES, FloatingAlign } from '@/utils/ui/floating'
import { SEARCH_DEBOUNCE_MS } from '@/lib/limits'
import { fieldInvalidClasses, fieldSurfaceClasses } from '@/utils/ui/input'
import { Key } from '@/utils/ui/keys'
import {
  TYPEAHEAD_RESET_MS,
  filterOptions,
  isPrintableKey,
  optionDomId,
  stepActive,
  typeaheadIndex,
} from '@/utils/ui/select'
import type { UiSelectOption } from '@/utils/ui/select'
import { UiTransitionKind } from '@/utils/ui/transition'

const { t } = useI18n()

/** Props for UiSelect. */
const props = defineProps<{
  /** Currently selected value. */
  modelValue: T
  /** Options to display. */
  options: readonly UiSelectOption<T>[]
  /** Text shown in the trigger while nothing is selected. */
  placeholder?: string
  /** Disables the select. */
  disabled?: boolean
  /** Enables a filter field inside the dropdown. */
  searchable?: boolean
  /**
   * The options are already the matches for the typed term (a lookup that lives behind the API).
   * The component stops filtering, asks for matches through `search` and implies a filter field.
   */
  remote?: boolean
  /** The owner is fetching options: a loading row is shown under the ones already there. */
  loading?: boolean
  /** Text of the row shown when no option is left; defaults to the kit's "No results". */
  emptyLabel?: string
  /**
   * Label of the current value while it is not among the options (a record the owner has not
   * loaded). Without it such a value shows the placeholder.
   */
  valueLabel?: string
  /** Gives the dropdown panel a wider minimum width, for long labels (country and phone lists). */
  wide?: boolean
  /** Id of the trigger button, so a caption can point at it (`UiField` hands one out). */
  id?: string
  /** Marks the value as refused by the server (red border, `aria-invalid`). */
  invalid?: boolean
  /**
   * The small, quiet look for toolbars: a trigger sized like a small button and a softer list (inset
   * rounded rows, a tinted selection instead of a solid one). Forms keep the default.
   */
  compact?: boolean
  /**
   * Accessible name of the trigger, for a select whose visible text is only the chosen value (a country
   * calling code). Without it the trigger is named by its text or by the caption pointing at `id`.
   */
  ariaLabel?: string
  /**
   * Joins the trigger to a field on its right (a phone number): square right corners and no right
   * border, so the two read as one control.
   */
  attached?: boolean
}>()

const emit = defineEmits<{
  /** Emitted when the user picks an option. */
  'update:modelValue': [value: T]
  /** Emitted by a `remote` list with the term to look up: the empty term on open, then the typed term. */
  search: [term: string]
}>()

/** Open flag, outside-pointer and Escape handling shared with the other floating lists. */
const { open, root, trigger, panel, show, hide, onEscape } = useDropdown()

/** Free room below the trigger, in pixels, under which the list (a filter field and about eight rows) opens above it. */
const LIST_MIN_ROOM_PX = 300

/** Gap between the trigger and the list, in pixels. */
const LIST_GAP_PX = 6

// The list is drawn in the page body, so no frosted card after the trigger can paint over it.
useFloatingPlace(trigger, panel, open, { align: FloatingAlign.Start, minRoom: LIST_MIN_ROOM_PX, gap: LIST_GAP_PX })

/** Id of the listbox, from which every option id derives. */
const listId = `${useId()}-list`

/** Filter field ref, focused when the list opens. */
const searchInput = ref<HTMLInputElement | null>(null)

/** Term typed into the filter field. */
const search = ref('')

/** Index of the highlighted row in the visible list, -1 for none. */
const activeIndex = ref(-1)

/** The option last picked, kept so a `remote` list that swaps its options still has the trigger's label. */
const picked = shallowRef<UiSelectOption<T> | null>(null)

/** Row elements by index, for scrolling the highlighted row into view. Not reactive: only read on demand. */
const optionEls = new Map<number, HTMLElement>()

/** Pending `remote` lookup, cleared when the list closes or the component unmounts. */
let searchTimer: ReturnType<typeof setTimeout> | undefined

/** Characters typed so far on a plain list, for the jump to a label. */
let typed = ''

/** When the last jump character was typed, to start a new word after a pause. */
let typedAt = 0

/** Whether the popup carries a filter field. */
const hasSearch = computed<boolean>(() => Boolean(props.searchable || props.remote))

/** Trigger classes shared by every shape: the kit's field surface (fill, hairline, focus ring) laid out as a row with the chevron at the end. */
const triggerBaseClasses = `w-full flex items-center justify-between gap-2 ${fieldSurfaceClasses}`

/**
 * Trigger height, padding, type size and radius: the default field look, a small button's when compact, or a field-joined one when attached.
 *
 * @returns The trigger's size and shape classes.
 */
const triggerSizeClasses = computed<string>(() => {
  if (props.compact) {
    return 'h-8 px-3 text-[0.8125rem] rounded-[9px]'
  }
  return props.attached
    ? 'h-10.5 px-3.5 text-sm rounded-l-control border-r-0'
    : 'h-10.5 px-3.5 text-sm rounded-control'
})

/**
 * The list's frame: the default, or a softer inset one when compact.
 *
 * @returns The panel's fixed place, border, surface and width classes (the width of the trigger unless `wide`).
 */
const panelClasses = computed<string>(() => {
  // A wide list is at least 18rem, but never wider than the screen less a margin, so on a phone it cannot reach past the page edge and widen the page.
  const width = props.wide ? 'min-w-[18rem] max-w-[calc(100vw-4rem)]' : 'w-[var(--fp-width)]'
  return props.compact
    ? `${FLOATING_PLACE_CLASSES} z-50 min-w-[5.5rem] origin-top overflow-hidden rounded-xl border border-border bg-surface-card p-1 shadow-lg shadow-black/20 ${width}`
    : `${FLOATING_PLACE_CLASSES} z-50 overflow-hidden rounded-[10px] border border-border bg-surface-card shadow-xl ${width}`
})

/**
 * The classes of one row of the list.
 *
 * @param selected - Whether the row is the chosen option.
 * @param active - Whether the row is the highlighted one (keyboard or pointer).
 * @param muted - Whether the row is a dim "none" choice.
 * @returns The row's size, shape and colours.
 */
const optionClasses = (selected: boolean, active: boolean, muted: boolean): string => {
  const base = props.compact ? 'rounded-lg px-2.5 py-1.5 text-[0.78rem]' : 'px-3 py-1.5 text-[0.82rem]'
  if (selected) {
    const look = props.compact
      ? 'bg-primary-500/10 font-medium text-primary-400 light:text-primary-700'
      : 'bg-primary-400 light:bg-primary-600 text-surface-base font-medium'
    return `${base} ${look} ${active ? 'ring-1 ring-inset ring-primary-500/50' : ''}`
  }
  const tone = muted ? 'text-text-muted' : 'text-text-secondary'
  return `${base} ${active ? 'bg-surface-hover text-text-primary' : tone}`
}

/** The options the reader can pick from now: matched by the owner when remote, else filtered here. */
const visibleOptions = computed<UiSelectOption<T>[]>(() =>
  hasSearch.value ? filterOptions(props.options, search.value, !props.remote) : [...props.options],
)

/** The option the model points at: from the options, else the one last picked. */
const selectedOption = computed<UiSelectOption<T> | undefined>(() => {
  const found = props.options.find(o => o.value === props.modelValue)
  if (found) {
    return found
  }
  return picked.value?.value === props.modelValue ? picked.value : undefined
})

/** The label of the currently selected option (short form for the trigger). */
const selectedLabel = computed<string>(
  () => selectedOption.value?.label ?? props.valueLabel ?? props.placeholder ?? '—',
)

/**
 * Whether the trigger is showing the placeholder (or a dim "none" choice) instead of a chosen label.
 *
 * Decided by whether an option matches the value, never by comparing texts: a select whose
 * placeholder equals the chosen option's label (the language switch shows "English" for both)
 * would otherwise read as empty and be drawn dimmed, like a disabled control.
 */
const showingPlaceholder = computed<boolean>(() =>
  selectedOption.value ? Boolean(selectedOption.value.muted) : props.valueLabel === undefined,
)

/** The DOM id of the highlighted row, named by `aria-activedescendant`; absent when closed or none. */
const activeId = computed<string | undefined>(() =>
  open.value && activeIndex.value >= 0 ? optionDomId(listId, activeIndex.value) : undefined,
)

/** The sentence of the empty row. */
const emptyText = computed<string>(() => props.emptyLabel ?? t('ui.uiSelect.noResults'))

/**
 * Remembers the DOM element of a row (or forgets it when the row goes away).
 *
 * @param index - Index of the row.
 * @param el - The element the template ref reports, or null on unmount.
 */
const setOptionEl = (index: number, el: unknown): void => {
  if (el instanceof HTMLElement) {
    optionEls.set(index, el)
  } else {
    optionEls.delete(index)
  }
}

/** Scrolls the highlighted row into view once the DOM shows it. */
const scrollActiveIntoView = (): void => {
  void nextTick(() => optionEls.get(activeIndex.value)?.scrollIntoView?.({ block: 'nearest' }))
}

/**
 * The row the highlight starts on.
 *
 * @param count - Number of visible rows.
 * @returns 0, or -1 when there is no row to highlight.
 */
const firstIndex = (count: number): number => (count > 0 ? 0 : -1)

/** Cancels a pending `remote` lookup. */
const cancelSearchTimer = (): void => {
  clearTimeout(searchTimer)
  searchTimer = undefined
}

/** Opens the list with the chosen row highlighted, focuses the filter field and, when remote, asks for the first matches. */
const openList = (): void => {
  if (props.disabled) {
    return
  }
  show()
  const chosen = visibleOptions.value.findIndex(o => o.value === props.modelValue)
  activeIndex.value = chosen >= 0 ? chosen : firstIndex(visibleOptions.value.length)
  if (props.remote) {
    emit('search', '')
  }
  if (hasSearch.value) {
    void nextTick(() => searchInput.value?.focus())
  }
  scrollActiveIntoView()
}

/** Opens a closed list and closes an open one (the trigger's click). */
const toggleList = (): void => {
  if (open.value) {
    hide()
  } else {
    openList()
  }
}

/**
 * Picks an option and closes the list, handing the focus back to the trigger.
 *
 * @param option - The option to select.
 */
const select = (option: UiSelectOption<T>): void => {
  picked.value = option
  emit('update:modelValue', option.value)
  hide(true)
}

/** Picks the highlighted row, if there is one. */
const pickActive = (): void => {
  const option = visibleOptions.value[activeIndex.value]
  if (option) {
    select(option)
  }
}

/**
 * Jumps the highlight to the row a typed word points at (plain lists only).
 *
 * @param char - The typed character.
 */
const typeahead = (char: string): void => {
  const now = Date.now()
  typed = now - typedAt > TYPEAHEAD_RESET_MS ? char : typed + char
  typedAt = now
  // A longer word may stay on the row it already points at; a fresh letter moves on to the next match.
  const from = typed.length > 1 ? activeIndex.value - 1 : activeIndex.value
  const index = typeaheadIndex(visibleOptions.value.map(o => o.label), typed, from)
  if (index >= 0) {
    activeIndex.value = index
    scrollActiveIntoView()
  }
}

/**
 * Keyboard contract of the trigger and of the filter field.
 *
 * @param event - The key event.
 */
const onKeydown = (event: KeyboardEvent): void => {
  if (props.disabled) {
    return
  }
  if (!open.value) {
    if (event.key === Key.ArrowDown || event.key === Key.ArrowUp) {
      event.preventDefault()
      openList()
    } else if (hasSearch.value && isPrintableKey(event)) {
      // Typing on a closed searchable list opens it with the typed character as the first search character.
      event.preventDefault()
      openList()
      search.value = event.key
    }
    return
  }
  onEscape(event)
  switch (event.key) {
    case Key.ArrowDown:
    case Key.ArrowUp:
      event.preventDefault()
      activeIndex.value = stepActive(activeIndex.value, visibleOptions.value.length, event.key)
      scrollActiveIntoView()
      break
    case Key.Home:
    case Key.End:
      // In the filter field these keys belong to the caret.
      if (!hasSearch.value) {
        event.preventDefault()
        activeIndex.value = stepActive(activeIndex.value, visibleOptions.value.length, event.key)
        scrollActiveIntoView()
      }
      break
    case Key.Enter:
      event.preventDefault()
      pickActive()
      break
    case Key.Tab:
      hide()
      break
    default:
      if (!hasSearch.value && isPrintableKey(event)) {
        typeahead(event.key)
      }
  }
}

watch(open, (isOpen) => {
  if (!isOpen) {
    cancelSearchTimer()
    search.value = ''
    activeIndex.value = -1
  }
})

watch(search, () => {
  activeIndex.value = firstIndex(visibleOptions.value.length)
  if (props.remote && open.value) {
    cancelSearchTimer()
    searchTimer = setTimeout(() => emit('search', search.value), SEARCH_DEBOUNCE_MS)
  }
})

// Fresh matches from the owner can leave the highlight past the end or on nothing.
watch(() => visibleOptions.value.length, (count) => {
  if (open.value && (activeIndex.value < 0 || activeIndex.value >= count)) {
    activeIndex.value = firstIndex(count)
  }
})

onUnmounted(cancelSearchTimer)
</script>

<template>
  <div ref="root" class="relative">
    <!-- Trigger: the combobox itself on a plain list, the popup's opener when the popup holds the filter field -->
    <button
      ref="trigger"
      type="button"
      :disabled="props.disabled"
      :role="hasSearch ? undefined : 'combobox'"
      :aria-expanded="open"
      aria-haspopup="listbox"
      :aria-controls="open ? listId : undefined"
      :aria-activedescendant="hasSearch ? undefined : activeId"
      data-testid="ui-select-trigger"
      :id="props.id"
      :aria-label="props.ariaLabel"
      :aria-invalid="props.invalid || undefined"
      :class="[triggerBaseClasses, triggerSizeClasses, open ? 'border-text-accent ring-2 ring-text-accent/40' : '', props.invalid ? fieldInvalidClasses : '']"
      @click="toggleList"
      @keydown="onKeydown"
    >
      <span class="truncate" :class="showingPlaceholder ? 'text-text-muted' : 'text-text-primary'">
        {{ selectedLabel }}
      </span>
      <svg
        class="text-text-secondary shrink-0 transition-transform duration-200"
        :class="[compact ? 'w-3.5 h-3.5' : 'w-4 h-4', open ? 'rotate-180' : '']"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        stroke-width="2"
        aria-hidden="true"
      >
        <polyline points="6 9 12 15 18 9" />
      </svg>
    </button>

    <!-- Dropdown panel: in the page body, so no card around the trigger can paint over it -->
    <Teleport to="body">
      <UiTransition :kind="UiTransitionKind.Dropdown">
        <div
          v-if="open"
          ref="panel"
          data-testid="ui-select-panel"
          :class="panelClasses"
          @mousedown.stop
        >
          <!-- Filter field -->
          <div v-if="hasSearch" class="p-1.5 border-b border-border">
            <input
              ref="searchInput"
              v-model="search"
              type="text"
              role="combobox"
              aria-autocomplete="list"
              aria-expanded="true"
              :aria-controls="listId"
              :aria-activedescendant="activeId"
              :aria-label="t('shared.search')"
              :placeholder="t('shared.search')"
              data-testid="ui-select-search"
              class="w-full h-8 px-3 text-[0.8125rem] rounded-[9px]"
              :class="fieldSurfaceClasses"
              @keydown="onKeydown"
            >
          </div>

          <div
            :id="listId"
            role="listbox"
            class="max-h-56 overflow-y-auto"
            :class="compact ? 'flex flex-col gap-0.5' : 'py-1'"
          >
            <div
              v-for="(opt, index) in visibleOptions"
              :id="optionDomId(listId, index)"
              :key="String(opt.value)"
              :ref="(el) => setOptionEl(index, el)"
              role="option"
              data-testid="ui-select-option"
              :aria-selected="opt.value === props.modelValue"
              class="w-full flex cursor-pointer items-center justify-between transition-colors duration-150"
              :class="optionClasses(opt.value === props.modelValue, index === activeIndex, Boolean(opt.muted))"
              @mousedown.prevent.stop="select(opt)"
              @mousemove="activeIndex = index"
            >
              <span class="flex min-w-0 flex-1 flex-col">
                <span v-if="opt.caption" class="truncate text-[0.68rem] font-semibold opacity-70">{{ opt.caption }}</span>
                <span class="truncate">{{ opt.displayLabel ?? opt.label }}</span>
                <span v-if="opt.description" class="truncate text-[0.75rem] opacity-70">{{ opt.description }}</span>
              </span>
              <svg
                v-if="opt.value === props.modelValue"
                class="w-3.5 h-3.5 shrink-0 ml-2"
                :class="compact ? 'text-primary-400 light:text-primary-700' : 'text-surface-base'"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2.5"
                aria-hidden="true"
              >
                <polyline points="20 6 9 17 4 12" />
              </svg>
            </div>
          </div>

          <!-- Loading and empty rows sit beside the listbox, not in it: they are not options -->
          <div v-if="props.loading" role="status" aria-busy="true" data-testid="ui-select-loading" class="px-3 py-2">
            <UiSkeleton :lines="3" />
            <span class="sr-only">{{ t('ui.loading') }}</span>
          </div>
          <p v-else-if="visibleOptions.length === 0" role="status" data-testid="ui-select-empty" class="px-3 py-2 text-[0.82rem] text-text-secondary">
            {{ emptyText }}
          </p>
        </div>
      </UiTransition>
    </Teleport>
  </div>
</template>
