<script setup lang="ts">
/**
 * System settings view: the storefront's identity, content, appearance and branding cards, then the
 * key-value list of every other global setting.
 *
 * Only the first load replaces the page with a loading state: every save re-fetches the list, and
 * swapping the cards for a spinner and back would unmount them and drop every unsaved draft in the
 * other cards. A refused first load shows the API's sentence with a retry instead of the cards.
 *
 * The outcome of a save (success, or the API's sentence) shows next to the control that
 * was saved: inside its card, or in the value cell of its table row.
 */
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BrandColorsCard from '@/components/settings/BrandColorsCard.vue'
import BrandingCard from '@/components/settings/BrandingCard.vue'
import HomeSectionsCard from '@/components/settings/HomeSectionsCard.vue'
import HostingDetailsCard from '@/components/settings/HostingDetailsCard.vue'
import PortalAppearanceCard from '@/components/settings/PortalAppearanceCard.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsLoading from '@/components/settings/SettingsLoading.vue'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import SiteIdentityCard from '@/components/settings/SiteIdentityCard.vue'
import StorefrontContentCard from '@/components/settings/StorefrontContentCard.vue'
import TrackingChatCard from '@/components/settings/TrackingChatCard.vue'
import TypographyCard from '@/components/settings/TypographyCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import { useSettingsStore } from '@/stores/settings'
import type { Setting, SettingSaveResult } from '@/types/setting'

const { t } = useI18n()

/** Settings store: the list, the loading flag and the refusal sentence. */
const store = useSettingsStore()

/** True while a table row's save is in flight, so the table disables its controls. */
const saving = ref(false)

/** How the last save of each table row went, keyed by setting id; cleared by the next edit. */
const rowOutcomes = ref<Record<number, SettingSaveResult | null>>({})

/**
 * Unsaved edits of the table rows, keyed by setting id.
 *
 * Every seeded key (the storefront's contact details, footer widgets and app launcher among them)
 * is editable here. Editing is per row rather than a bulk form so a mistake in one value cannot
 * be saved along with the others.
 */
const drafts = ref<Record<number, string>>({})

/**
 * Reads the outcome of the store's last action: saved, or the API's refusal sentence.
 *
 * @param ok - Whether the API accepted the change.
 * @returns The outcome a control shows next to itself.
 */
const resultOf = (ok: boolean): SettingSaveResult => ({ saved: ok, error: ok ? null : store.actionError })

onMounted(store.fetchSettings)

/**
 * Persists one setting for a card, which shows the outcome next to the saved control.
 *
 * It is returned rather than swallowed, because the caller has to know whether it may drop the
 * operator's text.
 *
 * @param id - Setting ID.
 * @param value - New value.
 * @returns Whether the value was stored, and the API's sentence when it was refused.
 */
const onSave = async (id: number, value: string): Promise<SettingSaveResult> => resultOf(await store.updateSetting(id, value))

/**
 * Current text for a row: the operator's unsaved edit, or the stored value.
 *
 * @param setting - The settings row.
 * @returns The text the input shows.
 */
const draftOf = (setting: Setting): string => drafts.value[setting.id] ?? (setting.value ?? '')

/**
 * Whether a row has an edit worth saving.
 *
 * @param setting - The settings row.
 * @returns True when the draft differs from the stored value.
 */
const isDirty = (setting: Setting): boolean => {
  if (!(setting.id in drafts.value)) {
    return false
  }
  return drafts.value[setting.id] !== (setting.value ?? '')
}

/**
 * Records an edit of a table row and forgets the outcome of its previous save.
 *
 * @param setting - The settings row.
 * @param value - The text now typed for it.
 */
const edit = (setting: Setting, value: string): void => {
  drafts.value[setting.id] = value
  rowOutcomes.value[setting.id] = null
}

/**
 * Saves one row and drops its draft, so the input falls back to the stored value.
 *
 * The draft is dropped only on success: dropping it either way would replace whatever the operator
 * typed with the value already stored, leaving an error and no way back to the text that
 * caused it.
 *
 * @param setting - The settings row.
 */
const saveRow = async (setting: Setting): Promise<void> => {
  const draft = drafts.value[setting.id]
  if (!isDirty(setting) || draft === undefined) {
    return
  }
  rowOutcomes.value[setting.id] = null
  saving.value = true
  const result = await onSave(setting.id, draft)
  saving.value = false
  rowOutcomes.value[setting.id] = result
  if (result.saved) {
    delete drafts.value[setting.id]
  }
}
</script>

<template>
  <UiStack gap="none" data-testid="system-settings-view">
    <UiPageHeader :title="t('settings.systemSettings')" />
    <!-- The installed version, quietly under the title; absent when it could not be loaded. -->
    <VersionLabel testid="settings-version" class="-mt-4 mb-4" />

    <SettingsLoading v-if="store.loading && store.settings.length === 0" :message="t('common.loading')" />
    <UiErrorState
      v-else-if="store.error && store.settings.length === 0"
      :message="store.error"
      testid="system-settings-error"
      @retry="store.fetchSettings()"
    />
    <template v-else>
      <!-- Identity first: the name is the first thing an operator sets, and everything below renders it. -->
      <SiteIdentityCard :settings="store.settings" :save="onSave" />
      <StorefrontContentCard :settings="store.settings" :save="onSave" />
      <PortalAppearanceCard :settings="store.settings" :save="onSave" />
      <HomeSectionsCard :settings="store.settings" :save="onSave" />
      <HostingDetailsCard :settings="store.settings" :save="onSave" />
      <BrandColorsCard :settings="store.settings" :save="onSave" />
      <TypographyCard :settings="store.settings" :save="onSave" />
      <BrandingCard :settings="store.settings" :save="onSave" />
      <TrackingChatCard :settings="store.settings" :save="onSave" />

      <UiCard class="overflow-hidden overflow-x-auto">
        <UiTable density="comfortable" :empty="store.settings.length === 0" :colspan="3">
          <template #empty>
            {{ t('settings.table.noSettings') }}
          </template>
          <UiTableHead>
            <UiTableRow>
              <UiTableHeader align="left">{{ t('settings.table.key') }}</UiTableHeader>
              <UiTableHeader align="left">{{ t('settings.table.value') }}</UiTableHeader>
              <UiTableHeader align="left">{{ t('settings.table.description') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="setting in store.settings" :key="setting.id" hoverable :data-testid="`setting-row-${setting.id}`">
              <UiTableCell class="font-mono text-text-primary">{{ setting.key }}</UiTableCell>
              <UiTableCell>
                <UiStack direction="row" align="center" gap="sm">
                  <UiInput
                    :model-value="draftOf(setting)"
                    type="text"
                    size="sm"
                    :placeholder="t('shared.value')"
                    :data-testid="`setting-value-${setting.id}`"
                    class="min-w-48 flex-1"
                    @update:model-value="edit(setting, $event)"
                    @keyup.enter="saveRow(setting)"
                  />
                  <UiButton
                    v-if="isDirty(setting)"
                    type="button"
                    variant="primary"
                    size="sm"
                    :disabled="saving"
                    :data-testid="`setting-save-${setting.id}`"
                    @click="saveRow(setting)"
                  >
                    {{ saving ? t('common.saving') : t('common.save') }}
                  </UiButton>
                </UiStack>
                <SettingOutcome :outcome="rowOutcomes[setting.id] ?? null" :data-testid="`setting-outcome-${setting.id}`" />
              </UiTableCell>
              <UiTableCell muted>{{ setting.description ?? '—' }}</UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiCard>
    </template>
  </UiStack>
</template>
