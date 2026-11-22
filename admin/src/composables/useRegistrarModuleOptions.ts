/**
 * Loads the registrars a TLD or a domain list can name.
 *
 * A registrar is an installed registrar plugin, and its id is the module the API stores, so the choices come from the
 * plugin list the Plugins page already reads: nothing about a registrar is written into the interface.
 */

import { computed } from 'vue'
import type { ComputedRef } from 'vue'
import type { SelectOption } from '@/lib/constants'
import { usePluginsStore } from '@/stores/plugins'
import { PluginType } from '@/types/plugin'

/** What {@link useRegistrarModuleOptions} hands back. */
export interface RegistrarModuleOptions {
  /** The loaded registrar plugins as choices: the plugin id as the value and its name as the label; empty until {@link RegistrarModuleOptions.load} resolves. */
  options: ComputedRef<SelectOption[]>
  /** Fetches the installed plugins through the plugins store. */
  load: () => Promise<void>
  /**
   * Names a stored registrar module.
   *
   * @param module - The module as the API sent it, in any letter case.
   * @returns The registrar's name, or the module itself when no loaded registrar plugin has that id.
   */
  labelOf: (module: string) => string
  /**
   * Finds the option a stored registrar module selects.
   *
   * @param module - The module as the API sent it, in any letter case.
   * @returns The plugin id as the option holds it, or the module itself when no loaded registrar plugin has that id.
   */
  idOf: (module: string) => string
}

/**
 * Creates the registrar choices and the call that fills them.
 *
 * @returns The reactive choices, their loader and the name lookup.
 */
export const useRegistrarModuleOptions = (): RegistrarModuleOptions => {
  const store = usePluginsStore()

  /** The loaded plugins whose type is registrar; the API sends the type in lower case on the list. */
  const options = computed<SelectOption[]>(() =>
    store.plugins
      .filter(plugin => plugin.isLoaded && plugin.type.toLowerCase() === PluginType.Registrar.toLowerCase())
      .map(plugin => ({ value: plugin.id, label: plugin.name })),
  )

  /** Fills the choices from the plugins store. */
  const load = async (): Promise<void> => {
    await store.fetchAll()
  }

  /**
   * Finds the option a stored registrar module selects; the ids are lower case and older rows are not.
   *
   * @param module - The module as the API sent it, in any letter case.
   * @returns The option, or undefined when no loaded registrar plugin has that id.
   */
  const optionOf = (module: string): SelectOption | undefined =>
    options.value.find(option => option.value.toLowerCase() === module.toLowerCase())

  /**
   * Names a stored registrar module the way the options do.
   *
   * @param module - The module as the API sent it, in any letter case.
   * @returns The registrar's name, or the module itself when no loaded registrar plugin has that id.
   */
  const labelOf = (module: string): string => optionOf(module)?.label ?? module

  /**
   * Finds the option a stored registrar module selects.
   *
   * @param module - The module as the API sent it, in any letter case.
   * @returns The plugin id as the option holds it, or the module itself when no loaded registrar plugin has that id.
   */
  const idOf = (module: string): string => optionOf(module)?.value ?? module

  return { options, load, labelOf, idOf }
}
