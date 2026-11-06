/**
 * The selection a preview block and a chat block of one view share: which elements are selected, on which release, and
 * whether the mode is on. Chips hold only what the plugin's server answered, so no text from a frame ever reaches the page.
 *
 * Rendering wiring like the view context, so it travels by provide/inject; every call goes through `stores/plugins.ts`.
 *
 * @module composables/usePluginSelection
 */

import type { InjectionKey, Ref } from 'vue'
import { PLUGIN_SELECTION_MAX_IDS } from '~/constants/pluginSelection'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginSelectionChip, PluginSelectionClaim } from '~/types/pluginpreviewselection'
import type { PluginViewRoute } from '~/types/pluginviewroute'
import { readPluginField } from '~/utils/plugins'

/** What the blocks of a view share. */
export interface PluginSelectionState {
  /** Whether the mode is on. */
  active: Ref<boolean>
  /** The selected elements as the server named them. */
  chips: Ref<PluginSelectionChip[]>
  /** The sentence of a failed lookup; empty when there is none. */
  error: Ref<string>
  /** Adds an element, or removes it when it is selected. */
  toggle: (route: PluginViewRoute, release: string, id: string) => Promise<void>
  /** Removes an element. */
  remove: (id: string) => void
  /** Empties the selection and leaves the mode. */
  clear: () => void
  /** What a message would carry; null without a selection. */
  claim: () => PluginSelectionClaim | null
}

/** The injection key of the selection. */
const PLUGIN_SELECTION_KEY: InjectionKey<PluginSelectionState> = Symbol('plugin-selection')

/**
 * Reads the items of a lookup's answer, those asked for and of the right shape.
 *
 * @param data - The answer.
 * @param asked - The ids asked about.
 * @returns The chips.
 */
const chipsOf = (data: unknown, asked: readonly string[]): PluginSelectionChip[] => {
  const items = readPluginField(data, 'items')
  const chips: PluginSelectionChip[] = []
  for (const item of Array.isArray(items) ? items : []) {
    const id = readPluginField(item, 'id')
    const label = readPluginField(item, 'label')
    if (typeof id === 'string' && typeof label === 'string' && asked.includes(id)) {
      chips.push({ id, label })
    }
  }
  return chips
}

/**
 * Creates a view's selection and hands it to the blocks below the calling component.
 *
 * @param pluginId - The plugin.
 * @param values - The page's route values.
 * @returns The selection.
 */
export const providePluginSelection = (pluginId: string, values: Record<string, string>): PluginSelectionState => {
  const store = usePluginsStore()
  /** Whether the mode is on. */
  const active = ref(false)
  /** The selected elements. */
  const chips = ref<PluginSelectionChip[]>([])
  /** The sentence of a failed lookup. */
  const error = ref('')
  /** The release the chips were looked up on. */
  let chipRelease = ''
  /** Counts the lookups, so an answer overtaken by a later toggle or a clear is dropped. */
  let generation = 0

  /**
   * Adds or removes an element and asks the server what the selection really holds.
   *
   * @param route - The lookup route.
   * @param release - The release on the preview.
   * @param id - The element id, a claim.
   * @returns Resolves when the answer was taken or dropped.
   */
  const toggle = async (route: PluginViewRoute, release: string, id: string): Promise<void> => {
    const current = chips.value.map(chip => chip.id)
    const asked = current.includes(id) ? current.filter(other => other !== id) : [...current, id]
    if (asked.length > PLUGIN_SELECTION_MAX_IDS) {
      return
    }
    const mine = ++generation
    if (asked.length === 0) {
      chips.value = []
      return
    }
    const result = await store.callRoute(pluginId, route, values, { releaseId: release, ids: asked })
    if (mine !== generation) {
      return
    }
    error.value = result.ok || result.unavailable ? '' : result.error
    if (result.ok) {
      chips.value = chipsOf(result.data, asked)
      chipRelease = release
    } else {
      chips.value = []
    }
  }

  /**
   * Removes an element.
   *
   * @param id - The element id.
   * @returns Nothing.
   */
  const remove = (id: string): void => {
    generation++
    chips.value = chips.value.filter(chip => chip.id !== id)
  }

  /**
   * Empties the selection and leaves the mode.
   *
   * @returns Nothing.
   */
  const clear = (): void => {
    generation++
    chips.value = []
    active.value = false
    error.value = ''
  }

  /**
   * What a message would carry.
   *
   * @returns The claim, or null without a selection.
   */
  const claim = (): PluginSelectionClaim | null =>
    (chips.value.length ? { releaseId: chipRelease, ids: chips.value.map(chip => chip.id) } : null)

  const state: PluginSelectionState = { active, chips, error, toggle, remove, clear, claim }
  provide(PLUGIN_SELECTION_KEY, state)
  return state
}

/**
 * Reads the selection of the view the calling block is rendered in.
 *
 * @returns The selection.
 * @throws Error when the block is rendered outside a plugin view, which is a host bug.
 */
export const usePluginSelection = (): PluginSelectionState => {
  const state = inject(PLUGIN_SELECTION_KEY, null)
  if (!state) {
    throw new Error('A plugin block is rendered outside a plugin view.')
  }
  return state
}
