<template>
  <UiStack v-if="lines.length" :gap="UiGap.Lg" data-testid="checkout-domain-fields">
    <UiStack v-for="line in lines" :key="`${line.domainAction}:${line.domain}`" :gap="UiGap.Sm">
      <UiText :size="UiTextSize.Sm" weight="semibold" data-testid="checkout-domain-fields-name">{{ line.domain }}</UiText>
      <DomainFieldsForm
        :fields="fieldsOf(line)"
        :model-value="line.extendedAttributes ?? {}"
        :errors="checkout.fieldErrors"
        :testid="`checkout-domain-fields-${line.domain}`"
        @update:model-value="edit(line, $event)"
      />
    </UiStack>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * CheckoutDomainFields: asks, for every domain registration and transfer in the cart, the extra values the registrar plugin
 * of its extension asks for, so every way a domain reaches the cart (search, transfer, a bundle) is covered in one place.
 * The values ride on the cart line in memory only (see `CartItem.extendedAttributes`) and are sent with the order, which the
 * API checks again before anything is created; the API's refusal of a field is shown next to it.
 */
import { computed, watch } from 'vue'
import { storeToRefs } from 'pinia'
import DomainFieldsForm from '~/components/domains/DomainFieldsForm.vue'
import { useAuthStore } from '~/stores/auth'
import { useCartStore } from '~/stores/cart'
import { useCheckoutStore } from '~/stores/checkout'
import { useDomainsStore } from '~/stores/domains'
import type { CartItem } from '~/types/cartitem'
import { CartItemType } from '~/types/cartitemtype'
import type { DomainField } from '~/types/domainfield'
import { DomainFieldOperation } from '~/types/domainfieldoperation'
import { DomainOrderAction } from '~/types/domainorderaction'
import { attributesFor, holderOf } from '~/utils/domainFields'
import { UiGap, UiTextSize } from '~/utils/ui/enums'

/** The cart whose domain lines are asked. */
const cart = useCartStore()
/** The checkout, whose refusal of a field is shown. */
const checkout = useCheckoutStore()
/** The domains store, which reads the fields. */
const domains = useDomainsStore()
/** The signed-in account, whose company decides the kind of registrant; a guest is a person. */
const { user } = storeToRefs(useAuthStore())

/** The fields of each line by `action:domain`, as last read. */
const fieldsByLine = reactive<Record<string, DomainField[]>>({})

/** A cart line that registers or transfers a domain: its name and its action are known. */
type AskedLine = CartItem & { domain: string, domainAction: DomainOrderAction }

/**
 * Tells whether a cart line is a registration or a transfer, the only lines the registry asks extra values for.
 *
 * @param item - The cart line.
 * @returns True for a domain line that registers or transfers.
 */
const isAskedLine = (item: CartItem): item is AskedLine =>
  item.itemType === CartItemType.Domain && !!item.domain &&
  (item.domainAction === DomainOrderAction.Register || item.domainAction === DomainOrderAction.Transfer)

/** The cart lines that register or transfer a domain. */
const lines = computed(() => cart.items.filter(isAskedLine))

/**
 * The key a line's fields are kept under.
 *
 * @param line - The cart line.
 * @returns \`action:domain\`.
 */
const keyOf = (line: CartItem): string => `${line.domainAction}:${line.domain}`

/**
 * Reads one line's fields.
 *
 * @param line - The cart line.
 * @returns The fields, empty until they are read.
 */
const fieldsOf = (line: CartItem): DomainField[] => fieldsByLine[keyOf(line)] ?? []

/**
 * Reads the fields of every asked line for the current kind of registrant and drops the values of a field that no longer applies.
 *
 * @returns Resolves when every line has been read.
 */
const load = async (): Promise<void> => {
  const holder = holderOf(user.value?.companyName)
  await Promise.all(lines.value.map(async (line) => {
    const operation = line.domainAction === DomainOrderAction.Transfer ? DomainFieldOperation.Transfer : DomainFieldOperation.Register
    const fields = await domains.loadDomainFields(line.domain, operation, holder)
    fieldsByLine[keyOf(line)] = fields
    cart.setExtendedAttributes(line.domain, line.domainAction, attributesFor(fields, line.extendedAttributes ?? {}))
  }))
}

/**
 * Stores an edit of one line's values on the cart line.
 *
 * @param line - The asked cart line.
 * @param values - The whole set of values after the edit.
 */
const edit = (line: AskedLine, values: Record<string, string>): void => {
  cart.setExtendedAttributes(line.domain, line.domainAction,attributesFor(fieldsOf(line), values))
}

// Read again when a domain joins or leaves the cart, or when the signed-in company (the kind of registrant) is known.
watch(() => [lines.value.map(keyOf).join('|'), user.value?.companyName], load, { immediate: true })
</script>
