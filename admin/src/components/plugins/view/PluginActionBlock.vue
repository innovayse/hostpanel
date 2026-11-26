<script setup lang="ts">
/**
 * A button that calls one of the plugin's admin routes, named by `{ method, path }`: after the
 * confirmation the view asks for, if any; then it reads the named blocks again, opens the named page and
 * shows the sentence the plugin answered with (`message` of the reply), or the refusal's sentence. A
 * `followUp` then keeps reading the named blocks until the work the call started has ended.
 *
 * Who may call the route is decided by the server on the call. A standalone action shows its sentence
 * under its button; inside a header it hands the sentence to the header's line instead. A `labelSource`
 * lets the label follow data; `label` stands until it is read and whenever it is empty. A result card's
 * `body` is fixed at the click, so a refresh under an open confirmation cannot change what was asked.
 *
 * An `openUrlFrom` opens a blank tab in the click itself, reads that field of the reply, and sends the tab to it when it
 * passes the plugin's allowed origins; a failed call or a refused address closes the tab and shows an error.
 */
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { usePluginActionMessageOutlet, usePluginFollowUp, usePluginSource, usePluginView } from '@/composables/usePluginView'
import { openBlankTab, pluginButtonVariant, pluginOpenUrlTarget, readPluginField } from '@/lib/pluginView'
import { usePluginUiStore } from '@/stores/pluginUi'
import { PluginBlockTone } from '@/types/pluginview'
import type { PluginActionBlock } from '@/types/pluginview'
import type { UiButtonVariant } from '@/utils/ui/button'
import { UiDialogVariant } from '@/utils/ui/dialog'
import { UiGap } from '@/utils/ui/layout'
import { UiSize } from '@/utils/ui/size'
import { UiTone } from '@/utils/ui/tone'
import { UiActionKind } from '@/utils/ui/actions'

/** Props for PluginActionBlock. */
const props = defineProps<{
  /** The block. */
  block: PluginActionBlock
}>()

/** The view's context: values, refresh and navigation. */
const context = usePluginView()
/** Translates the sentences this block owns. */
const { t } = useI18n()
/** The plugin ui store, which makes the call. */
const store = usePluginUiStore()
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
let sending: Record<string, unknown> = {}

/** The label read, when the view names a source for it. */
const { value: labelRead } = usePluginSource(() => props.block.labelSource, () => props.block.id)

/** The button's text. */
const label = computed<string>(() =>
  (typeof labelRead.value === 'string' && labelRead.value !== '' ? labelRead.value : props.block.label))

/** The button's variant. */
const variant = computed<UiButtonVariant>(() => pluginButtonVariant(props.block.style))

/** The confirmation's tone: a destructive action is asked as one. */
const confirmTone = computed<UiTone>(() => (props.block.style === PluginBlockTone.Danger ? UiTone.Danger : UiTone.Warning))

/** The tone of the sentence under the button. */
const messageTone = computed<UiTone>(() => (failed.value ? UiTone.Danger : UiTone.Success))

/**
 * Calls the route and applies what the view asks for after a success.
 *
 * @returns Resolves when the call has ended.
 */
const run = async (): Promise<void> => {
  if (running.value) {
    return
  }
  // The tab opens before the call is awaited: only the click itself may open one.
  const openUrlFrom = props.block.openUrlFrom
  const tab = openUrlFrom ? openBlankTab() : null
  if (openUrlFrom && tab === null) {
    confirming.value = false
    failed.value = true
    message.value = t('plugins.pluginPage.openUrlBlocked')
    outlet?.({ text: message.value, failed: true })
    return
  }
  running.value = true
  const result = await store.callRoute(context.pluginId, props.block.route, context.values, sending, context.query)
  running.value = false
  confirming.value = false
  const target = tab && result.ok && openUrlFrom ? pluginOpenUrlTarget(result.data, openUrlFrom, context.externalOrigins) : null
  if (tab) {
    if (target) {
      tab.location.href = target.href
    } else {
      tab.close()
    }
  }
  const refused = result.ok && openUrlFrom !== undefined && target === null
  failed.value = !result.ok || refused
  const answered = readPluginField(result.data, 'message')
  if (refused) {
    message.value = t('plugins.pluginPage.openUrlRefused')
  } else {
    message.value = result.ok ? (typeof answered === 'string' ? answered : '') : result.error
  }
  const sentence = message.value
  const takeBack = outlet?.({ text: sentence, failed: failed.value })
  if (result.ok) {
    context.refresh(props.block.refresh ?? [])
    // Once the work has ended the blocks show the result, and the sentence would only say it still runs.
    followUp(props.block.followUp, () => {
      takeBack?.()
      if (message.value === sentence) {
        message.value = ''
      }
    })
    if (props.block.navigate) {
      context.open(props.block.navigate, result.data)
    }
  } else if (!result.unavailable && props.block.body) {
    // What the card's body carried may be what moved: the blocks it names read again.
    context.refresh(props.block.refresh ?? [])
  }
}

/**
 * Calls the route, after the confirmation when the view asks for one.
 *
 * @returns Nothing.
 */
const start = (): void => {
  sending = { ...props.block.body }
  if (props.block.confirm) {
    confirming.value = true
    return
  }
  void run()
}
</script>

<template>
  <UiStack :gap="UiGap.Xs" data-testid="plugin-action">
    <UiButton :action="UiActionKind.Generate"
      :variant="variant"
      :size="UiSize.Sm"
      :loading="running"
      :disabled="running"
      data-testid="plugin-action-button"
      @click="start"
    >
      {{ label }}
    </UiButton>
    <UiStatusMessage v-if="!outlet" :message="message || null" :tone="messageTone" data-testid="plugin-action-message" />
    <UiDialog
      v-if="block.confirm"
      :open="confirming"
      :variant="UiDialogVariant.Confirm"
      :title="block.confirm"
      :confirm-label="label"
      :loading="running"
      :tone="confirmTone"
      @confirm="run"
      @close="confirming = false"
    />
  </UiStack>
</template>
