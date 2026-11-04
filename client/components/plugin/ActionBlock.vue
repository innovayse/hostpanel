<template>
  <UiStack gap="xs" data-testid="plugin-action">
    <UiButton
      :variant="variant"
      size="sm"
      :loading="running"
      :disabled="running"
      data-testid="plugin-action-button"
      @click="start"
    >
      {{ block.label }}
    </UiButton>
    <UiText v-if="message" size="xs" :tone="failed ? 'danger' : 'success'" data-testid="plugin-action-message">{{ message }}</UiText>
    <UiConfirmDialog
      v-if="block.confirm"
      :open="confirming"
      :title="block.confirm"
      :variant="confirmVariant"
      :loading="running"
      @confirm="run"
      @cancel="confirming = false"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * PluginActionBlock - a button that calls one of the plugin's routes, named by `{ method, path }`: after the
 * confirmation the view asks for, if any; then it reads the named blocks again, opens the named page, and shows
 * the sentence the plugin answered with (`message` of the reply), or the refusal's sentence from the store. A
 * `followUp` then keeps reading the named blocks until the work the call started has ended (`usePluginFollowUp`).
 *
 * Who may call the route is decided by the server on the call; a refusal is shown under the button like any
 * other failure.
 */
import { usePluginFollowUp, usePluginView } from '~/composables/usePluginView'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginActionBlock } from '~/types/pluginblock'
import { PluginBlockTone } from '~/types/pluginblocktone'
import { UiConfirmVariant } from '~/utils/ui/enums'
import { pluginButtonVariant, readPluginField } from '~/utils/plugins'

/** Props of PluginActionBlock. */
interface Props {
  /** The block. */
  block: PluginActionBlock
}

/** The props. */
const props = defineProps<Props>()

/** The view's context: values, refresh and navigation. */
const context = usePluginView()
/** The plugin store, which makes the call. */
const store = usePluginsStore()
/** Starts the follow-up refresh; stopped when the block unmounts. */
const followUp = usePluginFollowUp()

/** Whether the confirmation is open. */
const confirming = ref(false)
/** True while the call runs. */
const running = ref(false)
/** The sentence shown under the button; empty when there is none. */
const message = ref('')
/** Whether the last call failed. */
const failed = ref(false)

/** The button's variant. */
const variant = computed(() => pluginButtonVariant(props.block.style))

/** The confirmation's look: a destructive action is asked as one. */
const confirmVariant = computed(() =>
  props.block.style === PluginBlockTone.Danger ? UiConfirmVariant.Danger : UiConfirmVariant.Warning)

/**
 * Calls the route, after the confirmation when the view asks for one.
 *
 * @returns Nothing.
 */
const start = (): void => {
  if (props.block.confirm) {
    confirming.value = true
    return
  }
  void run()
}

/**
 * Calls the route and applies what the view asks for after a success.
 *
 * @returns Resolves when the call has ended.
 */
const run = async (): Promise<void> => {
  if (running.value) {
    return
  }
  running.value = true
  const result = await store.callRoute(context.pluginId, props.block.route, context.values, {})
  running.value = false
  confirming.value = false
  failed.value = !result.ok
  const answered = readPluginField(result.data, 'message')
  message.value = result.ok ? (typeof answered === 'string' ? answered : '') : result.error
  if (result.ok) {
    context.refresh(props.block.refresh ?? [])
    followUp(props.block.followUp)
    if (props.block.navigate) {
      context.open(props.block.navigate, result.data)
    }
  }
}
</script>
