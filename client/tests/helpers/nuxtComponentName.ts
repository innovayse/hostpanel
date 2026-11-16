/**
 * The name Nuxt gives a component file, in one place so the mount helpers that register components by hand and
 * the guard that reads which names are ours agree on it.
 *
 * Nuxt names a component after its folders and its file (`components/client/services/ServiceCard.vue`
 * is `ClientServicesServiceCard`) and drops the folder parts a file name already starts with
 * (`services/setup/SetupWizard.vue` is `ClientServicesSetupWizard`).
 *
 * @module tests/helpers/nuxtComponentName
 */

/**
 * Splits a PascalCase file name into its words.
 *
 * @param name - For example `SetupStepCard`.
 * @returns For example `['Setup', 'Step', 'Card']`.
 */
const words = (name: string): string[] => name.match(/[A-Z][a-z0-9]*/g) ?? [name]

/**
 * Builds the name Nuxt gives a component file.
 *
 * @param folders - Folder names below `components/`, for example `['client', 'services']`.
 * @param file - File name without extension.
 * @returns The PascalCase auto-import name.
 */
export const nuxtComponentName = (folders: string[], file: string): string => {
  const prefix = folders.flatMap((folder) => words(folder.charAt(0).toUpperCase() + folder.slice(1)))
  const fileWords = words(file)
  const kept: string[] = []
  while (prefix.length > 0 && prefix[0]!.toLowerCase() !== fileWords[0]!.toLowerCase()) {
    kept.push(prefix.shift()!) // loop guard keeps the array non-empty
  }
  return [...kept, ...fileWords].join('')
}
