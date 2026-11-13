<script setup lang="ts">
/**
 * Dialog that creates or edits one product bundle: name, description, sort order, the component
 * products with the primary one marked, the optional domain registration and the discount. It owns
 * the form fields; the screen owns saving: the dialog only reports the finished payload and shows
 * the API's refusal sentence and per-field messages it is given. A new bundle always starts
 * active, so the Active switch appears only when an existing bundle is edited.
 */
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiRadio from '@/components/ui/UiRadio.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import type { CurrencyDto } from '@/types/currency'
import type { Product } from '@/types/product'
import {
  BUNDLE_DOMAIN_YEARS_MAX,
  BUNDLE_DOMAIN_YEARS_MIN,
  BUNDLE_MAX_PERCENTAGE,
} from '@/types/productbundle'
import type { ProductBundle, ProductBundleItem, ProductBundlePayload } from '@/types/productbundle'
import { nextRowKey } from '@/lib/rowKey'
import { BundleDiscountType, ProductType } from '@/types/productenums'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiDirection } from '@/utils/ui/layout'
import { UiTone } from '@/utils/ui/tone'

/** Props for ProductBundleFormDialog. */
const props = defineProps<{
  /** Whether the dialog is shown. */
  open: boolean
  /** The bundle being edited; null creates a new one. */
  bundle: ProductBundle | null
  /** Every product of the catalogue; the domain products are left out of the pickers. */
  products: Product[]
  /** Every configured currency, offered for a fixed discount. */
  currencies: CurrencyDto[]
  /** Whether a save is in flight. */
  saving: boolean
  /** The API's sentence for a refused save, null when none. */
  error: string | null
  /** The API's per-field messages for a refused save, keyed by camelCase field name. */
  fieldErrors: Record<string, string>
}>()

const emit = defineEmits<{
  /** The form was confirmed; carries the payload to save. */
  save: [payload: ProductBundlePayload]
  /** The dialog was dismissed. */
  close: []
}>()

const { t } = useI18n()

/** One component row of the form: a stable key for the list and the product picked in it, if any. */
interface ComponentRow {
  /** Identifies the row across edits; never the position, which changes when a row is removed. */
  key: number
  /** The product picked in the row; null until one is chosen. */
  productId: number | null
}

/** Years of registration a bundle with a domain starts with. */
const DEFAULT_DOMAIN_YEARS = BUNDLE_DOMAIN_YEARS_MIN

/**
 * Builds an empty component row with a fresh key.
 *
 * @returns The new row.
 */
const newRow = (): ComponentRow => ({ key: nextRowKey(), productId: null })

/** Form field: bundle name. */
const name = ref('')

/** Form field: description. */
const description = ref('')

/** Form field: position in the list. */
const sortOrder = ref(0)

/** Form field: the component rows. */
const rows = ref<ComponentRow[]>([])

/** Form field: the key of the primary component's row, null while there is no row. */
const primaryKey = ref<number | null>(null)

/** Form field: whether a domain registration is included. */
const includesDomain = ref(false)

/** Form field: years of registration included. */
const domainYears = ref(DEFAULT_DOMAIN_YEARS)

/** Form field: how the bundle is discounted. */
const discountType = ref<BundleDiscountType>(BundleDiscountType.None)

/** Form field: the discount amount, a percentage or a fixed sum. */
const discountAmount = ref(0)

/** Form field: the currency of a fixed discount. */
const discountCurrency = ref<string | null>(null)

/** Form field: whether the storefront offers the bundle (edit only). */
const isActive = ref(true)

/** The products a component may be: the catalogue without domain products. */
const sellableProducts = computed<Product[]>(() => props.products.filter(product => product.type !== ProductType.Domain))

/** Options of the discount type picker. */
const discountTypeOptions = computed<{ value: BundleDiscountType; label: string }[]>(() => [
  { value: BundleDiscountType.None, label: t('settings.productBundles.discountTypeNone') },
  { value: BundleDiscountType.Percentage, label: t('settings.productBundles.discountTypePercentage') },
  { value: BundleDiscountType.Fixed, label: t('settings.productBundles.discountTypeFixed') },
])

/** Options of the fixed discount currency picker. */
const currencyOptions = computed<{ value: string | null; label: string }[]>(() =>
  props.currencies.map(currency => ({ value: currency.code, label: currency.code })),
)

/** The largest discount amount the picked type allows; a fixed sum has no ceiling of its own here. */
const maxDiscount = computed<number | undefined>(() =>
  discountType.value === BundleDiscountType.Percentage ? BUNDLE_MAX_PERCENTAGE : undefined,
)

/** The components the form holds: every row with a product, the primary one marked. */
const items = computed<ProductBundleItem[]>(() =>
  rows.value.flatMap(row =>
    row.productId === null ? [] : [{ productId: row.productId, isPrimary: row.key === primaryKey.value }],
  ),
)

/** Whether the form can be saved: it has a name and a primary component. */
const canSave = computed<boolean>(() => name.value.trim() !== '' && items.value.some(item => item.isPrimary))

/**
 * Lists the products a row may pick: every sellable product except those other rows already hold,
 * so a product cannot be added twice.
 *
 * @param row - The row the options are for.
 * @returns The picker options.
 */
const productOptions = (row: ComponentRow): { value: number | null; label: string }[] => {
  const taken = new Set(rows.value.filter(other => other.key !== row.key).map(other => other.productId))
  return sellableProducts.value
    .filter(product => !taken.has(product.id))
    .map(product => ({ value: product.id, label: product.name }))
}

/**
 * Adds an empty component row; the first row becomes the primary one.
 *
 * @returns Nothing.
 */
const addRow = (): void => {
  const row = newRow()
  rows.value.push(row)
  if (primaryKey.value === null) {
    primaryKey.value = row.key
  }
}

/**
 * Marks a component row as the bundle's primary one.
 *
 * @param key - The key of the row to mark.
 * @returns Nothing.
 */
const makePrimary = (key: number): void => {
  primaryKey.value = key
}

/**
 * Removes a component row; when it was the primary one the first remaining row takes over.
 *
 * @param key - The key of the row to remove.
 * @returns Nothing.
 */
const removeRow = (key: number): void => {
  rows.value = rows.value.filter(row => row.key !== key)
  if (primaryKey.value === key) {
    primaryKey.value = rows.value[0]?.key ?? null
  }
}

/**
 * Reports the finished payload; the fields the picked options make meaningless are sent empty.
 *
 * @returns Nothing.
 */
const submit = (): void => {
  const type = discountType.value
  emit('save', {
    name: name.value.trim(),
    description: description.value.trim() || null,
    sortOrder: sortOrder.value,
    includesDomain: includesDomain.value,
    domainYears: includesDomain.value ? domainYears.value : null,
    discountType: type,
    discountAmount: type === BundleDiscountType.None ? 0 : discountAmount.value,
    discountCurrency: type === BundleDiscountType.Fixed ? discountCurrency.value : null,
    items: items.value,
    isActive: props.bundle ? isActive.value : undefined,
  })
}

// The form starts from the bundle the screen opens, or empty (with one component row) for a new one.
watch(
  () => [props.open, props.bundle] as const,
  ([open, bundle]) => {
    if (!open) {
      return
    }
    name.value = bundle?.name ?? ''
    description.value = bundle?.description ?? ''
    sortOrder.value = bundle?.sortOrder ?? 0
    rows.value = (bundle?.components ?? []).map(component => ({ ...newRow(), productId: component.productId }))
    primaryKey.value = null
    if (bundle) {
      const primary = bundle.components.findIndex(component => component.isPrimary)
      primaryKey.value = rows.value[Math.max(primary, 0)]?.key ?? null
    } else {
      addRow()
    }
    includesDomain.value = bundle?.includesDomain ?? false
    domainYears.value = bundle?.domainYears ?? DEFAULT_DOMAIN_YEARS
    discountType.value = bundle?.discountType ?? BundleDiscountType.None
    discountAmount.value = bundle?.discountAmount ?? 0
    discountCurrency.value = bundle?.discountCurrency ?? null
    isActive.value = bundle?.isActive ?? true
  },
  { immediate: true },
)
</script>

<template>
  <UiDialog
    :open="open"
    :title="bundle ? t('settings.productBundles.editTitle') : t('settings.productBundles.createTitle')"
    size="lg"
    data-testid="product-bundle-form"
    @close="emit('close')"
  >
    <UiStack>
      <UiAlert v-if="error" :variant="UiTone.Danger" data-testid="product-bundle-form-error">{{ error }}</UiAlert>

      <UiField :label="t('shared.name')" :error="fieldErrors.name" required>
        <template #default="{ id, invalid }">
          <UiInput
            :id="id"
            v-model="name"
            :invalid="invalid"
            :placeholder="t('settings.productBundles.namePlaceholder')"
            data-testid="product-bundle-form-name"
          />
        </template>
      </UiField>

      <UiField :label="t('shared.description')" :error="fieldErrors.description">
        <template #default="{ invalid }">
          <UiTextarea
            v-model="description"
            :invalid="invalid"
            :placeholder="t('settings.productBundles.descriptionPlaceholder')"
            data-testid="product-bundle-form-description"
          />
        </template>
      </UiField>

      <UiField
        :label="t('settings.productBundles.sortOrder')"
        :hint="t('settings.productBundles.sortOrderHint')"
        :error="fieldErrors.sortOrder"
      >
        <template #default="{ id, invalid }">
          <UiNumberInput
            :id="id"
            v-model="sortOrder"
            :invalid="invalid"
            :min="0"
            :placeholder="t('settings.productBundles.sortOrderPlaceholder')"
            data-testid="product-bundle-form-sort-order"
          />
        </template>
      </UiField>

      <UiField
        :label="t('settings.productBundles.components')"
        :hint="t('settings.productBundles.componentsHint')"
        :error="fieldErrors.items"
        required
      >
        <UiStack gap="sm">
          <UiStack
            v-for="row in rows"
            :key="row.key"
            :direction="UiDirection.Row"
            gap="sm"
            align="center"
            data-testid="product-bundle-form-row"
          >
            <UiRadio
              :model-value="primaryKey ?? undefined"
              :value="row.key"
              :aria-label="t('settings.productBundles.makePrimary')"
              data-testid="product-bundle-form-primary"
              @update:model-value="makePrimary(row.key)"
            />
            <UiSelect
              v-model="row.productId"
              :options="productOptions(row)"
              :placeholder="t('settings.productBundles.productPlaceholder')"
              searchable
              class="min-w-0 flex-1"
              data-testid="product-bundle-form-product"
            />
            <UiButton
              :variant="UiButtonVariant.Ghost"
              size="sm"
              data-testid="product-bundle-form-remove"
              @click="removeRow(row.key)"
            >
              {{ t('settings.productBundles.removeComponent') }}
            </UiButton>
          </UiStack>
          <UiStack :direction="UiDirection.Row">
            <UiButton
              :variant="UiButtonVariant.Secondary"
              size="sm"
              data-testid="product-bundle-form-add-component"
              @click="addRow"
            >
              {{ t('settings.productBundles.addComponent') }}
            </UiButton>
          </UiStack>
        </UiStack>
      </UiField>

      <UiStack :direction="UiDirection.Row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('settings.productBundles.includesDomain') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('settings.productBundles.includesDomainHint') }}</UiText>
        </UiStack>
        <UiToggle v-model="includesDomain" data-testid="product-bundle-form-includes-domain" />
      </UiStack>

      <UiField
        v-if="includesDomain"
        :label="t('settings.productBundles.domainYears')"
        :error="fieldErrors.domainYears"
      >
        <template #default="{ id, invalid }">
          <UiNumberInput
            :id="id"
            v-model="domainYears"
            :invalid="invalid"
            :min="BUNDLE_DOMAIN_YEARS_MIN"
            :max="BUNDLE_DOMAIN_YEARS_MAX"
            :placeholder="t('settings.productBundles.domainYearsPlaceholder')"
            data-testid="product-bundle-form-domain-years"
          />
        </template>
      </UiField>

      <UiField :label="t('settings.productBundles.discountType')">
        <UiSelect
          v-model="discountType"
          :options="discountTypeOptions"
          :placeholder="t('settings.productBundles.discountTypePlaceholder')"
          data-testid="product-bundle-form-discount-type"
        />
      </UiField>

      <UiGrid v-if="discountType !== BundleDiscountType.None" :columns="2" gap="sm">
        <UiField :label="t('settings.productBundles.discountAmount')" :error="fieldErrors.discountAmount">
          <template #default="{ id, invalid }">
            <UiNumberInput
              :id="id"
              v-model="discountAmount"
              :invalid="invalid"
              :min="0"
              :max="maxDiscount"
              :step="0.01"
              :placeholder="t('settings.productBundles.discountAmountPlaceholder')"
              data-testid="product-bundle-form-discount-amount"
            />
          </template>
        </UiField>
        <UiField
          v-if="discountType === BundleDiscountType.Fixed"
          :label="t('settings.productBundles.discountCurrency')"
          :error="fieldErrors.discountCurrency"
        >
          <UiSelect
            v-model="discountCurrency"
            :options="currencyOptions"
            :placeholder="t('settings.productBundles.discountCurrencyPlaceholder')"
            data-testid="product-bundle-form-discount-currency"
          />
        </UiField>
      </UiGrid>

      <UiStack v-if="bundle" :direction="UiDirection.Row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('shared.active') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('settings.productBundles.activeHint') }}</UiText>
        </UiStack>
        <UiToggle v-model="isActive" data-testid="product-bundle-form-active" />
      </UiStack>
    </UiStack>
    <template #footer>
      <UiButton :variant="UiButtonVariant.Secondary" data-testid="product-bundle-form-cancel" @click="emit('close')">
        {{ t('shared.cancel') }}
      </UiButton>
      <UiButton
        :variant="UiButtonVariant.Primary"
        :loading="saving"
        :disabled="!canSave"
        data-testid="product-bundle-form-save"
        @click="submit"
      >
        {{ t('shared.save') }}
      </UiButton>
    </template>
  </UiDialog>
</template>
