<template>
  <UiStack gap="xs" data-testid="plugin-action">
    <UiButton
      :variant="variant"
      size="sm"
      :loading="running"
      :disabled="running || body === null"
      data-testid="plugin-action-button"
      @click="start"
    >
      {{ label }}
    </UiButton>
    <UiText v-if="message && !outlet" size="xs" :tone="failed ? 'danger' : 'success'" data-testid="plugin-action-message">{{ message }}</UiText>
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
 * PluginActionBlock. A button that calls one of the plugin's routes, named by `{ method, path }`: after the
 * confirmation the view asks for, if any; then it reads the named blocks again, opens the named page, and shows
 * the sentence the plugin answered with (`message` of the reply), or the refusal's sentence from the store. A
 * `followUp` then keeps reading the named blocks until the work the call started has ended (`usePluginFollowUp`).
 *
 * Who may call the route is decided by the server on the call; a refusal is shown like any other failure. A
 * standalone action shows its sentence under its button; inside a header it hands the sentence to the header's
 * outlet instead, so the sentence never grows one button's column and the row of buttons stays aligned. A `labelSource`
 * lets the label follow data (a version number); `label` stands until it is read and whenever it is empty.
 *
 * A `bodySource` builds the call's body from data the page read, in the same read as the label, so the button sends the
 * value on its label; it stays disabled while a member is missing. A result card's `body` is written by the plugin. Either
 * way the body is fixed at the click, so a refresh under an open confirmation cannot change what was asked, and a refused
 * call reads the data again, since what it carried may be what moved.
 */
import { usePluginActionMessageOutlet, usePluginFollowUp, usePluginSource, usePluginView } from '~/composables/usePluginView'
import { usePluginsStore } from '~/stores/plugins'
import type { PluginActionBlock, PluginActionBodyValue, PluginSource } from '~/types/pluginblock'
import { PluginBlockTone } from '~/types/pluginblocktone'
import { UiConfirmVariant } from '~/utils/ui/enums'
import { pluginActionBody, pluginButtonVariant, readPluginField } from '~/utils/plugins'

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
/** Starts the follow-up refresh; the view ends it when the page is left, not when this block hides. */
const followUp = usePluginFollowUp(() => props.block.id)
/** The header's message line, when the action sits in one; null for a standalone action. */
const outlet = usePluginActionMessageOutlet()

/** Whether the confirmation is open. */
const confirming = ref(false)
/** True while the call runs. */
const running = ref(false)
/** The sentence shown under the button; empty when there is none. */
const message = ref('')
/** Whether the last call failed. */
const failed = ref(false)

/** The body fixed at the click; the call sends this one. */
let sending: Record<string, PluginActionBodyValue> = {}

/** The one read behind the button: the whole reply with a body source, whose label comes from the same reply, else the label's field. */
const source = (): PluginSource | undefined =>
  props.block.bodySource ? { route: props.block.bodySource.route } : props.block.labelSource

/** The value read. */
const { value: read, reload } = usePluginSource(source, () => props.block.id)

/** The label read, when the view names a source for it. */
const labelRead = computed(() => (props.block.bodySource && props.block.labelSource ? readPluginField(read.value, props.block.labelSource.field) : read.value))

/** The button's text. */
const label = computed(() => (props.block.labelSource && typeof labelRead.value === 'string' && labelRead.value !== '' ? labelRead.value : props.block.label))

/** The body the call would send now; null while a member the view asks for has not been read. */
const body = computed(() => (props.block.bodySource ? pluginActionBody(props.block.bodySource, read.value) : props.block.body ?? {}))

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
  if (body.value === null) {
    return
  }
  sending = body.value
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
  const result = await store.callRoute(context.pluginId, props.block.route, context.values, sending)
  running.value = false
  confirming.value = false
  failed.value = !result.ok
  const answered = readPluginField(result.data, 'message')
  message.value = result.ok ? (typeof answered === 'string' ? answered : '') : result.error
  const sentence = message.value
  const takeBack = outlet?.({ text: sentence, failed: failed.value })
  if (result.ok) {
    context.refresh(props.block.refresh ?? [])
    // The sentence tells what is going on ("being published"); once the work has ended, the blocks show the result and
    // the sentence would only say the work is still running.
    followUp(props.block.followUp, () => {
      takeBack?.()
      if (message.value === sentence) {
        message.value = ''
      }
    })
    if (props.block.navigate) {
      context.open(props.block.navigate, result.data)
    }
  } else if (!result.unavailable && (props.block.bodySource || props.block.body)) {
    context.refresh(props.block.refresh ?? [])
    if (props.block.bodySource && !props.block.refresh?.includes(props.block.id ?? '')) {
      void reload()
    }
  }
}
</script>
