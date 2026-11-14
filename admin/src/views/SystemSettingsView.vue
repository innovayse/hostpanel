<script setup lang="ts">
/**
 * System settings view: the settings of the whole installation, split into tabs (General, Appearance,
 * Integrations, Advanced) that live in the address (`?tab=`), so a reload, a shared link and the back
 * button restore the same tab.
 *
 * Each tab is its own {@link SettingsTabPanel}: it renders the cards the catalogue lists for the tab and
 * owns the draft registry and the sticky bar of that tab only, so an edit in one tab never enables
 * the bar of another. Every panel stays mounted and only the selected one is shown, so switching tabs and
 * back keeps each tab's unsaved edits. The Advanced tab lists every key no card owns.
 *
 * Only the first load replaces the cards with placeholders: every save re-fetches the list, and swapping
 * the cards for a spinner and back would unmount them and drop every unsaved draft. A refused first load
 * shows the API's sentence with a retry instead of the tab's content.
 */
import { computed, onMounted } from 'vue'
import type { Component } from 'vue'
import { useI18n } from 'vue-i18n'
import BrandingCard from '@/components/settings/BrandingCard.vue'
import RawSettingsTable from '@/components/settings/RawSettingsTable.vue'
import SettingsBreadcrumb from '@/components/settings/SettingsBreadcrumb.vue'
import SettingsTabPanel from '@/components/settings/SettingsTabPanel.vue'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import { useRouteTab } from '@/composables/useRouteTab'
import { AppRoute } from '@/lib/routes'
import { DEFAULT_SETTINGS_TAB, SETTINGS_TABS, SettingsTab } from '@/lib/settingsTabs'
import { useSettingsStore } from '@/stores/settings'
import type { SettingSaveResult } from '@/types/setting'
import type { UiTabItem } from '@/utils/ui/tabs'

const { t } = useI18n()

/** The trail back to the settings index. */
const crumbs = computed(() => [
  { label: t('nav.settings'), to: AppRoute.Settings },
  { label: t('nav.settingsChildren.general') },
])

/** Settings store: the list, the loading flag and the refusal sentence. */
const store = useSettingsStore()

/** The selected tab, kept in the address, and the action that selects another. */
const { tab, select } = useRouteTab(SettingsTab, DEFAULT_SETTINGS_TAB)

/** The strip's tabs, in the catalogue's order. */
const tabItems = computed<UiTabItem[]>(() =>
  SETTINGS_TABS.map((definition) => ({
    value: definition.tab,
    label: t(definition.label),
    testid: `settings-tab-${definition.tab}`,
  })),
)

/** True while the settings are read for the first time: the tab keeps its place as placeholders. */
const initialLoading = computed<boolean>(() => store.loading && store.settings.length === 0)

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
const onSave = async (id: number, value: string): Promise<SettingSaveResult> => {
  const ok = await store.updateSetting(id, value)
  return { saved: ok, error: ok ? null : store.actionError }
}

/**
 * Props a card needs beyond the settings list: only the branding card uploads files and saves them itself.
 *
 * @param card - The card about to render.
 * @returns The extra props of that card.
 */
const extraPropsOf = (card: Component): Record<string, unknown> => (card === BrandingCard ? { save: onSave } : {})

/**
 * Selects the tab the strip reports.
 *
 * @param value - The value of the chosen tab; one the strip itself was built from.
 */
const onSelect = (value: string): void => {
  const definition = SETTINGS_TABS.find((entry) => entry.tab === value)
  if (definition) {
    void select(definition.tab)
  }
}
</script>

<template>
  <UiStack gap="none" class="flex-1" data-testid="system-settings-view">
    <SettingsBreadcrumb :items="crumbs" />
    <UiPageHeader :title="t('settings.systemSettings')" />
    <!-- The installed version, quietly under the title; absent when it could not be loaded. -->
    <VersionLabel testid="settings-version" class="-mt-4 mb-4" />

    <UiTabs
      :items="tabItems"
      :model-value="tab"
      :aria-label="t('settings.tabs.label')"
      class="flex flex-1 flex-col"
      @update:model-value="onSelect"
    >
      <UiErrorState
        v-if="store.error && store.settings.length === 0"
        :message="store.error"
        testid="system-settings-error"
        @retry="store.fetchSettings()"
      />
      <!-- Every panel stays mounted and only the selected one shows, so each tab keeps its own drafts and bar. -->
      <template v-else>
        <SettingsTabPanel
          v-for="definition in SETTINGS_TABS"
          v-show="definition.tab === tab"
          :key="definition.tab"
          :save="onSave"
          :loading="initialLoading"
          :data-testid="`settings-panel-${definition.tab}`"
        >
          <template v-if="definition.tab === SettingsTab.Advanced">
            <UiStack gap="xs" class="mb-4">
              <UiHeading :level="2" variant="section">
                {{ t('settings.advanced.title') }}
              </UiHeading>
              <UiText tone="secondary" size="lg">
                {{ t('settings.advanced.description') }}
              </UiText>
            </UiStack>
            <RawSettingsTable :settings="store.settings" :loading="initialLoading" />
          </template>
          <UiStack v-else-if="initialLoading" gap="none" data-testid="system-settings-loading">
            <UiCard v-for="(card, index) in definition.cards" :key="index" loading :skeleton-lines="4" padding="md" class="mb-5" />
          </UiStack>
          <template v-else>
            <component
              :is="card"
              v-for="(card, index) in definition.cards"
              :key="index"
              :settings="store.settings"
              v-bind="extraPropsOf(card)"
            />
          </template>
        </SettingsTabPanel>
      </template>
    </UiTabs>
  </UiStack>
</template>
