/**
 * The element selections of one view. Each preview block that declares a selection holds its own: which elements are
 * selected, on which release, and whether the mode is on, so two previews never mix ids or clear each other. A chat reads
 * the one last selected on. Chips hold only what the plugin's server answered, so no text from a frame ever reaches the page.
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

/** One preview block's selection. */
export interface PluginSelectionState {
  /** Names the preview within its view. */
  owner: string
  /** Whether the mode is on. */
  active: Ref<boolean>
  /** The selected elements as the server named them. */
  chips: Ref<PluginSelectionChip[]>
  /** The sentence of a failed lookup; empty when there is none. */
  error: Ref<string>
  /** The release on this preview, as the plugin's server says; empty when unknown. */
  release: Ref<string>
  /** Makes this the selection the view's chat shows and sends. */
  focus: () => void
  /** Adds an element, or removes it when it is selected. */
  toggle: (route: PluginViewRoute, release: string, id: string) => Promise<void>
  /** Removes an element. */
  remove: (id: string) => void
  /** Empties the selection and leaves the mode. */
  clear: () => void
  /** What a message would carry; null without a selection. */
  claim: () => PluginSelectionClaim | null
}

/** A view's selections: one per preview that declares one, and which of them its chat reads. */
export interface PluginSelections {
  /** The selection of the preview last selected on; null before any. */
  current: Ref<PluginSelectionState | null>
  /** Opens the selection of one preview block. */
  open: () => PluginSelectionState
  /** Empties and forgets the selection of a preview block that is going away. */
  close: (state: PluginSelectionState) => void
  /** The release on a preview; empty when it is unknown or the preview is gone. */
  releaseOf: (owner: string) => string
}

/** The injection key of the selection. */
const PLUGIN_SELECTION_KEY: InjectionKey<PluginSelections> = Symbol('plugin-selection')

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
 * Creates the selection of one preview block.
 *
 * @param owner - Names the preview within its view.
 * @param lookup - Asks the plugin's server what a claimed selection holds.
 * @param focus - Makes this the selection the view's chat reads.
 * @returns The selection.
 */
const createSelection = (
  owner: string,
  lookup: (route: PluginViewRoute, body: PluginSelectionClaim) => ReturnType<ReturnType<typeof usePluginsStore>['callRoute']>,
  focus: (state: PluginSelectionState) => void,
): PluginSelectionState => {
  /** Whether the mode is on. */
  const active = ref(false)
  /** The selected elements. */
  const chips = ref<PluginSelectionChip[]>([])
  /** The sentence of a failed lookup. */
  const error = ref('')
  /** The release on the preview. */
  const release = ref('')
  /** The release the chips were looked up on. */
  let chipRelease = ''
  /** The ids the reader wants, ahead of the chips: a quick second toggle must build on the first, not on the last answer. */
  let wanted: string[] = []
  /** The lookup to make for {@link wanted}; null when the server has answered for it. */
  let due: { route: PluginViewRoute, release: string } | null = null
  /** Counts the clears, so an answer to a lookup from before one is dropped. */
  let generation = 0
  /** The running lookups, which every toggle arriving meanwhile only adds to. */
  let draining: Promise<void> | null = null

  /**
   * Makes the lookups one at a time, each for what is wanted by then, so a flood of toggles costs one lookup in flight and
   * one after it, whatever its length.
   *
   * @returns Resolves when the server has answered for what is wanted.
   */
  const drain = async (): Promise<void> => {
    while (due) {
      const { route, release: asking } = due
      due = null
      const asked = [...wanted]
      const mine = generation
      if (asked.length === 0) {
        chips.value = []
        error.value = ''
        continue
      }
      const result = await lookup(route, { releaseId: asking, ids: asked })
      // Overtaken by a clear or a later toggle: only the newest question's answer may show.
      if (mine !== generation || due) {
        continue
      }
      error.value = result.ok || result.unavailable ? '' : result.error
      // An element removed while the question ran stays removed.
      chips.value = result.ok ? chipsOf(result.data, asked.filter(id => wanted.includes(id))) : []
      wanted = chips.value.map(chip => chip.id)
      chipRelease = asking
    }
  }

  /**
   * Starts the lookups unless they run already.
   *
   * @returns Resolves when they ended.
   */
  const run = (): Promise<void> => {
    draining ??= drain().finally(() => {
      draining = null
    })
    return draining
  }

  /**
   * Adds or removes an element and asks the server what the selection really holds.
   *
   * @param route - The lookup route.
   * @param asking - The release on the preview.
   * @param id - The element id, a claim.
   * @returns Resolves when the server has answered for the selection.
   */
  const toggle = (route: PluginViewRoute, asking: string, id: string): Promise<void> => {
    const next = wanted.includes(id) ? wanted.filter(other => other !== id) : [...wanted, id]
    if (next.length > PLUGIN_SELECTION_MAX_IDS) {
      return Promise.resolve()
    }
    wanted = next
    due = { route, release: asking }
    focus(state)
    return run()
  }

  /**
   * Removes an element.
   *
   * @param id - The element id.
   * @returns Nothing.
   */
  const remove = (id: string): void => {
    wanted = wanted.filter(other => other !== id)
    chips.value = chips.value.filter(chip => chip.id !== id)
  }

  /**
   * Empties the selection and leaves the mode.
   *
   * @returns Nothing.
   */
  const clear = (): void => {
    generation++
    wanted = []
    due = null
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

  /** The selection. */
  const state: PluginSelectionState = { owner, active, chips, error, release, focus: () => focus(state), toggle, remove, clear, claim }
  return state
}

/**
 * Creates a view's selections and hands them to the blocks below the calling component.
 *
 * @param pluginId - The plugin.
 * @param values - The page's route values.
 * @returns The selections.
 */
export const providePluginSelection = (pluginId: string, values: Record<string, string>): PluginSelections => {
  const store = usePluginsStore()
  /** The selection of the preview last selected on. */
  const current = shallowRef<PluginSelectionState | null>(null)
  /** The open selections, by owner. */
  const open = new Map<string, PluginSelectionState>()
  /** Numbers the owners. */
  let opened = 0

  const selections: PluginSelections = {
    current,
    open: () => {
      opened += 1
      const state = createSelection(
        `preview-${opened}`,
        (route, body) => store.callRoute(pluginId, route, values, { ...body }),
        (focused) => {
          current.value = focused
        },
      )
      open.set(state.owner, state)
      return state
    },
    close: (state) => {
      state.clear()
      open.delete(state.owner)
      if (current.value === state) {
        current.value = null
      }
    },
    releaseOf: owner => open.get(owner)?.release.value ?? '',
  }
  provide(PLUGIN_SELECTION_KEY, selections)
  return selections
}

/**
 * Reads the selections of the view the calling block is rendered in.
 *
 * @returns The selections.
 * @throws Error when the block is rendered outside a plugin view, which is a host bug.
 */
export const usePluginSelection = (): PluginSelections => {
  const state = inject(PLUGIN_SELECTION_KEY, null)
  if (!state) {
    throw new Error('A plugin block is rendered outside a plugin view.')
  }
  return state
}
