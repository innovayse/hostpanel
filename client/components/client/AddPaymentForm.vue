<template>
  <UiCard class="mb-4">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <h3 class="text-lg font-bold text-gray-900 dark:text-white">
        {{ $t('client.payment.addTitle') }}
      </h3>
      <button
        type="button"
        class="p-1.5 rounded-lg text-gray-400 hover:text-gray-700 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
        @click="$emit('cancel')"
      >
        <X :size="18" :stroke-width="2" />
      </button>
    </div>

    <form @submit.prevent="handleSubmit">
      <!-- ── Live Card Preview ──────────────────────────────────────── -->
      <div class="mb-8 flex justify-center">
        <div class="relative w-full max-w-sm h-48 rounded-2xl p-6 overflow-hidden select-none">
          <div class="absolute inset-0 bg-gradient-to-br from-cyan-500 via-cyan-600 to-blue-700 rounded-2xl" />
          <div class="absolute -top-10 -right-10 w-48 h-48 rounded-full bg-white/10" />
          <div class="absolute -bottom-12 -left-12 w-48 h-48 rounded-full bg-white/10" />
          <div class="relative z-10 h-full flex flex-col justify-between">
            <div class="flex items-center justify-between">
              <div class="w-10 h-7 rounded-md bg-gradient-to-br from-yellow-300 to-yellow-500 border border-yellow-400/50 flex items-center justify-center">
                <div class="w-6 h-4 rounded-sm border border-yellow-600/40 grid grid-cols-2 gap-0.5 p-0.5">
                  <div class="bg-yellow-600/30 rounded-sm" />
                  <div class="bg-yellow-600/30 rounded-sm" />
                  <div class="bg-yellow-600/30 rounded-sm" />
                  <div class="bg-yellow-600/30 rounded-sm" />
                </div>
              </div>
              <div class="text-white/90 font-bold text-sm tracking-wider">{{ cardBrand }}</div>
            </div>
            <div class="text-white font-mono text-xl tracking-[0.2em] font-medium">{{ maskedCardNumber }}</div>
            <div class="flex items-end justify-between">
              <div>
                <div class="text-white/50 text-xs uppercase tracking-widest mb-1">Cardholder</div>
                <div class="text-white font-semibold text-sm tracking-wide uppercase truncate max-w-[160px]">
                  {{ activeAddress.displayName || billingName || 'FULL NAME' }}
                </div>
              </div>
              <div class="text-right">
                <div class="text-white/50 text-xs uppercase tracking-widest mb-1">Expires</div>
                <div class="text-white font-semibold text-sm">{{ form.expiry || 'MM / YY' }}</div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- ── Payment Type ────────────────────────────────────────────── -->
      <div v-if="gateways.length" class="mb-6">
        <label class="block text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
          {{ $t('client.payment.typeLabel') }}
        </label>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="gw in gateways"
            :key="gw.value"
            type="button"
            class="flex items-center gap-2 px-4 py-2 rounded-xl border text-sm font-medium transition-all duration-150"
            :class="form.gatewayname === gw.value
              ? 'border-cyan-500 bg-cyan-500/10 text-cyan-600 dark:text-cyan-400'
              : 'border-gray-200 dark:border-white/10 text-gray-600 dark:text-gray-400 hover:border-gray-300 dark:hover:border-white/20 hover:text-gray-900 dark:hover:text-white'"
            @click="form.gatewayname = gw.value"
          >
            <span
              class="w-3.5 h-3.5 rounded-full border-2 flex items-center justify-center flex-shrink-0 transition-all"
              :class="form.gatewayname === gw.value ? 'border-cyan-500 bg-cyan-500' : 'border-gray-300 dark:border-white/30'"
            >
              <span v-if="form.gatewayname === gw.value" class="w-1.5 h-1.5 rounded-full bg-white" />
            </span>
            {{ gw.label }}
          </button>
        </div>
      </div>

      <!-- ── Card Number ─────────────────────────────────────────────── -->
      <div class="mb-4">
        <label class="block text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
          {{ $t('client.payment.cardNumber') }}
        </label>
        <div class="relative">
          <input
            v-model="form.cardNumber"
            type="text"
            inputmode="numeric"
            maxlength="19"
            :placeholder="$t('client.payment.cardNumberPlaceholder')"
            class="w-full pl-4 pr-12 py-3 rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 text-gray-900 dark:text-white text-base font-mono tracking-widest placeholder:tracking-normal placeholder:font-sans placeholder:text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 dark:focus:border-cyan-500 transition-colors"
            @input="formatCardNumber"
          />
          <CreditCard :size="18" :stroke-width="1.5" class="absolute right-4 top-1/2 -translate-y-1/2 text-gray-400 dark:text-gray-500" />
        </div>
      </div>

      <!-- ── Expiry + CVV ────────────────────────────────────────────── -->
      <div class="grid grid-cols-2 gap-4 mb-4">
        <div>
          <label class="block text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
            {{ $t('client.payment.expiryDate') }}
          </label>
          <input
            v-model="form.expiry"
            type="text"
            inputmode="numeric"
            maxlength="7"
            :placeholder="$t('client.payment.expiryPlaceholder')"
            class="w-full px-4 py-3 rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 text-gray-900 dark:text-white text-base font-mono tracking-wider placeholder:tracking-normal placeholder:font-sans placeholder:text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 dark:focus:border-cyan-500 transition-colors"
            @input="formatExpiry"
          />
        </div>
        <div>
          <label class="block text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
            {{ $t('client.payment.cvvLabel') }}
          </label>
          <div class="relative">
            <input
              v-model="form.cvv"
              :type="showCvv ? 'text' : 'password'"
              inputmode="numeric"
              maxlength="4"
              :placeholder="$t('client.payment.cvvPlaceholder')"
              class="w-full pl-4 pr-10 py-3 rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 text-gray-900 dark:text-white text-base font-mono tracking-widest placeholder:tracking-normal placeholder:font-sans placeholder:text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 dark:focus:border-cyan-500 transition-colors"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
              @click="showCvv = !showCvv"
            >
              <Eye v-if="!showCvv" :size="16" :stroke-width="1.5" />
              <EyeOff v-else :size="16" :stroke-width="1.5" />
            </button>
          </div>
        </div>
      </div>

      <!-- ── Description ────────────────────────────────────────────── -->
      <div class="mb-6">
        <label class="block text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-2">
          {{ $t('client.payment.descriptionLabel') }}
          <span class="normal-case font-normal text-gray-400 ml-1">{{ $t('client.payment.optional') }}</span>
        </label>
        <input
          v-model="form.description"
          type="text"
          placeholder="e.g. My personal card"
          class="w-full px-4 py-3 rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 dark:focus:border-cyan-500 transition-colors"
        />
      </div>

      <!-- ── Billing Address ─────────────────────────────────────────── -->
      <div class="mb-6">
        <label class="block text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
          {{ $t('client.payment.billingAddress') }}
        </label>

        <!-- Existing address card -->
        <button
          type="button"
          class="w-full flex items-center gap-3 p-4 rounded-xl border text-left transition-all duration-150"
          :class="!useNewAddress
            ? 'border-cyan-500 bg-cyan-500/5 dark:bg-cyan-500/10'
            : 'border-gray-200 dark:border-white/10 hover:border-gray-300 dark:hover:border-white/20'"
          @click="useNewAddress = false"
        >
          <span
            class="w-4 h-4 rounded-full border-2 flex items-center justify-center flex-shrink-0 transition-all"
            :class="!useNewAddress ? 'border-cyan-500 bg-cyan-500' : 'border-gray-300 dark:border-white/30'"
          >
            <span v-if="!useNewAddress" class="w-1.5 h-1.5 rounded-full bg-white" />
          </span>
          <div class="min-w-0 flex-1">
            <div class="text-sm font-semibold text-gray-900 dark:text-white">{{ billingName }}</div>
            <div v-if="billingAddress" class="text-xs text-gray-500 dark:text-gray-400 truncate">{{ billingAddress }}</div>
          </div>
        </button>

        <!-- New address card — shown after saving a new address -->
        <button
          v-if="useNewAddress"
          type="button"
          class="mt-2 w-full flex items-center gap-3 p-4 rounded-xl border border-cyan-500 bg-cyan-500/5 dark:bg-cyan-500/10 text-left transition-all duration-150"
          @click="openAddressModal"
        >
          <span class="w-4 h-4 rounded-full border-2 border-cyan-500 bg-cyan-500 flex items-center justify-center flex-shrink-0">
            <span class="w-1.5 h-1.5 rounded-full bg-white" />
          </span>
          <div class="min-w-0 flex-1">
            <div class="text-sm font-semibold text-gray-900 dark:text-white">{{ newAddr.firstname }} {{ newAddr.lastname }}</div>
            <div class="text-xs text-gray-500 dark:text-gray-400 truncate">
              {{ [newAddr.address1, newAddr.city, newAddr.country].filter(Boolean).join(', ') }}
            </div>
          </div>
          <span class="text-xs text-cyan-600 dark:text-cyan-400 font-medium flex-shrink-0">Edit</span>
        </button>

        <!-- "+ Add a new address" button -->
        <button
          type="button"
          class="mt-2 flex items-center gap-1.5 text-sm font-medium text-cyan-600 dark:text-cyan-400 hover:text-cyan-700 dark:hover:text-cyan-300 transition-colors py-1"
          @click="openAddressModal"
        >
          <Plus :size="15" :stroke-width="2.5" />
          {{ $t('client.payment.addNewAddress') }}
        </button>
      </div>

      <!-- ── Error ──────────────────────────────────────────────────── -->
      <UiAlert v-if="error" variant="error" class="mb-4">{{ error }}</UiAlert>

      <!-- ── Actions ────────────────────────────────────────────────── -->
      <div class="flex gap-3">
        <UiButton type="submit" :loading="loading" full-width>
          <CreditCard v-if="!loading" :size="15" :stroke-width="2" class="mr-1.5" />
          {{ loading ? $t('client.payment.adding') : $t('client.payment.saveChanges') }}
        </UiButton>
        <UiButton type="button" variant="outline" @click="$emit('cancel')">
          {{ $t('client.profile.cancel') }}
        </UiButton>
      </div>
    </form>
  </UiCard>

  <!-- ── Add New Billing Address Modal ─────────────────────────────── -->
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-200 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition-opacity duration-150 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="showAddressModal"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        @click.self="closeAddressModal"
      >
        <Transition
          enter-active-class="transition-all duration-200 ease-out"
          enter-from-class="opacity-0 scale-95 translate-y-2"
          enter-to-class="opacity-100 scale-100 translate-y-0"
          leave-active-class="transition-all duration-150 ease-in"
          leave-from-class="opacity-100 scale-100 translate-y-0"
          leave-to-class="opacity-0 scale-95 translate-y-2"
        >
          <div
            v-if="showAddressModal"
            class="w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-2xl bg-white dark:bg-[#13131a] border border-gray-200 dark:border-white/10 shadow-2xl"
          >
            <!-- Modal header -->
            <div class="flex items-center justify-between px-6 py-5 border-b border-gray-100 dark:border-white/10">
              <h2 class="text-xl font-bold text-gray-900 dark:text-white">
                {{ $t('client.payment.addNewAddress') }}
              </h2>
              <button
                type="button"
                class="p-1.5 rounded-lg text-gray-400 hover:text-gray-700 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
                @click="closeAddressModal"
              >
                <X :size="20" :stroke-width="2" />
              </button>
            </div>

            <!-- Modal body -->
            <div class="p-6">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-5">
                <!-- Left column -->
                <div class="space-y-5">
                  <!-- First Name -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.firstName') }}
                    </label>
                    <input
                      v-model="modalAddr.firstname"
                      type="text"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                  <!-- Last Name -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.lastName') }}
                    </label>
                    <input
                      v-model="modalAddr.lastname"
                      type="text"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                  <!-- Company Name -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.companyName') }}
                    </label>
                    <input
                      v-model="modalAddr.companyname"
                      type="text"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                  <!-- Phone Number -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.phone') }}
                    </label>
                    <input
                      v-model="modalAddr.phonenumber"
                      type="tel"
                      placeholder="201-555-0123"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                </div>

                <!-- Right column -->
                <div class="space-y-5">
                  <!-- Address 1 -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.address1') }}
                    </label>
                    <input
                      v-model="modalAddr.address1"
                      type="text"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                  <!-- Address 2 -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.address2') }}
                    </label>
                    <input
                      v-model="modalAddr.address2"
                      type="text"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                  <!-- City -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.city') }}
                    </label>
                    <input
                      v-model="modalAddr.city"
                      type="text"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                  <!-- State -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.state') }}
                    </label>
                    <input
                      v-model="modalAddr.state"
                      type="text"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                  <!-- Zip Code -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.postcode') }}
                    </label>
                    <input
                      v-model="modalAddr.postcode"
                      type="text"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                    />
                  </div>
                  <!-- Country -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
                      {{ $t('client.profile.country') }}
                    </label>
                    <select
                      v-model="modalAddr.country"
                      class="w-full px-3 py-2.5 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-[#13131a] text-gray-900 dark:text-white text-sm focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors appearance-none"
                    >
                      <option value="">— Select Country —</option>
                      <option v-for="c in countryOptions" :key="c.value" :value="c.value">{{ c.label }}</option>
                    </select>
                  </div>
                </div>
              </div>
            </div>

            <!-- Modal footer -->
            <div class="flex justify-end gap-3 px-6 py-4 border-t border-gray-100 dark:border-white/10">
              <UiButton type="button" variant="outline" @click="closeAddressModal">
                {{ $t('client.profile.cancel') }}
              </UiButton>
              <UiButton type="button" @click="confirmAddress">
                <Check :size="14" :stroke-width="2.5" class="mr-1.5" />
                {{ $t('client.profile.save') }}
              </UiButton>
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { CreditCard, Check, X, Eye, EyeOff, Plus } from 'lucide-vue-next'

interface Gateway { value: string; label: string }
interface CountryOption { value: string; label: string }

interface SubmitData {
  gatewayname: string
  cardNumber: string
  expiry: string
  cvv: string
  description: string
  useNewAddress: boolean
  newAddr: Record<string, string>
}

const props = defineProps<{
  gateways: Gateway[]
  billingName: string
  billingAddress: string
  countryOptions: CountryOption[]
  loading: boolean
  error: string
}>()

const emit = defineEmits<{
  submit: [data: SubmitData]
  cancel: []
}>()

const form = reactive({
  gatewayname: props.gateways[0]?.value ?? '',
  cardNumber: '',
  expiry: '',
  cvv: '',
  description: ''
})

const showCvv         = ref(false)
const useNewAddress   = ref(false)
const showAddressModal = ref(false)

const newAddr = reactive({
  firstname: '', lastname: '', companyname: '', phonenumber: '',
  address1: '', address2: '', city: '', state: '', postcode: '', country: ''
})

// Temp copy while modal is open — only committed on "Save"
const modalAddr = reactive({ ...newAddr })

// Pre-select first gateway when loaded asynchronously
watch(() => props.gateways, (gws) => {
  if (!form.gatewayname && gws.length) form.gatewayname = gws[0].value
})

// Card brand detection
const cardBrand = computed(() => {
  const n = form.cardNumber.replace(/\s/g, '')
  if (n.startsWith('4')) return 'VISA'
  if (/^5[1-5]/.test(n)) return 'MASTERCARD'
  if (/^3[47]/.test(n)) return 'AMEX'
  return 'CARD'
})

const maskedCardNumber = computed(() => {
  const raw = form.cardNumber.replace(/\s/g, '')
  if (!raw) return '**** **** **** ****'
  return raw.padEnd(16, '*').replace(/(.{4})/g, '$1 ').trim()
})

// Display name on card — shows new address name if using it
const activeAddress = computed(() => ({
  displayName: useNewAddress.value
    ? [newAddr.firstname, newAddr.lastname].filter(Boolean).join(' ')
    : ''
}))

function openAddressModal() {
  // Copy current newAddr into modalAddr for editing
  Object.assign(modalAddr, { ...newAddr })
  showAddressModal.value = true
}

function closeAddressModal() {
  showAddressModal.value = false
}

function confirmAddress() {
  // Commit modalAddr back to newAddr
  Object.assign(newAddr, { ...modalAddr })
  useNewAddress.value = true
  showAddressModal.value = false
}

function formatCardNumber(e: Event) {
  const raw = (e.target as HTMLInputElement).value.replace(/\D/g, '').slice(0, 16)
  form.cardNumber = raw.replace(/(.{4})/g, '$1 ').trim()
}

function formatExpiry(e: Event) {
  const raw = (e.target as HTMLInputElement).value.replace(/\D/g, '').slice(0, 4)
  form.expiry = raw.length > 2 ? raw.slice(0, 2) + ' / ' + raw.slice(2) : raw
}

function handleSubmit() {
  emit('submit', { ...form, useNewAddress: useNewAddress.value, newAddr: { ...newAddr } })
}
</script>
