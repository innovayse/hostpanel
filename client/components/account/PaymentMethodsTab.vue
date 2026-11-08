<template>
  <div data-testid="account-payment-tab">
    <!-- The API refuses every stored-method request of a member without the invoices permission, so
         the tab offers nothing and says why. -->
    <UiAlert v-if="!maySeePaymentMethods" variant="warning" data-testid="account-payment-denied">
      {{ $t('client.payment.permissionDenied') }}
    </UiAlert>

    <AccountStoredMethodsCard v-else />
  </div>
</template>

<script setup lang="ts">
/**
 * PaymentMethodsTab - the "Payment Methods" tab: the methods saved for later payments.
 *
 * The saved methods, their default and their removal live in the stored-methods card. A member of
 * the account without the permission to view and pay invoices sees the tab disabled with the reason.
 *
 * @module components/account/PaymentMethodsTab
 */

import { Permission } from '~/types/permission'
import { useClientStore } from '~/stores/client'

/** The client store, which knows what the signed-in person may do. */
const client = useClientStore()

/** Whether the person may see and change saved payment methods. */
const maySeePaymentMethods = computed(() => client.mayDo(Permission.ViewPayInvoices))
</script>
