/**
 * The state behind the plugin processing dialog, the one dialog for an install, an update, a removal, an archive upload and
 * a server restart: which plugin, which version, the steps with where each stands, the real percentage, the elapsed time
 * and how the job ended. The steps follow what the server streams for the change (`changeProgress` of the plugins store);
 * a change the host applies by restarting goes on in the same dialog through the restart wait, and a restart asked for
 * from the banner opens the same dialog by itself. The work itself is the store's promise handed to {@link run}.
 */
import { computed, onScopeDispose, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { usePoller } from '@/composables/usePoller'
import { formatStopwatch } from '@/lib/format'
import { failedChangeOf, failureSentence } from '@/lib/pluginChange'
import { pluginProgressView } from '@/lib/pluginProgress'
import type { PluginProgressView } from '@/lib/pluginProgress'
import { usePluginsStore } from '@/stores/plugins'
import { PluginProcessingPhase, PluginProcessingPlan, PluginProcessingStep, PluginRestartState } from '@/types/plugin'
import type { PluginActionOutcome } from '@/types/plugin'
import { PollKind } from '@/types/poller'

/** What one run is told besides its work. */
export interface PluginProcessingRun {
  /** Name of the plugin (or archive) shown in the dialog. */
  name: string
  /** What is being done to it, the dialog's heading line. */
  step: PluginProcessingStep
  /** Which list of steps the job walks. */
  plan: PluginProcessingPlan
  /** Id of the plugin, so a restart that failed to load it is told apart; omitted while it is not known. */
  pluginId?: string
  /** Version the job brings, omitted when it brings none (a removal) or it is not known. */
  version?: string | null
  /** Address of the plugin's icon, omitted when it has none. */
  iconUrl?: string | null
  /** Hex colour of the plugin's logo block. */
  color?: string
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

  /** Plugins store: the streamed step, the restart flow and the plugins it applied. */
  const store = usePluginsStore()

  /** Whether the dialog is shown. */
  const open = ref(false)

  /** Name of the plugin or archive being worked on. */
  const name = ref('')

  /** What is being done to it. */
  const step = ref<PluginProcessingStep>(PluginProcessingStep.Installing)

  /** Which list of steps the job walks. */
  const plan = ref<PluginProcessingPlan>(PluginProcessingPlan.Registry)

  /** Id of the plugin, empty while it is not known. */
  const pluginId = ref('')

  /** Version the job brings, null when none. */
  const version = ref<string | null>(null)

  /** Address of the plugin's icon, null when it has none. */
  const iconUrl = ref<string | null>(null)

  /** Hex colour of the plugin's logo block, undefined for the neutral one. */
  const color = ref<string | undefined>(undefined)

  /** True while the change request runs. */
  const running = ref(false)

  /** How the change request ended, null while it runs or for a restart from the banner. */
  const outcome = ref<PluginActionOutcome | null>(null)

  /** When the job started, in milliseconds. */
  const startedAt = ref(0)

  /** When the job ended, null while it runs. */
  const endedAt = ref<number | null>(null)

  /** The clock the elapsed time is read against, moved once a second while the job runs. */
  const now = ref(Date.now())

  /** The clock's poller: it only moves {@link now}. */
  const clock = usePoller({
    kind: PollKind.ProcessingClock,
    /** Moves the clock. */
    tick: () => {
      now.value = Date.now()
    },
  })

  /** Whether the job follows a server restart: a restart from the banner, or a change the host restarts for. */
  const followsRestart = computed<boolean>(() => plan.value === PluginProcessingPlan.Restart || outcome.value?.restarting === true)

  /** Whether the restart brought this plugin (or, for a restart from the banner, any plugin) back with a failed change. */
  const restartFailed = computed<boolean>(() =>
    store.restartFailures.some(plugin => plan.value === PluginProcessingPlan.Restart || plugin.id === pluginId.value),
  )

  /** The steps, the percentage and the phase. */
  const view = computed<PluginProgressView>(() => pluginProgressView({
    plan: plan.value,
    progress: store.changeProgress,
    outcome: outcome.value,
    restartState: followsRestart.value ? store.restartState : PluginRestartState.Idle,
    restartFailed: restartFailed.value,
  }))

  /** True while anything still runs; the dialog cannot be dismissed meanwhile. */
  const busy = computed<boolean>(() => running.value || view.value.phase === PluginProcessingPhase.Running)

  /** Whether the job failed. */
  const failed = computed<boolean>(() => view.value.phase === PluginProcessingPhase.Failed)

  /** Whether the restart did not come back in time, so the wait can be retried. */
  const canRetry = computed<boolean>(() => followsRestart.value && store.restartState === PluginRestartState.TimedOut)

  /** The elapsed time, as a stopwatch. */
  const elapsed = computed<string>(() => formatStopwatch((endedAt.value ?? now.value) - startedAt.value))

  /** The sentence of a failed restart: the plugin's own reason, or the time-out. */
  const restartFailure = computed<string>(() => {
    const plugin = store.restartFailures.find(item => plan.value === PluginProcessingPlan.Restart || item.id === pluginId.value)
    const failure = plugin === undefined ? null : failedChangeOf(plugin)
    return failure === null ? t('plugins.restart.timedOut') : `${t('plugins.restart.pluginFailed', { name: plugin?.name ?? name.value })} ${failureSentence(failure)}`
  })

  /** The sentence the job ended with, null while it runs. */
  const message = computed<string | null>(() => {
    if (busy.value) {
      return null
    }
    if (failed.value) {
      return outcome.value !== null && !outcome.value.ok ? (outcome.value.error ?? t('errors.noAnswer')) : restartFailure.value
    }
    if (plan.value === PluginProcessingPlan.Removal) {
      return t('plugins.processing.removedDone', { name: name.value })
    }
    if (followsRestart.value || outcome.value?.reloaded === true) {
      return version.value === null ? t('plugins.restart.running') : t('plugins.processing.loadedVersion', { version: version.value })
    }
    if (outcome.value?.stagedVersion !== null && outcome.value?.stagedVersion !== undefined) {
      return t('plugins.plugins.pluginStaged', { version: outcome.value.stagedVersion })
    }
    return t('plugins.processing.done')
  })

  /**
   * Opens the dialog for a new job and starts its clock.
   *
   * @param options - What the dialog shows about the job.
   */
  const begin = (options: Omit<PluginProcessingRun, 'work' | 'handsOff'>): void => {
    name.value = options.name
    step.value = options.step
    plan.value = options.plan
    pluginId.value = options.pluginId ?? ''
    version.value = options.version ?? null
    iconUrl.value = options.iconUrl ?? null
    color.value = options.color
    outcome.value = null
    startedAt.value = Date.now()
    now.value = startedAt.value
    endedAt.value = null
    open.value = true
    clock.start()
  }

  /**
   * Runs one piece of work behind the dialog and returns its outcome to the caller.
   *
   * @param options - The plugin, the step, the plan, the work and when the outcome is handed to another prompt.
   * @returns The outcome of the work.
   */
  const run = async (options: PluginProcessingRun): Promise<PluginActionOutcome> => {
    begin(options)
    running.value = true
    const result = await options.work()
    running.value = false
    outcome.value = result
    if (options.handsOff?.(result) === true) {
      open.value = false
    } else if (result.ok && result.restarting) {
      // The host restarts itself to apply the change: the same dialog goes on through the wait for it.
      void store.followRestart()
    }
    return result
  }

  /** Closes the dialog once the job is over, and ends the restart flow it followed; ignored while it runs. */
  const close = (): void => {
    if (busy.value) {
      return
    }
    open.value = false
    clock.stop()
    if (store.restartState !== PluginRestartState.Idle) {
      store.resetRestart()
    }
  }

  // The clock stops with the job, and a restart asked for from the banner opens this dialog on its own.
  watch(busy, (isBusy) => {
    if (!isBusy && open.value) {
      endedAt.value = Date.now()
      clock.stop()
    }
  })
  watch(() => store.restartState, (state) => {
    if (state === PluginRestartState.Restarting && !open.value) {
      begin({ name: t('plugins.restart.title'), step: PluginProcessingStep.Restarting, plan: PluginProcessingPlan.Restart })
    } else if (state === PluginRestartState.Idle && open.value && plan.value === PluginProcessingPlan.Restart && store.restartError !== null) {
      // The server refused the restart itself: the dialog ends with its sentence.
      outcome.value = { ok: false, stagedVersion: null, reloaded: false, restarting: false, error: store.restartError }
    }
  })

  // A screen that goes away mid-restart must not leave the restart poll running.
  onScopeDispose(() => {
    if (store.restartState !== PluginRestartState.Idle) {
      store.resetRestart()
    }
  })

  return {
    open, name, step, version, iconUrl, color, busy, failed, canRetry, message, view, elapsed, run, close,
  }
}

/** The state one screen's processing dialog shows and is driven by, as {@link usePluginProcessing} returns it. */
export type PluginProcessing = ReturnType<typeof usePluginProcessing>
