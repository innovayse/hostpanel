<script setup lang="ts">
/**
 * Products settings view: list and manage all products/plans.
 *
 * Shows the products in a table with create, edit and specification actions; fetches the products,
 * the groups and (when needed) the currencies on mount. A refused load offers a retry; a refused save
 * shows inside the product dialog next to its save button, a finished one above the table.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ProductFeaturesModal from '@/components/settings/ProductFeaturesModal.vue'
import ProductFormModal from '@/components/settings/ProductFormModal.vue'
import SettingsLoading from '@/components/settings/SettingsLoading.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiStatusMessage from '@/components/ui/UiStatusMessage.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiIconButton from '@/components/ui/UiIconButton.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'
import { useCurrenciesStore } from '@/stores/currencies'
import { formatAffixed } from '@/lib/money'
import { useSettingsStore } from '@/stores/settings'
import type { CreateProductPayload, Product } from '@/types/product'
import { ProductStatus, ProductType } from '@/types/productenums'
import { useResultBanner } from '@/composables/useResultBanner'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Settings store: owns the products and product groups. */
const store = useSettingsStore()

/** Currencies store: owns the base currency the price columns are written in. */
const currenciesStore = useCurrenciesStore()

/** Whether the product form dialog is visible. */
const showModal = ref(false)

/** Product being edited, or null for create mode. */
const editingProduct = ref<Product | null>(null)

/** Product whose specification is open, or null when the dialog is closed. */
const featuresProduct = ref<Product | null>(null)

/** True while saving a product. */
const saving = ref(false)

/** The base currency, used to format the grid's Monthly/Annual columns. */
const baseCurrency = computed(() => currenciesStore.currencies.find((c) => c.isBase))

/** Product type display labels. */
const typeLabels = computed<Record<string, string>>(() => ({
  [ProductType.SharedHosting]: t('settings.products.typeSharedHosting'),
  [ProductType.Vps]: t('settings.products.typeVps'),
  [ProductType.Dedicated]: t('settings.products.typeDedicated'),
  [ProductType.Domain]: t('settings.products.typeDomain'),
  [ProductType.Ssl]: t('settings.products.typeSsl'),
  [ProductType.Email]: t('settings.products.typeEmail'),
  [ProductType.Addon]: t('settings.products.typeAddon'),
  [ProductType.Backup]: t('settings.products.typeBackup'),
  [ProductType.Digital]: t('settings.products.typeDigital'),
  [ProductType.Other]: t('settings.products.typeOther'),
}))

/**
 * Loads the products, their groups and (once) the currencies.
 *
 * @returns Resolves when every load has settled.
 */
const loadAll = async (): Promise<void> => {
  await Promise.all([
    store.fetchProducts(),
    store.fetchProductGroups(),
    currenciesStore.currencies.length === 0 ? currenciesStore.fetchAll() : Promise.resolve(),
  ])
}

onMounted(loadAll)

/** The success line of the last save, shown above the table; a refusal shows inside the dialog. */
const feedback = useResultBanner()

/**
 * Lists the other currencies (besides the base) a product sells in, for the "+AMD, EUR" hint.
 *
 * @param product - The product to inspect.
 * @returns Currency codes the product has a price row for, excluding the base currency.
 */
const otherCurrencyCodes = (product: Product): string[] =>
  product.prices.map((p) => p.currencyCode).filter((code) => code !== baseCurrency.value?.code)

/**
 * Resolves the group name for a product.
 *
 * @param groupId - Product group ID.
 * @returns Group name, or a dash placeholder if not found.
 */
const groupName = (groupId: number): string => store.productGroups.find((g) => g.id === groupId)?.name ?? '—'

/**
 * Formats a price cell in the base currency. A product that does not sell a cycle in the base
 * currency has no figure for it, and the grid shows a dash rather than a number.
 *
 * @param amount - The price, or null when the product has none for that cycle.
 * @returns The formatted amount, or a dash placeholder when there is none.
 */
const formatPrice = (amount: number | null): string => {
  if (amount === null) {
    return '—'
  }
  const currency = baseCurrency.value
  if (!currency) {
    return amount.toFixed(2)
  }
  return formatAffixed(amount, currency)
}

/** Opens the dialog in create mode. */
const openCreate = (): void => {
  store.actionError = null
  editingProduct.value = null
  showModal.value = true
}

/**
 * Opens the dialog in edit mode for the given product.
 *
 * @param product - The product to edit.
 */
const openEdit = (product: Product): void => {
  store.actionError = null
  editingProduct.value = product
  showModal.value = true
}

/**
 * Handles the save event from the dialog: updates the edited product, or creates a new one.
 *
 * @param payload - The product data to save.
 */
const handleSave = async (payload: CreateProductPayload): Promise<void> => {
  feedback.clear()
  saving.value = true
  let saved: boolean
  if (editingProduct.value) {
    saved = await store.updateProduct(editingProduct.value.id, {
      name: payload.name,
      description: payload.description,
      website: payload.website,
      slug: payload.slug,
      packageName: payload.packageName,
      prices: payload.prices,
      serverGroupId: payload.serverGroupId,
      isFeatured: payload.isFeatured,
      sortOrder: payload.sortOrder,
    })
  } else {
    saved = (await store.createProduct(payload)) !== null
  }
  saving.value = false
  if (saved) {
    showModal.value = false
    feedback.show(t('shared.changesSavedSuccessfully'), false)
  }
}
</script>

<template>
  <UiStack gap="none" data-testid="products-view">
    <UiStatusMessage :message="feedback.message.value" :tone="UiTone.Success" class="mb-4" data-testid="products-feedback" />

    <UiPageHeader :title="t('settings.products.products')" :subtitle="t('settings.products.manageHostingPlansAndServices')">
      <template #actions>
        <UiButton type="button" variant="primary" data-testid="products-create" @click="openCreate">
          {{ t('settings.products.createProduct') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <SettingsLoading v-if="store.loading" :message="t('settings.products.loadingProducts')" />

    <UiErrorState
      v-else-if="store.error"
      :message="store.error"
      testid="products-error"
      @retry="loadAll"
    />

    <UiCard v-else class="overflow-hidden">
      <UiTable density="comfortable" :empty="store.products.length === 0" :colspan="7">
        <template #empty>
          {{ t('settings.products.noProductsFoundCreateYour') }}
        </template>
        <UiTableHead>
          <UiTableRow class="border-b border-border">
            <UiTableHeader align="left">{{ t('shared.name') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.products.group') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('shared.type') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.products.monthly') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('settings.products.annual') }}</UiTableHeader>
            <UiTableHeader align="left">{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader align="right">{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow
            v-for="product in store.products"
            :key="product.id"
            hoverable
            :data-testid="`product-row-${product.id}`"
          >
            <UiTableCell>
              <UiText tone="primary" size="lg" class="font-medium">{{ product.name }}</UiText>
              <UiText v-if="product.slug" tone="muted" size="xs">/{{ product.slug }}</UiText>
            </UiTableCell>
            <UiTableCell muted>{{ groupName(product.groupId) }}</UiTableCell>
            <UiTableCell muted>{{ typeLabels[product.type] ?? product.type }}</UiTableCell>
            <UiTableCell class="font-mono text-text-primary">
              {{ formatPrice(product.pricing.monthly) }}
              <UiText v-if="otherCurrencyCodes(product).length > 0" tone="muted" size="xs" class="ml-1 inline font-sans">
                +{{ otherCurrencyCodes(product).join(', ') }}
              </UiText>
            </UiTableCell>
            <UiTableCell class="font-mono text-text-primary">{{ formatPrice(product.pricing.annual) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="product.status === ProductStatus.Active ? 'success' : 'neutral'" small>
                {{ product.status === ProductStatus.Active ? t('shared.active') : t('shared.inactive') }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell align="right">
              <UiIconButton
                :label="t('settings.products.editSpecification')"
                size="sm"
                :data-testid="`product-spec-${product.id}`"
                @click="featuresProduct = product"
              >
                <UiIcon :markup="Icon.List" size="sm" />
              </UiIconButton>
              <UiIconButton
                :label="t('settings.products.editProduct')"
                size="sm"
                :data-testid="`product-edit-${product.id}`"
                @click="openEdit(product)"
              >
                <UiIcon :markup="Icon.Edit" size="sm" />
              </UiIconButton>
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiCard>

    <!-- Product form dialog -->
    <ProductFormModal
      v-if="showModal"
      :product="editingProduct"
      :groups="store.productGroups"
      :saving="saving"
      :error="store.actionError"
      @save="handleSave"
      @close="showModal = false"
    />

    <!-- Specification editor -->
    <ProductFeaturesModal
      v-if="featuresProduct"
      :product="featuresProduct"
      @close="featuresProduct = null"
    />
  </UiStack>
</template>
