<script setup lang="ts">
/**
 * Full-page form for creating or editing a homepage slide.
 *
 * Detects edit mode from the route params, loads the slide when editing, and carries
 * locale-tabbed translations with dynamic feature lists. Asks before leaving with unsaved edits.
 * A refused load (or a missing slide) shows with a retry instead of the form; a refused save shows
 * next to the save bar, a saved slide returns to the list.
 */
import { Icon as MdiIcon } from '@iconify/vue'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import SettingsFormFooter from '@/components/settings/SettingsFormFooter.vue'
import StorefrontImageField from '@/components/storefront/StorefrontImageField.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiColorField from '@/components/ui/UiColorField.vue'
import UiDateTimePicker from '@/components/ui/UiDateTimePicker.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiLocalizedField from '@/components/ui/UiLocalizedField.vue'
import UiLabel from '@/components/ui/UiLabel.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiRadio from '@/components/ui/UiRadio.vue'
import UiSchemaForm from '@/components/ui/UiSchemaForm.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import { UiTone } from '@/utils/ui/tone'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { useFormDirty } from '@/composables/useFormDirty'
import { useUnsavedChangesGuard } from '@/composables/useUnsavedChangesGuard'
import { Icon } from '@/lib/icons'
import { nextRowKey } from '@/lib/rowKey'
import { enumValueOf } from '@/lib/enumValue'
import { filledLocales, STOREFRONT_LOCALES } from '@/lib/storefrontTranslations'
import { useSettingsStore } from '@/stores/settings'
import { useSlidesStore } from '@/stores/slides'
import type { CreateSlidePayload, SlideTranslation, UpdateSlidePayload } from '@/types/slide'
import { SlideAudience } from '@/types/slideaudience'
import { ContentLanguage } from '@/locales/contentLanguage'
import { AppRoute } from '@/lib/routes'
import { withoutFields } from '@/lib/formSchema'
import schema, { SLIDE_LEARN_MORE_FIELD } from '@/views/SlideFormView.schema'

const { t } = useI18n()

/** Current route; carries the slide id in edit mode. */
const route = useRoute()

/** Router, used to return to the list. */
const router = useRouter()

/** Slides store: loads, creates and updates the slide and uploads its image. */
const slidesStore = useSlidesStore()

/** Settings store: owns the products a slide can be linked to. */
const settingsStore = useSettingsStore()

/** One feature input: its text and a stable key that survives removals above it. */
interface FeatureField {
  /** Stable key of the row for list rendering. */
  key: number
  /** The feature text as typed. */
  text: string
}

/** One translation tab's field values. */
interface TranslationForm {
  /** Slide headline. */
  title: string
  /** Optional subtitle. */
  tagline: string
  /** Optional body text. */
  description: string
  /** Dynamic feature list. */
  features: FeatureField[]
  /** Optional CTA button label. */
  ctaText: string
  /** Optional CTA button link. */
  ctaUrl: string
}

/** Preset icons for quick selection: MDI icon names rendered via the icon library, with their label key. */
const ICON_PRESETS = [
  { name: 'cloud-check', labelKey: 'settings.slideForm.hosting' },
  { name: 'earth', labelKey: 'settings.slideForm.globe' },
  { name: 'brain', labelKey: 'settings.slideForm.ai' },
  { name: 'chart-bar', labelKey: 'settings.slideForm.analytics' },
  { name: 'school', labelKey: 'settings.slideForm.education' },
  { name: 'office-building', labelKey: 'settings.slideForm.property' },
  { name: 'cart', labelKey: 'settings.slideForm.eCommerce' },
  { name: 'silverware-fork-knife', labelKey: 'settings.slideForm.restaurant' },
  { name: 'view-dashboard', labelKey: 'nav.dashboard' },
  { name: 'rocket-launch', labelKey: 'settings.slideForm.launch' },
  { name: 'shield-check', labelKey: 'settings.slideForm.security' },
  { name: 'star', labelKey: 'settings.slideForm.premium' },
  { name: 'server', labelKey: 'clients.clientServiceDetail.server' },
  { name: 'cellphone', labelKey: 'settings.slideForm.mobile' },
  { name: 'code-tags', labelKey: 'currencies.table.code' },
  { name: 'palette', labelKey: 'settings.slideForm.design' },
  { name: 'headset', labelKey: 'nav.support' },
  { name: 'currency-usd', labelKey: 'settings.slideForm.finance' },
  { name: 'camera', labelKey: 'settings.slideForm.media' },
  { name: 'heart-pulse', labelKey: 'settings.slideForm.health' },
]

/** Target audience options with their label key. */
const AUDIENCE_OPTIONS = [
  { value: SlideAudience.All, labelKey: 'settings.slideForm.all' },
  { value: SlideAudience.Guest, labelKey: 'settings.slideForm.guest' },
  { value: SlideAudience.Authenticated, labelKey: 'settings.slideForm.authenticated' },
]

/** Whether we are in edit mode (slide ID present in the route). */
const isEditMode = computed<boolean>(() => !!route.params.id)

/** True while loading existing slide data in edit mode. */
const loadingSlide = ref(false)

/** True while saving the form. */
const saving = ref(false)

/** The API's sentence for the last refused save, null when none. */
const saveError = ref<string | null>(null)

/** Sentence for a failed load of the slide being edited (or its absence), null when none. */
const loadError = ref<string | null>(null)

/** MDI icon name input value. */
const iconName = ref('')

/** Hex brand color input value. */
const brandColor = ref('#06b6d4')

/** Image URL input value. */
const imageUrl = ref('')

/** Optional demo link input value. */
const demoUrl = ref('')

/** Optional learn-more link input value. */
const learnMoreUrl = ref('')

/** Selected product ID (0 means none). */
const productId = ref<number>(0)

/** Display order input value. */
const sortOrder = ref(0)

/** Whether the slide is active. */
const isActive = ref(true)

/** Target audience selection. */
const targetAudience = ref<string>(SlideAudience.All)

/** Optional visibility start moment. */
const visibleFrom = ref<Date | null>(null)

/** Optional visibility end moment. */
const visibleUntil = ref<Date | null>(null)

/** Currently active translation tab locale. */
const activeLocale = ref<ContentLanguage>(ContentLanguage.En)

/**
 * Creates a blank translation form object.
 *
 * @returns Empty translation form with default values.
 */
const createEmptyTranslation = (): TranslationForm => ({
  title: '',
  tagline: '',
  description: '',
  features: [],
  ctaText: '',
  ctaUrl: '',
})

/**
 * Reactive translations keyed by locale code. Written out per locale rather than built from the
 * enum: `Object.fromEntries` produces a string index signature, which cannot prove every locale is
 * present, so adding a locale to the enum fails to compile here until its tab is initialised too.
 */
const translations = reactive<Record<ContentLanguage, TranslationForm>>({
  [ContentLanguage.En]: createEmptyTranslation(),
  [ContentLanguage.Ru]: createEmptyTranslation(),
  [ContentLanguage.Hy]: createEmptyTranslation(),
  [ContentLanguage.De]: createEmptyTranslation(),
  [ContentLanguage.Es]: createEmptyTranslation(),
})

/** Whether a linked product is selected. */
const hasLinkedProduct = computed<boolean>(() => !!productId.value)

/** Options of the linked-product dropdown: "None" (0) first, then every product. */
const productOptions = computed(() => [
  { value: 0, label: t('shared.none') },
  ...settingsStore.products.map((p) => ({ value: p.id, label: p.name })),
])

/** The columns of the form: the schema's, without the learn more link while a linked product supplies it. */
const columns = computed(() => (hasLinkedProduct.value ? withoutFields(schema.columns, [SLIDE_LEARN_MORE_FIELD]) : schema.columns))

/** The help lines under the fields. */
const hints = computed(() => ({
  demoUrl: t('settings.slideForm.optionalTryDemoButtonLink'),
  [SLIDE_LEARN_MORE_FIELD]: t('settings.slideForm.optionalSecondaryLinkShownAs'),
  isActive: t('settings.slideForm.showThisSlideToVisitors'),
}))

/** Tracks whether the form has been modified since loading, and settles it once populated or saved. */
const { isDirty, settle } = useFormDirty([
  iconName, brandColor, imageUrl, demoUrl, learnMoreUrl, productId, sortOrder, isActive, targetAudience, visibleFrom, visibleUntil, translations,
])

/** Parks a route change while the reader is asked about unsaved edits. */
const leaveGuard = useUnsavedChangesGuard(() => isDirty.value && !saving.value)

/** Auto-fills the Learn More URL from the linked product, and clears it when the link is removed. */
watch(productId, (newId) => {
  if (!newId) {
    learnMoreUrl.value = ''
    return
  }
  const product = settingsStore.products.find((p) => p.id === newId)
  if (product?.slug) {
    learnMoreUrl.value = `/products/${product.slug}`
  } else if (product?.name) {
    learnMoreUrl.value = `/products/${product.name.toLowerCase().replace(/\s+/g, '-')}`
  }
})

/**
 * Loads the slide being edited into the form; a refusal or a missing slide is kept in `loadError`.
 *
 * @returns Resolves when the load has settled.
 */
const loadSlide = async (): Promise<void> => {
  loadError.value = null
  loadingSlide.value = true
  const id = Number(route.params.id)
  const slide = await slidesStore.fetchSlide(id)
  loadingSlide.value = false
  if (!slide) {
    // A refused load carries the API's own sentence; "not found" is only for a list that loaded without the slide.
    loadError.value = slidesStore.error ?? t('settings.slideForm.notFound', { id })
    return
  }

  iconName.value = slide.iconName
  brandColor.value = slide.brandColor
  imageUrl.value = slide.imageUrl
  demoUrl.value = slide.demoUrl ?? ''
  learnMoreUrl.value = slide.learnMoreUrl ?? ''
  productId.value = slide.productId ?? 0
  sortOrder.value = slide.sortOrder
  isActive.value = slide.isActive
  targetAudience.value = slide.targetAudience
  visibleFrom.value = slide.visibleFrom ? new Date(slide.visibleFrom) : null
  visibleUntil.value = slide.visibleUntil ? new Date(slide.visibleUntil) : null

  for (const locale of Object.values(ContentLanguage)) {
    translations[locale] = createEmptyTranslation()
  }
  for (const stored of slide.translations) {
    const locale = enumValueOf(ContentLanguage, stored.locale)
    if (locale !== null) {
      translations[locale] = {
        title: stored.title,
        tagline: stored.tagline ?? '',
        description: stored.description ?? '',
        features: (stored.features ?? []).map((text) => ({ key: nextRowKey(), text })),
        ctaText: stored.ctaText ?? '',
        ctaUrl: stored.ctaUrl ?? '',
      }
    }
  }

  // Populating the form is not an edit.
  await settle()
}

onMounted(async () => {
  await settingsStore.fetchProducts()
  if (isEditMode.value) {
    await loadSlide()
  }
})

/**
 * Adds a new empty feature input to the specified locale's feature list.
 *
 * @param locale - The locale to add the feature to.
 */
const addFeature = (locale: ContentLanguage): void => {
  translations[locale].features.push({ key: nextRowKey(), text: '' })
}

/**
 * Removes a feature from the specified locale's feature list by index.
 *
 * @param locale - The locale to remove the feature from.
 * @param index - The index of the feature to remove.
 */
const removeFeature = (locale: ContentLanguage, index: number): void => {
  translations[locale].features.splice(index, 1)
}

/**
 * Tells whether a language's slide text is written: it has a title.
 *
 * @param form - One language's text.
 * @returns True when the language is worth sending and its tab needs no marker.
 */
const isFilled = (form: TranslationForm): boolean => form.title.trim() !== ''

/** Whether each language has its title written, for the markers on the tabs. */
const filled = computed(() => filledLocales(translations, isFilled))

/**
 * Collects the translations that have a title, trimmed, with empty optional parts as null.
 *
 * @returns One entry per locale whose title is not blank.
 */
const collectTranslations = (): SlideTranslation[] => {
  const collected: SlideTranslation[] = []
  for (const locale of Object.values(ContentLanguage)) {
    const form = translations[locale]
    if (isFilled(form)) {
      const features = form.features.map((f) => f.text).filter((text) => text.trim())
      collected.push({
        locale,
        title: form.title.trim(),
        tagline: form.tagline.trim() || null,
        description: form.description.trim() || null,
        features: features.length > 0 ? features : null,
        ctaText: form.ctaText.trim() || null,
        ctaUrl: form.ctaUrl.trim() || null,
      })
    }
  }
  return collected
}

/** Collects form data and dispatches create or update via the store; returns to the list on success. */
const handleSave = async (): Promise<void> => {
  saving.value = true
  saveError.value = null

  const payload: CreateSlidePayload = {
    iconName: iconName.value,
    brandColor: brandColor.value,
    imageUrl: imageUrl.value,
    demoUrl: demoUrl.value || null,
    learnMoreUrl: learnMoreUrl.value || null,
    productId: productId.value || null,
    sortOrder: sortOrder.value,
    isActive: isActive.value,
    targetAudience: targetAudience.value,
    visibleFrom: visibleFrom.value?.toISOString() ?? null,
    visibleUntil: visibleUntil.value?.toISOString() ?? null,
    translations: collectTranslations(),
  }

  let saved: boolean
  if (isEditMode.value) {
    const id = Number(route.params.id)
    const update: UpdateSlidePayload = { ...payload, id }
    saved = await slidesStore.updateSlide(id, update)
  } else {
    saved = (await slidesStore.createSlide(payload)) !== null
  }
  saving.value = false

  if (saved) {
    await settle()
    void router.push(AppRoute.SettingsSlides)
  } else {
    saveError.value = slidesStore.actionError
  }
}

/** Navigates back to the slides list; the unsaved-changes guard asks first if needed. */
const handleCancel = (): void => {
  void router.push(AppRoute.SettingsSlides)
}
</script>

<template>
  <UiStack gap="none" class="pb-24" data-testid="slide-form-view">
    <UiPageHeader :title="isEditMode ? t('settings.slides.editSlide') : t('settings.slideForm.createSlide')" />


    <UiErrorState v-if="loadError" :message="loadError" testid="slide-form-load-error" @retry="loadSlide" />

    <template v-else>
      <UiSchemaForm :columns="columns" :loading="loadingSlide" :errors="slidesStore.fieldErrors" :hints="hints" class="mb-5">
        <template #field-iconName>
          <UiStack gap="sm">
            <UiStack direction="row" wrap gap="xs">
              <UiButton
                v-for="preset in ICON_PRESETS"
                :key="preset.name"
                type="button"
                size="sm"
                :variant="iconName === preset.name ? 'primary' : 'secondary'"
                :aria-pressed="iconName === preset.name"
                :title="preset.name"
                :data-testid="`slide-form-icon-${preset.name}`"
                @click="iconName = preset.name"
              >
                <MdiIcon :icon="`mdi:${preset.name}`" />
                {{ t(preset.labelKey) }}
              </UiButton>
            </UiStack>
            <UiStack direction="row" align="center" gap="sm">
              <MdiIcon v-if="iconName" :icon="`mdi:${iconName}`" class="text-xl text-text-primary" />
              <UiInput
                v-model="iconName"
                type="text"
                :placeholder="t('settings.slideForm.orTypeMdiIconName')"
                data-testid="slide-form-icon-name"
                class="flex-1"
              />
            </UiStack>
          </UiStack>
        </template>

        <template #field-brandColor>
          <UiColorField
            v-model="brandColor"
            :placeholder="t('shared.examples.colorHex')"
            :aria-label="t('settings.slideForm.brandColor')"
            data-testid="slide-form-brand-color-hex"
          />
        </template>

        <template #field-imageUrl>
          <StorefrontImageField
            v-model="imageUrl"
            :alt="t('settings.slideForm.slidePreview')"
            testid="slide-form-image"
            :upload="slidesStore.uploadImage"
            :upload-error="slidesStore.uploadError"
          />
        </template>

        <template #field-demoUrl="{ id, describedby }">
          <UiInput
            :id="id"
            v-model="demoUrl"
            type="text"
            :placeholder="t('settings.slideForm.demoUrlPlaceholder')"
            :aria-describedby="describedby"
            data-testid="slide-form-demo-url"
          />
        </template>

        <template #field-learnMoreUrl="{ id, describedby }">
          <UiInput
            :id="id"
            v-model="learnMoreUrl"
            type="text"
            :placeholder="t('settings.slideForm.productsExampleOrHttpsExample')"
            :aria-describedby="describedby"
            data-testid="slide-form-learn-more-url"
          />
        </template>

        <template #field-productId>
          <UiSelect
            v-model="productId"
            :options="productOptions"
            :placeholder="t('shared.none')"
            data-testid="slide-form-product"
          />
        </template>

        <template #field-sortOrder>
          <UiNumberInput
            v-model="sortOrder"
            :min="0"
            placeholder="0"
            data-testid="slide-form-sort-order"
          />
        </template>

        <template #field-isActive>
          <UiToggle v-model="isActive" data-testid="slide-form-active" />
        </template>

        <template #field-targetAudience>
          <UiStack direction="row" align="center" wrap>
            <UiLabel v-for="option in AUDIENCE_OPTIONS" :key="option.value" variant="inline">
              <UiRadio
                v-model="targetAudience"
                name="slide-target-audience"
                :value="option.value"
                :data-testid="`slide-form-audience-${option.value}`"
              />
              {{ t(option.labelKey) }}
            </UiLabel>
          </UiStack>
        </template>

        <template #field-visibleFrom>
          <UiDateTimePicker v-model="visibleFrom" :placeholder="t('settings.slideForm.noStartDate')" />
        </template>

        <template #field-visibleUntil>
          <UiDateTimePicker
            v-model="visibleUntil"
            :min-date="visibleFrom ?? undefined"
            :placeholder="t('settings.slideForm.noEndDate')"
          />
        </template>

        <template #field-locales>
          <UiLocalizedField v-model="activeLocale" :locales="STOREFRONT_LOCALES" :filled="filled" testid="slide-form-tab" />
        </template>

        <template #field-title="{ id }">
          <UiInput
            :id="id"
            v-model="translations[activeLocale].title"
            type="text"
            :placeholder="t('settings.slideForm.slideTitle')"
            data-testid="slide-form-title"
          />
        </template>

        <template #field-tagline="{ id }">
          <UiInput
            :id="id"
            v-model="translations[activeLocale].tagline"
            type="text"
            :placeholder="t('settings.slideForm.shortSubtitle')"
            data-testid="slide-form-tagline"
          />
        </template>

        <template #field-description="{ id }">
          <UiTextarea
            :id="id"
            v-model="translations[activeLocale].description"
            rows="3"
            :placeholder="t('settings.slideForm.slideBodyText')"
            data-testid="slide-form-description"
            class="resize-none"
          />
        </template>

        <template #field-features>
          <UiStack gap="sm">
            <UiStack
              v-for="(feature, featureIndex) in translations[activeLocale].features"
              :key="feature.key"
              direction="row"
              align="center"
              gap="sm"
            >
              <UiInput
                v-model="feature.text"
                type="text"
                :placeholder="t('settings.slideForm.featureNumber', { number: featureIndex + 1 })"
                :data-testid="`slide-form-feature-${featureIndex}`"
                class="flex-1"
              />
              <UiIconButton
                :label="t('settings.slideForm.removeFeature')"
                size="sm"
                :data-testid="`slide-form-feature-remove-${featureIndex}`"
                @click="removeFeature(activeLocale, featureIndex)"
              >
                <UiIcon :markup="Icon.Close" size="sm" />
              </UiIconButton>
            </UiStack>
            <UiButton
              type="button"
              variant="link"
              size="sm"
              class="self-start"
              data-testid="slide-form-feature-add"
              @click="addFeature(activeLocale)"
            >
              <UiIcon :markup="Icon.Add" size="sm" />
              {{ t('settings.slideForm.addFeature') }}
            </UiButton>
          </UiStack>
        </template>

        <template #field-ctaText="{ id }">
          <UiInput
            :id="id"
            v-model="translations[activeLocale].ctaText"
            type="text"
            :placeholder="t('settings.slideForm.getStarted')"
            data-testid="slide-form-cta-text"
          />
        </template>

        <template #field-ctaUrl="{ id }">
          <UiInput
            :id="id"
            v-model="translations[activeLocale].ctaUrl"
            type="text"
            :placeholder="t('shared.examples.internalPath')"
            data-testid="slide-form-cta-url"
          />
        </template>
      </UiSchemaForm>

      <UiStatusMessage :message="saveError" :tone="UiTone.Danger" class="mb-5" data-testid="slide-form-error" />

      <SettingsFormFooter
        :save-label="isEditMode ? t('shared.saveChanges') : t('settings.slideForm.createSlide')"
        :saving="saving"
        :dirty="!loadingSlide"
        @cancel="handleCancel"
        @save="handleSave"
      />
    </template>

    <!-- Unsaved-changes question, asked while the route change waits -->
    <UiDialog
      v-if="leaveGuard.pending.value"
      variant="confirm"
      :title="t('settings.slideForm.unsavedChanges')"
      :message="t('settings.slideForm.youHaveUnsavedChangesAre')"
      :confirm-label="t('settings.slideForm.leave')"
      tone="warning"
      @confirm="leaveGuard.confirmLeave"
      @close="leaveGuard.cancelLeave"
    />
  </UiStack>
</template>
