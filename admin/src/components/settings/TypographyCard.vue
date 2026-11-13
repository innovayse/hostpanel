<script setup lang="ts">
/**
 * Typography card: the heading and body typefaces the storefront uses.
 *
 * Two selects over the four faces the storefront ships (all self-hosted; nothing here or on the
 * storefront loads from a font CDN), and a three-language preview underneath so an operator sees
 * Armenian, Russian and English in the pair before saving. The preview renders in each face through
 * a `font-brand-*` utility class, never an inline style. Empty means "the template's own", which is
 * what every install has until the operator chooses.
 */
import { computed, toRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useSettingDraft } from '@/composables/useSettingDraft'
import SettingNotSeeded from '@/components/settings/SettingNotSeeded.vue'
import SettingOutcome from '@/components/settings/SettingOutcome.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { BRAND_FONT_OPTIONS, previewClass } from '@/lib/brandFonts'
import { SettingEnv, SettingKey } from '@/types/setting'
import type { Setting } from '@/types/setting'

const { t } = useI18n()

/** Props for TypographyCard. */
const props = defineProps<{
  /** All system settings, as loaded from the backend. */
  settings: Setting[]
}>()

/** The settings list as a ref, which is what the draft composable watches. */
const settings = toRef(props, 'settings')

/** Draft of the heading face. */
const heading = useSettingDraft(settings, SettingKey.BrandFontHeading)

/** Draft of the body face. */
const body = useSettingDraft(settings, SettingKey.BrandFontBody)

/** The two selects, drafts of the page's save bar like every other control on it. */
const FIELDS = [
  { key: SettingKey.BrandFontHeading, env: SettingEnv[SettingKey.BrandFontHeading], testid: 'heading', labelKey: 'settings.typography.heading', draft: heading },
  { key: SettingKey.BrandFontBody, env: SettingEnv[SettingKey.BrandFontBody], testid: 'body', labelKey: 'settings.typography.body', draft: body },
]

/** Sample lines, one per storefront language. */
const SAMPLE_KEYS = [
  'settings.typography.sampleHy',
  'settings.typography.sampleRu',
  'settings.typography.sampleEn',
]

/** Options of both selects: "the template's own" first, then the four faces. */
const fontOptions = computed(() => [
  { value: '', label: t('settings.typography.templateDefault') },
  ...BRAND_FONT_OPTIONS.map((option) => ({ value: option.value as string, label: t(option.labelKey) })),
])
</script>

<template>
  <SettingsSection
    :columns="2"
    :title="t('settings.typography.title')"
    :description="t('settings.typography.description')"
  >
    <template v-for="field in FIELDS" :key="field.key">
      <SettingNotSeeded v-if="!field.draft.setting.value" :setting-key="field.key" :env-name="field.env" />
      <UiStack v-else gap="xs">
        <UiText tone="secondary" size="md">
          {{ t(field.labelKey) }}
        </UiText>
        <UiStack direction="row" align="center">
          <UiSelect
            v-model="field.draft.value.value"
            :options="fontOptions"
            :placeholder="t('settings.typography.templateDefault')"
            :data-testid="`typography-${field.testid}`"
            class="min-w-48 flex-1"
          />
          <SettingOutcome :outcome="field.draft.outcome.value" />
        </UiStack>
      </UiStack>
    </template>

    <!-- The pair as the storefront will set it: a heading in the heading face, a line in the body face. -->
    <UiCard padding="md" elevated class="md:col-span-2" data-testid="typography-preview">
      <UiStack v-for="sampleKey in SAMPLE_KEYS" :key="sampleKey" gap="xs" class="mb-2 last:mb-0">
        <UiText tone="primary" size="lg" class="font-bold" :class="previewClass(heading.value.value)">
          {{ t(sampleKey) }}
        </UiText>
        <UiText tone="secondary" size="md" :class="previewClass(body.value.value)">
          {{ t('settings.typography.sampleBody') }}
        </UiText>
      </UiStack>
    </UiCard>
  </SettingsSection>
</template>
