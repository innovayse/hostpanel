<template>
  <UiStack gap="none" class="bg-classic-page text-classic-ink" data-testid="classic-checkout">
    <!-- Page header: a flat band with a hairline, no backdrop -->
    <UiStack gap="none" class="border-b border-classic-border bg-classic-surface-2 py-10 md:py-14">
      <UiContainer>
        <UiStack gap="none">
          <UiStack direction="row" align="center" gap="xs" class="mb-4 inline-flex self-start rounded-md bg-classic-brand-soft px-3 py-1">
            <ShieldCheck :size="14" class="text-classic-brand" />
            <UiText as="span" size="xs" tone="inherit" weight="semibold" class="uppercase tracking-wider text-classic-brand">{{ $t('checkout.secureCheckout') }}</UiText>
          </UiStack>
          <UiHeading :level="UiHeadingLevel.H1" size="title" class="mb-3 !font-semibold !text-classic-ink">
            {{ $t('checkout.title') }}
          </UiHeading>
          <UiText tone="inherit" size="lg" class="max-w-[75ch] text-classic-muted">
            {{ $t('checkout.subtitle') }}
          </UiText>
        </UiStack>
      </UiContainer>
    </UiStack>

    <!-- Main content -->
    <UiSection :contained="false" padding="md" class="relative">
      <UiContainer>
        <!-- Empty cart state -->
        <UiCard
          v-if="cart.isEmpty"
          padding="none"
          tone="bare"
          class="rounded-lg border-classic-border bg-classic-surface py-8 shadow-sm"
          data-testid="checkout-empty"
        >
          <UiEmptyState :icon="ShoppingCart" :title="$t('cart.empty')" :description="$t('cart.emptyDesc')">
            <UiButton :to="localePath('/hosting')" variant="primary" size="lg" data-testid="checkout-browse">
              {{ $t('cart.browseHosting') }}
            </UiButton>
          </UiEmptyState>
        </UiCard>

        <!-- Checkout grid -->
        <UiGrid v-else cols="1" gap="xl" class="items-start lg:grid-cols-12" data-testid="checkout-grid">
          <!-- Left column: steps -->
          <UiStack gap="lg" class="lg:col-span-7">
            <!-- Account section -->
            <UiCard padding="none" tone="bare" class="rounded-lg border-classic-border bg-classic-surface p-6 shadow-sm md:p-8" data-testid="checkout-account">
              <UiStack direction="row" align="center" gap="md" class="mb-6">
                <UiStack direction="row" align="center" justify="center" gap="none" class="h-10 w-10 shrink-0 rounded-md bg-classic-brand-soft text-classic-brand">
                  <User :size="20" :stroke-width="2" />
                </UiStack>
                <UiStack gap="none">
                  <UiHeading :level="UiHeadingLevel.H3" size="subsection" class="!text-lg !font-semibold !text-classic-ink">{{ $t('checkout.account') }}</UiHeading>
                  <UiText size="sm" tone="inherit" class="text-classic-muted">{{ $t('checkout.identifyPrompt') }}</UiText>
                </UiStack>
              </UiStack>

              <!-- Signed-in state -->
              <UiStack
                v-if="isLoggedIn"
                direction="row"
                align="center"
                justify="between"
                gap="md"
                class="rounded-md border border-classic-border bg-classic-surface-2 p-4"
                data-testid="checkout-signed-in"
              >
                <UiStack direction="row" align="center" gap="md">
                  <UiStack direction="row" align="center" justify="center" gap="none" class="h-9 w-9 rounded-full bg-green-500/15 text-green-700 dark:text-green-400">
                    <CheckCircle :size="18" />
                  </UiStack>
                  <UiStack gap="none">
                    <UiText size="sm" weight="semibold">{{ $t('checkout.loggedInAs') }}</UiText>
                    <UiText size="sm" tone="success" data-testid="checkout-user-email">{{ userEmail }}</UiText>
                  </UiStack>
                </UiStack>
                <UiButton variant="ghost" size="sm" class="text-xs font-semibold" data-testid="checkout-not-you" @click="handleLogout">
                  {{ $t('checkout.notYou') }}
                </UiButton>
              </UiStack>

              <!-- Guest form -->
              <UiStack v-else gap="lg" data-testid="checkout-guest-form">
                <UiStack direction="row" align="center" gap="sm" class="rounded-md border border-classic-border bg-classic-surface-2 p-4">
                  <Info :size="16" class="shrink-0 text-classic-brand" />
                  <UiText size="sm" tone="inherit" class="text-classic-muted">
                    {{ $t('checkout.guestNotice') }}
                    <UiLink :to="localePath(CLIENT_LOGIN_PATH)" tone="inherit" class="ml-1 font-semibold text-classic-brand hover:underline" :underline="false" data-testid="checkout-sign-in">
                      {{ $t('checkout.signIn') }}
                    </UiLink>
                  </UiText>
                </UiStack>

                <UiGrid cols="2" gap="lg">
                  <UiInput
                    v-model="form.firstname"
                    :label="$t('checkout.fields.firstname')"
                    :placeholder="$t('checkout.placeholders.firstname')"
                    required
                    testid="checkout-firstname"
                  />
                  <UiInput
                    v-model="form.lastname"
                    :label="$t('checkout.fields.lastname')"
                    :placeholder="$t('checkout.placeholders.lastname')"
                    required
                    testid="checkout-lastname"
                  />
                </UiGrid>

                <UiInput
                  v-model="form.email"
                  type="email"
                  :label="$t('checkout.fields.email')"
                  :placeholder="$t('checkout.placeholders.email')"
                  required
                  testid="checkout-email"
                />

                <UiGrid cols="2" gap="lg">
                  <UiInput
                    v-model="form.password"
                    type="password"
                    :label="$t('checkout.fields.password')"
                    :placeholder="$t('checkout.placeholders.password')"
                    required
                    testid="checkout-password"
                  />
                  <UiPhoneInput
                    v-model="form.phonenumber"
                    :label="$t('checkout.fields.phonenumber')"
                    required
                  />
                </UiGrid>

                <UiSelect
                  :model-value="currencyStore.payerCode ?? undefined"
                  :label="$t('cart.currency.label')"
                  :placeholder="$t('cart.currency.placeholder')"
                  :options="currencyOptions"
                  testid="checkout-currency"
                  @update:model-value="value => changeGuestCurrency(String(value))"
                />
              </UiStack>
            </UiCard>

            <!-- Payment method section -->
            <UiCard padding="none" tone="bare" class="rounded-lg border-classic-border bg-classic-surface p-6 shadow-sm md:p-8" data-testid="checkout-payment">
              <UiStack direction="row" align="center" gap="md" class="mb-6">
                <UiStack direction="row" align="center" justify="center" gap="none" class="h-10 w-10 shrink-0 rounded-md bg-classic-brand-soft text-classic-brand">
                  <CreditCard :size="20" :stroke-width="2" />
                </UiStack>
                <UiStack gap="none">
                  <UiHeading :level="UiHeadingLevel.H3" size="subsection" class="!text-lg !font-semibold !text-classic-ink">{{ $t('checkout.paymentMethod') }}</UiHeading>
                  <UiText size="sm" tone="inherit" class="text-classic-muted">{{ $t('checkout.choosePayment') }}</UiText>
                </UiStack>
              </UiStack>

              <UiAlert v-if="!mayPlaceOrders" variant="warning" data-testid="checkout-permission-denied">
                {{ $t('checkout.permissionDenied') }}
              </UiAlert>

              <UiGrid v-if="methodsPending" cols="2" gap="sm" data-testid="checkout-methods-loading">
                <UiSkeleton v-for="i in 3" :key="i" shape="block" class="h-14 rounded-md" />
              </UiGrid>

              <UiText v-else-if="!paymentMethods.length" tone="subtle" class="rounded-md border border-dashed border-classic-border p-6 text-center italic" data-testid="checkout-no-methods">
                {{ $t('checkout.noPaymentMethods') }}
              </UiText>

              <UiGrid v-else cols="2" gap="md" data-testid="checkout-methods">
                <CheckoutGatewayOption
                  v-for="method in paymentMethods"
                  :key="method.module"
                  v-model="selectedMethod"
                  :method="method"
                  :disabled="!mayPlaceOrders"
                  name="payment-method"
                  :testid="`checkout-method-${method.module}`"
                  active-class="border-classic-brand bg-classic-brand-soft"
                  idle-class="border-classic-border bg-classic-surface hover:border-classic-brand/50"
                  accent-class="text-classic-brand"
                  muted-class="text-classic-muted"
                />
              </UiGrid>

              <!-- Card form -->
              <Transition
                enter-active-class="transition-all duration-300"
                enter-from-class="opacity-0 max-h-0"
                enter-to-class="opacity-100 max-h-40"
                leave-active-class="transition-all duration-300"
                leave-from-class="opacity-100 max-h-40"
                leave-to-class="opacity-0 max-h-0"
              >
                <UiStack v-if="mayPlaceOrders && selectedMethod === PaymentModule.Stripe" gap="none" class="mt-6 overflow-hidden" data-testid="checkout-card-form">
                  <CheckoutStripeCardForm ref="stripeCardFormRef" />
                  <UiCheckbox
                    v-model="saveCard"
                    :label="$t('checkout.saveCard')"
                    :description="$t('checkout.saveCardHint')"
                    class="mt-4"
                    testid="checkout-save-card"
                  />
                </UiStack>
              </Transition>
            </UiCard>
          </UiStack>

          <!-- Right column: summary. A light panel with a solid brand rule, readable in both modes. -->
          <UiStack as="aside" gap="lg" class="sticky top-24 lg:col-span-5">
            <UiCard
              padding="none"
              tone="bare"
              class="rounded-lg border-classic-border border-t-4 border-t-classic-brand bg-classic-surface p-6 shadow-md md:p-8"
              data-testid="checkout-summary"
            >
              <UiHeading :level="UiHeadingLevel.H3" size="subsection" class="mb-6 flex items-center gap-3 !text-lg !font-semibold !text-classic-ink">
                <Receipt :size="20" class="text-classic-brand" />
                {{ $t('checkout.yourOrder') }}
              </UiHeading>

              <UiAlert v-if="staleItems.length" variant="warning" class="mb-4" data-testid="checkout-stale">
                {{ $t('cart.staleCurrencyNotice') }}
              </UiAlert>

              <UiStack gap="sm" class="mb-6">
                <UiStack
                  v-for="item in cart.itemsInCurrency(currencyStore.payerCode)"
                  :key="item.pid"
                  direction="row"
                  align="start"
                  justify="between"
                  gap="md"
                  class="rounded-md border border-classic-border bg-classic-surface-2 p-3"
                  data-testid="checkout-item"
                >
                  <UiStack direction="row" align="center" gap="sm">
                    <UiStack direction="row" align="center" justify="center" gap="none" class="h-9 w-9 shrink-0 rounded-md bg-classic-brand-soft text-classic-brand">
                      <Server :size="18" />
                    </UiStack>
                    <UiStack gap="none">
                      <UiText size="sm" tone="inherit" weight="semibold" truncate class="w-32 md:w-full">{{ item.name }}</UiText>
                      <UiText size="xs" tone="inherit" class="mt-0.5 text-classic-muted">{{ item.cycleLabel }}</UiText>
                    </UiStack>
                  </UiStack>
                  <UiText size="sm" tone="inherit" weight="semibold" class="whitespace-nowrap">{{ itemPrice(item) }}</UiText>
                </UiStack>
              </UiStack>

              <!-- Total divider -->
              <UiDivider class="my-5" />

              <UiStack direction="row" align="center" justify="between" gap="none" class="mb-6">
                <UiText tone="inherit" weight="medium" class="text-classic-muted">{{ $t('cart.total') }}</UiText>
                <UiText as="span" tone="inherit" weight="bold" class="text-2xl text-classic-ink" data-testid="checkout-total">{{ totalLabel }}</UiText>
              </UiStack>

              <!-- Error box -->
              <UiAlert v-if="orderError" variant="error" class="mb-6 animate-shake" data-testid="checkout-error">
                {{ orderError }}
              </UiAlert>

              <!-- Submit. Disabled with no method selected: the gateway list came back
                   empty because every gateway is switched off in the admin. -->
              <UiButton
                variant="primary"
                size="md"
                full-width
                :loading="submitting"
                :disabled="submitting || cart.isEmpty || !selectedMethod || !mayPlaceOrders"
                class="!h-12 font-semibold"
                data-testid="checkout-submit"
                @click="submitOrder"
              >
                <Lock v-if="!submitting" :size="18" class="mr-2" />
                {{ submitting ? submittingLabel : $t('checkout.placeOrder') }}
              </UiButton>
              <CheckoutSubmitHint :message="submittingHint" />

              <UiText size="xs" tone="inherit" weight="medium" class="mt-5 flex items-center justify-center gap-2 text-classic-muted">
                <ShieldCheck :size="14" class="text-green-600 dark:text-green-400" />
                {{ $t('cart.secureCheckout') }}
              </UiText>
            </UiCard>

            <!-- Help card -->
            <UiCard padding="md" tone="bare" class="rounded-lg border-classic-border bg-classic-surface" data-testid="checkout-help">
              <UiStack direction="row" align="center" gap="md">
                <UiStack direction="row" align="center" justify="center" gap="none" class="h-10 w-10 rounded-md bg-classic-surface-2 text-classic-muted">
                  <HelpCircle :size="20" />
                </UiStack>
                <UiStack gap="none">
                  <UiText size="sm" weight="semibold">{{ $t('checkout.needHelp') }}</UiText>
                  <UiText size="xs" tone="inherit" class="text-classic-muted">{{ $t('checkout.supportAlways') }}</UiText>
                </UiStack>
                <UiLink :to="localePath('/contact')" :underline="false" class="ml-auto text-xs font-semibold" data-testid="checkout-contact">
                  {{ $t('checkout.contactUs') }}
                </UiLink>
              </UiStack>
            </UiCard>
          </UiStack>
        </UiGrid>
      </UiContainer>
    </UiSection>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * ClassicCheckout - the classic template's checkout: the account step (signed-in summary or
 * guest form), the payment-method step with the card form, and the order summary with the
 * submit button.
 *
 * Drawn with the classic tokens only: a flat header band, white bordered panels, one solid brand
 * colour for icons, the selected method and the submit button. No gradients, glows or blurs.
 *
 * The order flow lives in `stores/checkout.ts`, shared with the other checkout template; this
 * glue around it (guest form, card element reference, router) is `composables/useCheckoutPage.ts`,
 * shared with the other checkout template.
 */
import {
  CreditCard, ShoppingCart, Server, CheckCircle, User,
  Lock, ShieldCheck, Info, Receipt, HelpCircle
} from 'lucide-vue-next'
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { PaymentModule } from '~/types/paymentmodule'
import { UiHeadingLevel } from '~/utils/ui/enums'
import { useCheckoutPage } from '~/composables/useCheckoutPage'

/** State and actions of the checkout flow, shared with the other checkout template. */
const {
  localePath,
  cart,
  currencyStore,
  isLoggedIn,
  mayPlaceOrders,
  staleItems,
  totalLabel,
  submittingLabel,
  submittingHint,
  paymentMethods,
  selectedMethod,
  submitting,
  orderError,
  itemPrice,
  currencyOptions,
  changeGuestCurrency,
  methodsPending,
  userEmail,
  handleLogout,
  form,
  stripeCardFormRef,
  saveCard,
  submitOrder
} = await useCheckoutPage()
</script>
