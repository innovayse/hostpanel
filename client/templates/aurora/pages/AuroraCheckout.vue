<template>
  <UiStack gap="none" class="relative overflow-hidden font-aurora text-tx">
    <UiBackdrop variant="aurora" />

    <UiContainer class="relative">
      <UiStack as="section" gap="none" class="relative pb-5 pt-[clamp(36px,5vw,56px)]">
        <UiLink
          :to="localePath('/hosting')"
          tone="inherit"
          :underline="false"
          testid="checkout-back"
          class="self-start text-sm text-mut2 hover:text-ac1"
        >
          {{ $t('checkout.title') }}
        </UiLink>
        <UiHeading
          :level="1"
          size="bare"
          class="mt-3.5 font-display text-[clamp(28px,4.4vw,44px)] font-bold -tracking-[0.02em] text-tx"
          data-testid="checkout-title"
        >
          {{ $t('checkout.title') }}
        </UiHeading>
        <UiText size="inherit" tone="inherit" class="mt-3 max-w-[75ch] text-[17px] leading-relaxed text-mut">
          {{ $t('checkout.subtitle') }}
        </UiText>
        <UiStack direction="row" align="center" gap="xs" class="mt-3.5 text-sm text-mut">
          <ShieldCheck :size="16" class="text-ok" />
          <UiText as="span" size="sm" tone="inherit">{{ $t('cart.secureCheckout') }}</UiText>
        </UiStack>
      </UiStack>

      <UiStack as="section" gap="none" class="relative pb-[clamp(56px,8vw,84px)]">
        <!-- Empty cart -->
        <UiCard
          v-if="cart.isEmpty"
          tone="bare"
          padding="none"
          data-testid="checkout-empty"
          class="w-full border-line bg-surf py-20 text-center"
        >
          <UiStack align="center" gap="none">
            <UiTile
              size="lg"
              rounded
              :icon="ShoppingCart"
              class="mb-6 border border-line2 bg-acbg text-ac1"
            />
            <UiHeading :level="2" size="bare" class="mb-3 font-display text-2xl font-bold text-tx">{{ $t('cart.empty') }}</UiHeading>
            <UiText tone="inherit" class="mb-10 text-mut">{{ $t('cart.emptyDesc') }}</UiText>
            <UiButton
              variant="bare"
              :to="localePath('/hosting')"
              data-testid="checkout-browse-hosting"
              :class="[BRAND_BUTTON, 'px-7 py-3.5 text-base']"
            >
              {{ $t('cart.browseHosting') }}
            </UiButton>
          </UiStack>
        </UiCard>

        <UiSplit v-else ratio="start-wide" gap="lg" class="items-start">
          <template #start>
            <UiStack gap="lg">
              <!-- Account -->
              <UiCard tone="bare" padding="lg" data-testid="checkout-account" class="border-line bg-surf">
                <UiStack direction="row" align="center" gap="sm">
                  <UiTile size="lg" :icon="User" class="border border-line2 bg-acbg text-ac1" />
                  <UiHeading :level="3" size="bare" class="text-[17px] font-bold text-tx">{{ $t('checkout.account') }}</UiHeading>
                </UiStack>

                <UiCard
                  v-if="isLoggedIn"
                  tone="bare"
                  padding="md"
                  data-testid="checkout-logged-in"
                  class="mt-5 border-line bg-surf"
                >
                  <UiStack direction="row" wrap align="center" justify="between" gap="md">
                    <UiStack direction="row" align="center" gap="sm">
                      <UiTile size="lg" rounded :icon="CheckCircle" class="bg-acbg text-ok" />
                      <UiStack gap="none">
                        <UiText as="span" size="sm" tone="inherit" weight="semibold" class="text-tx">{{ $t('checkout.loggedInAs') }}</UiText>
                        <UiText as="span" size="sm" tone="inherit" class="text-ok">{{ userEmail }}</UiText>
                      </UiStack>
                    </UiStack>
                    <UiButton
                      variant="bare"
                      data-testid="checkout-not-you"
                      class="text-xs font-bold text-mut2 hover:text-tx"
                      @click="handleLogout"
                    >
                      {{ $t('checkout.notYou') }}
                    </UiButton>
                  </UiStack>
                </UiCard>

                <UiStack v-else gap="lg" class="mt-5">
                  <UiCard tone="bare" padding="md" class="border-line2 bg-acbg">
                    <UiStack direction="row" align="center" gap="sm">
                      <Info :size="16" class="shrink-0 text-ac1" />
                      <UiText size="sm" tone="inherit" class="text-mut">
                        {{ $t('checkout.guestNotice') }}
                        <UiLink
                          :to="localePath(CLIENT_LOGIN_PATH)"
                          tone="inherit"
                          :underline="false"
                          testid="checkout-sign-in"
                          class="ml-1 font-bold text-ac1 hover:underline"
                        >
                          {{ $t('checkout.signIn') }}
                        </UiLink>
                      </UiText>
                    </UiStack>
                  </UiCard>

                  <UiGrid cols="2" gap="md">
                    <UiInput
                      v-model="form.firstname"
                      testid="checkout-firstname"
                      :label="$t('checkout.fields.firstname')"
                      :placeholder="$t('checkout.placeholders.firstname')"
                      required
                    />
                    <UiInput
                      v-model="form.lastname"
                      testid="checkout-lastname"
                      :label="$t('checkout.fields.lastname')"
                      :placeholder="$t('checkout.placeholders.lastname')"
                      required
                    />
                  </UiGrid>

                  <UiInput
                    v-model="form.email"
                    type="email"
                    testid="checkout-email"
                    :label="$t('checkout.fields.email')"
                    :placeholder="$t('checkout.placeholders.email')"
                    required
                  />

                  <UiGrid cols="2" gap="md">
                    <UiInput
                      v-model="form.password"
                      type="password"
                      testid="checkout-password"
                      :label="$t('checkout.fields.password')"
                      :placeholder="$t('checkout.placeholders.password')"
                      required
                    />
                    <UiPhoneInput v-model="form.phonenumber" testid="checkout-phone" :label="$t('checkout.fields.phonenumber')" required />
                  </UiGrid>

                  <UiSelect
                    :model-value="currencyStore.payerCode ?? undefined"
                    testid="checkout-currency"
                    :label="$t('cart.currency.label')"
                    :placeholder="$t('cart.currency.placeholder')"
                    :options="currencyOptions"
                    @update:model-value="value => changeGuestCurrency(String(value))"
                  />
                </UiStack>
              </UiCard>

              <!-- Payment method -->
              <UiCard tone="bare" padding="lg" data-testid="checkout-payment" class="border-line bg-surf">
                <UiStack direction="row" align="center" gap="sm">
                  <UiTile size="lg" :icon="CreditCard" class="border border-line2 bg-acbg text-ac1" />
                  <UiHeading :level="3" size="bare" class="text-[17px] font-bold text-tx">{{ $t('checkout.paymentMethod') }}</UiHeading>
                </UiStack>

                <UiAlert v-if="!mayPlaceOrders" variant="warning" class="mt-5" data-testid="checkout-permission-denied">
                  {{ $t('checkout.permissionDenied') }}
                </UiAlert>

                <UiGrid v-if="methodsPending" cols="2" gap="sm" class="mt-5">
                  <UiSkeleton v-for="i in 3" :key="i" shape="block" class="h-14" />
                </UiGrid>

                <UiCard
                  v-else-if="!paymentMethods.length"
                  tone="bare"
                  padding="lg"
                  data-testid="checkout-no-methods"
                  class="mt-5 border-dashed border-line2 text-center"
                >
                  <UiText tone="inherit" class="text-mut2">{{ $t('checkout.noPaymentMethods') }}</UiText>
                </UiCard>

                <UiGrid v-else cols="2" gap="sm" class="mt-5" data-testid="checkout-methods">
                  <CheckoutGatewayOption
                    v-for="method in paymentMethods"
                    :key="method.module"
                    v-model="selectedMethod"
                    :method="method"
                    :disabled="!mayPlaceOrders"
                    name="aurora-payment-method"
                    :testid="`checkout-method-${method.module}`"
                    active-class="border-ac1 bg-acbg"
                    idle-class="border-line bg-surf hover:border-line2"
                    accent-class="text-ac1"
                    muted-class="text-mut2"
                  />
                </UiGrid>

                <UiStack v-if="mayPlaceOrders && selectedMethod === PaymentModule.Stripe" gap="none" class="mt-6 overflow-hidden">
                  <CheckoutStripeCardForm ref="stripeCardFormRef" />
                  <UiCheckbox
                    v-model="saveCard"
                    :label="$t('checkout.saveCard')"
                    :description="$t('checkout.saveCardHint')"
                    class="mt-4"
                    testid="checkout-save-card"
                  />
                </UiStack>
              </UiCard>
            </UiStack>
          </template>

          <template #end>
            <UiStack as="aside" gap="lg">
              <UiCard tone="bare" padding="lg" data-testid="checkout-summary" class="border-line2 bg-card shadow-panel">
                <UiStack direction="row" align="center" gap="sm" class="text-ac1">
                  <Receipt :size="20" />
                  <UiHeading :level="3" size="bare" class="text-[17px] font-bold text-tx">{{ $t('checkout.yourOrder') }}</UiHeading>
                </UiStack>

                <UiAlert v-if="staleItems.length" variant="warning" class="mt-5" data-testid="checkout-stale">
                  {{ $t('cart.staleCurrencyNotice') }}
                </UiAlert>

                <UiStack gap="sm" class="mt-5">
                  <UiCard
                    v-for="item in cart.itemsInCurrency(currencyStore.payerCode)"
                    :key="item.pid"
                    tone="bare"
                    padding="sm"
                    :data-testid="`checkout-item-${item.pid}`"
                    class="border-line bg-surf"
                  >
                    <UiStack direction="row" align="start" justify="between" gap="md">
                      <UiStack direction="row" align="center" gap="sm" class="min-w-0">
                        <UiTile size="lg" :icon="Server" class="bg-acbg text-ac1" />
                        <UiStack gap="none" class="min-w-0">
                          <UiText as="span" size="sm" tone="inherit" weight="bold" truncate class="text-tx">{{ item.name }}</UiText>
                          <UiText as="span" size="inherit" tone="inherit" weight="bold" class="mt-0.5 text-[10px] uppercase tracking-widest text-ac1">
                            {{ item.cycleLabel }}
                          </UiText>
                        </UiStack>
                      </UiStack>
                      <UiText as="span" size="sm" tone="inherit" weight="bold" class="whitespace-nowrap text-tx">{{ itemPrice(item) }}</UiText>
                    </UiStack>
                  </UiCard>
                </UiStack>

                <UiStack gap="none" aria-hidden="true" class="my-6 h-px bg-line" />

                <UiStack direction="row" align="center" justify="between" class="mb-6">
                  <UiText as="span" size="inherit" tone="inherit" weight="medium" class="text-mut">{{ $t('cart.total') }}</UiText>
                  <UiText
                    as="span"
                    size="inherit"
                    tone="inherit"
                    weight="bold"
                    class="text-3xl -tracking-[0.02em] text-tx"
                    data-testid="checkout-total"
                  >
                    {{ totalLabel }}
                  </UiText>
                </UiStack>

                <UiAlert v-if="orderError" variant="error" class="mb-5" data-testid="checkout-error">
                  {{ orderError }}
                </UiAlert>

                <!-- Disabled with no method selected: the gateway list came back empty because
                     every gateway is switched off in the admin. -->
                <UiButton
                  variant="primary"
                  size="md"
                  full-width
                  data-testid="checkout-place-order"
                  :loading="submitting"
                  :disabled="submitting || cart.isEmpty || !selectedMethod || !mayPlaceOrders"
                  class="h-14 rounded-xl font-bold"
                  @click="submitOrder"
                >
                  <Lock v-if="!submitting" :size="20" class="mr-2" />
                  {{ submitting ? submittingLabel : $t('checkout.placeOrder') }}
                </UiButton>
                <CheckoutSubmitHint :message="submittingHint" />

                <UiStack direction="row" align="center" justify="center" gap="xs" class="mt-5 text-xs text-mut2">
                  <ShieldCheck :size="14" class="text-ok" />
                  <UiText as="span" size="xs" tone="inherit">{{ $t('cart.secureCheckout') }}</UiText>
                </UiStack>
              </UiCard>

              <UiCard tone="bare" padding="md" data-testid="checkout-help" class="border-line bg-surf">
                <UiStack direction="row" align="center" gap="md">
                  <UiTile size="lg" rounded :icon="HelpCircle" class="border border-line2 text-mut" />
                  <UiStack gap="none">
                    <UiText as="span" size="sm" tone="inherit" weight="bold" class="text-tx">{{ $t('aurora.checkout.helpTitle') }}</UiText>
                    <UiText as="span" size="xs" tone="inherit" class="text-mut2">{{ $t('aurora.checkout.helpBody') }}</UiText>
                  </UiStack>
                  <UiLink
                    :to="localePath('/contact')"
                    tone="inherit"
                    :underline="false"
                    testid="checkout-help-cta"
                    class="ml-auto text-xs font-bold text-ac1 hover:text-ac2"
                  >
                    {{ $t('aurora.checkout.helpCta') }}
                  </UiLink>
                </UiStack>
              </UiCard>
            </UiStack>
          </template>
        </UiSplit>
      </UiStack>
    </UiContainer>
  </UiStack>
</template>

<script setup lang="ts">
/**
 * aurora checkout page.
 *
 * The order flow lives in `stores/checkout.ts`, shared with the other checkout template; this
 * glue around it (guest form, card element, router) is `composables/useCheckoutPage.ts`, shared
 * with the other checkout template. Every surface is composed from
 * the kit; the template's own palette comes from token classes on bare kit components.
 */
import {
  CreditCard, ShoppingCart, Server, CheckCircle, User,
  Lock, ShieldCheck, Info, Receipt, HelpCircle,
} from 'lucide-vue-next'
import { CLIENT_LOGIN_PATH } from '~/constants/clientRoutes'
import { BRAND_BUTTON } from '~/templates/aurora/buttons'
import { PaymentModule } from '~/types/paymentmodule'
import { useCheckoutPage } from '~/composables/useCheckoutPage'

/** Translator for the active locale. */
const { t: $t } = useI18n()

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
