<script setup lang="ts">
/**
 * Products settings view: list and manage all products/plans.
 *
 * Shows one page of the products in a table with create, edit and specification actions, behind a search box and a
 * filter dialog (group, type, status) with its removable chips. The search, the filters and the page live in the address
 * (`useListQuery`), so a reload, a shared link and the back button restore the same list, and the API applies them before
 * it pages. A refused load offers a retry; a refused save and a finished one are toasted
 * (the API's per-field messages stay under their inputs in the dialog), and the page the address names is read again.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import ProductFeaturesModal from '@/components/settings/ProductFeaturesModal.vue'
import ProductFormModal from '@/components/settings/ProductFormModal.vue'
import ProductGroupFormModal from '@/components/settings/ProductGroupFormModal.vue'
import ProductGroupsDialog from '@/components/settings/ProductGroupsDialog.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiField from '@/components/ui/UiField.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiListToolbar from '@/components/ui/UiListToolbar.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiDataTable from '@/components/ui/UiDataTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiSchemaTableHead from '@/components/ui/UiSchemaTableHead.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import schema from '@/views/ProductsView.schema'
import UiStatusBadge from '@/components/ui/UiStatusBadge.vue'
import UiText from '@/components/ui/UiText.vue'
import { useCurrenciesStore } from '@/stores/currencies'
import { formatAffixed, groupedAmount } from '@/lib/money'
import { Icon } from '@/lib/icons'
import { useSettingsStore } from '@/stores/settings'
import { PRODUCT_PRICE_CYCLES } from '@/types/product'
import type { CreateProductPayload, Product, ProductGroup, ProductGroupPayload, ProductListFilters } from '@/types/product'
import { ProductStatus, ProductType } from '@/types/productenums'
import { useListQuery } from '@/composables/useListQuery'
import { useOutcomeToast } from '@/composables/useOutcomeToast'
import { useDocsGuide } from '@/composables/useDocsGuide'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiSize } from '@/utils/ui/size'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { EMPTY_VALUE } from '@/lib/format'
import { schemaHeader } from '@/lib/schemaHeader'
import { UiActionKind } from '@/utils/ui/actions'
import { headerAction } from '@/utils/ui/actionBar'
import type { UiHeaderAction } from '@/utils/ui/actionBar'

const { t } = useI18n()

/** The header actions, each with its icon; on a phone only the primary one stays a button. */
const headerActions = computed<UiHeaderAction[]>(() => [
  headerAction(UiActionKind.Add, { label: t('settings.productForm.createProduct'), variant: UiButtonVariant.Primary, testid: 'products-create', onSelect: openCreate }),
  headerAction(UiActionKind.Manage, { label: t('settings.productGroups.manage'), variant: UiButtonVariant.Secondary, testid: 'products-groups', onSelect: openGroups }),
])

/** The filter fields the dialog edits; each is one query key and one `ProductListFilters` member. */
const FilterKey = {
  Group: 'group',
  Type: 'type',
  Status: 'status',
} as const

/** Settings store: owns the products and product groups. */
const store = useSettingsStore()

/** Currencies store: owns the base currency the price columns are written in. */
const currenciesStore = useCurrenciesStore()

/** The group, type and status filters, the search text and the page of the list, kept in the address. */
const { draft, search, appliedSearch, page, activeCount, apply, reset, cancel, remove, clearAll, setPage, narrowed, loadOnChange, enumOf, idOf, chipsOf }
  = useListQuery(Object.values(FilterKey))

/** Whether the product form dialog is visible. */
const showModal = ref(false)

/** Product being edited, or null for create mode. */
const editingProduct = ref<Product | null>(null)

/** Product whose specification is open, or null when the dialog is closed. */
const featuresProduct = ref<Product | null>(null)

/** True while saving a product. */
const saving = ref(false)

/** Whether the product groups dialog is visible. */
const showGroups = ref(false)

/** Whether the group form dialog is visible. */
const showGroupForm = ref(false)

/** Group being edited, or null for create mode. */
const editingGroup = ref<ProductGroup | null>(null)

/** True while saving a group. */
const savingGroup = ref(false)

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

/** Status display labels. */
const statusLabels = computed<Record<ProductStatus, string>>(() => ({
  [ProductStatus.Active]: t('shared.active'),
  [ProductStatus.Inactive]: t('shared.inactive'),
}))

/** Group filter options: "any" first, then every product group. */
const groupOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...store.productGroups.map((group) => ({ value: String(group.id), label: group.name })),
])

/** Type filter options: "any" first, then every product type. */
const typeOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(ProductType).map((type) => ({ value: type, label: typeLabels.value[type] ?? type })),
])

/** Status filter options: "any" first, then both statuses. */
const statusOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...Object.values(ProductStatus).map((status) => ({ value: status, label: statusLabels.value[status] })),
])

/** The group in the address as a group id, undefined when none is set or the value is not an id. */
const groupOfAddress = idOf(FilterKey.Group)

/** The type in the address as a product type, undefined when none is set or the value is not one. */
const typeOfAddress = enumOf(FilterKey.Type, ProductType)

/** The status in the address as a product status, undefined when none is set or the value is not one. */
const statusOfAddress = enumOf(FilterKey.Status, ProductStatus)

/** What the address narrows the list by, as the store sends it to the API. */
const filters = computed<ProductListFilters>(() => ({
  search: appliedSearch.value,
  groupId: groupOfAddress.value,
  type: typeOfAddress.value,
  status: statusOfAddress.value,
}))

/** One chip per applied filter, with the value as the operator reads it. */
const chips = chipsOf(() => [
  {
    key: FilterKey.Group,
    label: t('settings.products.group'),
    value: store.productGroups.find((group) => group.id === groupOfAddress.value)?.name,
  },
  {
    key: FilterKey.Type,
    label: t('shared.type'),
    value: typeOfAddress.value === undefined ? undefined : typeLabels.value[typeOfAddress.value],
  },
  {
    key: FilterKey.Status,
    label: t('shared.status'),
    value: statusOfAddress.value === undefined ? undefined : statusLabels.value[statusOfAddress.value],
  },
])

/**
 * Loads the page the address names with the filters and search it holds.
 *
 * @returns True when loaded; false when refused (see `store.error`).
 */
const load = (): Promise<boolean> => store.fetchProductsPage(page.value, filters.value)

/**
 * Loads the page, the groups (the filter and the group column read them) and (once) the currencies.
 *
 * @returns Resolves when every load has settled.
 */
const loadAll = async (): Promise<void> => {
  await Promise.all([
    load(),
    store.fetchProductGroups(),
    currenciesStore.currencies.length === 0 ? currenciesStore.fetchAll() : Promise.resolve(),
  ])
}

// Every change of a filter, the search or the page loads the list the address names.
loadOnChange(load)

// The page itself loads with the address; the groups and the currencies are read once.
onMounted(() => {
  void store.fetchProductGroups()
  if (currenciesStore.currencies.length === 0) {
    void currenciesStore.fetchAll()
  }
})

/** Whether the current page has any product to list. */
const hasRows = computed<boolean>(() => store.productPage.length > 0)

/** True while the first load is in flight and there are no rows yet; the toolbar and the table then keep their layout. */
const initialLoading = computed<boolean>(() => store.loading && !hasRows.value)

/** Reports the outcome of a save as a toast: the success line and the refusal both. */
const feedback = useOutcomeToast()
/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

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
const groupName = (groupId: number): string => store.productGroups.find((g) => g.id === groupId)?.name ?? EMPTY_VALUE

/**
 * Formats a price cell in the base currency. A product that does not sell a cycle in the base
 * currency has no figure for it, and the grid shows a dash rather than a number.
 *
 * @param amount - The price, or null when the product has none for that cycle.
 * @returns The formatted amount, or a dash placeholder when there is none.
 */
const formatPrice = (amount: number | null): string => {
  if (amount === null) {
    return EMPTY_VALUE
  }
  const currency = baseCurrency.value
  if (!currency) {
    return groupedAmount(amount)
  }
  return formatAffixed(amount, currency)
}

/** Opens the product groups dialog, reading the methods the checkout offers so a group can be restricted to them. */
const openGroups = (): void => {
  void store.fetchPaymentMethods()
  showGroups.value = true
}

/**
 * Opens the group form in create mode, or in edit mode for the given group.
 *
 * @param group - The group to edit, or null to create one.
 */
const openGroupForm = (group: ProductGroup | null): void => {
  store.fieldErrors = {}
  editingGroup.value = group
  showGroupForm.value = true
}

/**
 * Handles the save event of the group form: updates the edited group, or creates a new one, then reads the groups again.
 *
 * @param payload - The group data to save.
 */
const handleGroupSave = async (payload: ProductGroupPayload): Promise<void> => {
  savingGroup.value = true
  const saved = editingGroup.value
    ? await store.updateProductGroup(editingGroup.value.id, payload)
    : (await store.createProductGroup(payload)) !== null
  savingGroup.value = false
  if (saved) {
    showGroupForm.value = false
    feedback.show(t('shared.changesSavedSuccessfully'), false)
    await store.fetchProductGroups()
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/** Opens the dialog in create mode. */
const openCreate = (): void => {
  store.fieldErrors = {}
  editingProduct.value = null
  showModal.value = true
}

/**
 * Opens the dialog in edit mode for the given product.
 *
 * @param product - The product to edit.
 */
const openEdit = (product: Product): void => {
  store.fieldErrors = {}
  editingProduct.value = product
  showModal.value = true
}

/**
 * Handles the save event from the dialog: updates the edited product, or creates a new one, then reads the page again.
 *
 * @param payload - The product data to save.
 */
const handleSave = async (payload: CreateProductPayload): Promise<void> => {
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
      sku: payload.sku,
      metadata: payload.metadata,
      gracePeriodDays: payload.gracePeriodDays,
      isHidden: payload.isHidden,
      isDigitalService: payload.isDigitalService,
      maxActiveServices: payload.maxActiveServices,
      onePerClient: payload.onePerClient,
      trial: payload.trial,
    })
  } else {
    saved = (await store.createProduct(payload)) !== null
  }
  saving.value = false
  if (saved) {
    showModal.value = false
    feedback.show(t('shared.changesSavedSuccessfully'), false)
    await load()
  } else {
    feedback.show(store.actionError ?? t('errors.noAnswer'), true)
  }
}

/**
 * Row actions for a product.
 *
 * @param product - The product whose actions to build.
 * @returns The action list passed to UiRowActions.
 */
const productActions = (product: Product): UiMenuAction[] => {
  /**
   * Opens the specification editor for this product.
   * @returns Nothing.
   */
  const onSpec = (): void => { featuresProduct.value = product }
  /**
   * Opens the edit dialog for this product.
   * @returns Nothing.
   */
  const onEdit = (): void => openEdit(product)
  return [
    { label: t('settings.products.editSpecification'), icon: Icon.List, testid: `product-spec-${product.id}`, onSelect: onSpec },
    { label: t('settings.productForm.editProduct'), icon: Icon.Edit, testid: `product-edit-${product.id}`, onSelect: onEdit },
  ]
}
</script>

<template>
  <UiStack data-testid="products-view">
    <UiPageHeader :actions="headerActions" v-bind="schemaHeader(schema, t)">
      <template #actions>
        <PageHelpLink :url="guideUrl" />
        
      </template>
    </UiPageHeader>

    <UiListToolbar
      v-model:search="search"
      search-testid="products-search"
      :search-placeholder="t('settings.products.searchPlaceholder')"
      :active-count="activeCount"
      :chips="chips"
      :loading="initialLoading"
      @apply="apply"
      @reset="reset"
      @cancel="cancel"
      @remove="remove"
      @clear="clearAll"
    >
      <UiStack>
        <UiField :label="t('settings.products.group')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.group"
              :options="groupOptions"
              :placeholder="t('shared.any')"
              data-testid="products-filter-group"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.type')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.type"
              :options="typeOptions"
              :placeholder="t('shared.any')"
              data-testid="products-filter-type"
            />
          </template>
        </UiField>
        <UiField :label="t('shared.status')">
          <template #default="{ id }">
            <UiSelect
              :id="id"
              v-model="draft.status"
              :options="statusOptions"
              :placeholder="t('shared.any')"
              data-testid="products-filter-status"
            />
          </template>
        </UiField>
      </UiStack>
    </UiListToolbar>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="products-error"
      @retry="loadAll"
    />

    <UiDataTable
      v-else
      :loading="initialLoading"
      :empty="!store.loading && !hasRows"
      :page="page"
      :total="store.productTotalCount"
      :page-size="store.productPageSize"
      pagination-testid="products-pagination"
      data-testid="products-table"
      @update:page="setPage"
    >
      <UiSchemaTableHead :columns="schema.table.columns" />
      <UiTableBody>
        <UiTableRow
          v-for="product in store.productPage"
          :key="product.id"
          hoverable
          :data-testid="`product-row-${product.id}`"
        >
          <UiTableCell>
            <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Lg" truncate class="font-medium">{{ product.name }}</UiText>
            <UiText v-if="product.slug" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" truncate>/{{ product.slug }}</UiText>
          </UiTableCell>
          <UiTableCell muted>{{ groupName(product.groupId) }}</UiTableCell>
          <UiTableCell muted>{{ typeLabels[product.type] ?? product.type }}</UiTableCell>
          <UiTableCell v-for="(cycle, position) in PRODUCT_PRICE_CYCLES" :key="cycle" numeric>
            {{ formatPrice(product.pricing[cycle]) }}
            <UiText v-if="position === 0 && otherCurrencyCodes(product).length > 0" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="ml-1 inline font-sans">
              +{{ otherCurrencyCodes(product).join(', ') }}
            </UiText>
          </UiTableCell>
          <UiTableCell>
            <UiStatusBadge
              :state="product.status === ProductStatus.Active"
              :on-label="t('shared.active')"
              :off-label="t('shared.inactive')"
            />
          </UiTableCell>
          <UiTableCell actions>
            <UiRowActions :actions="productActions(product)" :testid="`product-actions-${product.id}`" />
          </UiTableCell>
        </UiTableRow>
      </UiTableBody>
      <template #empty>
        <UiEmptyState
          :title="narrowed ? t('settings.products.noProductsMatchYourFilters') : t('settings.products.noProductsFoundCreateYour')"
          data-testid="products-empty"
        >
          <template #icon>
            <UiIcon :markup="Icon.Box" :size="UiSize.Lg" />
          </template>
          <template v-if="activeCount > 0" #actions>
            <UiButton :variant="UiButtonVariant.Link" data-testid="products-clear-empty" @click="clearAll">
              {{ t('shared.clearFilters') }}
            </UiButton>
          </template>
        </UiEmptyState>
      </template>
    </UiDataTable>

    <!-- Product form dialog -->
    <ProductFormModal
      v-if="showModal"
      :product="editingProduct"
      :groups="store.productGroups"
      :saving="saving"
      :field-errors="store.fieldErrors"
      @save="handleSave"
      @clear-field-error="store.clearFieldError"
      @close="showModal = false"
    />

    <!-- Product groups: the list, then the form of one group -->
    <ProductGroupsDialog
      v-if="showGroups"
      :groups="store.productGroups"
      :methods="store.paymentMethods"
      @add="openGroupForm(null)"
      @edit="openGroupForm"
      @close="showGroups = false"
    />
    <ProductGroupFormModal
      v-if="showGroupForm"
      :group="editingGroup"
      :methods="store.paymentMethods"
      :saving="savingGroup"
      @save="handleGroupSave"
      @close="showGroupForm = false"
    />

    <!-- Specification editor -->
    <ProductFeaturesModal
      v-if="featuresProduct"
      :product="featuresProduct"
      @close="featuresProduct = null"
    />
  </UiStack>
</template>
