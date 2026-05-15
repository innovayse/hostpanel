<template>
  <div>
    <NuxtLink
      to="/client/services"
      class="inline-flex items-center gap-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white text-sm transition-colors mb-6"
    >
      <ArrowLeft :size="16" :stroke-width="2" />
      {{ $t('client.services.backTo') }}
    </NuxtLink>

    <!-- Loading -->
    <div v-if="pending" class="space-y-4">
      <div class="h-20 rounded-2xl bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 animate-pulse" />
      <div class="h-80 rounded-2xl bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 animate-pulse" />
    </div>

    <!-- Error -->
    <div v-else-if="error || !service" class="text-center py-20">
      <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
      <p class="text-gray-500 dark:text-gray-400">{{ $t('client.services.notFound') }}</p>
    </div>

    <div v-else>
      <!-- Page header -->
      <div class="mb-6 flex items-center justify-between gap-4 flex-wrap">
        <div class="flex items-center gap-4">
          <div class="w-12 h-12 rounded-xl bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center flex-shrink-0">
            <ServerIcon :size="22" :stroke-width="2" class="text-cyan-400" />
          </div>
          <div>
            <h1 class="text-xl font-bold text-gray-900 dark:text-white">{{ service.name }}</h1>
            <p class="text-gray-500 dark:text-gray-400 text-sm">{{ service.domain || service.groupname }}</p>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <UiButton
            v-if="service.serverhostname && service.status === 'Active'"
            variant="primary"
            size="sm"
            :loading="ssoLoading"
            class="hidden sm:flex"
            @click="loginToCpanel"
          >
            <Monitor :size="14" class="mr-1.5" />
            {{ $t('client.services.actionLoginCpanel') }}
          </UiButton>
          <ClientStatusBadge :status="service.status" />
        </div>
      </div>

      <!-- Cancellation Requested banner -->
      <div
        v-if="cancelStatus?.pending"
        class="mb-5 flex items-center gap-3 rounded-xl border border-red-200 dark:border-red-500/20 bg-red-50 dark:bg-red-500/10 px-4 py-3"
      >
        <XCircle :size="18" :stroke-width="2" class="text-red-500 flex-shrink-0" />
        <div class="flex-1 text-sm text-red-700 dark:text-red-300">
          <span class="font-medium">{{ $t('client.services.cancellationPending') }}</span>
          <span v-if="cancelStatus.type" class="text-red-500 dark:text-red-400"> &mdash; {{ cancelStatus.type === 'Immediate' ? $t('client.services.cancelImmediate') : $t('client.services.cancelEndOfPeriod') }}</span>
        </div>
      </div>

      <!-- Setup Required banner -->
      <div
        v-if="service.status === 'Pending'"
        class="mb-5 flex flex-col sm:flex-row items-center gap-4 rounded-xl border border-yellow-200 dark:border-yellow-500/20 bg-yellow-50 dark:bg-yellow-500/10 px-4 py-4"
      >
        <div class="flex items-center gap-3 flex-1 w-full">
          <AlertCircle :size="20" :stroke-width="2" class="text-yellow-600 dark:text-yellow-400 flex-shrink-0" />
          <div class="text-sm text-yellow-800 dark:text-yellow-300">
            <h3 class="font-bold mb-0.5">{{ $t('client.services.setupRequired') }}</h3>
            <p class="opacity-90">{{ $t('client.services.setupRequiredNotice') }}</p>
          </div>
        </div>
        <UiButton
          :to="`/client/services/${serviceId}/setup`"
          variant="primary"
          size="sm"
          class="shrink-0 w-full sm:w-auto"
        >
          <Zap :size="14" class="mr-1.5" />
          {{ $t('client.services.completeSetup') }}
        </UiButton>
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
                {{ $t('client.services.sectionManage') }}
              </span>
            </div>
            <nav class="p-1">
              <button
                v-for="tab in tabs"
                :key="tab.key"
                class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors text-left"
                :class="activeTab === tab.key
                  ? 'bg-primary-50 dark:bg-primary-500/10 text-primary-600 dark:text-primary-400 font-medium'
                  : 'text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/5'"
                @click="activeTab = tab.key"
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
                {{ $t('client.services.sectionActions') }}
              </span>
            </div>
            <nav class="p-1">
              <NuxtLink
                to="/client/tickets/new"
                class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/5 transition-colors"
              >
                <MessageSquare :size="15" :stroke-width="2" class="flex-shrink-0" />
                {{ $t('client.services.actionOpenTicket') }}
              </NuxtLink>
              <NuxtLink
                v-if="service.status === 'Pending'"
                :to="`/client/services/${serviceId}/setup`"
                class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-yellow-600 dark:text-yellow-400 hover:bg-yellow-50 dark:hover:bg-yellow-500/10 transition-colors font-medium"
              >
                <Zap :size="15" :stroke-width="2" class="flex-shrink-0" />
                {{ $t('client.services.completeSetup') }}
              </NuxtLink>
            </nav>
          </div>

          <!-- Quick Links section -->
          <div v-if="service.serverhostname" class="rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 overflow-hidden">
            <div class="px-4 py-2.5 border-b border-gray-100 dark:border-white/10 flex items-center gap-2">
              <Link2 :size="13" :stroke-width="2" class="text-gray-400 dark:text-gray-500" />
              <span class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">
                {{ $t('client.services.sectionQuickLinks') }}
              </span>
            </div>
            <nav class="p-1">
              <button
                :disabled="ssoLoading"
                class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/5 transition-colors disabled:opacity-60 disabled:cursor-wait"
                @click="loginToCpanel"
              >
                <Loader v-if="ssoLoading" :size="15" :stroke-width="2" class="flex-shrink-0 animate-spin" />
                <Monitor v-else :size="15" :stroke-width="2" class="flex-shrink-0" />
                {{ $t('client.services.linkCpanel') }}
              </button>
              <a
                :href="`https://${service.serverhostname}:2096`"
                target="_blank"
                rel="noopener"
                class="flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/5 transition-colors"
              >
                <Mail :size="15" :stroke-width="2" class="flex-shrink-0" />
                {{ $t('client.services.linkWebmail') }}
              </a>
            </nav>
          </div>
        </div>

        <!-- Main content -->
        <div class="flex-1 min-w-0">

          <!-- ── Overview ──────────────────────────────── -->
          <template v-if="activeTab === 'overview'">
            <!-- Suspension reason alert -->
            <UiAlert
              v-if="service.status === 'Suspended' && service.suspensionreason"
              variant="error"
              :icon-size="16"
              :title="$t('client.services.suspendedReason')"
              class="mb-5"
            >
              {{ service.suspensionreason }}
            </UiAlert>

            <!-- Details grid -->
            <UiCard class="mb-4">
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <LayoutGrid :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.services.serviceDetails') }}
              </h2>
              <dl class="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-5">
                <UiDescriptionItem :label="$t('client.services.plan')" :value="service.name" value-class="font-medium" />
                <UiDescriptionItem :label="$t('client.services.domain')" :value="service.domain || '\u2014'" />
                <UiDescriptionItem :label="$t('client.services.status')">
                  <ClientStatusBadge :status="service.status" />
                </UiDescriptionItem>
                <UiDescriptionItem :label="$t('client.services.billingCycle')" :value="service.billingcycle || '\u2014'" value-class="capitalize" />
                <UiDescriptionItem :label="$t('client.services.regDate')" :value="service.regdate" />
                <UiDescriptionItem :label="$t('client.services.nextDueDate')" :value="service.nextduedate" />
                <UiDescriptionItem :label="$t('client.services.billingAmount')" :value="formatAmount(service.recurringamount, store.user?.currency)" value-class="font-medium" />
                <UiDescriptionItem :label="$t('client.services.firstPayment')" :value="formatAmount(service.firstpaymentamount, store.user?.currency)" />
                <UiDescriptionItem :label="$t('client.services.paymentMethod')" :value="service.paymentmethodname || service.paymentmethod || '\u2014'" value-class="capitalize" />
              </dl>
            </UiCard>

            <!-- Server Info -->
            <UiCard class="mb-4">
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <ServerIcon :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.services.serverInfo') }}
              </h2>
              <dl class="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-5">
                <UiDescriptionItem :label="$t('client.services.server')" :value="service.servername || '\u2014'" />
                <UiDescriptionItem :label="$t('client.services.ipAddress')" :value="service.dedicatedip || service.serverip || '\u2014'" value-class="font-mono text-xs" />
                <UiDescriptionItem :label="$t('client.services.username')" :value="service.username || '\u2014'" value-class="font-mono text-xs" />
                <UiDescriptionItem v-if="hostingInfo?.nameservers?.length" :label="$t('client.services.nameservers')">
                  <div class="space-y-0.5">
                    <div v-for="ns in hostingInfo.nameservers" :key="ns" class="text-sm font-mono text-gray-900 dark:text-white">{{ ns }}</div>
                  </div>
                </UiDescriptionItem>
              </dl>

              <!-- Usage bars -->
              <div v-if="service.disklimit && service.disklimit !== '0'" class="mt-6 space-y-3">
                <div>
                  <div class="flex justify-between text-xs text-gray-500 mb-1">
                    <span>{{ $t('client.services.disk') }}</span>
                    <span>{{ service.diskusage }}MB / {{ service.disklimit }}MB</span>
                  </div>
                  <div class="h-1.5 rounded-full bg-gray-200 dark:bg-white/10 overflow-hidden">
                    <div
                      class="h-full rounded-full bg-gradient-to-r from-cyan-500 to-primary-500"
                      :style="{ width: `${Math.min(100, (parseInt(service.diskusage) / parseInt(service.disklimit)) * 100)}%` }"
                    />
                  </div>
                </div>
                <div v-if="service.bwlimit && service.bwlimit !== '0'">
                  <div class="flex justify-between text-xs text-gray-500 mb-1">
                    <span>{{ $t('client.services.bandwidth') }}</span>
                    <span>{{ service.bwusage }}MB / {{ service.bwlimit }}MB</span>
                  </div>
                  <div class="h-1.5 rounded-full bg-gray-200 dark:bg-white/10 overflow-hidden">
                    <div
                      class="h-full rounded-full bg-gradient-to-r from-secondary-500 to-cyan-500"
                      :style="{ width: `${Math.min(100, (parseInt(service.bwusage) / parseInt(service.bwlimit)) * 100)}%` }"
                    />
                  </div>
                </div>
              </div>

              <!-- SSL Information -->
              <div v-if="hostingInfo?.ssl" class="mt-6 pt-6 border-t border-gray-100 dark:border-white/10">
                <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200 mb-4 flex items-center gap-2">
                  <ShieldCheck v-if="hostingInfo.ssl.valid" :size="16" :stroke-width="2" class="text-green-500" />
                  <ShieldX v-else :size="16" :stroke-width="2" class="text-red-400" />
                  {{ $t('client.services.sslInfo') }}
                </h3>
                <dl class="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-4">
                  <UiDescriptionItem :label="$t('client.services.sslStatus')">
                    <span v-if="hostingInfo.ssl.valid" class="inline-flex items-center gap-1.5 text-sm text-green-600 dark:text-green-400 font-medium">
                      <span class="w-2 h-2 rounded-full bg-green-500" />
                      {{ $t('client.services.sslValid') }}
                    </span>
                    <span v-else class="inline-flex items-center gap-1.5 text-sm text-red-500 font-medium">
                      <span class="w-2 h-2 rounded-full bg-red-500" />
                      {{ $t('client.services.sslInvalid') }}
                    </span>
                  </UiDescriptionItem>
                  <UiDescriptionItem :label="$t('client.services.sslIssuer')" :value="hostingInfo.ssl.issuer || '\u2014'" />
                  <UiDescriptionItem :label="$t('client.services.sslStartDate')" :value="hostingInfo.ssl.startDate" />
                  <UiDescriptionItem :label="$t('client.services.sslExpiryDate')" :value="hostingInfo.ssl.expiryDate" />
                </dl>
              </div>
              <div v-else-if="service.domain && !hostingInfo?.ssl" class="mt-6 pt-6 border-t border-gray-100 dark:border-white/10">
                <div class="flex items-center gap-2 text-sm text-gray-400">
                  <ShieldAlert :size="16" :stroke-width="2" />
                  {{ $t('client.services.sslNotDetected') }}
                </div>
              </div>

              <!-- Visit Website / Manage Domain -->
              <div v-if="service.domain" class="mt-6 pt-6 border-t border-gray-100 dark:border-white/10 flex flex-wrap gap-3">
                <a
                  :href="`https://${service.domain}`"
                  target="_blank"
                  rel="noopener"
                  class="inline-flex items-center gap-2 px-4 py-2 rounded-lg border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
                >
                  <Globe :size="15" :stroke-width="2" />
                  {{ $t('client.services.visitWebsite') }}
                </a>
                <NuxtLink
                  to="/client/domains"
                  class="inline-flex items-center gap-2 px-4 py-2 rounded-lg border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
                >
                  <Settings :size="15" :stroke-width="2" />
                  {{ $t('client.services.manageDomain') }}
                </NuxtLink>
              </div>
            </UiCard>

            <!-- SSH Access -->
            <UiCard v-if="sshInfo?.hasAccess" class="mb-4">
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <Terminal :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.services.sshAccess') }}
                <span class="ml-auto text-xs font-medium px-2 py-0.5 rounded-full" :class="sshInfo.shellType === 'full' ? 'bg-green-100 dark:bg-green-500/15 text-green-700 dark:text-green-400' : 'bg-yellow-100 dark:bg-yellow-500/15 text-yellow-700 dark:text-yellow-400'">
                  {{ sshInfo.shellType === 'full' ? $t('client.services.sshFull') : $t('client.services.sshJailed') }}
                </span>
              </h2>
              <dl class="grid grid-cols-1 sm:grid-cols-2 gap-x-10 gap-y-5 mb-5">
                <UiDescriptionItem :label="$t('client.services.sshHost')" :value="sshInfo.host" value-class="font-mono text-xs" />
                <UiDescriptionItem :label="$t('client.services.sshPort')" :value="String(sshInfo.port)" value-class="font-mono text-xs" />
                <UiDescriptionItem :label="$t('client.services.sshUsername')" :value="sshInfo.username" value-class="font-mono text-xs" />
              </dl>
              <!-- Connection command with copy button -->
              <div class="flex items-center gap-2 p-3 rounded-lg bg-gray-900 dark:bg-black/40 border border-gray-200 dark:border-white/10">
                <code class="flex-1 text-xs text-green-400 font-mono truncate">ssh {{ sshInfo.username }}@{{ sshInfo.host }} -p {{ sshInfo.port }}</code>
                <button
                  type="button"
                  class="flex-shrink-0 flex items-center gap-1.5 text-xs text-gray-400 hover:text-white transition-colors"
                  @click="copySSHCommand"
                >
                  <CheckCircle v-if="sshCopied" :size="14" :stroke-width="2" class="text-green-400" />
                  <Copy v-else :size="14" :stroke-width="2" />
                  {{ sshCopied ? $t('client.services.sshCopied') : '' }}
                </button>
              </div>
            </UiCard>

            <!-- Configurable Options -->
            <UiCard v-if="configOptions.length" class="mb-4">
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <SlidersHorizontal :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.services.configOptions') }}
              </h2>
              <dl class="space-y-3">
                <div v-for="opt in configOptions" :key="opt.id" class="flex justify-between text-sm">
                  <dt class="text-gray-500">{{ opt.option }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ opt.value }}</dd>
                </div>
              </dl>
            </UiCard>

            <!-- What would you like to do today? -->
            <div class="mb-4 p-5 rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5">
              <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200 mb-3">{{ $t('client.services.whatToday') }}</h3>
              <ul class="space-y-2">
                <li v-if="service.serverhostname">
                  <button :disabled="ssoLoading" class="text-primary-600 dark:text-primary-400 text-sm hover:underline flex items-center gap-1.5 disabled:opacity-60" @click="loginToCpanel">
                    <Loader v-if="ssoLoading" :size="14" :stroke-width="2" class="animate-spin" />
                    <ChevronRight v-else :size="14" :stroke-width="2" />
                    {{ $t('client.services.quickCpanel') }}
                  </button>
                </li>
                <li>
                  <button class="text-primary-600 dark:text-primary-400 text-sm hover:underline flex items-center gap-1.5" @click="loadInvoices">
                    <ChevronRight :size="14" :stroke-width="2" />
                    {{ $t('client.services.quickViewInvoices') }}
                  </button>
                </li>
                <li>
                  <button class="text-primary-600 dark:text-primary-400 text-sm hover:underline flex items-center gap-1.5" @click="activeTab = 'cancel'">
                    <ChevronRight :size="14" :stroke-width="2" />
                    {{ $t('client.services.quickCancel') }}
                  </button>
                </li>
              </ul>
            </div>

            <!-- Related Invoices -->
            <UiCard>
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <FileText :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.services.relatedInvoices') }}
              </h2>

              <div v-if="invoicesLoading" class="space-y-3">
                <div v-for="i in 3" :key="i" class="h-10 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
              </div>

              <div v-else-if="!relatedInvoices.length" class="text-center py-6 text-gray-400 text-sm">
                {{ $t('client.services.noInvoices') }}
              </div>

              <div v-else class="overflow-x-auto">
                <table class="w-full text-sm">
                  <thead>
                    <tr class="border-b border-gray-100 dark:border-white/10">
                      <th class="text-left text-xs font-semibold text-gray-500 uppercase tracking-wide pb-3">{{ $t('client.services.invoiceId') }}</th>
                      <th class="text-left text-xs font-semibold text-gray-500 uppercase tracking-wide pb-3">{{ $t('client.services.invoiceDate') }}</th>
                      <th class="text-left text-xs font-semibold text-gray-500 uppercase tracking-wide pb-3">{{ $t('client.services.invoiceDue') }}</th>
                      <th class="text-right text-xs font-semibold text-gray-500 uppercase tracking-wide pb-3">{{ $t('client.services.invoiceAmount') }}</th>
                      <th class="text-right text-xs font-semibold text-gray-500 uppercase tracking-wide pb-3">{{ $t('client.services.invoiceStatus') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="inv in relatedInvoices"
                      :key="inv.id"
                      class="border-b border-gray-50 dark:border-white/5 last:border-0"
                    >
                      <td class="py-3">
                        <NuxtLink
                          :to="`/client/invoices/${inv.id}`"
                          class="text-primary-600 dark:text-primary-400 hover:underline font-medium"
                        >
                          #{{ inv.id }}
                        </NuxtLink>
                      </td>
                      <td class="py-3 text-gray-500 dark:text-gray-400">{{ inv.date }}</td>
                      <td class="py-3 text-gray-500 dark:text-gray-400">{{ inv.duedate }}</td>
                      <td class="py-3 text-right text-gray-900 dark:text-white font-medium">{{ inv.currencyprefix }}{{ inv.total }}{{ inv.currencysuffix }}</td>
                      <td class="py-3 text-right"><ClientStatusBadge :status="inv.status" /></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </UiCard>
          </template>

          <!-- ── Change Password ───────────────────────── -->
          <template v-else-if="activeTab === 'password'">
            <UiCard>
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <KeyRound :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                {{ $t('client.services.passwordTitle') }}
              </h2>

              <!-- Success state -->
              <div v-if="passwordSuccess" class="text-center py-6">
                <div class="w-14 h-14 rounded-full bg-green-500/10 border border-green-500/20 flex items-center justify-center mx-auto mb-4">
                  <CheckCircle :size="28" :stroke-width="2" class="text-green-400" />
                </div>
                <p class="text-sm text-green-400">{{ $t('client.services.passwordSuccess') }}</p>
              </div>

              <template v-else>
                <UiForm :error="passwordError" spacing="md" @submit="changePassword">
                  <UiInput
                    v-model="newPassword"
                    type="password"
                    :label="$t('client.services.newPassword')"
                    :placeholder="$t('client.services.newPasswordPlaceholder')"
                    required
                  />
                  <UiInput
                    v-model="confirmPassword"
                    type="password"
                    :label="$t('client.services.confirmPassword')"
                    :placeholder="$t('client.services.confirmPasswordPlaceholder')"
                    required
                  />

                  <template #actions>
                    <UiButton type="submit" :loading="passwordChanging">
                      <KeyRound v-if="!passwordChanging" :size="14" :stroke-width="2" class="mr-1.5" />
                      {{ passwordChanging ? $t('client.services.passwordChanging') : $t('client.services.passwordBtn') }}
                    </UiButton>
                  </template>
                </UiForm>
              </template>
            </UiCard>
          </template>

          <!-- ── Request Cancellation ──────────────────── -->
          <template v-else-if="activeTab === 'cancel'">
            <UiCard>
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <XCircle :size="18" :stroke-width="2" class="text-red-400" />
                {{ $t('client.services.cancelTitle') }}
              </h2>

              <!-- Success state -->
              <div v-if="cancelDone" class="text-center py-6">
                <div class="w-14 h-14 rounded-full bg-green-500/10 border border-green-500/20 flex items-center justify-center mx-auto mb-4">
                  <CheckCircle :size="28" :stroke-width="2" class="text-green-400" />
                </div>
                <p class="text-sm text-green-400">{{ $t('client.services.cancelSuccess') }}</p>
              </div>

              <template v-else>
                <div class="text-sm text-gray-500 dark:text-gray-400 mb-5">
                  {{ $t('client.services.cancelDesc') }}
                </div>

                <UiForm :error="cancelError" spacing="sm" @submit="showCancelConfirm = true">
                  <!-- Cancellation type -->
                  <div class="space-y-2.5">
                    <label class="text-sm font-medium text-gray-700 dark:text-gray-200">{{ $t('client.services.cancelType') }}</label>
                    <UiRadio
                      name="cancel-type"
                      value="End of Billing Period"
                      :model-value="cancelType"
                      :label="$t('client.services.cancelEndOfPeriod')"
                      @update:model-value="cancelType = 'End of Billing Period'"
                    />
                    <div class="ml-7 text-xs text-gray-400">{{ $t('client.services.cancelEndOfPeriodDesc') }}</div>
                    <UiRadio
                      name="cancel-type"
                      value="Immediate"
                      :model-value="cancelType"
                      :label="$t('client.services.cancelImmediate')"
                      @update:model-value="cancelType = 'Immediate'"
                    />
                    <div class="ml-7 text-xs text-gray-400">{{ $t('client.services.cancelImmediateDesc') }}</div>
                  </div>

                  <!-- Reason -->
                  <UiTextarea
                    v-model="cancelReason"
                    :label="$t('client.services.cancelReason')"
                    :placeholder="$t('client.services.cancelReasonPlaceholder')"
                    :rows="4"
                  />

                  <template #actions>
                    <UiButton type="submit" variant="danger" :loading="cancelSubmitting">
                      <XCircle v-if="!cancelSubmitting" :size="14" :stroke-width="2" class="mr-1.5" />
                      {{ cancelSubmitting ? $t('client.services.cancelSubmitting') : $t('client.services.cancelBtn') }}
                    </UiButton>
                  </template>
                </UiForm>
              </template>
            </UiCard>

            <!-- Cancel confirm modal -->
            <UiConfirmModal
              :open="showCancelConfirm"
              :title="$t('client.services.cancelConfirmTitle')"
              :description="$t('client.services.cancelConfirmDesc')"
              variant="danger"
              :loading="cancelSubmitting"
              @confirm="submitCancellation"
              @cancel="showCancelConfirm = false"
            />
          </template>

        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  ArrowLeft, AlertCircle, Settings, Zap, LayoutGrid, ChevronRight,
  ExternalLink, MessageSquare, Link2, Monitor, Mail, Loader,
  KeyRound, XCircle, CheckCircle, SlidersHorizontal, Terminal, Copy,
  FileText, Server as ServerIcon, Globe, ShieldCheck, ShieldX, ShieldAlert,
} from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const route = useRoute()
const store = useClientStore()
const { format: formatAmount } = useCurrency()
const serviceId = route.params.id as string

// ── Data ──────────────────────────────────────────────────────────────────────
const { data: service, pending, error } = await useApi<{
  id: number
  pid: number
  regdate: string
  name: string
  groupname: string
  domain: string
  dedicatedip: string
  serverid: number
  servername: string
  serverip: string
  serverhostname: string
  suspensionreason: string
  firstpaymentamount: string
  recurringamount: string
  paymentmethod: string
  paymentmethodname: string
  billingcycle: string
  nextduedate: string
  status: string
  username: string
  diskusage: string
  disklimit: string
  bwusage: string
  bwlimit: string
  lastupdate: string
  configoptions?: {
    configoption?: Array<{ id: number; option: string; type: string; value: string }>
  }
}>(`/api/portal/client/services/${serviceId}`)

await useAsyncData('client-user', () => store.fetchUser())

// ── Hosting Info (nameservers + SSL) ──────────────────────────────────────────
const { data: hostingInfo } = await useApi<{
  nameservers: string[]
  ssl: { valid: boolean; issuer: string; startDate: string; expiryDate: string } | null
}>(`/api/portal/client/services/${serviceId}/hosting-info`, { default: () => ({ nameservers: [], ssl: null }) })

// ── Cancellation status ──────────────────────────────────────────────────────
const { data: cancelStatus } = await useApi<{
  pending: boolean
  type?: string
  reason?: string
  date?: string
}>(`/api/portal/client/services/${serviceId}/cancellation-status`, { default: () => ({ pending: false }) })

// ── SSH Info ──────────────────────────────────────────────────────────────────
const { data: sshInfo } = await useApi<{
  hasAccess: boolean
  host?: string
  username?: string
  port?: number
  shellType?: 'full' | 'jailed'
}>(`/api/portal/client/services/${serviceId}/ssh-info`, { default: () => ({ hasAccess: false }) })

const sshCopied = ref(false)

function copySSHCommand() {
  if (!sshInfo.value?.hasAccess) return
  const cmd = `ssh ${sshInfo.value.username}@${sshInfo.value.host} -p ${sshInfo.value.port}`
  navigator.clipboard.writeText(cmd).then(() => {
    sshCopied.value = true
    setTimeout(() => { sshCopied.value = false }, 2000)
  })
}

// ── Computed ──────────────────────────────────────────────────────────────────
const configOptions = computed(() => service.value?.configoptions?.configoption || [])

// ── Tabs ──────────────────────────────────────────────────────────────────────
const activeTab = ref('overview')

const tabs = computed(() => [
  { key: 'overview',  label: t('client.services.tabOverview'),        icon: LayoutGrid },
  { key: 'password',  label: t('client.services.tabChangePassword'),  icon: KeyRound   },
  { key: 'cancel',    label: t('client.services.tabCancel'),          icon: XCircle    },
])

// ── Password Change ───────────────────────────────────────────────────────────
const newPassword = ref('')
const confirmPassword = ref('')
const passwordChanging = ref(false)
const passwordSuccess = ref(false)
const passwordError = ref('')

async function changePassword() {
  passwordError.value = ''

  if (newPassword.value.length < 8) {
    passwordError.value = t('client.services.passwordTooShort')
    return
  }
  if (newPassword.value !== confirmPassword.value) {
    passwordError.value = t('client.services.passwordMismatch')
    return
  }

  passwordChanging.value = true
  try {
    await apiFetch(`/api/portal/client/services/${serviceId}/change-password`, {
      method: 'POST',
      body: { password: newPassword.value },
    })
    passwordSuccess.value = true
  } catch (err: any) {
    passwordError.value = err?.data?.statusMessage || t('client.services.passwordError')
  } finally {
    passwordChanging.value = false
  }
}

// ── Cancellation ──────────────────────────────────────────────────────────────
const cancelType = ref('End of Billing Period')
const cancelReason = ref('')
const cancelSubmitting = ref(false)
const cancelError = ref('')
const cancelDone = ref(false)
const showCancelConfirm = ref(false)

async function submitCancellation() {
  showCancelConfirm.value = false
  cancelSubmitting.value = true
  cancelError.value = ''
  try {
    await apiFetch(`/api/portal/client/services/${serviceId}/cancel`, {
      method: 'POST',
      body: { type: cancelType.value, reason: cancelReason.value },
    })
    cancelDone.value = true
  } catch (err: any) {
    cancelError.value = err?.data?.statusMessage || t('client.services.cancelError')
  } finally {
    cancelSubmitting.value = false
  }
}

// ── Related Invoices ──────────────────────────────────────────────────────────
const relatedInvoices = ref<Array<{
  id: number; date: string; duedate: string; total: string; status: string
  currencyprefix: string; currencysuffix: string
}>>([])
const invoicesLoading = ref(true)
const invoicesLoaded = ref(false)

async function loadInvoices() {
  if (invoicesLoaded.value) return
  invoicesLoading.value = true
  try {
    relatedInvoices.value = await apiFetch(`/api/portal/client/services/${serviceId}/invoices`)
    invoicesLoaded.value = true
  } catch { /* silently fail */ } finally {
    invoicesLoading.value = false
  }
}

// ── cPanel SSO ────────────────────────────────────────────────────────────────
const ssoLoading = ref(false)

async function loginToCpanel() {
  if (ssoLoading.value) return
  ssoLoading.value = true
  try {
    const { url } = await apiFetch<{ url: string }>(`/api/portal/client/services/${serviceId}/cpanel-sso`)
    window.open(url, '_blank', 'noopener')
  } catch (err: any) {
    // Fallback: open plain cPanel login page if SSO fails
    const hostname = service.value?.serverhostname
    if (hostname) window.open(`https://${hostname}:2083`, '_blank', 'noopener')
  } finally {
    ssoLoading.value = false
  }
}

// Check if setup is needed
watch(service, (s) => {
  if (!pending.value && s && s.status !== 'Terminated' && s.status !== 'Cancelled' && !s.username) {
    // Only redirect hosting products (they usually have a username field in WHMCS if provisioned)
    // If username is empty, it needs setup.
    navigateTo(`/client/services/${serviceId}/setup`)
  }
}, { immediate: true })

// Load invoices when overview tab is active
onMounted(() => loadInvoices())
</script>
