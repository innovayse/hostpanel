<script setup lang="ts">
/**
 * Dialog for creating or editing a product.
 *
 * Shows the product type cards (create mode only), the group selector, the name, slug and package
 * fields, the server group, the description, a pricing grid with one row per configured currency,
 * and a hidden toggle. Emits `save` with the payload on submit, and `close` on cancel, Escape or a
 * backdrop click. A refused save (the `error` prop) shows next to the save button.
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import UiButton from '@/components/ui/UiButton.vue'
import UiCheckbox from '@/components/ui/UiCheckbox.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiField from '@/components/ui/UiField.vue'
import UiForm from '@/components/ui/UiForm.vue'
import UiGrid from '@/components/ui/UiGrid.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiInput from '@/components/ui/UiInput.vue'
import UiNumberInput from '@/components/ui/UiNumberInput.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTextarea from '@/components/ui/UiTextarea.vue'
import UiToggle from '@/components/ui/UiToggle.vue'
import { Icon } from '@/lib/icons'
import { useCurrenciesStore } from '@/stores/currencies'
import { useServersStore } from '@/stores/servers'
import type { CreateProductPayload, Product, ProductGroup, ProductPrice } from '@/types/product'
import { ProductStatus, ProductType } from '@/types/productenums'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Servers store: owns the server groups the product can be assigned to. */
const serversStore = useServersStore()

/** Currencies store: owns the configured currencies the pricing grid has a row for. */
const currenciesStore = useCurrenciesStore()

/** Props for ProductFormModal. */
const props = defineProps<{
  /** Product to edit, or null when creating a new product. */
  product: Product | null
  /** Available product groups for the group dropdown. */
  groups: ProductGroup[]
  /** True while the save request is in flight. */
  saving: boolean
  /** The API's sentence for the last refused save, shown next to the save button; null when none. */
  error?: string | null
}>()

const emit = defineEmits<{
  /** Emitted when the user submits a valid form. */
  save: [payload: CreateProductPayload]
  /** Emitted when the user closes or cancels the dialog. */
  close: []
}>()

/** One pricing row per configured currency, keyed by ISO 4217 code. */
interface PriceRow {
  /** Whether the product is sold in this currency. */
  enabled: boolean
  /** Monthly price; meaningless when {@link PriceRow.enabled} is false. */
  monthly: number
  /** Annual price; meaningless when {@link PriceRow.enabled} is false. */
  annual: number
}

/** Whether the dialog is in edit mode (a product was provided). */
const isEditMode = computed<boolean>(() => props.product !== null)

/** The product types offered as cards in create mode, with their icon and label. */
const productTypes = computed(() => [
  { value: ProductType.SharedHosting, label: t('settings.productForm.sharedHosting'), icon: Icon.Rack },
  { value: ProductType.Vps, label: t('settings.productForm.vps'), icon: Icon.Cloud },
  { value: ProductType.Dedicated, label: t('settings.productForm.dedicated'), icon: Icon.Dedicated },
  { value: ProductType.Email, label: t('settings.productForm.email'), icon: Icon.Mail },
  { value: ProductType.Addon, label: t('settings.productForm.addon'), icon: Icon.Puzzle },
  { value: ProductType.Backup, label: t('settings.productForm.backup'), icon: Icon.Archive },
  { value: ProductType.Digital, label: t('settings.productForm.digital'), icon: Icon.Download },
  { value: ProductType.Other, label: t('settings.productForm.other'), icon: Icon.Box },
])

/** Selected product type. */
const type = ref<string>(ProductType.SharedHosting)

/** Selected product group ID. */
const groupId = ref(0)

/** Product name input value. */
const name = ref('')

/** URL slug input value. */
const slug = ref('')

/** Hosting package name input value. */
const packageName = ref('')

/** Whether the user has manually edited the slug field. */
const slugManuallyEdited = ref(false)

/** Product description textarea value. */
const description = ref('')

/** Pricing grid rows, one per configured currency, keyed by ISO 4217 code. */
const priceRows = ref<Record<string, PriceRow>>({})

/** Selected server group ID, or null for no group. */
const serverGroupId = ref<number | null>(null)

/** Whether the product is hidden (maps to Inactive status). */
const isHidden = ref(false)

/** Whether the product is the group's recommended plan. */
const isFeatured = ref(false)

/** Position inside the group, lowest first. */
const sortOrder = ref(0)

/** Available server groups (owned by the servers store). */
const serverGroups = computed(() => serversStore.groups)

/**
 * Rebuilds the pricing rows from the configured currencies and, when editing, the product's
 * existing prices. The base currency's row is always enabled.
 *
 * @param existingPrices - The product's stored prices, or undefined when creating.
 */
const resetPriceRows = (existingPrices?: ProductPrice[]): void => {
  const rows: Record<string, PriceRow> = {}
  for (const currency of currenciesStore.enabledCurrencies) {
    const existing = existingPrices?.find((p) => p.currencyCode === currency.code)
    rows[currency.code] = {
      enabled: currency.isBase || existing !== undefined,
      monthly: existing?.monthly ?? 0,
      annual: existing?.annual ?? 0,
    }
  }
  priceRows.value = rows
}

/** Fetches the available server groups and the configured currencies on mount. */
onMounted(async () => {
  await serversStore.fetchGroups()
  if (currenciesStore.currencies.length === 0) {
    await currenciesStore.fetchAll()
  }
  resetPriceRows(props.product?.prices)
})

/**
 * Initializes or resets the fields based on the product prop: populated from the product when
 * editing, default and empty when creating.
 */
watch(() => props.product, (p) => {
  if (p) {
    type.value = p.type
    groupId.value = p.groupId
    name.value = p.name
    slug.value = p.slug ?? ''
    slugManuallyEdited.value = true
    packageName.value = p.packageName ?? ''
    description.value = p.description ?? ''
    serverGroupId.value = p.serverGroupId ?? null
    isHidden.value = p.status === ProductStatus.Inactive
    isFeatured.value = p.isFeatured
    sortOrder.value = p.sortOrder
    resetPriceRows(p.prices)
  } else {
    type.value = ProductType.SharedHosting
    groupId.value = props.groups[0]?.id ?? 0
    name.value = ''
    slug.value = ''
    slugManuallyEdited.value = false
    packageName.value = ''
    description.value = ''
    serverGroupId.value = null
    isHidden.value = false
    isFeatured.value = false
    sortOrder.value = 0
    resetPriceRows()
  }
}, { immediate: true })

/** Configured currencies with a pricing row, base first. */
const currencyRows = computed(() =>
  [...currenciesStore.enabledCurrencies].sort((a, b) => {
    if (a.isBase === b.isBase) {
      return 0
    }
    return a.isBase ? -1 : 1
  }),
)

/**
 * Returns the pricing row for a currency code, creating a blank one if {@link resetPriceRows} has
 * not run for it yet: every code in {@link currencyRows} is populated on mount and on every
 * product change, so this is a defensive default rather than a path the UI normally takes.
 *
 * @param code - ISO 4217 currency code.
 * @returns The row to bind the grid's inputs to.
 */
const rowFor = (code: string): PriceRow => {
  const existing = priceRows.value[code]
  if (existing) {
    return existing
  }
  const blank: PriceRow = { enabled: false, monthly: 0, annual: 0 }
  priceRows.value[code] = blank
  return blank
}

/** Options of the group dropdown. */
const groupOptions = computed(() => props.groups.map((g) => ({ value: g.id, label: g.name })))

/** Options of the server group dropdown, with a "None" option; 0 stands for no server group. */
const serverGroupOptions = computed(() => [
  { value: 0, label: t('settings.productForm.noneAutoSelectByModule') },
  ...serverGroups.value.map((g) => ({ value: g.id, label: g.name })),
])

/** Maps between the nullable server group id and the numeric model of the dropdown (0 = none). */
const serverGroupModel = computed<number>({
  /**
   * The dropdown's value for the current server group.
   *
   * @returns The group id, 0 when there is none.
   */
  get: () => serverGroupId.value ?? 0,
  /**
   * Stores the dropdown's pick.
   *
   * @param v - The chosen group id, 0 for none.
   */
  set: (v: number) => {
    serverGroupId.value = v === 0 ? null : v
  },
})

/**
 * Auto-generates the slug from the product name, until the user edits the slug by hand: lower
 * case, spaces to hyphens, everything else that is not a letter, digit or hyphen dropped.
 */
watch(name, (newName) => {
  if (!slugManuallyEdited.value) {
    slug.value = newName
      .toLowerCase()
      .replace(/\s+/g, '-')
      .replace(/[^a-z0-9-]/g, '')
  }
})

/**
 * Takes over the slug from the name once the user types in the slug field.
 *
 * @param value - The new slug text.
 */
const handleSlugInput = (value: string): void => {
  slug.value = value
  slugManuallyEdited.value = true
}

/**
 * Builds the `prices` payload from the pricing rows: enabled rows only, a blank cycle sent as
 * `null` rather than zero.
 *
 * @returns One entry per enabled currency.
 */
const buildPricesPayload = (): ProductPrice[] =>
  Object.entries(priceRows.value)
    .filter(([, row]) => row.enabled)
    .map(([currencyCode, row]) => ({
      currencyCode,
      monthly: row.monthly || null,
      annual: row.annual || null,
    }))

/** Submits the form by emitting the save event with the current field values. */
const handleSubmit = (): void => {
  emit('save', {
    groupId: groupId.value,
    name: name.value,
    description: description.value || null,
    website: null,
    slug: slug.value || null,
    packageName: packageName.value || null,
    type: type.value,
    prices: buildPricesPayload(),
    serverGroupId: serverGroupId.value || null,
    isFeatured: isFeatured.value,
    sortOrder: Math.max(0, Math.trunc(Number(sortOrder.value)) || 0),
  })
}
</script>

<template>
  <UiDialog
    :title="isEditMode ? t('settings.productForm.editProduct') : t('settings.productForm.createNewProduct')"
    size="lg"
    @close="emit('close')"
  >
    <UiForm stacked data-testid="product-form" @submit="handleSubmit">
      <!-- Product type (create mode only) -->
      <UiField v-if="!isEditMode" :label="t('settings.productForm.productType')">
        <UiGrid :columns="2" gap="sm">
          <UiButton
            v-for="pt in productTypes"
            :key="pt.value"
            type="button"
            :variant="type === pt.value ? 'primary' : 'secondary'"
            :aria-pressed="type === pt.value"
            :data-testid="`product-form-type-${pt.value}`"
            @click="type = pt.value"
          >
            <UiIcon :markup="pt.icon" />
            {{ pt.label }}
          </UiButton>
        </UiGrid>
      </UiField>

      <UiField :label="t('settings.productForm.productGroup')">
        <UiSelect
          v-model="groupId"
          :options="groupOptions"
          :placeholder="t('settings.productForm.selectAGroup')"
          data-testid="product-form-group"
        />
      </UiField>

      <UiField :label="t('shared.productName')">
        <template #default="{ id }">
          <UiInput
            :id="id"
            v-model="name"
            required
            type="text"
            :placeholder="t('settings.productForm.eGStarterHosting')"
            data-testid="product-form-name"
          />
        </template>
      </UiField>

      <UiField :label="t('settings.productForm.slugUrl')" :hint="t('settings.productForm.autoGeneratedFromNameEdit')">
        <template #default="{ id, describedby }">
          <UiInput
            :id="id"
            :model-value="slug"
            type="text"
            :aria-describedby="describedby"
            :placeholder="t('settings.productForm.eGStarterHosting2')"
            data-testid="product-form-slug"
            @update:model-value="handleSlugInput"
          />
        </template>
      </UiField>

      <UiField :label="t('settings.productForm.packageName')" :hint="t('settings.productForm.hostingPackageNameUsedFor')">
        <template #default="{ id, describedby }">
          <UiInput
            :id="id"
            v-model="packageName"
            type="text"
            :aria-describedby="describedby"
            :placeholder="t('settings.productForm.eGStarterProBusiness')"
            data-testid="product-form-package"
          />
        </template>
      </UiField>

      <UiField :label="t('settings.productForm.serverGroup')" :hint="t('settings.productForm.assignToAServerGroup')">
        <UiSelect
          v-model="serverGroupModel"
          :options="serverGroupOptions"
          :placeholder="t('settings.productForm.noneAutoSelectByModule')"
          data-testid="product-form-server-group"
        />
      </UiField>

      <UiField :label="t('shared.description')">
        <template #default="{ id }">
          <UiTextarea
            :id="id"
            v-model="description"
            rows="3"
            :placeholder="t('settings.productForm.briefProductDescription')"
            data-testid="product-form-description"
            class="resize-none"
          />
        </template>
      </UiField>

      <UiField :label="t('settings.productForm.sortOrder')" :hint="t('settings.productForm.sortOrderHint')">
        <UiNumberInput
          v-model="sortOrder"
          :min="0"
          :step="1"
          :placeholder="t('settings.productForm.sortOrderPlaceholder')"
          data-testid="product-form-sort-order"
        />
      </UiField>

      <!-- Pricing grid -->
      <UiField :label="t('settings.productForm.pricing')" :hint="t('settings.productForm.aBlankCycleLeavesThe')">
        <UiTable density="compact" class="rounded-xl border border-border" data-testid="product-form-prices">
          <UiTableHead>
            <UiTableRow class="border-b border-border">
              <UiTableHeader align="left">{{ t('shared.enabled') }}</UiTableHeader>
              <UiTableHeader align="left">{{ t('shared.currency') }}</UiTableHeader>
              <UiTableHeader align="left">{{ t('settings.productForm.monthly') }}</UiTableHeader>
              <UiTableHeader align="left">{{ t('settings.productForm.annual') }}</UiTableHeader>
            </UiTableRow>
          </UiTableHead>
          <UiTableBody divided>
            <UiTableRow v-for="currency in currencyRows" :key="currency.code" :data-testid="`product-form-price-${currency.code}`">
              <UiTableCell>
                <UiCheckbox
                  v-if="!currency.isBase"
                  :model-value="rowFor(currency.code).enabled"
                  :aria-label="currency.code"
                  @update:model-value="rowFor(currency.code).enabled = $event"
                />
                <UiText v-else tone="primary" size="xs">{{ t('settings.productForm.base') }}</UiText>
              </UiTableCell>
              <UiTableCell class="font-medium text-text-primary">{{ currency.code }}</UiTableCell>
              <UiTableCell>
                <UiNumberInput
                  v-model="rowFor(currency.code).monthly"
                  :min="0"
                  :step="0.01"
                  :disabled="!rowFor(currency.code).enabled"
                  placeholder="0.00"
                />
              </UiTableCell>
              <UiTableCell>
                <UiNumberInput
                  v-model="rowFor(currency.code).annual"
                  :min="0"
                  :step="0.01"
                  :disabled="!rowFor(currency.code).enabled"
                  placeholder="0.00"
                />
              </UiTableCell>
            </UiTableRow>
          </UiTableBody>
        </UiTable>
      </UiField>

      <!-- Recommended-plan toggle -->
      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('settings.productForm.featured') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('settings.productForm.featuredHint') }}</UiText>
        </UiStack>
        <UiToggle v-model="isFeatured" data-testid="product-form-featured" />
      </UiStack>

      <!-- Hidden toggle -->
      <UiStack direction="row" align="center" justify="between">
        <UiStack gap="xs">
          <UiText tone="primary" size="md">{{ t('shared.hidden') }}</UiText>
          <UiText tone="muted" size="sm">{{ t('settings.productForm.hideThisProductFromThe') }}</UiText>
        </UiStack>
        <UiToggle v-model="isHidden" data-testid="product-form-hidden" />
      </UiStack>

      <UiStatusMessage :message="error ?? null" :tone="UiTone.Danger" data-testid="product-form-error" />

      <UiStack direction="row" align="center" justify="end" gap="sm">
        <UiButton type="button" variant="secondary" data-testid="product-form-cancel" @click="emit('close')">
          {{ t('shared.cancel') }}
        </UiButton>
        <UiButton type="submit" variant="primary" :disabled="saving" data-testid="product-form-save">
          {{ saving ? t('shared.saving') : isEditMode ? t('shared.saveChanges') : t('settings.productForm.createProduct') }}
        </UiButton>
      </UiStack>
    </UiForm>
  </UiDialog>
</template>
