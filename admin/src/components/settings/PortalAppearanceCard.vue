<script setup lang="ts">
/**
 * Portal appearance card: the storefront template, the colour mode a visitor starts in, and
 * whether visitors may switch it.
 *
 * Constrained controls rather than the free-text fields the settings table offers. The API
 * refuses a value outside each key's vocabulary, and the portal falls back to its default on
 * anything it does not recognise, so a typo would not break the site, but it would silently
 * revert an operator's choice with no indication why. A dropdown and a radio group remove the
 * failure entirely.
 */
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiRadio from '@/components/ui/UiRadio.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { PortalTemplate, PortalThemeMode } from '@/types/portalappearance'
import { SettingEnv, SettingKey, SettingToggle } from '@/types/setting'
import type { Setting } from '@/types/setting'

const { t } = useI18n()

/** Props for PortalAppearanceCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** Templates with their label and hint keys. */
const TEMPLATES = [
  { value: PortalTemplate.Aurora, labelKey: 'settings.portalAppearance.templates.aurora.label', hintKey: 'settings.portalAppearance.templates.aurora.hint' },
  { value: PortalTemplate.Nova, labelKey: 'settings.portalAppearance.templates.nova.label', hintKey: 'settings.portalAppearance.templates.nova.hint' },
  { value: PortalTemplate.Classic, labelKey: 'settings.portalAppearance.templates.classic.label', hintKey: 'settings.portalAppearance.templates.classic.hint' },
]

/** Colour modes with their label keys. */
const THEME_MODES = [
  { value: PortalThemeMode.Light, labelKey: 'settings.portalAppearance.themeModes.light' },
  { value: PortalThemeMode.Dark, labelKey: 'settings.portalAppearance.themeModes.dark' },
  { value: PortalThemeMode.System, labelKey: 'settings.portalAppearance.themeModes.system' },
]

/** Settings key of the template. */
const TEMPLATE_KEY = SettingKey.Template

/** Settings key of the default colour mode. */
const THEME_KEY = SettingKey.ThemeDefault

/** Settings key of the visitor's light/dark switch. */
const TOGGLE_KEY = SettingKey.ThemeUserToggle

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Draft of the template row. */
const templateDraft = useSettingDraft(settings, TEMPLATE_KEY, PortalTemplate.Classic)

/** Draft of the default colour mode row. */
const themeDraft = useSettingDraft(settings, THEME_KEY, PortalThemeMode.Dark)

/** Draft of the visitor-switch row. */
const toggleDraft = useSettingDraft(settings, TOGGLE_KEY, SettingToggle.On)

/** The switch's view of the boolean setting: only the literal `false` is off. */
const toggleEnabled = computed<boolean>({
  /**
   * Whether the switch shows as on.
   *
   * @returns False only for the literal `false`.
   */
  get: () => toggleDraft.value.value !== SettingToggle.Off,
  /**
   * Writes the switch back as the string the API stores.
   *
   * @param on - Whether the switch is now on.
   */
  set: (on: boolean) => {
    toggleDraft.value.value = on ? SettingToggle.On : SettingToggle.Off
  },
})

/** Options of the template dropdown. */
const templateOptions = computed(() => TEMPLATES.map((tpl) => ({ value: tpl.value as string, label: t(tpl.labelKey) })))

/** The hint line of the chosen template. */
const templateHint = computed<string>(() => {
  const chosen = TEMPLATES.find((tpl) => tpl.value === templateDraft.value.value)
  return chosen ? t(chosen.hintKey) : ''
})

/** Keys the seeder has not created yet, so the operator knows why a control is missing. */
const missingKeys = computed<SettingKey[]>(() => {
  const rows: [SettingKey, Setting | undefined][] = [
    [TEMPLATE_KEY, templateDraft.setting.value],
    [THEME_KEY, themeDraft.setting.value],
    [TOGGLE_KEY, toggleDraft.setting.value],
  ]
  return rows.filter(([, row]) => !row).map(([key]) => key)
})
</script>

<template>
  <SettingsSection
    :columns="2"
    :title="t('settings.portalAppearance.title')"
    :description="t('settings.portalAppearance.description')"
  >
    <SettingNotSeeded
      v-for="key in missingKeys"
      :key="key"
      :setting-key="key"
      :env-name="SettingEnv[key]"
    />

    <!-- Template -->
    <UiStack v-if="templateDraft.setting.value" direction="row" align="end" wrap>
      <UiSelect
        v-model="templateDraft.value.value"
        :options="templateOptions"
        :placeholder="t('settings.portalAppearance.template')"
        data-testid="portal-template"
        class="min-w-56"
      />
      <SettingOutcome :outcome="templateDraft.outcome.value" />
      <UiText tone="muted" size="md">
        {{ templateHint }}
      </UiText>
    </UiStack>

    <!-- Colour mode -->
    <UiStack v-if="themeDraft.setting.value" gap="sm">
      <UiHeading :level="3" variant="card">
        {{ t('settings.portalAppearance.themeDefault') }}
      </UiHeading>
      <UiText tone="muted" size="sm">
        {{ t('settings.portalAppearance.themeDefaultHint') }}
      </UiText>
      <UiStack direction="row" align="center" wrap>
        <UiLabel v-for="mode in THEME_MODES" :key="mode.value" variant="inline">
          <UiRadio
            v-model="themeDraft.value.value"
            name="portal-theme-default"
            :value="mode.value"
            :data-testid="`portal-theme-${mode.value}`"
          />
          {{ t(mode.labelKey) }}
        </UiLabel>
        <SettingOutcome :outcome="themeDraft.outcome.value" />
      </UiStack>
    </UiStack>

    <!-- Visitor toggle -->
    <UiStack v-if="toggleDraft.setting.value" direction="row" align="center" wrap>
      <UiToggle v-model="toggleEnabled" data-testid="portal-user-toggle" />
      <UiStack gap="xs">
        <UiText tone="primary" size="md">
          {{ t('settings.portalAppearance.userToggle') }}
        </UiText>
        <UiText tone="muted" size="sm">
          {{ t('settings.portalAppearance.userToggleHint') }}
        </UiText>
      </UiStack>
      <SettingOutcome :outcome="toggleDraft.outcome.value" />
    </UiStack>
  </SettingsSection>
</template>
