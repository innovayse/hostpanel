/**
 * The steps and the percentage the plugin processing dialog shows, worked out from what the server streamed (the stage and,
 * while the package downloads, its bytes), how the change ended and where the restart that applies it stands. Pure: the
 * dialog's state hands everything in. The percentage is real: each step has a weight, the download step moves with its
 * bytes when the registry declared a size, and a step whose progress the server cannot measure counts only once done.
 */
import { UiStepState } from '@/components/ui/UiStepList.vue'
import { PluginChangeStage, PluginProcessingPhase, PluginProcessingPlan, PluginProgressStep, PluginRestartState } from '@/types/plugin'
import type { PluginActionOutcome, PluginChangeProgressDto } from '@/types/plugin'

/** One step of a plan and its share of the bar, in percent. */
interface WeightedStep {
  /** The step. */
  step: PluginProgressStep
  /** Its share of the whole bar; the plan's shares add up to 100. */
  weight: number
}

/** One step as the dialog lists it. */
export interface PluginProgressStepView {
  /** The step, which names its label. */
  step: PluginProgressStep
  /** Where it stands. */
  state: UiStepState
}

/** What the dialog draws for a job. */
export interface PluginProgressView {
  /** The steps in order. */
  steps: PluginProgressStepView[]
  /** The share done, 0 to 100. */
  percent: number
  /** Where the job stands as a whole. */
  phase: PluginProcessingPhase
}

/** What the dialog knows about a job right now. */
export interface PluginProgressInput {
  /** Which list of steps the job walks. */
  plan: PluginProcessingPlan
  /** The last step the server streamed, null before the first. */
  progress: PluginChangeProgressDto | null
  /** How the change request ended, null while it runs. */
  outcome: PluginActionOutcome | null
  /** Where the restart that applies the change stands. */
  restartState: PluginRestartState
  /** Whether the restart brought the plugin back with a failed change. */
  restartFailed: boolean
}

/** The full bar, in percent. */
const FULL = 100

/** The steps of each plan with their weights; the last step of a plan that has one is the closing state and weighs nothing. */
const PLANS: Record<PluginProcessingPlan, WeightedStep[]> = {
  [PluginProcessingPlan.Registry]: [
    { step: PluginProgressStep.Download, weight: 50 },
    { step: PluginProgressStep.Verify, weight: 10 },
    { step: PluginProgressStep.Stage, weight: 15 },
    { step: PluginProgressStep.Apply, weight: 25 },
    { step: PluginProgressStep.Loaded, weight: 0 },
  ],
  [PluginProcessingPlan.Archive]: [
    { step: PluginProgressStep.Upload, weight: 25 },
    { step: PluginProgressStep.Verify, weight: 15 },
    { step: PluginProgressStep.Stage, weight: 20 },
    { step: PluginProgressStep.Apply, weight: 40 },
    { step: PluginProgressStep.Loaded, weight: 0 },
  ],
  [PluginProcessingPlan.ArchiveRead]: [{ step: PluginProgressStep.Upload, weight: 100 }],
  [PluginProcessingPlan.Removal]: [
    { step: PluginProgressStep.Remove, weight: 40 },
    { step: PluginProgressStep.Apply, weight: 60 },
    { step: PluginProgressStep.Removed, weight: 0 },
  ],
  [PluginProcessingPlan.Restart]: [
    { step: PluginProgressStep.Restart, weight: 100 },
    { step: PluginProgressStep.Loaded, weight: 0 },
  ],
}

/** The plan step each streamed stage runs; the restart stage runs the apply step, which it then names. */
const STAGE_STEPS: Record<PluginChangeStage, PluginProgressStep[]> = {
  [PluginChangeStage.Download]: [PluginProgressStep.Download],
  [PluginChangeStage.Verify]: [PluginProgressStep.Verify],
  [PluginChangeStage.Stage]: [PluginProgressStep.Stage, PluginProgressStep.Remove],
  [PluginChangeStage.Apply]: [PluginProgressStep.Apply],
  [PluginChangeStage.Restart]: [PluginProgressStep.Apply, PluginProgressStep.Restart],
}

/**
 * Finds the step a streamed stage runs in a plan.
 *
 * @param plan - The plan's steps.
 * @param progress - The last streamed step, null before the first.
 * @returns Its index; the first step before anything was streamed.
 */
const runningIndex = (plan: WeightedStep[], progress: PluginChangeProgressDto | null): number => {
  if (progress === null) {
    return 0
  }
  const index = plan.findIndex(entry => STAGE_STEPS[progress.stage].includes(entry.step))
  return Math.max(0, index)
}

/**
 * Names the apply step by how the change is applied: in place, by a restart, or not known yet.
 *
 * @param input - What the dialog knows.
 * @returns The step to show in the apply slot.
 */
const applyStep = (input: PluginProgressInput): PluginProgressStep => {
  if (input.outcome?.reloaded === true) {
    return PluginProgressStep.Reload
  }
  const restarts = input.progress?.stage === PluginChangeStage.Restart
    || input.outcome?.restarting === true
    || (input.outcome?.ok === true && input.outcome.stagedVersion !== null)
  return restarts ? PluginProgressStep.Restart : PluginProgressStep.Apply
}

/**
 * Where the job stands: the index of the step that runs or failed, and how that step stands; every step before it is done
 * and every step after it is waiting. An index past the end means every step is done.
 *
 * @param input - What the dialog knows.
 * @param plan - The plan's steps.
 * @returns The index and the state of that step.
 */
const cursorOf = (input: PluginProgressInput, plan: WeightedStep[]): { index: number; state: UiStepState } => {
  const applyIndex = plan.findIndex(entry => entry.step === PluginProgressStep.Apply || entry.step === PluginProgressStep.Restart)
  const last = plan.length - 1
  const followsRestart = input.plan === PluginProcessingPlan.Restart || input.outcome?.restarting === true
  if (input.outcome === null && !followsRestart) {
    return { index: runningIndex(plan, input.progress), state: UiStepState.Running }
  }
  if (input.outcome !== null && !input.outcome.ok) {
    return { index: runningIndex(plan, input.progress), state: UiStepState.Failed }
  }
  if (followsRestart) {
    if (input.restartState === PluginRestartState.TimedOut) {
      return { index: applyIndex, state: UiStepState.Failed }
    }
    if (input.restartState === PluginRestartState.Running) {
      return input.restartFailed ? { index: last, state: UiStepState.Failed } : { index: plan.length, state: UiStepState.Done }
    }
    return { index: applyIndex, state: UiStepState.Running }
  }
  // A staged version the host does not restart for by itself waits for the admin's restart: the apply step is not done.
  if (applyStep(input) === PluginProgressStep.Restart) {
    return { index: applyIndex, state: UiStepState.Pending }
  }
  return { index: plan.length, state: UiStepState.Done }
}

/**
 * The share of the running step that is done: the download's bytes when the registry declared a size, nothing otherwise.
 *
 * @param step - The running step.
 * @param progress - The last streamed step.
 * @returns A fraction from 0 to 1.
 */
const runningFraction = (step: PluginProgressStep, progress: PluginChangeProgressDto | null): number => {
  if (step !== PluginProgressStep.Download || progress === null || progress.bytesTotal === null || progress.bytesTotal <= 0) {
    return 0
  }
  return Math.min(1, progress.bytesDone / progress.bytesTotal)
}

/**
 * Works out the steps, the percentage and the phase of a processing job.
 *
 * @param input - What the dialog knows about the job right now.
 * @returns What the dialog draws.
 */
export const pluginProgressView = (input: PluginProgressInput): PluginProgressView => {
  const plan = PLANS[input.plan]
  const apply = applyStep(input)
  const cursor = cursorOf(input, plan)
  const steps = plan.map((entry, index): PluginProgressStepView => {
    const step = entry.step === PluginProgressStep.Apply ? apply : entry.step
    if (index < cursor.index) {
      return { step, state: UiStepState.Done }
    }
    return { step, state: index === cursor.index ? cursor.state : UiStepState.Pending }
  })
  const done = plan.reduce((sum, entry, index) => (index < cursor.index ? sum + entry.weight : sum), 0)
  const current = plan[cursor.index]
  const running = current !== undefined && cursor.state === UiStepState.Running ? current.weight * runningFraction(current.step, input.progress) : 0
  const percent = cursor.index >= plan.length ? FULL : Math.min(FULL, done + running)
  const phase = cursor.state === UiStepState.Failed
    ? PluginProcessingPhase.Failed
    : cursor.state === UiStepState.Running ? PluginProcessingPhase.Running : PluginProcessingPhase.Done
  return { steps, percent, phase }
}
