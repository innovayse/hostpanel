<script setup lang="ts">
/**
 * System settings view: the settings of the whole installation, split into tabs (General, Appearance,
 * Integrations, Advanced) that live in the address (`?tab=`), so a reload, a shared link and the back
 * button restore the same tab.
 *
 * The tabs are the ones the screen's schema lists (the skeleton draws the same strip). Each tab is its own
 * {@link SettingsTabPanel}: it renders the cards the catalogue lists for the tab and
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
import SettingsTabPanel from '@/components/settings/SettingsTabPanel.vue'
import VersionLabel from '@/components/layout/VersionLabel.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import { useRouteTab } from '@/composables/useRouteTab'
import { DEFAULT_SETTINGS_TAB, cardsOfTab } from '@/lib/settingsTabs'
import { schemaHeader } from '@/lib/schemaHeader'
import { useSettingsStore } from '@/stores/settings'
import { SettingsTab } from '@/types/setting'
import type { UiTabItem } from '@/utils/ui/tabs'
import { UiGap } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import schema from '@/views/SystemSettingsView.schema'

const { t } = useI18n()

/** Settings store: the list, the loading flag and the refusal sentence. */
const store = useSettingsStore()

/** The selected tab, kept in the address, and the action that selects another. */
const { tab, select } = useRouteTab(SettingsTab, DEFAULT_SETTINGS_TAB)

/** The strip's tabs, in the schema's order. */
const tabItems = computed<UiTabItem[]>(() =>
  schema.tabs.map((entry) => ({
    value: entry.key,
    label: t(entry.label),
    testid: `settings-tab-${entry.key}`,
  })),
)

/** True while the settings are read for the first time: every card keeps its place with its fields as placeholders. */
const initialLoading = computed<boolean>(() => store.loading && store.settings.length === 0)

onMounted(store.fetchSettings)

/**
 * Props a card needs beyond the settings list: only the branding card uploads files and saves them itself.
 *
 * @param card - The card about to render.
 * @returns The extra props of that card.
 */
const extraPropsOf = (card: Component): Record<string, unknown> => (card === BrandingCard ? { save: store.saveSetting } : {})

/**
 * Selects the tab the strip reports.
 *
 * @param value - The value of the chosen tab; one the strip itself was built from.
 */
const onSelect = (value: string): void => {
  const chosen = Object.values(SettingsTab).find((entry) => entry === value)
  if (chosen !== undefined) {
    void select(chosen)
  }
}
</script>

<template>
  <UiStack :gap="UiGap.None" class="flex-1" data-testid="system-settings-view">
    <UiPageHeader v-bind="schemaHeader(schema, t)" />
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
          v-for="entry in schema.tabs"
          v-show="entry.key === tab"
          :key="entry.key"
          :save="store.saveSetting"
          :loading="initialLoading"
          :data-testid="`settings-panel-${entry.key}`"
        >
          <template v-if="entry.key === SettingsTab.Advanced">
            <UiStack :gap="UiGap.Xs" class="mb-4">
              <UiHeading :level="2" :variant="UiHeadingVariant.Section">
                {{ t('settings.advanced.title') }}
              </UiHeading>
              <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Lg">
                {{ t('settings.advanced.description') }}
              </UiText>
            </UiStack>
            <RawSettingsTable :settings="store.settings" :loading="initialLoading" />
          </template>
          <!-- The real cards also while the settings load: their fields draw as placeholders (the panel provides the flag) -->
          <template v-else>
            <component
              :is="card"
              v-for="(card, index) in cardsOfTab(entry.key)"
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
