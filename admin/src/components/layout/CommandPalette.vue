<script setup lang="ts">
/**
 * The command palette: a search box over the panel's pages and create actions, opened with Ctrl K / Cmd K
 * or by anything that calls `useCommandPaletteStore().open()` (the top bar's search button).
 *
 * Pages come from the sidebar's own definition (`lib/navigation.ts`, with the plugins' Add-ons), so the two
 * can never list different pages; create actions come from `lib/commandPalette.ts`. While the search is
 * empty the viewer's recently opened entries lead; typing filters, highlights the match and groups the rest.
 * It is a WAI-ARIA combobox (the field) over a listbox: Arrow Up/Down move through the entries, Enter opens
 * the highlighted one, Escape closes. On a phone it fills the screen. The shell mounts it once; this
 * component registers the global shortcut itself and removes it when it goes away.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { buildPaletteEntries, filterPaletteEntries, PaletteGroup, splitLabel } from '@/lib/commandPalette'
import type { PaletteEntry } from '@/lib/commandPalette'
import { Icon } from '@/lib/icons'
import { useNavSections } from '@/composables/useNavSections'
import { useCommandPaletteStore } from '@/stores/commandPalette'
import { Key } from '@/utils/ui/keys'
import { useModalSurface } from '@/utils/ui/modalSurface'
import { UiTransitionKind } from '@/utils/ui/transition'

/** A group of entries as listed: its heading and the entries under it. */
interface PaletteSection {
  /** Which group this is. */
  group: PaletteGroup
  /** Entries in display order. */
  entries: PaletteEntry[]
}

const { t } = useI18n()

/** Router, to open the chosen entry. */
const router = useRouter()

/** Palette store: open state and the viewer's recent pages. */
const palette = useCommandPaletteStore()

/** The navigation sections (the panel's own and the Add-ons plugins declare), offered as pages. */
const navSections = useNavSections()

/** Base of the ids that tie the field, the list and its options together. */
const baseId = useId()

/** The text typed in the field. */
const query = ref('')

/** Index of the highlighted entry in the flat list. */
const activeIndex = ref(0)

/** The palette panel, for focus handling. */
const panel = ref<HTMLElement | null>(null)

/** The option elements by flat index, to scroll the highlighted one into view. */
const optionElements = new Map<number, HTMLElement>()

/** Every entry the palette can open. */
const allEntries = computed<PaletteEntry[]>(() =>
  buildPaletteEntries(navSections.value, t),
)

/** The groups as listed: recent first while the search is empty, then actions and pages (filtered when typing). */
const sections = computed<PaletteSection[]>(() => {
  const typing = query.value.trim() !== ''
  const found = filterPaletteEntries(allEntries.value, query.value)
  const result: PaletteSection[] = []
  if (!typing) {
    const recent = palette.recent
      .map((path) => allEntries.value.find((entry) => entry.to === path))
      .filter((entry): entry is PaletteEntry => entry !== undefined)
    result.push({ group: PaletteGroup.Recent, entries: recent })
  }
  result.push({ group: PaletteGroup.Actions, entries: found.filter((entry) => entry.group === PaletteGroup.Actions) })
  result.push({ group: PaletteGroup.Navigation, entries: found.filter((entry) => entry.group === PaletteGroup.Navigation) })
  return result.filter((section) => section.entries.length > 0)
})

/** All listed entries in display order; the highlight index points into this list. */
const flatEntries = computed<PaletteEntry[]>(() => sections.value.flatMap((section) => section.entries))

/** Index of the first entry of each group within {@link flatEntries}. */
const sectionOffsets = computed<number[]>(() => {
  let offset = 0
  return sections.value.map((section) => {
    const start = offset
    offset += section.entries.length
    return start
  })
})

/**
 * The flat index of one entry of one group.
 *
 * @param sectionIndex - Position of the group among the listed groups.
 * @param entryIndex - Position of the entry within its group.
 * @returns The index of the entry within {@link flatEntries}.
 */
const flatIndex = (sectionIndex: number, entryIndex: number): number => (sectionOffsets.value[sectionIndex] ?? 0) + entryIndex

/**
 * DOM id of the option at a flat index.
 *
 * @param index - The option's flat index.
 * @returns The element id of that option.
 */
const optionId = (index: number): string => `${baseId}-option-${index}`

/** DOM id of the highlighted option, or undefined when nothing is listed. */
const activeOptionId = computed<string | undefined>(() => (flatEntries.value.length === 0 ? undefined : optionId(activeIndex.value)))

/**
 * Stores or forgets the element of one option, kept to scroll the highlighted option into view.
 *
 * @param index - The option's flat index.
 * @param element - The element, or null when it is removed.
 */
const setOptionElement = (index: number, element: unknown): void => {
  if (element instanceof HTMLElement) {
    optionElements.set(index, element)
  } else {
    optionElements.delete(index)
  }
}

/**
 * The heading of a group.
 *
 * @param group - The group.
 * @returns The translated heading.
 */
const groupLabel = (group: PaletteGroup): string => t(`commandPalette.groups.${group}`)

/**
 * Opens an entry: remembers it for the viewer, closes the palette and navigates.
 *
 * @param entry - The chosen entry.
 */
const choose = async (entry: PaletteEntry): Promise<void> => {
  palette.remember(entry.to)
  palette.close()
  await router.push(entry.to)
}

/**
 * Moves the highlight, wrapping at both ends.
 *
 * @param step - 1 for the next entry, -1 for the previous one.
 */
const move = (step: number): void => {
  const count = flatEntries.value.length
  if (count > 0) {
    activeIndex.value = (activeIndex.value + step + count) % count
  }
}

/** Closes the palette (Escape, the backdrop, the phone close button). */
const close = (): void => {
  palette.close()
}

/** Keyboard handling shared with the other modal surfaces: Escape closes, Tab stays inside. */
const { onKeydown: surfaceKeydown, focusIn } = useModalSurface(panel, () => palette.isOpen, close)

/**
 * Arrow keys move the highlight and Enter opens it; every other key goes to the shared modal handling.
 *
 * @param event - The keystroke that reached the panel.
 */
const onKeydown = (event: KeyboardEvent): void => {
  if (event.key === Key.ArrowDown || event.key === Key.ArrowUp) {
    event.preventDefault()
    move(event.key === Key.ArrowDown ? 1 : -1)
    return
  }
  if (event.key === Key.Enter) {
    event.preventDefault()
    const entry = flatEntries.value[activeIndex.value]
    if (entry !== undefined) {
      void choose(entry)
    }
    return
  }
  surfaceKeydown(event)
}

/**
 * The global shortcut: Ctrl K or Cmd K toggles the palette from anywhere on the page.
 *
 * @param event - The keystroke that reached the window.
 */
const onShortcut = (event: KeyboardEvent): void => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
    event.preventDefault()
    palette.toggle()
  }
}

// A new search starts at the top of the list.
watch(query, () => {
  activeIndex.value = 0
})

// Each opening starts from an empty search.
watch(() => palette.isOpen, (open) => {
  if (open) {
    query.value = ''
    activeIndex.value = 0
  }
})

// The highlighted option stays visible while the arrow keys walk a long list.
watch(activeIndex, async (index) => {
  await nextTick()
  optionElements.get(index)?.scrollIntoView?.({ block: 'nearest' })
})

onMounted(() => {
  window.addEventListener('keydown', onShortcut)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onShortcut)
})
</script>

<template>
  <Teleport to="body">
    <UiTransition :kind="UiTransitionKind.Dialog" @after-enter="focusIn">
      <div
        v-if="palette.isOpen"
        data-testid="command-palette-backdrop"
        class="fixed inset-0 z-50 flex items-start justify-center bg-black/60 backdrop-blur-sm sm:px-4 sm:pt-[12vh]"
        @click.self="close"
      >
        <div
          ref="panel"
          data-testid="command-palette"
          role="dialog"
          aria-modal="true"
          :aria-label="t('commandPalette.title')"
          tabindex="-1"
          class="glass-strong flex h-dvh w-full flex-col overflow-hidden border-border-strong shadow-2xl outline-none sm:h-auto sm:max-h-[70dvh] sm:max-w-xl sm:rounded-panel sm:border"
          @keydown="onKeydown"
        >
          <div class="flex shrink-0 items-center gap-2 border-b border-border-subtle p-3">
            <div class="relative min-w-0 flex-1">
              <span class="pointer-events-none absolute left-3 top-1/2 flex -translate-y-1/2 items-center text-text-muted">
                <UiIcon :markup="Icon.Search" />
              </span>
              <UiInput
                v-model="query"
                class="pl-9"
                data-testid="command-palette-input"
                role="combobox"
                aria-autocomplete="list"
                aria-expanded="true"
                :aria-controls="`${baseId}-list`"
                :aria-activedescendant="activeOptionId"
                :aria-label="t('commandPalette.title')"
                :placeholder="t('commandPalette.placeholder')"
                autocomplete="off"
              />
            </div>
            <UiIconButton
              :label="t('commandPalette.close')"
              size="sm"
              class="sm:hidden"
              data-testid="command-palette-close"
              @click="close"
            >
              <UiIcon :markup="Icon.Close" size="sm" />
            </UiIconButton>
          </div>

          <div
            :id="`${baseId}-list`"
            role="listbox"
            :aria-label="t('commandPalette.title')"
            data-testid="command-palette-list"
            class="min-h-0 flex-1 overflow-y-auto p-2"
          >
            <div
              v-if="flatEntries.length === 0"
              role="status"
              data-testid="command-palette-empty"
              class="px-3 py-10 text-center"
            >
              <UiText tone="muted">
                {{ t('commandPalette.empty', { query: query.trim() }) }}
              </UiText>
            </div>
            <div
              v-for="(section, sectionIndex) in sections"
              :key="section.group"
              role="group"
              :aria-labelledby="`${baseId}-group-${section.group}`"
              :data-testid="`command-palette-group-${section.group}`"
              class="pb-1"
            >
              <div
                :id="`${baseId}-group-${section.group}`"
                class="px-3 pb-1 pt-2 text-[0.68rem] font-semibold uppercase tracking-[0.08em] text-text-muted"
              >
                {{ groupLabel(section.group) }}
              </div>
              <div
                v-for="(entry, entryIndex) in section.entries"
                :id="optionId(flatIndex(sectionIndex, entryIndex))"
                :key="`${section.group}:${entry.to}`"
                :ref="(element) => setOptionElement(flatIndex(sectionIndex, entryIndex), element)"
                role="option"
                :aria-selected="flatIndex(sectionIndex, entryIndex) === activeIndex"
                :data-testid="`command-palette-option-${flatIndex(sectionIndex, entryIndex)}`"
                class="flex cursor-pointer items-center gap-3 rounded-control px-3 py-2 text-sm text-text-secondary transition-colors"
                :class="flatIndex(sectionIndex, entryIndex) === activeIndex ? 'bg-surface-hover text-text-primary' : ''"
                @click="choose(entry)"
                @mousemove="activeIndex = flatIndex(sectionIndex, entryIndex)"
              >
                <UiIcon :markup="entry.icon" class="text-text-muted" />
                <span class="min-w-0 flex-1 truncate">
                  <template v-for="part in splitLabel(entry.label, query)" :key="part.start">
                    <mark v-if="part.match" class="bg-transparent font-semibold text-text-accent">{{ part.text }}</mark>
                    <template v-else>{{ part.text }}</template>
                  </template>
                </span>
                <span v-if="entry.hint !== ''" class="shrink-0 text-xs text-text-muted">{{ entry.hint }}</span>
              </div>
            </div>
          </div>

          <div class="hidden shrink-0 items-center gap-4 border-t border-border-subtle px-4 py-2 text-xs text-text-muted sm:flex" data-testid="command-palette-footer">
            <span>{{ t('commandPalette.hintNavigate') }}</span>
            <span>{{ t('commandPalette.hintOpen') }}</span>
            <span>{{ t('commandPalette.hintClose') }}</span>
          </div>
        </div>
      </div>
    </UiTransition>
  </Teleport>
</template>
