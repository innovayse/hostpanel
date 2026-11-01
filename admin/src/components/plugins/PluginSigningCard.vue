<script setup lang="ts">
/**
 * The "Allow unsigned plugins" switch of the plugins page, with its warning.
 *
 * An uploaded plugin runs inside the server, so a production server refuses a plugin package that is
 * unsigned, changed after signing or signed by an unknown key. This switch is the admin's way to lift
 * that, and so it always carries the warning, and an extra, louder one while it is on. The row is the
 * `plugins.allow_unsigned` setting, saved through the same draft-then-save path as every other
 * settings row; a row the server never seeded is replaced by a notice, because the panel cannot create settings.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import { useSettingSwitch } from '@/composables/useSettingSwitch'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingSaveButton from '@/components/settings/SettingSaveButton.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { PluginSettingKey, SettingToggle } from '@/types/setting'
import type { Setting, SettingSave } from '@/types/setting'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginSigningCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** Persists one row and answers how it went; the card shows the outcome next to the switch. */
  save: SettingSave
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Draft of the row; an empty row reads as off, the server's default. */
const draft = useSettingDraft(settings, PluginSettingKey.AllowUnsigned, SettingToggle.Off)

/** The switch's view of the row: only the literal `true` is on. */
const enabled = useSettingSwitch(draft)
</script>

<template>
  <UiCard padding="md" data-testid="plugin-signing">
    <UiStack gap="sm">
      <UiHeading :level="2" variant="section">{{ t('plugins.pluginSigning.title') }}</UiHeading>
      <UiText tone="secondary" size="md">{{ t('plugins.pluginSigning.description') }}</UiText>

      <UiAlert variant="warning" data-testid="plugin-signing-warning">
        {{ t('plugins.pluginSigning.warning') }}
      </UiAlert>

      <UiAlert v-if="!draft.setting.value" variant="warning" data-testid="plugin-signing-not-seeded">
        {{ t('plugins.pluginSigning.notSeeded', { key: PluginSettingKey.AllowUnsigned }) }}
      </UiAlert>
      <template v-else>
        <UiAlert v-if="enabled" variant="danger" data-testid="plugin-signing-active">
          {{ t('plugins.pluginSigning.active') }}
        </UiAlert>
        <UiStack direction="row" align="center" wrap>
          <UiToggle v-model="enabled" data-testid="plugin-allow-unsigned" />
          <UiStack gap="xs" class="min-w-56 flex-1">
            <UiText tone="primary" size="md">{{ t('plugins.pluginSigning.label') }}</UiText>
            <UiText tone="muted" size="sm">{{ t('plugins.pluginSigning.hint') }}</UiText>
          </UiStack>
          <SettingSaveButton
            :dirty="draft.dirty.value"
            :saving="draft.saving.value"
            data-testid="plugin-allow-unsigned-save"
            @click="draft.submit(save)"
          />
          <SettingOutcome :outcome="draft.outcome.value" />
        </UiStack>
      </template>
    </UiStack>
  </UiCard>
</template>
