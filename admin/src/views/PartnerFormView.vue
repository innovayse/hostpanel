<script setup lang="ts">
/**
 * Full-page form for creating or editing a storefront partner.
 *
 * Detects edit mode from the route params and loads the partner when editing. The partner name is
 * translated, so it sits in en / ru / hy tabs. Asks before leaving with unsaved edits.
 */
import { computed, reactive } from 'vue'
import { useI18n } from 'vue-i18n'
import SettingsSection from '@/components/settings/SettingsSection.vue'
import StorefrontFormShell from '@/components/storefront/StorefrontFormShell.vue'
import StorefrontImageField from '@/components/storefront/StorefrontImageField.vue'
import UiField from '@/components/ui/UiField.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTabs from '@/components/ui/UiTabs.vue'
import UiText from '@/components/ui/UiText.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useStorefrontForm } from '@/composables/useStorefrontForm'
import { useStorefrontLocaleTabs } from '@/composables/useStorefrontLocaleTabs'
import { blankTranslations, collectTranslations, formsFromTranslations } from '@/lib/storefrontTranslations'
import { usePartnersStore } from '@/stores/partners'
import type { Partner, PartnerPayload, PartnerTranslation } from '@/types/partner'
import { AppRoute } from '@/lib/routes'

const { t } = useI18n()

/** Partners store: loads, creates and updates the partner and uploads its logo. */
const store = usePartnersStore()

/**
 * Creates a blank name form.
 *
 * @returns An empty name.
 */
const blankName = (): PartnerTranslation => ({ name: '' })

/** The form state; any change marks the form dirty. */
const form = reactive({
  logoUrl: '',
  url: '',
  sortOrder: 0,
  isActive: true,
  translations: blankTranslations(blankName),
})

/**
 * Copies a stored partner into the form.
 *
 * @param item - The stored partner.
 */
const populate = (item: Partner): void => {
  form.logoUrl = item.logoUrl ?? ''
  form.url = item.url ?? ''
  form.sortOrder = item.sortOrder
  form.isActive = item.isActive
  form.translations = formsFromTranslations(item.translations, blankName)
}

/**
 * Builds the payload from the form: trimmed text, and only the locales with a name.
 *
 * @returns The create / update payload.
 */
const build = (): PartnerPayload => ({
  logoUrl: form.logoUrl,
  url: form.url.trim(),
  sortOrder: form.sortOrder,
  isActive: form.isActive,
  translations: collectTranslations(
    form.translations,
    (text) => text.name.trim() !== '',
    (text) => ({ name: text.name.trim() }),
  ),
})

/** The locale tab state. */
const { activeLocale, tabs, select } = useStorefrontLocaleTabs('partner-form-tab')

/** The create / edit flow: loading, saving, the dirty flag. */
const flow = useStorefrontForm<Partner, PartnerPayload>({ store, form, listPath: AppRoute.StorefrontPartners, populate, build })

/** The trail above the form. */
const breadcrumb = computed(() => [
  { label: t('storefront.partners.title'), to: AppRoute.StorefrontPartners },
  { label: flow.isEditMode.value ? t('storefront.partners.editTitle') : t('storefront.partners.createTitle') },
])
</script>

<template>
  <StorefrontFormShell
    testid="partner"
    :breadcrumb="breadcrumb"
    :save-label="flow.isEditMode.value ? t('shared.saveChanges') : t('storefront.partners.create')"
    :loading="flow.loadingEntry.value"
    :saving="flow.saving.value"
    :dirty="flow.isDirty.value"
    :error="flow.saveError.value"
    :load-error="flow.loadError.value"
    @retry="flow.reload"
    @save="flow.save"
    @cancel="flow.cancel"
  >
    <SettingsSection :title="t('storefront.form.general')">
      <StorefrontImageField
        v-model="form.logoUrl"
        :label="t('storefront.partners.logo')"
        :upload="store.uploadImage"
        :error="store.fieldErrors.logoUrl"
        :upload-error="store.uploadError"
        testid="partner-form-logo"
      />

      <UiField :label="t('storefront.partners.website')" :error="store.fieldErrors.url">
        <template #default="{ id }">
          <UiInput :id="id" v-model="form.url" type="text" :placeholder="t('storefront.partners.websitePlaceholder')" data-testid="partner-form-url" />
        </template>
      </UiField>

      <UiField :label="t('storefront.form.sortOrder')" :error="store.fieldErrors.sortOrder">
        <UiNumberInput v-model="form.sortOrder" :min="0" placeholder="0" data-testid="partner-form-sort-order" />
      </UiField>

      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('shared.active') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('storefront.partners.activeHint') }}</UiText>
        </UiStack>
        <UiToggle v-model="form.isActive" data-testid="partner-form-active" />
      </UiStack>
    </SettingsSection>

    <SettingsSection :title="t('storefront.form.translations')">
      <UiTabs :items="tabs" :model-value="activeLocale" @update:model-value="select" />

      <UiField :label="t('storefront.partners.name')" :error="store.fieldErrors.translations">
        <template #default="{ id }">
          <UiInput :id="id" v-model="form.translations[activeLocale].name" type="text" :placeholder="t('storefront.partners.namePlaceholder')" data-testid="partner-form-name" />
        </template>
      </UiField>
    </SettingsSection>
  </StorefrontFormShell>
</template>
