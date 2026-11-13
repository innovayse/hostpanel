<script setup lang="ts">
/**
 * Product bundles screen: the offers that sell products together, each with an optional domain
 * registration and a discount. Lists the bundles; creates and edits through a dialog and deletes
 * after a confirmation. A refused load offers a retry; a refused delete shows the API's sentence
 * above the table.
 */
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import PageHelpLink from '@/components/layout/PageHelpLink.vue'
import ProductBundleFormDialog from '@/components/settings/ProductBundleFormDialog.vue'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiDialog from '@/components/ui/UiDialog.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiErrorState from '@/components/ui/UiErrorState.vue'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import type { UiMenuAction } from '@/utils/ui/menu'
import UiStack from '@/components/ui/UiStack.vue'
import UiTable from '@/components/ui/UiTable.vue'
import UiTableBody from '@/components/ui/UiTableBody.vue'
import UiTableCell from '@/components/ui/UiTableCell.vue'
import UiTableHead from '@/components/ui/UiTableHead.vue'
import UiTableHeader from '@/components/ui/UiTableHeader.vue'
import UiTableRow from '@/components/ui/UiTableRow.vue'
import UiText from '@/components/ui/UiText.vue'
import { useDocsGuide } from '@/composables/useDocsGuide'
import { useCurrenciesStore } from '@/stores/currencies'
import { useProductBundlesStore } from '@/stores/productBundles'
import { useSettingsStore } from '@/stores/settings'
import type { ProductBundle, ProductBundlePayload } from '@/types/productbundle'
import { BundleDiscountType } from '@/types/productenums'
import { UiButtonVariant } from '@/utils/ui/button'
import { UiTableDensity } from '@/utils/ui/injection'
import { UiTone } from '@/utils/ui/tone'

const { t } = useI18n()

/** Product bundles store, the only caller of the bundle endpoints. */
const store = useProductBundlesStore()

/** Settings store: owns the product list the component pickers offer. */
const settingsStore = useSettingsStore()

/** Currencies store: owns the currency list a fixed discount is picked from. */
const currenciesStore = useCurrenciesStore()

/** Link to the docs guide for this screen, or null when docsUrl is not configured. */
const { guideUrl } = useDocsGuide()

/** Whether the create/edit dialog is open. */
const formOpen = ref(false)

/** The bundle the dialog edits; null while it creates a new one. */
const editing = ref<ProductBundle | null>(null)

/** The bundle waiting for the delete confirmation, null when no prompt is open. */
const deleteTarget = ref<ProductBundle | null>(null)

/** True while the confirmed delete is running. */
const deleting = ref(false)

/** The question shown for the staged bundle. */
const deleteMessage = computed<string>(() =>
  t('settings.productBundles.deleteMessage', { name: deleteTarget.value?.name ?? '' }),
)

/**
 * Loads the bundles and what the form's pickers need: the products and, once, the currencies.
 *
 * @returns Promise that resolves when every load has settled.
 */
const loadAll = async (): Promise<void> => {
  await Promise.all([
    store.fetchAll(),
    settingsStore.fetchProducts(),
    currenciesStore.currencies.length === 0 ? currenciesStore.fetchAll() : Promise.resolve(),
  ])
}

/**
 * Words the domain a bundle includes.
 *
 * @param bundle - The bundle to describe.
 * @returns The registration period, or a dash when the bundle includes no domain.
 */
const domainLabel = (bundle: ProductBundle): string =>
  bundle.includesDomain && bundle.domainYears !== null
    ? t('settings.productBundles.years', { count: bundle.domainYears }, bundle.domainYears)
    : '—'

/**
 * Words the discount of a bundle.
 *
 * @param bundle - The bundle to describe.
 * @returns The percentage, the fixed sum with its currency, or a dash when there is no discount.
 */
const discountLabel = (bundle: ProductBundle): string => {
  switch (bundle.discountType) {
    case BundleDiscountType.Percentage:
      return t('settings.productBundles.discountPercentage', { amount: bundle.discountAmount })
    case BundleDiscountType.Fixed:
      return t('settings.productBundles.discountFixed', {
        amount: bundle.discountAmount,
        currency: bundle.discountCurrency ?? '',
      })
    default:
      return '—'
  }
}

/**
 * Lists the products of a bundle by name, the primary one first as the API orders them.
 *
 * @param bundle - The bundle to describe.
 * @returns The product names, comma separated.
 */
const componentsLabel = (bundle: ProductBundle): string => bundle.components.map(component => component.name).join(', ')

/**
 * Opens the dialog empty, to create a bundle.
 *
 * @returns Nothing.
 */
const openCreate = (): void => {
  store.resetSaveError()
  editing.value = null
  formOpen.value = true
}

/**
 * Opens the dialog filled with a bundle, to edit it.
 *
 * @param bundle - The bundle to edit.
 * @returns Nothing.
 */
const openEdit = (bundle: ProductBundle): void => {
  store.resetSaveError()
  editing.value = bundle
  formOpen.value = true
}

/**
 * Saves the dialog's payload as a new bundle or as the edited one; the dialog closes on success
 * and stays open with the refusal on failure.
 *
 * @param payload - The finished form.
 * @returns Promise that resolves when the save has finished.
 */
const save = async (payload: ProductBundlePayload): Promise<void> => {
  const saved = editing.value
    ? await store.update(editing.value.id, payload)
    : await store.create(payload)
  if (saved) {
    formOpen.value = false
  }
}

/**
 * Deletes the staged bundle and closes the prompt.
 *
 * @returns Promise that resolves when the delete has finished.
 */
const confirmDelete = async (): Promise<void> => {
  if (!deleteTarget.value) {
    return
  }
  deleting.value = true
  await store.remove(deleteTarget.value.id)
  deleting.value = false
  deleteTarget.value = null
}

/**
 * Stages a bundle for the delete confirmation.
 *
 * @param bundle - The bundle to delete once confirmed.
 * @returns Nothing.
 */
const stageDelete = (bundle: ProductBundle): void => {
  store.clearActionError()
  deleteTarget.value = bundle
}

/**
 * The actions of one row's menu: edit and delete.
 *
 * @param bundle - The row's bundle.
 * @returns The menu actions.
 */
const rowActions = (bundle: ProductBundle): UiMenuAction[] => [
  {
    label: t('shared.edit'),
    /**
     * Opens the edit dialog for this row's bundle.
     *
     * @returns Nothing the menu reads.
     */
    onSelect: () => openEdit(bundle),
    testid: `product-bundle-edit-${bundle.id}`,
  },
  {
    label: t('shared.delete'),
    danger: true,
    /**
     * Stages this row's bundle for the delete confirmation.
     *
     * @returns Nothing the menu reads.
     */
    onSelect: () => stageDelete(bundle),
    testid: `product-bundle-delete-${bundle.id}`,
  },
]

onMounted(async () => {
  // A refusal of another visit must not show on this list.
  store.clearActionError()
  await loadAll()
})
</script>

<template>
  <UiStack>
    <UiPageHeader :title="t('settings.productBundles.title')" :subtitle="t('settings.productBundles.subtitle')">
      <template #actions>
        <PageHelpLink :url="guideUrl" />
        <UiButton :variant="UiButtonVariant.Primary" data-testid="product-bundles-add" @click="openCreate">
          {{ t('settings.productBundles.add') }}
        </UiButton>
      </template>
    </UiPageHeader>

    <UiAlert v-if="store.actionError" :variant="UiTone.Danger" data-testid="product-bundles-action-error">
      {{ store.actionError }}
    </UiAlert>

    <UiErrorState
      v-if="store.error"
      :message="store.error"
      :loading="store.loading"
      testid="product-bundles-error"
      @retry="loadAll"
    />

    <UiCard v-else-if="store.loading || store.bundles.length > 0">
      <UiTable :density="UiTableDensity.Comfortable" :loading="store.loading" data-testid="product-bundles-table">
        <UiTableHead>
          <UiTableRow>
            <UiTableHeader>{{ t('shared.name') }}</UiTableHeader>
            <UiTableHeader>{{ t('settings.productBundles.components') }}</UiTableHeader>
            <UiTableHeader>{{ t('settings.productBundles.domain') }}</UiTableHeader>
            <UiTableHeader>{{ t('settings.productBundles.discount') }}</UiTableHeader>
            <UiTableHeader>{{ t('shared.status') }}</UiTableHeader>
            <UiTableHeader actions>{{ t('shared.actions') }}</UiTableHeader>
          </UiTableRow>
        </UiTableHead>
        <UiTableBody divided>
          <UiTableRow v-for="bundle in store.bundles" :key="bundle.id" data-testid="product-bundle-row">
            <UiTableCell>
              <UiText tone="primary" size="lg" class="font-medium">{{ bundle.name }}</UiText>
              <UiText v-if="bundle.description" tone="muted" size="xs">{{ bundle.description }}</UiText>
            </UiTableCell>
            <UiTableCell muted>{{ componentsLabel(bundle) }}</UiTableCell>
            <UiTableCell muted>{{ domainLabel(bundle) }}</UiTableCell>
            <UiTableCell>{{ discountLabel(bundle) }}</UiTableCell>
            <UiTableCell>
              <UiBadge :tone="bundle.isActive ? UiTone.Success : UiTone.Neutral" small>
                {{ bundle.isActive ? t('shared.active') : t('shared.inactive') }}
              </UiBadge>
            </UiTableCell>
            <UiTableCell actions>
              <UiRowActions :actions="rowActions(bundle)" :testid="`product-bundle-actions-${bundle.id}`" />
            </UiTableCell>
          </UiTableRow>
        </UiTableBody>
      </UiTable>
    </UiCard>

    <UiCard v-else>
      <UiEmptyState :title="t('settings.productBundles.empty')" />
    </UiCard>

    <ProductBundleFormDialog
      :open="formOpen"
      :bundle="editing"
      :products="settingsStore.products"
      :currencies="currenciesStore.currencies"
      :saving="store.saving"
      :error="store.saveError"
      :field-errors="store.fieldErrors"
      @save="save"
      @close="formOpen = false"
    />

    <UiDialog
      v-if="deleteTarget"
      variant="confirm"
      :title="t('settings.productBundles.deleteTitle')"
      :message="deleteMessage"
      :confirm-label="t('shared.delete')"
      :loading="deleting"
      :tone="UiTone.Danger"
      data-testid="product-bundle-delete-confirm"
      @confirm="confirmDelete"
      @close="deleteTarget = null"
    />
  </UiStack>
</template>
