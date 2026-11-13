<script setup lang="ts">
/**
 * Hosting details card: the figures and facts the performance, security, installer, guarantee and
 * support sections of the storefront show.
 *
 * Text fields (response time, sites hosted, support response, installer app count, money back
 * days and the response time of each support channel) are drafts of the page's save bar; an empty value hides
 * that figure. The SSL, DDoS and no hidden fees switches are settings rows too. Whether a whole section shows is decided in the home sections card.
 */
import { toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import type { SettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingToggleField from '@/components/settings/SettingToggleField.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { SettingEnv, SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'
import { SHORT_TEXT_MAX_LENGTH } from '@/lib/limits'

const { t } = useI18n()

/** The most digits the money back days value takes (the API accepts 1 to 3 digits). */
const MONEY_BACK_DAYS_MAX_LENGTH = 3

/** Props for HostingDetailsCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** One editable text field of the card. */
interface HostingField {
  /** Settings key the field edits. */
  key: SettingKey
  /** Environment variable the portal falls back to until the row exists. */
  env: string
  /** Suffix of the field's test ids. */
  testid: string
  /** Locale key of the label. */
  labelKey: string
  /** Locale key of the placeholder. */
  hintKey: string
  /** The most characters the field takes. */
  maxLength: number
  /** The field's draft state. */
  draft: SettingDraft
}

/** The editable text fields, each with its own draft. */
const FIELDS: HostingField[] = [
  {
    key: SettingKey.StatsResponseTime,
    env: SettingEnv[SettingKey.StatsResponseTime],
    testid: 'stats-response-time',
    labelKey: 'settings.hostingDetails.statsResponseTime.label',
    hintKey: 'settings.hostingDetails.statsResponseTime.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.StatsResponseTime),
  },
  {
    key: SettingKey.StatsSitesHosted,
    env: SettingEnv[SettingKey.StatsSitesHosted],
    testid: 'stats-sites-hosted',
    labelKey: 'settings.hostingDetails.statsSitesHosted.label',
    hintKey: 'settings.hostingDetails.statsSitesHosted.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.StatsSitesHosted),
  },
  {
    key: SettingKey.StatsSupportResponse,
    env: SettingEnv[SettingKey.StatsSupportResponse],
    testid: 'stats-support-response',
    labelKey: 'settings.hostingDetails.statsSupportResponse.label',
    hintKey: 'settings.hostingDetails.statsSupportResponse.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.StatsSupportResponse),
  },
  {
    key: SettingKey.InstallerAppCount,
    env: SettingEnv[SettingKey.InstallerAppCount],
    testid: 'installer-app-count',
    labelKey: 'settings.hostingDetails.installerAppCount.label',
    hintKey: 'settings.hostingDetails.installerAppCount.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.InstallerAppCount),
  },
  {
    key: SettingKey.GuaranteeMoneyBackDays,
    env: SettingEnv[SettingKey.GuaranteeMoneyBackDays],
    testid: 'guarantee-money-back-days',
    labelKey: 'settings.hostingDetails.guaranteeMoneyBackDays.label',
    hintKey: 'settings.hostingDetails.guaranteeMoneyBackDays.hint',
    maxLength: MONEY_BACK_DAYS_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.GuaranteeMoneyBackDays),
  },
  {
    key: SettingKey.SupportResponseChat,
    env: SettingEnv[SettingKey.SupportResponseChat],
    testid: 'support-response-chat',
    labelKey: 'settings.hostingDetails.supportResponseChat.label',
    hintKey: 'settings.hostingDetails.supportResponseChat.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.SupportResponseChat),
  },
  {
    key: SettingKey.SupportResponseTicket,
    env: SettingEnv[SettingKey.SupportResponseTicket],
    testid: 'support-response-ticket',
    labelKey: 'settings.hostingDetails.supportResponseTicket.label',
    hintKey: 'settings.hostingDetails.supportResponseTicket.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.SupportResponseTicket),
  },
  {
    key: SettingKey.SupportResponseEmail,
    env: SettingEnv[SettingKey.SupportResponseEmail],
    testid: 'support-response-email',
    labelKey: 'settings.hostingDetails.supportResponseEmail.label',
    hintKey: 'settings.hostingDetails.supportResponseEmail.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.SupportResponseEmail),
  },
  {
    key: SettingKey.SupportResponsePhone,
    env: SettingEnv[SettingKey.SupportResponsePhone],
    testid: 'support-response-phone',
    labelKey: 'settings.hostingDetails.supportResponsePhone.label',
    hintKey: 'settings.hostingDetails.supportResponsePhone.hint',
    maxLength: SHORT_TEXT_MAX_LENGTH,
    draft: useSettingDraft(settings, SettingKey.SupportResponsePhone),
  },
]

/** One on/off row of the card. */
interface HostingToggle {
  /** Settings key the switch edits. */
  key: SettingKey
  /** Suffix of the switch's test id. */
  testid: string
  /** Locale key of the label. */
  labelKey: string
  /** Locale key of the hint. */
  hintKey: string
}

/** The switches; each row is empty until the operator turns it on, so each reads as off by default. */
const TOGGLES: HostingToggle[] = [
  { key: SettingKey.FeaturesSsl, testid: 'features-ssl', labelKey: 'settings.hostingDetails.featuresSsl.label', hintKey: 'settings.hostingDetails.featuresSsl.hint' },
  { key: SettingKey.FeaturesDdos, testid: 'features-ddos', labelKey: 'settings.hostingDetails.featuresDdos.label', hintKey: 'settings.hostingDetails.featuresDdos.hint' },
  { key: SettingKey.GuaranteeNoHiddenFees, testid: 'guarantee-no-hidden-fees', labelKey: 'settings.hostingDetails.guaranteeNoHiddenFees.label', hintKey: 'settings.hostingDetails.guaranteeNoHiddenFees.hint' },
]
</script>

<template>
  <SettingsSection
    :columns="2"
    :title="t('settings.hostingDetails.title')"
    :description="t('settings.hostingDetails.description')"
  >
    <template v-for="field in FIELDS" :key="field.key">
      <SettingNotSeeded v-if="!field.draft.setting.value" :setting-key="field.key" :env-name="field.env" />
      <UiStack v-else direction="row" align="end" wrap>
        <UiInput
          v-model="field.draft.value.value"
          :label="t(field.labelKey)"
          :placeholder="t(field.hintKey)"
          :maxlength="field.maxLength"
          :data-testid="`hosting-${field.testid}`"
          class="min-w-72 flex-1"
        />
        <SettingOutcome :outcome="field.draft.outcome.value" />
      </UiStack>
    </template>

    <SettingToggleField
      v-for="toggle in TOGGLES"
      :key="toggle.key"
      :settings="settings"
      :setting-key="toggle.key"
      :label="t(toggle.labelKey)"
      :hint="t(toggle.hintKey)"
      :default-on="false"
      :testid="`hosting-${toggle.testid}`"
    />
  </SettingsSection>
</template>
