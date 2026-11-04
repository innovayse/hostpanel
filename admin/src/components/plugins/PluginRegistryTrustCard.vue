<script setup lang="ts">
/**
 * The "Registry trust" choice of the Browse tab, with its warning.
 *
 * It sets whose plugins this server may install from the registry: verified publishers only, any
 * publisher the registry lists, or only packages signed with the operator's own keys. The row is
 * the `plugins.registry_trust` setting, saved through the same draft-then-save path as every other
 * settings row (like the signing switch); a row the server never seeded is replaced by a notice,
 * because the panel cannot create settings. Choosing the widest level shows a warning.
 */
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingSaveButton from '@/components/settings/SettingSaveButton.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { PluginRegistryTrust } from '@/types/plugin'
import { PluginSettingKey } from '@/types/setting'
import type { Setting, SettingSave } from '@/types/setting'

/** Translation function. */
const { t } = useI18n()

/** Props for PluginRegistryTrustCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
  /** Persists one row and answers how it went; the card shows the outcome next to the select. */
  save: SettingSave
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Draft of the row; an empty row reads as the strictest level, the server's default. */
const draft = useSettingDraft(settings, PluginSettingKey.RegistryTrust, PluginRegistryTrust.VerifiedOnly)

/** Options of the select: one per trust level. */
const options = computed<{ value: PluginRegistryTrust; label: string }[]>(() =>
  Object.values(PluginRegistryTrust).map(value => ({ value, label: t(`plugins.registryTrust.options.${value}`) })),
)

/**
 * The draft as a trust level. The draft holds free text, so a stored value that is no level reads as the
 * strictest one, the server's default; a pick writes the level's own string back.
 */
const trust = computed<PluginRegistryTrust>({
  /**
   * Reads the draft as a trust level.
   *
   * @returns The level the draft holds, or the strictest level when it holds none.
   */
  get: () => Object.values(PluginRegistryTrust).find(level => level === draft.value.value) ?? PluginRegistryTrust.VerifiedOnly,
  /**
   * Writes the picked level back into the draft as its own string.
   *
   * @param level - The trust level the operator picked.
   */
  set: (level) => {
    draft.value.value = level
  },
})

/** True while the widest level is chosen, which shows the warning. */
const wide = computed<boolean>(() => trust.value === PluginRegistryTrust.AnyRegistryPublisher)
</script>

<template>
  <UiCard padding="md" data-testid="plugin-registry-trust">
    <UiStack gap="sm">
      <UiHeading :level="2" variant="section">{{ t('plugins.registryTrust.title') }}</UiHeading>
      <UiText tone="secondary" size="md">{{ t('plugins.registryTrust.description') }}</UiText>

      <UiAlert v-if="!draft.setting.value" variant="warning" data-testid="plugin-registry-trust-not-seeded">
        {{ t('plugins.registryTrust.notSeeded', { key: PluginSettingKey.RegistryTrust }) }}
      </UiAlert>
      <template v-else>
        <UiAlert v-if="wide" variant="danger" data-testid="plugin-registry-trust-warning">
          {{ t('plugins.registryTrust.warning') }}
        </UiAlert>
        <UiStack direction="row" align="center" wrap>
          <UiStack gap="xs" class="min-w-56 flex-1">
            <UiText tone="primary" size="md">{{ t('plugins.registryTrust.label') }}</UiText>
            <UiText tone="muted" size="sm">{{ t('plugins.registryTrust.hint') }}</UiText>
          </UiStack>
          <UiSelect
            v-model="trust"
            :options="options"
            :placeholder="t('plugins.registryTrust.placeholder')"
            class="w-72"
            data-testid="plugin-registry-trust-select"
          />
          <SettingSaveButton
            :dirty="draft.dirty.value"
            :saving="draft.saving.value"
            data-testid="plugin-registry-trust-save"
            @click="draft.submit(save)"
          />
          <SettingOutcome :outcome="draft.outcome.value" />
        </UiStack>
      </template>
    </UiStack>
  </UiCard>
</template>
