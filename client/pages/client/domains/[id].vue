<template>
  <div>
    <NuxtLink
      to="/client/domains"
      class="inline-flex items-center gap-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white text-sm transition-colors mb-6"
    >
      <ArrowLeft :size="16" :stroke-width="2" />
      {{ $t('client.domains.backTo') }}
    </NuxtLink>

    <!-- Loading -->
    <div v-if="pending" class="space-y-4">
      <div class="h-20 rounded-2xl bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 animate-pulse" />
      <div class="h-80 rounded-2xl bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 animate-pulse" />
    </div>

    <!-- Error -->
    <div v-else-if="!domain" class="text-center py-20">
      <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
      <p class="text-gray-500 dark:text-gray-400">{{ $t('client.domains.notFound') }}</p>
    </div>

    <div v-else>
      <!-- Page header -->
      <div class="mb-6 flex items-center justify-between gap-4 flex-wrap">
        <div class="flex items-center gap-4">
          <div class="w-12 h-12 rounded-xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center flex-shrink-0">
            <Globe :size="22" :stroke-width="2" class="text-primary-400" />
          </div>
          <div>
            <h1 class="text-xl font-bold text-gray-900 dark:text-white">{{ fullDomainName }}</h1>
            <p class="text-gray-500 dark:text-gray-400 text-sm">{{ $t('client.domains.expires') }} {{ formatDate(domain.expiresAt) }}</p>
          </div>
        </div>
        <ClientStatusBadge :status="domain.status" />
      </div>

      <!-- Layout: sidebar + content -->
      <div class="flex gap-6 items-start flex-col lg:flex-row">

        <!-- Left sidebar -->
        <div class="w-full lg:w-56 flex-shrink-0 space-y-3">
          <!-- Manage section -->
          <div class="rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 overflow-hidden">
            <div class="px-4 py-2.5 border-b border-gray-100 dark:border-white/10 flex items-center gap-2">
              <Settings :size="13" :stroke-width="2" class="text-gray-400 dark:text-gray-500" />
              <span class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">
                {{ $t('client.domains.sectionManage') }}
              </span>
            </div>
            <nav class="p-1">
              <button
                v-for="tab in tabs"
                :key="tab.key"
                class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors text-left"
                :class="activeTab === tab.key
                  ? 'bg-primary-50 dark:bg-primary-500/10 text-primary-600 dark:text-primary-400 font-medium'
                  : (isPending && tab.key !== 'overview')
                    ? 'text-gray-300 dark:text-gray-600 cursor-not-allowed'
                    : 'text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/5'"
                :disabled="isPending && tab.key !== 'overview'"
                @click="!isPending || tab.key === 'overview' ? activeTab = tab.key : undefined"
              >
                <component :is="tab.icon" :size="15" :stroke-width="2" class="flex-shrink-0" />
                {{ tab.label }}
              </button>
            </nav>
          </div>

          <!-- Actions section -->
          <div class="rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 overflow-hidden">
            <div class="px-4 py-2.5 border-b border-gray-100 dark:border-white/10 flex items-center gap-2">
              <Zap :size="13" :stroke-width="2" class="text-gray-400 dark:text-gray-500" />
              <span class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">
                {{ $t('client.domains.sectionActions') }}
              </span>
            </div>
            <nav class="p-1">
              <button
                class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-left text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/5 transition-colors"
                @click="activeTab = 'renew'"
              >
                <RefreshCw :size="15" :stroke-width="2" class="flex-shrink-0" />
                {{ $t('client.domains.actionRenewDomain') }}
              </button>
              <NuxtLink
                to="/domains"
                class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/5 transition-colors"
              >
                <PlusCircle :size="15" :stroke-width="2" class="flex-shrink-0" />
                {{ $t('client.domains.actionRegisterNew') }}
              </NuxtLink>
            </nav>
          </div>
        </div>

        <!-- Main content -->
        <div class="flex-1 min-w-0">

          <!-- ── Pending Registration / Transfer Banner ─ -->
          <UiAlert
            v-if="isPending"
            variant="info"
            :icon-size="16"
            :title="domain.status === 'PendingTransfer' ? $t('client.domains.pendingTransferTitle') : $t('client.domains.pendingRegistrationTitle')"
            class="mb-5"
          >
            {{ domain.status === 'PendingTransfer' ? $t('client.domains.pendingTransferDesc') : $t('client.domains.pendingRegistrationDesc') }}
          </UiAlert>

          <!-- ── Overview ──────────────────────────────── -->
          <template v-if="activeTab === 'overview'">
            <!-- Unlock warning (hidden while domain is still pending) -->
            <UiAlert
              v-if="!lockEnabled && !isPending"
              variant="error"
              :icon-size="16"
              :title="$t('client.domains.unlockWarningTitle')"
              class="mb-5"
            >
              {{ $t('client.domains.unlockWarningDesc') }}
            </UiAlert>

            <!-- Details grid -->
            <UiCard>
              <dl class="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-5">
                <UiDescriptionItem :label="$t('client.domains.fieldDomain')" :value="fullDomainName" value-class="font-medium" />
                <UiDescriptionItem :label="$t('client.domains.fieldFirstPayment')" :value="domain.firstPaymentAmount ? `$${domain.firstPaymentAmount} USD` : '—'" />
                <UiDescriptionItem :label="$t('client.domains.fieldRegistrationDate')" :value="formatDate(domain.registeredAt)" />
                <UiDescriptionItem :label="$t('client.domains.fieldRecurring')" :value="`$${domain.recurringAmount} / ${domain.registrationPeriod}yr`" />
                <UiDescriptionItem :label="$t('client.domains.fieldNextDueDate')" :value="formatDate(domain.nextDueDate)" />
                <UiDescriptionItem :label="$t('client.domains.fieldPaymentMethod')" :value="domain.paymentMethod || '—'" value-class="capitalize" />
                <UiDescriptionItem :label="$t('client.domains.fieldStatus')">
                  <ClientStatusBadge :status="domain.status" />
                </UiDescriptionItem>
              </dl>
            </UiCard>

            <!-- What would you like to do today? (hidden while domain is still pending) -->
            <div v-if="!isPending" class="mt-5 p-5 rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5">
              <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200 mb-3">{{ $t('client.domains.whatToday') }}</h3>
              <ul class="space-y-2">
                <li>
                  <button class="text-primary-600 dark:text-primary-400 text-sm hover:underline flex items-center gap-1.5" @click="activeTab = 'nameservers'">
                    <ChevronRight :size="14" :stroke-width="2" />
                    {{ $t('client.domains.quickNameservers') }}
                  </button>
                </li>
                <li>
                  <button class="text-primary-600 dark:text-primary-400 text-sm hover:underline flex items-center gap-1.5" @click="activeTab = 'contact'">
                    <ChevronRight :size="14" :stroke-width="2" />
                    {{ $t('client.domains.quickContact') }}
                  </button>
                </li>
                <li>
                  <button class="text-primary-600 dark:text-primary-400 text-sm hover:underline flex items-center gap-1.5" @click="activeTab = 'lock'">
                    <ChevronRight :size="14" :stroke-width="2" />
                    {{ $t('client.domains.quickLock') }}
                  </button>
                </li>
                <li>
                  <button class="text-primary-600 dark:text-primary-400 text-sm hover:underline flex items-center gap-1.5" @click="activeTab = 'renew'">
                    <ChevronRight :size="14" :stroke-width="2" />
                    {{ $t('client.domains.quickRenew') }}
                  </button>
                </li>
              </ul>
            </div>
          </template>

          <!-- ── Renew Domain ─────────────────────────── -->
          <template v-else-if="activeTab === 'renew'">
            <!-- Success state -->
            <UiCard v-if="renewSuccess">
              <div class="text-center py-6">
                <div class="w-14 h-14 rounded-full bg-green-500/10 border border-green-500/20 flex items-center justify-center mx-auto mb-4">
                  <CheckCircle :size="28" :stroke-width="2" class="text-green-400" />
                </div>
                <h2 class="text-base font-bold text-gray-900 dark:text-white mb-2">{{ $t('client.domains.renewOrderPlaced') }}</h2>
                <p class="text-sm text-gray-500 dark:text-gray-400 mb-5">{{ $t('client.domains.renewOrderDesc') }}</p>
                <NuxtLink
                  :to="`/client/invoices/${renewSuccess.invoiceId}`"
                  class="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-gradient-to-r from-cyan-600 to-primary-600 text-white font-semibold text-sm hover:opacity-90 transition-opacity"
                >
                  <CreditCard :size="14" :stroke-width="2" />
                  {{ $t('client.domains.renewPayInvoice') }}
                </NuxtLink>
              </div>
            </UiCard>

            <UiCard v-else>
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-6 flex items-center gap-2">
                <RefreshCw :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.domains.tabRenew') }}
              </h2>

              <!-- Domain row -->
              <div class="flex items-center justify-between gap-4 py-4 border-b border-gray-100 dark:border-white/10 mb-5">
                <div>
                  <p class="font-semibold text-gray-900 dark:text-white">{{ fullDomainName }}</p>
                  <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                    {{ $t('client.domains.renewExpiry') }} {{ formatDate(domain.expiresAt) }}
                  </p>
                </div>
                <span
                  class="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-medium"
                  :class="isExpired
                    ? 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-300'
                    : isExpiringSoon
                      ? 'bg-yellow-100 dark:bg-yellow-500/20 text-yellow-700 dark:text-yellow-300'
                      : 'bg-gray-100 dark:bg-white/10 text-gray-600 dark:text-gray-400'"
                >
                  {{ isExpired ? $t('client.domains.renewPastPeriod') : isExpiringSoon ? $t('client.domains.renewExpiringSoon') : $t('client.domains.renewActive') }}
                </span>
              </div>

              <UiForm :error="renewError" spacing="sm" @submit="placeRenewal">
                <!-- Renewal period -->
                <UiSelect
                  v-model="renewYears"
                  :label="$t('client.domains.renewPeriodLabel')"
                  :options="renewPeriodOptions"
                  size="sm"
                />

                <!-- Order summary -->
                <div class="rounded-xl border border-gray-200 dark:border-white/10 overflow-hidden">
                  <div class="px-4 py-3 bg-gray-50 dark:bg-white/5 border-b border-gray-200 dark:border-white/10">
                    <p class="text-sm font-semibold text-gray-700 dark:text-gray-200">{{ $t('client.domains.renewOrderSummary') }}</p>
                  </div>
                  <div class="px-4 py-4 space-y-2">
                    <div class="flex justify-between text-sm">
                      <span class="text-gray-500 dark:text-gray-400">{{ $t('client.domains.renewDomain') }}</span>
                      <span class="text-gray-900 dark:text-white font-medium">{{ fullDomainName }}</span>
                    </div>
                    <div class="flex justify-between text-sm">
                      <span class="text-gray-500 dark:text-gray-400">{{ $t('client.domains.renewPeriod') }}</span>
                      <span class="text-gray-900 dark:text-white font-medium">{{ renewYears }} {{ $t('client.domains.renewYear') }}</span>
                    </div>
                    <div class="flex justify-between text-sm pt-2 border-t border-gray-100 dark:border-white/10">
                      <span class="text-gray-500 dark:text-gray-400">{{ $t('client.domains.renewAmount') }}</span>
                      <span class="text-gray-900 dark:text-white font-semibold">${{ (domain.recurringAmount * renewYears).toFixed(2) }} USD</span>
                    </div>
                  </div>
                </div>

                <!-- Payment method -->
                <UiSelect
                  v-if="renewPayOptions.length"
                  v-model="renewPayMethod"
                  :label="$t('client.domains.renewPaymentLabel')"
                  :options="renewPayOptions"
                  size="sm"
                />

                <template #actions>
                  <UiButton type="submit" :loading="renewSaving" :full-width="true" :disabled="!renewPayMethod">
                    <RefreshCw v-if="!renewSaving" :size="14" :stroke-width="2" class="mr-1.5" />
                    {{ renewSaving ? $t('client.domains.renewPlacing') : $t('client.domains.renewBtn') }}
                  </UiButton>
                </template>
              </UiForm>
            </UiCard>
          </template>

          <!-- ── Auto Renew ────────────────────────────── -->
          <template v-else-if="activeTab === 'autorenew'">
            <UiCard>
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <RefreshCw :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.domains.tabAutoRenew') }}
              </h2>
              <div class="flex items-center justify-between p-4 rounded-xl bg-gray-50 dark:bg-white/5 border border-gray-200 dark:border-white/10">
                <div>
                  <div class="text-gray-900 dark:text-white text-sm font-medium">
                    {{ autoRenewEnabled ? $t('client.domains.autoRenewOn') : $t('client.domains.autoRenewOff') }}
                  </div>
                  <div class="text-gray-500 dark:text-gray-400 text-xs mt-0.5">{{ $t('client.domains.autoRenewDesc') }}</div>
                </div>
                <button
                  class="relative w-12 h-6 rounded-full transition-all duration-300 focus:outline-none border"
                  :class="autoRenewEnabled
                    ? 'bg-green-500 dark:bg-green-500/30 border-green-600 dark:border-green-500/40'
                    : 'bg-gray-200 dark:bg-white/10 border-gray-300 dark:border-white/20'"
                  :disabled="autoRenewSaving"
                  @click="toggleAutoRenew"
                >
                  <Loader2 v-if="autoRenewSaving" :size="14" class="absolute top-0.5 left-0.5 w-5 h-5 text-white animate-spin" />
                  <span
                    v-else
                    class="absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white transition-transform duration-300 shadow-sm"
                    :class="autoRenewEnabled ? 'translate-x-6' : 'translate-x-0'"
                  />
                </button>
              </div>
            </UiCard>
          </template>

          <!-- ── Nameservers ───────────────────────────── -->
          <template v-else-if="activeTab === 'nameservers'">
            <UiCard>
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <Network :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.domains.nameservers') }}
              </h2>

              <div v-if="nsLoading" class="space-y-3">
                <div v-for="i in 2" :key="i" class="h-10 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
              </div>

              <UiForm v-else :error="nsError" :success="nsSuccess" spacing="sm" @submit="saveNameservers">
                <!-- Info note -->
                <div class="px-4 py-3 rounded-lg bg-blue-50 dark:bg-blue-500/10 border border-blue-200 dark:border-blue-500/20 text-blue-700 dark:text-blue-300 text-sm">
                  {{ $t('client.domains.nsInfoNote') }}
                </div>

                <!-- Radio: default / custom -->
                <div class="space-y-2.5">
                  <UiRadio
                    name="ns-type"
                    :value="false"
                    :model-value="nsUseCustom"
                    :label="$t('client.domains.nsUseDefault')"
                    @update:model-value="nsUseCustom = false"
                  />
                  <UiRadio
                    name="ns-type"
                    :value="true"
                    :model-value="nsUseCustom"
                    :label="$t('client.domains.nsUseCustom')"
                    @update:model-value="nsUseCustom = true"
                  />
                </div>

                <!-- NS inputs -->
                <div v-for="n in 5" :key="n" class="flex items-center gap-3">
                  <span class="text-sm text-gray-500 dark:text-gray-400 w-24 flex-shrink-0">Nameserver {{ n }}</span>
                  <UiInput
                    v-model="nsForm[`ns${n}` as keyof typeof nsForm]"
                    :placeholder="`ns${n}.example.com`"
                    :disabled="!nsUseCustom"
                    type="text"
                    size="sm"
                    class="flex-1"
                  />
                </div>

                <template #actions>
                  <UiButton type="submit" size="sm" :loading="nsSaving" :full-width="true">
                    <Save v-if="!nsSaving" :size="14" :stroke-width="2" class="mr-1.5" />
                    {{ nsSaving ? $t('client.domains.saving') : $t('client.domains.saveNameservers') }}
                  </UiButton>
                </template>
              </UiForm>
            </UiCard>
          </template>

          <!-- ── Registrar Lock ────────────────────────── -->
          <template v-else-if="activeTab === 'lock'">
            <div class="space-y-4">

              <!-- Registrar Lock -->
              <UiCard>
                <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                  <Lock :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                  {{ $t('client.domains.registrarLock') }}
                </h2>
                <div class="flex items-center justify-between p-4 rounded-xl bg-gray-50 dark:bg-white/5 border border-gray-200 dark:border-white/10">
                  <div>
                    <div class="text-gray-900 dark:text-white text-sm font-medium">
                      {{ lockEnabled ? $t('client.domains.lockOn') : $t('client.domains.lockOff') }}
                    </div>
                    <div class="text-gray-500 dark:text-gray-400 text-xs mt-0.5">{{ $t('client.domains.lockDesc') }}</div>
                  </div>
                  <button
                    class="relative w-12 h-6 rounded-full transition-all duration-300 focus:outline-none border"
                    :class="lockEnabled
                      ? 'bg-green-500 dark:bg-green-500/30 border-green-600 dark:border-green-500/40'
                      : 'bg-gray-200 dark:bg-white/10 border-gray-300 dark:border-white/20'"
                    :disabled="lockSaving"
                    @click="toggleLock"
                  >
                    <Loader2 v-if="lockSaving" :size="14" class="absolute top-0.5 left-0.5 w-5 h-5 text-white animate-spin" />
                    <span
                      v-else
                      class="absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white transition-transform duration-300 shadow-sm"
                      :class="lockEnabled ? 'translate-x-6' : 'translate-x-0'"
                    />
                  </button>
                </div>
              </UiCard>

              <!-- ID Protection -->
              <UiCard>
                <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                  <ShieldCheck :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                  {{ $t('client.domains.idProtection') }}
                </h2>
                <div class="flex items-center justify-between p-4 rounded-xl bg-gray-50 dark:bg-white/5 border border-gray-200 dark:border-white/10">
                  <div>
                    <div class="text-gray-900 dark:text-white text-sm font-medium">
                      {{ idProtectEnabled ? $t('client.domains.idProtectOn') : $t('client.domains.idProtectOff') }}
                    </div>
                    <div class="text-gray-500 dark:text-gray-400 text-xs mt-0.5">{{ $t('client.domains.idProtectionDesc') }}</div>
                  </div>
                  <button
                    class="relative w-12 h-6 rounded-full transition-all duration-300 focus:outline-none border"
                    :class="idProtectEnabled
                      ? 'bg-green-500 dark:bg-green-500/30 border-green-600 dark:border-green-500/40'
                      : 'bg-gray-200 dark:bg-white/10 border-gray-300 dark:border-white/20'"
                    :disabled="idProtectSaving"
                    @click="toggleIdProtect"
                  >
                    <Loader2 v-if="idProtectSaving" :size="14" class="absolute top-0.5 left-0.5 w-5 h-5 text-white animate-spin" />
                    <span
                      v-else
                      class="absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white transition-transform duration-300 shadow-sm"
                      :class="idProtectEnabled ? 'translate-x-6' : 'translate-x-0'"
                    />
                  </button>
                </div>
              </UiCard>

              <!-- EPP Code -->
              <UiCard>
                <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                  <Key :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                  {{ $t('client.domains.eppCode') }}
                </h2>
                <div class="p-4 rounded-xl bg-gray-50 dark:bg-white/5 border border-gray-200 dark:border-white/10">
                  <div class="text-gray-500 dark:text-gray-400 text-xs mb-3">{{ $t('client.domains.eppDesc') }}</div>
                  <UiAlert v-if="lockEnabled" :icon-size="14" class="mb-3 !p-3 text-xs">
                    {{ $t('client.domains.lockWarning') }}
                  </UiAlert>
                  <div v-if="eppMessage" class="mb-3 text-sm" :class="eppError ? 'text-red-400' : 'text-green-400'">
                    {{ eppMessage }}
                  </div>
                  <UiButton size="sm" variant="subtle" :loading="eppSending" :full-width="true" @click="requestEpp">
                    <Mail v-if="!eppSending" :size="14" :stroke-width="2" class="mr-1.5" />
                    {{ eppSending ? $t('client.domains.sendingEpp') : $t('client.domains.emailEpp') }}
                  </UiButton>
                </div>
              </UiCard>

            </div>
          </template>

          <!-- ── Contact Information ───────────────────── -->
          <template v-else-if="activeTab === 'contact'">
            <UiCard>
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <UserCog :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.domains.tabContactInfo') }}
              </h2>

              <!-- Loading -->
              <div v-if="whoisLoading" class="space-y-3">
                <div v-for="i in 6" :key="i" class="h-10 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
              </div>

              <template v-else>
                <!-- Info note -->
                <div class="mb-5 flex gap-3 rounded-xl border border-blue-200 dark:border-blue-500/30 bg-blue-50 dark:bg-blue-500/10 p-3.5">
                  <Info :size="16" :stroke-width="2" class="text-blue-500 dark:text-blue-400 flex-shrink-0 mt-0.5" />
                  <p class="text-xs text-blue-700 dark:text-blue-300 leading-relaxed">
                    {{ $t('client.domains.contactInfoNote') }}
                  </p>
                </div>

                <!-- Contact type tabs -->
                <UiTabs
                  v-model="activeContact"
                  :tabs="contactTypeOptions"
                  class="mb-5"
                />

                <!-- Radio: use existing / specify custom -->
                <div class="space-y-3 mb-5">
                  <UiRadio
                    name="contactMode"
                    :value="true"
                    :model-value="contactUseExisting[activeContact]"
                    :label="$t('client.domains.contactUseExisting')"
                    @update:model-value="contactUseExisting[activeContact] = true"
                  />
                  <!-- Choose Contact dropdown -->
                  <div v-if="contactUseExisting[activeContact]" class="ml-8">
                    <UiSelect
                      :model-value="selectedContactId[activeContact]"
                      :label="$t('client.domains.contactChoose')"
                      :options="contactOptions"
                      size="sm"
                      @update:model-value="(val) => { selectedContactId[activeContact] = val as string; applySelectedContact(activeContact) }"
                    />
                  </div>
                  <UiRadio
                    name="contactMode"
                    :value="false"
                    :model-value="contactUseExisting[activeContact]"
                    :label="$t('client.domains.contactSpecifyCustom')"
                    @update:model-value="contactUseExisting[activeContact] = false"
                  />
                </div>

                <!-- Contact form -->
                <UiForm :error="whoisError" :success="whoisSuccess" spacing="sm" @submit="saveWhois">
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <UiInput v-model="whoisForm[activeContact].firstName" :label="$t('client.domains.contactFirstName')" :disabled="contactUseExisting[activeContact]" size="sm" />
                    <UiInput v-model="whoisForm[activeContact].lastName" :label="$t('client.domains.contactLastName')" :disabled="contactUseExisting[activeContact]" size="sm" />
                  </div>
                  <UiInput v-model="whoisForm[activeContact].organization" :label="$t('client.domains.contactCompany')" :disabled="contactUseExisting[activeContact]" size="sm" />
                  <UiInput v-model="whoisForm[activeContact].email" :label="$t('client.domains.contactEmail')" type="email" :disabled="contactUseExisting[activeContact]" size="sm" />
                  <UiInput v-model="whoisForm[activeContact].address1" :label="$t('client.domains.contactAddress1')" :disabled="contactUseExisting[activeContact]" size="sm" />
                  <UiInput v-model="whoisForm[activeContact].address2" :label="$t('client.domains.contactAddress2')" :disabled="contactUseExisting[activeContact]" size="sm" />
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <UiInput v-model="whoisForm[activeContact].city" :label="$t('client.domains.contactCity')" :disabled="contactUseExisting[activeContact]" size="sm" />
                    <UiInput v-model="whoisForm[activeContact].state" :label="$t('client.domains.contactState')" :disabled="contactUseExisting[activeContact]" size="sm" />
                  </div>
                  <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <UiInput v-model="whoisForm[activeContact].postalCode" :label="$t('client.domains.contactPostcode')" :disabled="contactUseExisting[activeContact]" size="sm" />
                    <UiInput v-model="whoisForm[activeContact].country" :label="$t('client.domains.contactCountry')" :disabled="contactUseExisting[activeContact]" size="sm" />
                  </div>
                  <UiInput v-model="whoisForm[activeContact].phone" :label="$t('client.domains.contactPhone')" type="tel" :disabled="contactUseExisting[activeContact]" size="sm" />
                  <template #actions>
                    <UiButton type="button" size="sm" variant="ghost" @click="cancelWhois">
                      {{ $t('client.domains.cancelChanges') }}
                    </UiButton>
                    <UiButton type="submit" size="sm" :loading="whoisSaving">
                      <Save v-if="!whoisSaving" :size="14" :stroke-width="2" class="mr-1.5" />
                      {{ whoisSaving ? $t('client.domains.saving') : $t('client.domains.saveChanges') }}
                    </UiButton>
                  </template>
                </UiForm>
              </template>
            </UiCard>
          </template>

        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  ArrowLeft, AlertCircle, Globe, Network, Save, Settings, Key, Mail,
  Lock, RefreshCw, PlusCircle, ChevronRight, Zap, UserCog, LayoutGrid, ShieldCheck, Loader2, Info,
  CheckCircle, CreditCard
} from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const route = useRoute()
const domainId = route.params.id as string

/** Shape of the DomainDto returned by the C# backend. */
interface DomainDetail {
  /** Domain primary key. */
  id: number
  /** Domain name (e.g. "example"). */
  name: string
  /** Top-level domain including the dot (e.g. ".com"). */
  tld: string
  /** Current lifecycle status. */
  status: string
  /** ISO 8601 registration date. */
  registeredAt: string
  /** ISO 8601 expiration date. */
  expiresAt: string
  /** ISO 8601 next renewal payment due date. */
  nextDueDate: string
  /** One-time registration cost. */
  firstPaymentAmount: number
  /** Recurring registration price. */
  recurringAmount: number
  /** Payment method label. */
  paymentMethod: string | null
  /** Registration period in years. */
  registrationPeriod: number
  /** Whether the domain is set to auto-renew at expiration. */
  autoRenew: boolean
  /** Whether the domain is locked against unauthorized transfers. */
  isLocked: boolean
  /** Whether WHOIS privacy is enabled. */
  whoisPrivacy: boolean
  /** Name of the registrar module. */
  registrar: string | null
}

// ── Domain ────────────────────────────────────────────────────────────────────
const { data: _domainRaw, pending } = await useApi<DomainDetail>(`/api/portal/client/domains/${domainId}`)
const domain = computed(() => _domainRaw.value as DomainDetail | null)

/** Full domain name (the backend's Name field is already the FQDN). */
const fullDomainName = computed(() => domain.value?.name ?? '')

// ── Helpers ───────────────────────────────────────────────────────────────────

/**
 * Formats an ISO 8601 date string for display, returning a dash for invalid dates.
 *
 * @param d - ISO 8601 date string.
 * @returns Localized date string or em-dash placeholder.
 */
function formatDate(d: string): string {
  if (!d || d.startsWith('0000') || d.startsWith('0001')) return '—'
  const date = new Date(d)
  return isNaN(date.getTime()) ? '—' : date.toLocaleDateString()
}

// ── Tabs ──────────────────────────────────────────────────────────────────────
const activeTab = ref('overview')

const tabs = computed(() => [
  { key: 'overview',     label: t('client.domains.tabOverview'),      icon: LayoutGrid  },
  { key: 'autorenew',   label: t('client.domains.tabAutoRenew'),     icon: RefreshCw   },
  { key: 'nameservers', label: t('client.domains.tabNameservers'),   icon: Network     },
  { key: 'lock',        label: t('client.domains.tabRegistrarLock'), icon: Lock        },
  { key: 'contact',     label: t('client.domains.tabContactInfo'),   icon: UserCog     },
])

// ── Renew ─────────────────────────────────────────────────────────────────────
/** Whether the domain is still being processed by the registrar (not yet live). */
const isPending = computed(() =>
  domain.value?.status === 'PendingRegistration' || domain.value?.status === 'PendingTransfer'
)

/** Whether the domain status is Expired. */
const isExpired = computed(() =>
  domain.value?.status === 'Expired'
)

/** Whether the domain expires within the next 30 days. */
const isExpiringSoon = computed(() => {
  const d = domain.value?.expiresAt
  if (!d || d.startsWith('0001')) return false
  const diff = (new Date(d).getTime() - Date.now()) / 86400000
  return diff <= 30 && diff > 0
})

// Renewal order form
const renewYears   = ref<number>(1)
const renewPayMethod = ref('')
const renewSaving  = ref(false)
const renewError   = ref('')
const renewSuccess = ref<{ orderId: number; invoiceId: number } | null>(null)

const renewPeriodOptions = computed(() => [
  { label: `1 ${t('client.domains.renewYear')}`,   value: 1 },
  { label: `2 ${t('client.domains.renewYears')}`,  value: 2 },
  { label: `3 ${t('client.domains.renewYears')}`,  value: 3 },
])

// Fetch payment methods when renew tab opens
const renewPayMethods = ref<Array<{ gateway_name: string; description: string; card_last_four?: string }>>([])
watch(activeTab, async (tab) => {
  if (tab !== 'renew' || renewPayMethods.value.length) return
  try {
    renewPayMethods.value = await apiFetch('/api/portal/client/payment-methods') as typeof renewPayMethods.value
    if (renewPayMethods.value.length) renewPayMethod.value = renewPayMethods.value[0]!.gateway_name
  } catch { /* ignore */ }
})

const renewPayOptions = computed(() =>
  renewPayMethods.value.map(m => ({
    label: m.card_last_four ? `${m.description} •••• ${m.card_last_four}` : m.description,
    value: m.gateway_name,
  }))
)

async function placeRenewal() {
  renewSaving.value = true
  renewError.value  = ''
  try {
    const res = await apiFetch<{ orderId: number; invoiceId: number }>(
      `/api/portal/client/domains/${domainId}/renew-order`,
      { method: 'POST', body: { years: renewYears.value, paymentmethod: renewPayMethod.value } }
    )
    renewSuccess.value = res
  } catch (err: unknown) {
    renewError.value = (err as { data?: { statusMessage?: string } })?.data?.statusMessage || t('client.domains.renewError')
  } finally {
    renewSaving.value = false
  }
}

// ── Auto Renew ────────────────────────────────────────────────────────────────
const autoRenewEnabled = ref(false)
const autoRenewSaving  = ref(false)
watch(domain, (d) => { if (d) autoRenewEnabled.value = d.autoRenew }, { immediate: true })

/** Toggles auto-renew for the domain via the backend API. */
async function toggleAutoRenew() {
  autoRenewSaving.value = true
  const newVal = !autoRenewEnabled.value
  try {
    await apiFetch(`/api/portal/client/domains/${domainId}/autorenew`, {
      method: 'PUT',
      body: { enabled: newVal },
    })
    autoRenewEnabled.value = newVal
  } catch { /* keep current state */ } finally {
    autoRenewSaving.value = false
  }
}

// ── Lock ──────────────────────────────────────────────────────────────────────
const lockEnabled  = ref(false)
const lockSaving   = ref(false)

watch(domain, (d) => { if (d) lockEnabled.value = d.isLocked }, { immediate: true })

/** Toggles the registrar transfer lock for the domain via the backend API. */
async function toggleLock() {
  lockSaving.value = true
  const newLocked = !lockEnabled.value
  try {
    await apiFetch(`/api/portal/client/domains/${domainId}/lock`, {
      method: 'PUT',
      body: { enabled: newLocked }
    })
    lockEnabled.value = newLocked
  } catch { /* keep current state */ } finally {
    lockSaving.value = false
  }
}

// ── WHOIS Privacy ─────────────────────────────────────────────────────────────
const idProtectEnabled = ref(false)
const idProtectSaving  = ref(false)

watch(domain, (d) => { if (d) idProtectEnabled.value = d.whoisPrivacy }, { immediate: true })

/** Toggles WHOIS privacy protection for the domain via the backend API. */
async function toggleIdProtect() {
  idProtectSaving.value = true
  const newVal = !idProtectEnabled.value
  try {
    await apiFetch(`/api/portal/client/domains/${domainId}/idprotect`, {
      method: 'PUT',
      body: { enabled: newVal }
    })
    idProtectEnabled.value = newVal
  } catch { /* keep current state */ } finally {
    idProtectSaving.value = false
  }
}

// ── Nameservers ───────────────────────────────────────────────────────────────

/** Shape of nameserver entries returned by the backend. */
interface NameserverEntry {
  /** Nameserver record primary key. */
  id: number
  /** Nameserver hostname (e.g. "ns1.example.com"). */
  host: string
}

const nsLoading   = ref(false)
const nsSaving    = ref(false)
const nsError     = ref('')
const nsSuccess   = ref('')
const nsUseCustom = ref(true)
const nsForm      = reactive({ ns1: '', ns2: '', ns3: '', ns4: '', ns5: '' })

onMounted(async () => {
  nsLoading.value = true
  try {
    const data = await apiFetch<NameserverEntry[]>(
      `/api/portal/client/domains/${domainId}/nameservers`
    )
    // Map array of { id, host } to ns1..ns5 form fields
    data.forEach((ns, i) => {
      const key = `ns${i + 1}` as keyof typeof nsForm
      if (key in nsForm) nsForm[key] = ns.host
    })
    nsUseCustom.value = !!(nsForm.ns1 || nsForm.ns2)
  } catch { /* silently ignore */ } finally {
    nsLoading.value = false
  }
})

/** Saves the nameserver configuration via the backend API. */
async function saveNameservers() {
  nsSaving.value  = true
  nsError.value   = ''
  nsSuccess.value = ''
  try {
    // Collect non-empty nameservers into the array format the backend expects
    const nameservers = [nsForm.ns1, nsForm.ns2, nsForm.ns3, nsForm.ns4, nsForm.ns5]
      .filter(ns => ns.trim() !== '')
    await apiFetch(`/api/portal/client/domains/${domainId}/nameservers`, {
      method: 'PUT',
      body: { nameservers }
    })
    nsSuccess.value = t('client.domains.nsSuccess')
  } catch (err: unknown) {
    const msg = ((err as { data?: { statusMessage?: string } })?.data?.statusMessage || '') as string
    nsError.value = msg.toLowerCase().includes('registrar')
      ? t('client.domains.nsRegistrarError')
      : msg || t('client.domains.nsError')
  } finally {
    nsSaving.value = false
  }
}

// ── EPP ───────────────────────────────────────────────────────────────────────
const eppSending = ref(false)
const eppMessage = ref('')
const eppError   = ref(false)

/** Requests the EPP authorization code from the backend. */
async function requestEpp() {
  eppSending.value = true
  eppMessage.value = ''
  eppError.value   = false
  try {
    const res = await apiFetch<{ eppCode: string }>(
      `/api/portal/client/domains/${domainId}/epp`, { method: 'POST' }
    )
    eppMessage.value = res.eppCode
  } catch (err: unknown) {
    eppError.value   = true
    eppMessage.value = (err as { data?: { statusMessage?: string } })?.data?.statusMessage || t('client.domains.eppError')
  } finally {
    eppSending.value = false
  }
}

// ── WHOIS / Contact ───────────────────────────────────────────────────────────
/** Contact fields matching the backend ModifyContactRequest shape. */
interface ContactFields {
  /** Registrant first name. */
  firstName: string
  /** Registrant last name. */
  lastName: string
  /** Organization name. */
  organization: string
  /** Contact email address. */
  email: string
  /** Street address line 1. */
  address1: string
  /** Street address line 2. */
  address2: string
  /** City. */
  city: string
  /** State or province. */
  state: string
  /** Postal/zip code. */
  postalCode: string
  /** ISO 3166-1 alpha-2 country code. */
  country: string
  /** Contact phone number. */
  phone: string
}

const contactTypes = ['Registrant', 'Admin', 'Tech', 'Billing'] as const
type ContactType = typeof contactTypes[number]

/** Creates an empty contact fields object. */
const emptyContact = (): ContactFields => ({
  firstName: '', lastName: '', organization: '', email: '',
  address1: '', address2: '', city: '', state: '',
  postalCode: '', country: '', phone: ''
})

const activeContact = ref<ContactType>('Registrant')
const whoisLoading  = ref(false)
const whoisSaving   = ref(false)
const whoisError    = ref('')
const whoisSuccess  = ref('')

const whoisForm = reactive<Record<ContactType, ContactFields>>({
  Registrant: emptyContact(),
  Admin:      emptyContact(),
  Tech:       emptyContact(),
  Billing:    emptyContact(),
})

// Snapshot of loaded data for cancel
const whoisInitial = reactive<Record<ContactType, ContactFields>>({
  Registrant: emptyContact(),
  Admin:      emptyContact(),
  Tech:       emptyContact(),
  Billing:    emptyContact(),
})

/** A selectable contact entry for the "Choose Contact" dropdown. */
interface ContactEntry { id: string; label: string; fields: Partial<ContactFields> }
const contactsList = ref<ContactEntry[]>([])
let clientCache: Record<string, string> | null = null

// Per-contact-type state for radio/dropdown
const contactUseExisting = reactive<Record<ContactType, boolean>>({
  Registrant: true, Admin: true, Tech: true, Billing: true
})
const selectedContactId = reactive<Record<ContactType, string>>({
  Registrant: 'primary', Admin: 'primary', Tech: 'primary', Billing: 'primary'
})

const contactTypeOptions = contactTypes.map(ct => ({ label: ct, value: ct }))

const contactOptions = computed(() => [
  { label: t('client.domains.contactPrimaryProfile'), value: 'primary' },
  ...contactsList.value.map(c => ({ label: c.label, value: c.id })),
])

/** Pre-fill form for the given contact type from the chosen contact. */
async function applySelectedContact(ct: ContactType) {
  const id = selectedContactId[ct]
  if (id === 'primary') {
    if (!clientCache) {
      clientCache = await apiFetch('/api/portal/client/me') as Record<string, string>
    }
    Object.assign(whoisForm[ct], {
      firstName:    clientCache.firstname    || '',
      lastName:     clientCache.lastname     || '',
      organization: clientCache.companyname  || '',
      email:        clientCache.email        || '',
      address1:     clientCache.address1     || '',
      address2:     clientCache.address2     || '',
      city:         clientCache.city         || '',
      state:        clientCache.state        || '',
      postalCode:   clientCache.postcode     || '',
      country:      clientCache.country      || '',
      phone:        clientCache.phonenumber  || '',
    })
  } else {
    const entry = contactsList.value.find(c => c.id === id)
    if (entry) Object.assign(whoisForm[ct], entry.fields)
  }
}

// Load WHOIS + contacts when contact tab is first opened
watch(activeTab, async (tab) => {
  if (tab !== 'contact' || whoisLoaded) return
  whoisLoading.value = true
  try {
    const [whoisData, contactsData] = await Promise.all([
      apiFetch<Record<ContactType, ContactFields>>(`/api/portal/client/domains/${domainId}/whois`),
      apiFetch<Array<{ id: string; firstname: string; lastname: string; companyname?: string; email?: string; phonenumber?: string }>>('/api/portal/client/contacts'),
    ])
    for (const ct of contactTypes) {
      if (whoisData[ct]) {
        Object.assign(whoisForm[ct], whoisData[ct])
        Object.assign(whoisInitial[ct], whoisData[ct])
      }
    }
    contactsList.value = contactsData.map(c => ({
      id:    c.id,
      label: `${c.firstname} ${c.lastname}`.trim(),
      fields: {
        firstName:    c.firstname   || '',
        lastName:     c.lastname    || '',
        organization: c.companyname || '',
        email:        c.email       || '',
        phone:        c.phonenumber || '',
      },
    }))
    whoisLoaded = true
  } catch { /* leave empty */ } finally {
    whoisLoading.value = false
  }
})
let whoisLoaded = false

/** Reset current tab's form to loaded values */
function cancelWhois() {
  Object.assign(whoisForm[activeContact.value], whoisInitial[activeContact.value])
  whoisError.value   = ''
  whoisSuccess.value = ''
}

async function saveWhois() {
  whoisSaving.value  = true
  whoisError.value   = ''
  whoisSuccess.value = ''
  try {
    await apiFetch(`/api/portal/client/domains/${domainId}/whois`, {
      method: 'PUT',
      body: whoisForm
    })
    // Update snapshot so cancel reflects saved state
    for (const ct of contactTypes) Object.assign(whoisInitial[ct], whoisForm[ct])
    whoisSuccess.value = t('client.domains.contactSaved')
  } catch (err: unknown) {
    whoisError.value = (err as { data?: { statusMessage?: string } })?.data?.statusMessage || t('client.domains.contactError')
  } finally {
    whoisSaving.value = false
  }
}
</script>
