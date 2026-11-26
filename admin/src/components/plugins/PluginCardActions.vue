<script setup lang="ts">
/**
 * The buttons in the footer of a plugin card, drawn from the list of {@link PluginCardAction} the list screen
 * decides on: the placeholder of the update button, Install and Update (the same primary button), Details for a
 * plugin that needs a licence key, and the gear that opens the plugin's page. The installed list and the registry
 * both use it, so the two footers look and behave the same.
 *
 * The component owns no data: it reports which action was pressed and the screen reacts. Every control carries a
 * test id of the form `<prefix>-<plugin id>`; the prefix is the action's entry in `testidPrefixes` when the screen
 * names one, otherwise `plugin-card-action-<action>`.
 */
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import { Icon } from '@/lib/icons'
import { PluginCardAction } from '@/types/plugin'
import { UiActionKind } from '@/utils/ui/actions'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginCardActions. */
const props = withDefaults(defineProps<{
  /** The plugin the footer belongs to; the suffix of every test id. */
  pluginId: string
  /** The actions to draw, in order. */
  actions: PluginCardAction[]
  /** The version the Update button names, null when there is none to name. */
  version?: string | null
  /** True while the install or update runs: the Install and Update button shows its busy state and cannot be pressed again. */
  busy?: boolean
  /** Test id prefix per action, for a screen whose tests and flows already name them; an action without an entry gets the default prefix. */
  testidPrefixes?: Partial<Record<PluginCardAction, string>>
}>(), {
  version: null,
  busy: false,
  /**
   * Default for `testidPrefixes`.
   *
   * @returns No custom prefix.
   */
  testidPrefixes: () => ({}),
})

/** Events emitted by PluginCardActions. */
defineEmits<{
  /** Emitted with the action the operator pressed. */
  select: [action: PluginCardAction]
}>()

/**
 * Test id of the control of an action.
 *
 * @param action - The action.
 * @returns The prefix of the action followed by the plugin id.
 */
const testidOf = (action: PluginCardAction): string =>
  `${props.testidPrefixes[action] ?? `plugin-card-action-${action}`}-${props.pluginId}`
</script>

<template>
  <template v-for="action in actions" :key="action">
    <UiSkeletonBar
      v-if="action === PluginCardAction.Checking"
      class="h-8 w-28 rounded-control"
      aria-hidden="true"
      :data-testid="testidOf(action)"
    />
    <UiButton
      v-else-if="action === PluginCardAction.Details"
      :action="UiActionKind.View"
      type="button"
      :variant="UiButtonVariant.Primary"
      :size="UiSize.Sm"
      :data-testid="testidOf(action)"
      @click="$emit('select', action)"
    >
      {{ t('shared.details') }}
    </UiButton>
    <UiButton
      v-else-if="action === PluginCardAction.Install || action === PluginCardAction.Update"
      :icon="Icon.Download"
      type="button"
      :variant="UiButtonVariant.Primary"
      :size="UiSize.Sm"
      :loading="busy"
      :disabled="busy"
      :data-testid="testidOf(action)"
      @click="$emit('select', action)"
    >
      {{ action === PluginCardAction.Update ? t('plugins.pluginRegistry.update', { version }) : t('shared.install') }}
    </UiButton>
    <UiIconButton
      v-else
      :label="t('plugins.pluginRegistry.settings')"
      :variant="UiButtonVariant.Ghost"
      :size="UiSize.Sm"
      :data-testid="testidOf(action)"
      @click="$emit('select', action)"
    >
      <UiIcon :markup="Icon.Settings" :size="UiSize.Sm" />
    </UiIconButton>
  </template>
</template>
