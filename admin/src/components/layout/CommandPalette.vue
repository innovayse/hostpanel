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
import { computed, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import UiCommandDialog from '@/components/ui/UiCommandDialog.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiMark from '@/components/ui/UiMark.vue'
import UiOption from '@/components/ui/UiOption.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { buildPaletteEntries, filterPaletteEntries, PaletteGroup, splitLabel } from '@/lib/commandPalette'
import type { PaletteEntry } from '@/lib/commandPalette'
import { Icon } from '@/lib/icons'
import { useNavSections } from '@/composables/useNavSections'
import { useCommandPaletteStore } from '@/stores/commandPalette'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiAlignItems, UiBreakpoint, UiDirection, UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'
import { Key } from '@/utils/ui/keys'

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

/**
 * Arrow keys move the highlight and Enter opens it; Escape and Tab are the dialog surface's.
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
  }
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

onMounted(() => {
  window.addEventListener('keydown', onShortcut)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onShortcut)
})
</script>

<template>
  <UiCommandDialog
    :open="palette.isOpen"
    :label="t('commandPalette.title')"
    data-testid="command-palette"
    @close="close"
    @keydown="onKeydown"
  >
    <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.None" class="shrink-0 gap-2 border-b border-border-subtle p-3">
      <UiStack :gap="UiGap.None" class="relative min-w-0 flex-1">
        <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" class="pointer-events-none absolute left-3 top-1/2 flex -translate-y-1/2 items-center">
          <UiIcon :markup="Icon.Search" />
        </UiText>
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
      </UiStack>
      <UiIconButton
        :label="t('shared.close')"
        :variant="UiButtonVariant.Ghost"
        :size="UiSize.Sm"
        class="sm:hidden"
        data-testid="command-palette-close"
        @click="close"
      >
        <UiIcon :markup="Icon.Close" :size="UiSize.Sm" />
      </UiIconButton>
    </UiStack>

    <UiStack
      :id="`${baseId}-list`"
      :gap="UiGap.None"
      role="listbox"
      :aria-label="t('commandPalette.title')"
      data-testid="command-palette-list"
      class="min-h-0 flex-1 overflow-y-auto p-2"
    >
      <UiStack
        v-if="flatEntries.length === 0"
        :gap="UiGap.None"
        role="status"
        data-testid="command-palette-empty"
        class="px-3 py-10 text-center"
      >
        <UiText :tone="UiTextTone.Muted">
          {{ t('commandPalette.empty', { query: query.trim() }) }}
        </UiText>
      </UiStack>
      <UiStack
        v-for="(section, sectionIndex) in sections"
        :key="section.group"
        role="group"
        :aria-labelledby="`${baseId}-group-${section.group}`"
        :data-testid="`command-palette-group-${section.group}`"
        :gap="UiGap.None"
        class="pb-1"
      >
        <UiText
          :id="`${baseId}-group-${section.group}`"
          :tone="UiTextTone.Muted"
          class="px-3 pb-1 pt-2 text-[0.68rem] font-semibold uppercase tracking-[0.08em]"
        >
          {{ groupLabel(section.group) }}
        </UiText>
        <UiOption
          v-for="(entry, entryIndex) in section.entries"
          :id="optionId(flatIndex(sectionIndex, entryIndex))"
          :key="`${section.group}:${entry.to}`"
          :active="flatIndex(sectionIndex, entryIndex) === activeIndex"
          :data-testid="`command-palette-option-${flatIndex(sectionIndex, entryIndex)}`"
          @select="choose(entry)"
          @hover="activeIndex = flatIndex(sectionIndex, entryIndex)"
        >
          <UiIcon :markup="entry.icon" class="text-text-muted" />
          <UiText :as="UiTextTag.Span" :tone="UiTextTone.Plain" class="min-w-0 flex-1 truncate">
            <template v-for="part in splitLabel(entry.label, query)" :key="part.start">
              <UiMark v-if="part.match">{{ part.text }}</UiMark>
              <template v-else>{{ part.text }}</template>
            </template>
          </UiText>
          <UiText v-if="entry.hint !== ''" :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="shrink-0">{{ entry.hint }}</UiText>
        </UiOption>
      </UiStack>
    </UiStack>

    <UiStack
      :direction="UiDirection.Row"
      :align="UiAlignItems.Center"
      :gap="UiGap.None"
      :visible-from="UiBreakpoint.Sm"
      class="shrink-0 gap-4 border-t border-border-subtle px-4 py-2"
      data-testid="command-palette-footer"
    >
      <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('commandPalette.hintNavigate') }}</UiText>
      <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('commandPalette.hintOpen') }}</UiText>
      <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('commandPalette.hintClose') }}</UiText>
    </UiStack>
  </UiCommandDialog>
</template>
