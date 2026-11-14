<script setup lang="ts">
/**
 * The service filter fields of a list's filter dialog: status, product and billing cycle, each a `UiSelect` whose
 * first choice is "any". Shared by the admin service list and the services tab of a client's profile, so both offer
 * the same filters in the same words. The values are the draft the dialog edits (text, an empty string meaning
 * "any"); they reach the address only when the reader applies the dialog.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiField from '@/components/ui/UiField.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import UiStack from '@/components/ui/UiStack.vue'
import { BILLING_CYCLE_OPTIONS, SERVICE_STATUS_OPTIONS } from '@/lib/constants'
import type { Product } from '@/types/product'

const { t } = useI18n()

/** Props for ServiceFilterFields. */
const props = defineProps<{
  /** The products the product field offers. */
  products: readonly Product[]
  /** Prefix of the test ids; the fields are `<prefix>-status`, `<prefix>-product` and `<prefix>-billing-cycle`. */
  testidPrefix: string
}>()

/** The status being chosen, a service status or an empty string for any. */
const status = defineModel<string>('status', { required: true })

/** The product being chosen, a product id as text or an empty string for any. */
const productId = defineModel<string>('productId', { required: true })

/** The billing cycle being chosen, a billing cycle or an empty string for any. */
const billingCycle = defineModel<string>('billingCycle', { required: true })

/** Status choices; the empty value means any. */
const statusOptions = computed(() => [{ value: '', label: t('shared.any') }, ...SERVICE_STATUS_OPTIONS])

/** Product choices, one per product; the empty value means any. */
const productOptions = computed(() => [
  { value: '', label: t('shared.any') },
  ...props.products.map(product => ({ value: String(product.id), label: product.name })),
])

/** Billing cycle choices; the empty value means any. */
const billingCycleOptions = computed(() => [{ value: '', label: t('shared.any') }, ...BILLING_CYCLE_OPTIONS])
</script>

<template>
  <UiStack>
    <UiField :label="t('shared.status')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="status"
          :options="statusOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-status`"
        />
      </template>
    </UiField>
    <UiField :label="t('shared.productService')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="productId"
          :options="productOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-product`"
        />
      </template>
    </UiField>
    <UiField :label="t('shared.billingCycle')">
      <template #default="{ id }">
        <UiSelect
          :id="id"
          v-model="billingCycle"
          :options="billingCycleOptions"
          :placeholder="t('shared.any')"
          :data-testid="`${testidPrefix}-billing-cycle`"
        />
      </template>
    </UiField>
  </UiStack>
</template>
