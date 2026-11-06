<template>
  <ClientCreditBalanceCard v-if="!store.error" :credit="store.credit" :loading="store.loading" compact data-testid="dashboard-credit" />
</template>

<script setup lang="ts">
/**
 * The dashboard's credit widget: the balance and the button to add funds, read when the card appears.
 *
 * A credit that cannot be read (an identity that is not a customer account, or a failed request) shows nothing at all: the
 * dashboard already explains those cases once, and a second error beside the first would only repeat it.
 */
import { onMounted } from 'vue'
import { useCreditStore } from '~/stores/credit'

/** The credit store: the balance shown in the widget. */
const store = useCreditStore()

onMounted(async () => {
  await store.load(1)
})
</script>
