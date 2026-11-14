/**
 * The state behind the plugin processing dialog: which plugin is being worked on, what is being done,
 * whether it still runs and how it ended. One implementation for every screen that installs, updates,
 * removes or uploads a plugin; the work itself is the store's promise handed to {@link run}.
 */
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { PluginProcessingStep } from '@/types/plugin'
import type { PluginActionOutcome } from '@/types/plugin'

/** What one run is told besides its work. */
export interface PluginProcessingRun {
  /** Name of the plugin (or archive) shown in the dialog. */
  name: string
  /** What is being done to it. */
  step: PluginProcessingStep
  /** The store's call; its outcome decides what the dialog ends with. */
  work: () => Promise<PluginActionOutcome>
  /**
   * Asked once the work is over, with its outcome: true closes the dialog by itself because the result continues in
   * another prompt of the screen (a successful archive read opens its confirmation, a refused downgrade opens its own).
   */
  handsOff?: (outcome: PluginActionOutcome) => boolean
}

/**
 * Creates the processing state of one screen.
 *
 * @returns The state the dialog shows and the actions that drive it.
 */
export const usePluginProcessing = () => {
  /** Translation function. */
  const { t } = useI18n()

  /** Whether the dialog is shown. */
  const open = ref(false)

  /** Name of the plugin or archive being worked on. */
  const name = ref('')

  /** What is being done to it. */
  const step = ref<PluginProcessingStep>(PluginProcessingStep.Installing)

  /** True while the work runs; the dialog cannot be dismissed meanwhile. */
  const running = ref(false)

  /** Whether the work was refused. */
  const failed = ref(false)

  /** The sentence the work ended with: the API's refusal, or the staged-version note; null for a plain success. */
  const message = ref<string | null>(null)

  /**
   * Runs one piece of work behind the dialog and returns its outcome to the caller.
   *
   * @param options - The plugin, the step, the work and when the outcome is handed to another prompt.
   * @returns The outcome of the work.
   */
  const run = async (options: PluginProcessingRun): Promise<PluginActionOutcome> => {
    name.value = options.name
    step.value = options.step
    failed.value = false
    message.value = null
    running.value = true
    open.value = true
    const outcome = await options.work()
    running.value = false
    failed.value = !outcome.ok
    if (!outcome.ok) {
      message.value = outcome.error ?? t('errors.noAnswer')
    } else if (outcome.stagedVersion !== null) {
      message.value = t('plugins.plugins.pluginStaged', { version: outcome.stagedVersion })
    }
    if (options.handsOff?.(outcome) === true) {
      open.value = false
    }
    return outcome
  }

  /** Closes the dialog once the work is over; ignored while it runs. */
  const close = (): void => {
    if (!running.value) {
      open.value = false
    }
  }

  return { open, name, step, running, failed, message, run, close }
}

/** The state one screen's processing dialog shows and is driven by, as {@link usePluginProcessing} returns it. */
export type PluginProcessing = ReturnType<typeof usePluginProcessing>
