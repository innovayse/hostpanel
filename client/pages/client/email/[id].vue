<template>
  <div>
    <NuxtLink
      to="/client/email"
      class="inline-flex items-center gap-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white text-sm transition-colors mb-6"
    >
      <ArrowLeft :size="16" :stroke-width="2" />
      Back to Email Domains
    </NuxtLink>

    <!-- Loading -->
    <div v-if="pending" class="space-y-4">
      <div class="h-20 rounded-2xl bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 animate-pulse" />
      <div class="h-80 rounded-2xl bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 animate-pulse" />
    </div>

    <!-- Error / Not found -->
    <div v-else-if="!emailDomain" class="text-center py-20">
      <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
      <p class="text-gray-500 dark:text-gray-400">Email domain not found.</p>
    </div>

    <div v-else>
      <!-- Page header -->
      <div class="mb-6 flex items-center justify-between gap-4 flex-wrap">
        <div class="flex items-center gap-4">
          <div class="w-12 h-12 rounded-xl bg-primary-500/10 border border-primary-500/20 flex items-center justify-center flex-shrink-0">
            <Mail :size="22" :stroke-width="2" class="text-primary-400" />
          </div>
          <div>
            <h1 class="text-xl font-bold text-gray-900 dark:text-white">{{ emailDomain.domain }}</h1>
            <p class="text-gray-500 dark:text-gray-400 text-sm">Created {{ formatDate(emailDomain.createdAt) }}</p>
          </div>
        </div>
        <ClientStatusBadge :status="emailDomain.status" />
      </div>

      <!-- Layout: sidebar + content -->
      <div class="flex gap-6 items-start flex-col lg:flex-row">

        <!-- Left sidebar -->
        <div class="w-full lg:w-56 flex-shrink-0">
          <div class="rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 overflow-hidden">
            <div class="px-4 py-2.5 border-b border-gray-100 dark:border-white/10 flex items-center gap-2">
              <Settings :size="13" :stroke-width="2" class="text-gray-400 dark:text-gray-500" />
              <span class="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">
                Manage
              </span>
            </div>
            <nav class="p-1">
              <button
                v-for="tab in tabs"
                :key="tab.key"
                :disabled="tab.key !== 'dns' && !isActive"
                class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors text-left"
                :class="[
                  activeTab === tab.key
                    ? 'bg-primary-50 dark:bg-primary-500/10 text-primary-600 dark:text-primary-400 font-medium'
                    : 'text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/5',
                  tab.key !== 'dns' && !isActive ? 'opacity-40 cursor-not-allowed' : ''
                ]"
                @click="tab.key === 'dns' || isActive ? activeTab = tab.key : null"
              >
                <component :is="tab.icon" :size="15" :stroke-width="2" class="flex-shrink-0" />
                {{ tab.label }}
                <span v-if="tab.key !== 'dns' && !isActive" class="ml-auto text-[10px] text-gray-500 dark:text-gray-500">DNS required</span>
              </button>
            </nav>
          </div>
        </div>

        <!-- Main content -->
        <div class="flex-1 min-w-0">

          <!-- ── DNS Configuration ────────────────────────── -->
          <template v-if="activeTab === 'dns'">
            <UiCard>
              <h2 class="text-base font-bold text-gray-900 dark:text-white mb-5 flex items-center gap-2">
                <Shield :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                DNS Configuration
              </h2>

              <!-- DNS all-pass success alert -->
              <UiAlert
                v-if="dnsAllVerified"
                variant="success"
                title="DNS verified! Email domain is now active."
                :icon-size="16"
                class="mb-5"
              />

              <!-- DNS records table -->
              <div v-if="dnsLoading" class="space-y-3 mb-5">
                <div v-for="i in 4" :key="i" class="h-10 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
              </div>

              <div v-else-if="dnsRecords.length" class="overflow-x-auto mb-5">
                <table class="w-full text-sm">
                  <thead>
                    <tr class="border-b border-gray-200 dark:border-white/10">
                      <th class="text-left pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide w-20">Type</th>
                      <th class="text-left pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide w-40">Name</th>
                      <th class="text-left pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">Value</th>
                      <th class="text-center pb-3 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide w-20">Status</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-gray-100 dark:divide-white/5">
                    <tr v-for="record in dnsRecords" :key="record.type + record.name">
                      <td class="py-3 pr-4 font-mono text-xs text-gray-600 dark:text-gray-400">
                        <span class="inline-flex items-center px-2 py-0.5 rounded-md bg-gray-100 dark:bg-white/10 text-gray-700 dark:text-gray-300 font-mono text-xs">
                          {{ record.type }}
                        </span>
                      </td>
                      <td class="py-3 pr-4 font-mono text-xs text-gray-700 dark:text-gray-300 break-all">
                        {{ record.name }}
                      </td>
                      <td class="py-3 pr-4">
                        <div class="flex items-center gap-2">
                          <span class="font-mono text-xs text-gray-700 dark:text-gray-300 break-all">{{ record.value }}</span>
                          <button
                            class="flex-shrink-0 p-1.5 rounded-lg text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
                            :title="copiedKey === record.type + record.name ? 'Copied!' : 'Copy to clipboard'"
                            @click="copyToClipboard(record.value, record.type + record.name)"
                          >
                            <Check v-if="copiedKey === record.type + record.name" :size="13" :stroke-width="2" class="text-green-500" />
                            <Copy v-else :size="13" :stroke-width="2" />
                          </button>
                        </div>
                      </td>
                      <td class="py-3 text-center">
                        <CheckCircle
                          v-if="record.verified === true"
                          :size="18"
                          :stroke-width="2"
                          class="text-green-500 mx-auto"
                        />
                        <XCircle
                          v-else-if="record.verified === false"
                          :size="18"
                          :stroke-width="2"
                          class="text-red-400 mx-auto"
                        />
                        <span
                          v-else
                          class="inline-block w-4 h-4 rounded-full border-2 border-gray-300 dark:border-gray-600 mx-auto"
                        />
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <div v-else class="text-center py-8 mb-5">
                <Shield :size="32" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-2" />
                <p class="text-sm text-gray-500 dark:text-gray-400">No DNS records found.</p>
              </div>

              <!-- Verify DNS button -->
              <div class="flex justify-start">
                <UiButton
                  size="sm"
                  :loading="dnsVerifying"
                  @click="verifyDns"
                >
                  <ShieldCheck v-if="!dnsVerifying" :size="14" :stroke-width="2" class="mr-1.5" />
                  {{ dnsVerifying ? 'Verifying…' : 'Verify DNS' }}
                </UiButton>
              </div>

              <!-- Connection settings (only when Active) -->
              <template v-if="isActive">
                <div class="mt-6 pt-6 border-t border-gray-100 dark:border-white/10">
                  <h3 class="text-sm font-semibold text-gray-700 dark:text-gray-200 mb-4 flex items-center gap-2">
                    <Server :size="15" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                    Connection Settings
                  </h3>
                  <div class="space-y-3">
                    <!-- Incoming -->
                    <div class="rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 p-4">
                      <div class="flex items-center gap-2 mb-2">
                        <Download :size="14" :stroke-width="2" class="text-primary-400" />
                        <span class="text-xs font-semibold text-gray-600 dark:text-gray-300 uppercase tracking-wide">Incoming (IMAP)</span>
                      </div>
                      <div class="grid grid-cols-1 sm:grid-cols-3 gap-2 text-sm">
                        <div>
                          <span class="text-xs text-gray-500 dark:text-gray-400 block">Server</span>
                          <span class="text-gray-900 dark:text-white font-mono text-xs">{{ mailHostname }}</span>
                        </div>
                        <div>
                          <span class="text-xs text-gray-500 dark:text-gray-400 block">Port</span>
                          <span class="text-gray-900 dark:text-white font-mono text-xs">993</span>
                        </div>
                        <div>
                          <span class="text-xs text-gray-500 dark:text-gray-400 block">Security</span>
                          <span class="text-gray-900 dark:text-white font-mono text-xs">SSL</span>
                        </div>
                      </div>
                    </div>

                    <!-- Outgoing -->
                    <div class="rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 p-4">
                      <div class="flex items-center gap-2 mb-2">
                        <Upload :size="14" :stroke-width="2" class="text-primary-400" />
                        <span class="text-xs font-semibold text-gray-600 dark:text-gray-300 uppercase tracking-wide">Outgoing (SMTP)</span>
                      </div>
                      <div class="grid grid-cols-1 sm:grid-cols-3 gap-2 text-sm">
                        <div>
                          <span class="text-xs text-gray-500 dark:text-gray-400 block">Server</span>
                          <span class="text-gray-900 dark:text-white font-mono text-xs">{{ mailHostname }}</span>
                        </div>
                        <div>
                          <span class="text-xs text-gray-500 dark:text-gray-400 block">Port</span>
                          <span class="text-gray-900 dark:text-white font-mono text-xs">587</span>
                        </div>
                        <div>
                          <span class="text-xs text-gray-500 dark:text-gray-400 block">Security</span>
                          <span class="text-gray-900 dark:text-white font-mono text-xs">STARTTLS</span>
                        </div>
                      </div>
                    </div>

                    <!-- Webmail -->
                    <div class="rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 p-4">
                      <div class="flex items-center gap-2 mb-2">
                        <Globe :size="14" :stroke-width="2" class="text-primary-400" />
                        <span class="text-xs font-semibold text-gray-600 dark:text-gray-300 uppercase tracking-wide">Webmail</span>
                      </div>
                      <a
                        :href="`https://${mailHostname}`"
                        target="_blank"
                        rel="noopener noreferrer"
                        class="text-primary-600 dark:text-primary-400 hover:underline text-sm font-mono"
                      >
                        https://{{ mailHostname }}
                      </a>
                    </div>
                  </div>
                </div>
              </template>
            </UiCard>
          </template>

          <!-- ── Mailboxes ────────────────────────────────── -->
          <template v-else-if="activeTab === 'mailboxes'">
            <UiCard>
              <div class="flex items-center justify-between mb-5">
                <h2 class="text-base font-bold text-gray-900 dark:text-white flex items-center gap-2">
                  <Inbox :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                  Mailboxes
                </h2>
                <UiButton size="sm" @click="openCreateMailbox">
                  <Plus :size="14" :stroke-width="2" class="mr-1.5" />
                  Create Mailbox
                </UiButton>
              </div>

              <!-- Loading -->
              <div v-if="mailboxesLoading" class="space-y-3">
                <div v-for="i in 3" :key="i" class="h-12 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
              </div>

              <!-- Empty -->
              <div v-else-if="!mailboxes.length" class="text-center py-10">
                <Inbox :size="36" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-3" />
                <p class="text-sm text-gray-500 dark:text-gray-400 mb-4">No mailboxes yet.</p>
                <UiButton size="sm" variant="subtle" @click="openCreateMailbox">
                  <Plus :size="14" :stroke-width="2" class="mr-1.5" />
                  Create your first mailbox
                </UiButton>
              </div>

              <!-- Table -->
              <div v-else class="overflow-x-auto">
                <table class="w-full text-sm">
                  <thead>
                    <tr class="border-b border-gray-200 dark:border-white/10">
                      <th class="text-left pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">Email</th>
                      <th class="text-left pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide hidden sm:table-cell">Display Name</th>
                      <th class="text-left pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide hidden md:table-cell">Quota (MB)</th>
                      <th class="text-center pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">Status</th>
                      <th class="text-right pb-3 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-gray-100 dark:divide-white/5">
                    <tr v-for="mb in mailboxes" :key="mb.id">
                      <td class="py-3 pr-4 text-gray-900 dark:text-white font-medium text-sm">{{ mb.email }}</td>
                      <td class="py-3 pr-4 text-gray-600 dark:text-gray-400 text-sm hidden sm:table-cell">{{ mb.displayName || '—' }}</td>
                      <td class="py-3 pr-4 text-gray-600 dark:text-gray-400 text-sm hidden md:table-cell">{{ mb.quotaMb }}</td>
                      <td class="py-3 pr-4 text-center">
                        <ClientStatusBadge :status="mb.isActive ? 'Active' : 'Inactive'" />
                      </td>
                      <td class="py-3 text-right">
                        <div class="flex items-center justify-end gap-2">
                          <button
                            class="px-2.5 py-1.5 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-xs hover:border-primary-500/30 hover:text-primary-600 dark:hover:text-primary-400 transition-all"
                            @click="openChangePassword(mb)"
                          >
                            <KeyRound :size="12" :stroke-width="2" class="inline-block mr-1" />
                            Password
                          </button>
                          <button
                            class="px-2.5 py-1.5 rounded-lg border border-red-200 dark:border-red-500/20 text-red-500 dark:text-red-400 text-xs hover:bg-red-50 dark:hover:bg-red-500/10 transition-all"
                            @click="openDeleteMailbox(mb)"
                          >
                            <Trash2 :size="12" :stroke-width="2" class="inline-block mr-1" />
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </UiCard>
          </template>

          <!-- ── Aliases ─────────────────────────────────── -->
          <template v-else-if="activeTab === 'aliases'">
            <UiCard>
              <div class="flex items-center justify-between mb-5">
                <h2 class="text-base font-bold text-gray-900 dark:text-white flex items-center gap-2">
                  <ArrowRightLeft :size="18" :stroke-width="2" class="text-cyan-500 dark:text-cyan-400" />
                  Aliases
                </h2>
                <UiButton size="sm" @click="openCreateAlias">
                  <Plus :size="14" :stroke-width="2" class="mr-1.5" />
                  Create Alias
                </UiButton>
              </div>

              <!-- Loading -->
              <div v-if="aliasesLoading" class="space-y-3">
                <div v-for="i in 3" :key="i" class="h-12 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
              </div>

              <!-- Empty -->
              <div v-else-if="!aliases.length" class="text-center py-10">
                <ArrowRightLeft :size="36" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-3" />
                <p class="text-sm text-gray-500 dark:text-gray-400 mb-4">No aliases yet.</p>
                <UiButton size="sm" variant="subtle" @click="openCreateAlias">
                  <Plus :size="14" :stroke-width="2" class="mr-1.5" />
                  Create your first alias
                </UiButton>
              </div>

              <!-- Table -->
              <div v-else class="overflow-x-auto">
                <table class="w-full text-sm">
                  <thead>
                    <tr class="border-b border-gray-200 dark:border-white/10">
                      <th class="text-left pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">Source</th>
                      <th class="text-left pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">Destination</th>
                      <th class="text-center pb-3 pr-4 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">Status</th>
                      <th class="text-right pb-3 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-gray-100 dark:divide-white/5">
                    <tr v-for="alias in aliases" :key="alias.id">
                      <td class="py-3 pr-4 text-gray-900 dark:text-white font-medium text-sm">{{ alias.sourceAddress }}</td>
                      <td class="py-3 pr-4 text-gray-600 dark:text-gray-400 text-sm">{{ alias.destinationAddress }}</td>
                      <td class="py-3 pr-4 text-center">
                        <ClientStatusBadge :status="alias.isActive ? 'Active' : 'Inactive'" />
                      </td>
                      <td class="py-3 text-right">
                        <button
                          class="px-2.5 py-1.5 rounded-lg border border-red-200 dark:border-red-500/20 text-red-500 dark:text-red-400 text-xs hover:bg-red-50 dark:hover:bg-red-500/10 transition-all"
                          @click="openDeleteAlias(alias)"
                        >
                          <Trash2 :size="12" :stroke-width="2" class="inline-block mr-1" />
                          Delete
                        </button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </UiCard>
          </template>

        </div>
      </div>
    </div>

    <!-- ── Create Mailbox Modal ────────────────────────────── -->
    <UiModal v-model="showCreateMailbox" title="Create Mailbox" size="md">
      <UiForm :error="mailboxCreateError" @submit="submitCreateMailbox">
        <!-- Local part + domain suffix -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Email Address</label>
          <div class="flex items-center gap-0">
            <input
              v-model="mailboxForm.localPart"
              type="text"
              placeholder="user"
              required
              :disabled="mailboxCreating"
              class="flex-1 block border border-r-0 rounded-l-lg px-3 py-1.5 text-sm border-gray-300 dark:border-gray-700 bg-white dark:bg-white/5 text-gray-900 dark:text-white placeholder:text-gray-400 dark:placeholder:text-gray-500 focus:outline-none focus:ring-2 focus:ring-primary-500/50 focus:border-primary-500 transition-all"
            />
            <span class="inline-flex items-center px-3 py-1.5 text-sm border border-gray-300 dark:border-gray-700 bg-gray-50 dark:bg-white/5 text-gray-500 dark:text-gray-400 rounded-r-lg border-l-0">
              @{{ emailDomain?.domain }}
            </span>
          </div>
        </div>
        <UiInput
          v-model="mailboxForm.displayName"
          label="Display Name"
          placeholder="John Doe"
          type="text"
          :disabled="mailboxCreating"
          size="sm"
        />
        <UiInput
          v-model="mailboxForm.password"
          label="Password"
          type="password"
          placeholder="Strong password"
          required
          :disabled="mailboxCreating"
          size="sm"
        />
        <UiInput
          v-model="mailboxForm.quota"
          label="Quota (MB)"
          type="number"
          placeholder="500"
          :disabled="mailboxCreating"
          size="sm"
        />
        <template #actions>
          <button
            type="button"
            class="px-4 py-2 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-sm hover:text-gray-900 dark:hover:text-white transition-colors"
            :disabled="mailboxCreating"
            @click="showCreateMailbox = false"
          >
            Cancel
          </button>
          <UiButton type="submit" :loading="mailboxCreating">
            Create Mailbox
          </UiButton>
        </template>
      </UiForm>
    </UiModal>

    <!-- ── Change Password Modal ──────────────────────────── -->
    <UiModal v-model="showChangePassword" title="Change Password" size="sm">
      <UiForm :error="changePasswordError" @submit="submitChangePassword">
        <p class="text-sm text-gray-500 dark:text-gray-400 mb-2">
          Changing password for <strong class="text-gray-900 dark:text-white">{{ selectedMailbox?.email }}</strong>
        </p>
        <UiInput
          v-model="newPassword"
          label="New Password"
          type="password"
          placeholder="New strong password"
          required
          :disabled="changePasswordSaving"
          size="sm"
        />
        <template #actions>
          <button
            type="button"
            class="px-4 py-2 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-sm hover:text-gray-900 dark:hover:text-white transition-colors"
            :disabled="changePasswordSaving"
            @click="showChangePassword = false"
          >
            Cancel
          </button>
          <UiButton type="submit" :loading="changePasswordSaving">
            Update Password
          </UiButton>
        </template>
      </UiForm>
    </UiModal>

    <!-- ── Delete Mailbox Confirm ─────────────────────────── -->
    <UiConfirmModal
      :open="showDeleteMailbox"
      title="Delete Mailbox"
      :description="`Are you sure you want to delete ${selectedMailbox?.email}? This action cannot be undone.`"
      confirm-label="Delete"
      variant="danger"
      :loading="mailboxDeleting"
      @confirm="submitDeleteMailbox"
      @cancel="showDeleteMailbox = false"
    />

    <!-- ── Create Alias Modal ─────────────────────────────── -->
    <UiModal v-model="showCreateAlias" title="Create Alias" size="md">
      <UiForm :error="aliasCreateError" @submit="submitCreateAlias">
        <UiInput
          v-model="aliasForm.source"
          label="Source Address"
          placeholder="info@example.com"
          type="text"
          required
          :disabled="aliasCreating"
          size="sm"
        />
        <UiSelect
          v-model="aliasForm.destination"
          label="Destination Address"
          placeholder="Select a mailbox"
          :options="mailboxOptions"
          required
          :disabled="aliasCreating"
          size="sm"
        />
        <template #actions>
          <button
            type="button"
            class="px-4 py-2 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-sm hover:text-gray-900 dark:hover:text-white transition-colors"
            :disabled="aliasCreating"
            @click="showCreateAlias = false"
          >
            Cancel
          </button>
          <UiButton type="submit" :loading="aliasCreating">
            Create Alias
          </UiButton>
        </template>
      </UiForm>
    </UiModal>

    <!-- ── Delete Alias Confirm ───────────────────────────── -->
    <UiConfirmModal
      :open="showDeleteAlias"
      title="Delete Alias"
      :description="`Are you sure you want to delete the alias ${selectedAlias?.sourceAddress}? This action cannot be undone.`"
      confirm-label="Delete"
      variant="danger"
      :loading="aliasDeleting"
      @confirm="submitDeleteAlias"
      @cancel="showDeleteAlias = false"
    />
  </div>
</template>

<script setup lang="ts">
import {
  ArrowLeft, AlertCircle, Mail, Settings, Shield, ShieldCheck,
  CheckCircle, XCircle, Copy, Check, Server, Download, Upload, Globe,
  Inbox, Plus, KeyRound, Trash2, ArrowRightLeft
} from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const route = useRoute()
const emailId = route.params.id as string

// ── Interfaces ────────────────────────────────────────────────────────────────

interface EmailDomainDetail {
  id: string
  domain: string
  status: string
  mailboxCount: number
  maxMailboxes: number
  createdAt: string
}

interface DnsRecord {
  type: string
  name: string
  value: string
  verified?: boolean | null
}

interface Mailbox {
  id: string
  email: string
  displayName: string
  quota: number
  status: string
}

interface Alias {
  id: string
  source: string
  destination: string
  status: string
}

// ── Domain detail ─────────────────────────────────────────────────────────────
const { data: emailData, pending } = await useApi<EmailDomainDetail>(`/api/portal/client/email/${emailId}`)
const emailDomain = computed(() => emailData.value as EmailDomainDetail | null)

const isActive = computed(() => emailDomain.value?.status?.toLowerCase() === 'active')

// ── Date helper ───────────────────────────────────────────────────────────────
function formatDate(d: string): string {
  if (!d || isNaN(new Date(d).getTime())) return '—'
  return new Date(d).toLocaleDateString()
}

// ── Tabs ──────────────────────────────────────────────────────────────────────
const activeTab = ref('dns')

const tabs = [
  { key: 'dns',       label: 'DNS Configuration', icon: Shield         },
  { key: 'mailboxes', label: 'Mailboxes',          icon: Inbox          },
  { key: 'aliases',   label: 'Aliases',            icon: ArrowRightLeft },
]

// ── DNS Configuration ─────────────────────────────────────────────────────────
const dnsRecords   = ref<DnsRecord[]>([])
const dnsLoading   = ref(false)
const dnsVerifying = ref(false)

const dnsAllVerified = computed(() =>
  dnsRecords.value.length > 0 && dnsRecords.value.every(r => r.verified === true)
)

const mailHostname = computed(() => {
  const mx = dnsRecords.value.find(r => r.type === 'MX')
  return mx?.value ?? '{{ mailHostname }}'
})

// Load DNS records on mount
onMounted(async () => {
  dnsLoading.value = true
  try {
    const data = await apiFetch<DnsRecord[]>(`/api/portal/client/email/${emailId}/dns-records`)
    dnsRecords.value = data
  } catch { /* silently ignore */ } finally {
    dnsLoading.value = false
  }
})

/** POST to verify DNS, then refresh the records with verification status. */
async function verifyDns() {
  dnsVerifying.value = true
  try {
    const data = await apiFetch<DnsRecord[]>(`/api/portal/client/email/${emailId}/verify-dns`, {
      method: 'POST'
    })
    if (data) dnsRecords.value = data
  } catch { /* keep existing state */ } finally {
    dnsVerifying.value = false
  }
}

// ── Copy to clipboard ─────────────────────────────────────────────────────────
const copiedKey = ref('')

async function copyToClipboard(text: string, key: string) {
  try {
    await navigator.clipboard.writeText(text)
    copiedKey.value = key
    setTimeout(() => { copiedKey.value = '' }, 2000)
  } catch { /* ignore */ }
}

// ── Mailboxes ─────────────────────────────────────────────────────────────────
const mailboxes        = ref<Mailbox[]>([])
const mailboxesLoading = ref(false)
const showCreateMailbox  = ref(false)
const showDeleteMailbox  = ref(false)
const showChangePassword = ref(false)
const selectedMailbox    = ref<Mailbox | null>(null)
const mailboxCreating    = ref(false)
const mailboxDeleting    = ref(false)
const changePasswordSaving = ref(false)
const mailboxCreateError   = ref('')
const changePasswordError  = ref('')
const newPassword          = ref('')

const mailboxForm = reactive({
  localPart:   '',
  displayName: '',
  password:    '',
  quota:       '500',
})

async function loadMailboxes() {
  mailboxesLoading.value = true
  try {
    const data = await apiFetch<Mailbox[]>(`/api/portal/client/email/${emailId}/mailboxes`)
    mailboxes.value = data
  } catch { /* silently ignore */ } finally {
    mailboxesLoading.value = false
  }
}

function openCreateMailbox() {
  mailboxForm.localPart   = ''
  mailboxForm.displayName = ''
  mailboxForm.password    = ''
  mailboxForm.quota       = '500'
  mailboxCreateError.value = ''
  showCreateMailbox.value  = true
}

async function submitCreateMailbox() {
  const localPart = mailboxForm.localPart.trim()
  if (!localPart) return
  mailboxCreating.value    = true
  mailboxCreateError.value = ''
  try {
    await apiFetch(`/api/portal/client/email/${emailId}/mailboxes`, {
      method: 'POST',
      body: {
        localPart,
        displayName: mailboxForm.displayName.trim(),
        password:    mailboxForm.password,
        quota:       Number(mailboxForm.quota) || 500,
      },
    })
    showCreateMailbox.value = false
    await loadMailboxes()
  } catch (err: unknown) {
    const e = err as { data?: { statusMessage?: string }; statusMessage?: string }
    mailboxCreateError.value = e?.data?.statusMessage ?? e?.statusMessage ?? 'Failed to create mailbox.'
  } finally {
    mailboxCreating.value = false
  }
}

function openChangePassword(mb: Mailbox) {
  selectedMailbox.value     = mb
  newPassword.value         = ''
  changePasswordError.value = ''
  showChangePassword.value  = true
}

async function submitChangePassword() {
  if (!selectedMailbox.value) return
  changePasswordSaving.value = true
  changePasswordError.value  = ''
  try {
    await apiFetch(
      `/api/portal/client/email/${emailId}/mailboxes/${selectedMailbox.value.id}/password`,
      { method: 'PUT', body: { newPassword: newPassword.value } }
    )
    showChangePassword.value = false
  } catch (err: unknown) {
    const e = err as { data?: { statusMessage?: string }; statusMessage?: string }
    changePasswordError.value = e?.data?.statusMessage ?? e?.statusMessage ?? 'Failed to update password.'
  } finally {
    changePasswordSaving.value = false
  }
}

function openDeleteMailbox(mb: Mailbox) {
  selectedMailbox.value   = mb
  showDeleteMailbox.value = true
}

async function submitDeleteMailbox() {
  if (!selectedMailbox.value) return
  mailboxDeleting.value = true
  try {
    await apiFetch(
      `/api/portal/client/email/${emailId}/mailboxes/${selectedMailbox.value.id}`,
      { method: 'DELETE' }
    )
    showDeleteMailbox.value = false
    selectedMailbox.value   = null
    await loadMailboxes()
  } catch { /* keep modal open on error */ } finally {
    mailboxDeleting.value = false
  }
}

// ── Aliases ───────────────────────────────────────────────────────────────────
const aliases        = ref<Alias[]>([])
const aliasesLoading = ref(false)
const showCreateAlias = ref(false)
const showDeleteAlias = ref(false)
const selectedAlias   = ref<Alias | null>(null)
const aliasCreating   = ref(false)
const aliasDeleting   = ref(false)
const aliasCreateError = ref('')

const mailboxOptions = computed(() =>
  mailboxes.value.map(mb => ({ label: `${mb.email} (${mb.displayName})`, value: mb.email }))
)

const aliasForm = reactive({
  source:      '',
  destination: '',
})

async function loadAliases() {
  aliasesLoading.value = true
  try {
    const data = await apiFetch<Alias[]>(`/api/portal/client/email/${emailId}/aliases`)
    aliases.value = data
  } catch { /* silently ignore */ } finally {
    aliasesLoading.value = false
  }
}

function openCreateAlias() {
  aliasForm.source      = ''
  aliasForm.destination = ''
  aliasCreateError.value = ''
  showCreateAlias.value  = true
}

async function submitCreateAlias() {
  const source = aliasForm.source.trim()
  const dest   = aliasForm.destination.trim()
  if (!source || !dest) return
  aliasCreating.value    = true
  aliasCreateError.value = ''
  try {
    await apiFetch(`/api/portal/client/email/${emailId}/aliases`, {
      method: 'POST',
      body: { sourceAddress: source, destinationAddress: dest },
    })
    showCreateAlias.value = false
    await loadAliases()
  } catch (err: unknown) {
    const e = err as { data?: { statusMessage?: string }; statusMessage?: string }
    aliasCreateError.value = e?.data?.statusMessage ?? e?.statusMessage ?? 'Failed to create alias.'
  } finally {
    aliasCreating.value = false
  }
}

function openDeleteAlias(alias: Alias) {
  selectedAlias.value   = alias
  showDeleteAlias.value = true
}

async function submitDeleteAlias() {
  if (!selectedAlias.value) return
  aliasDeleting.value = true
  try {
    await apiFetch(
      `/api/portal/client/email/${emailId}/aliases/${selectedAlias.value.id}`,
      { method: 'DELETE' }
    )
    showDeleteAlias.value = false
    selectedAlias.value   = null
    await loadAliases()
  } catch { /* keep modal open on error */ } finally {
    aliasDeleting.value = false
  }
}

// ── Load data when tabs are first visited ─────────────────────────────────────
let mailboxesLoaded = false
let aliasesLoaded   = false

watch(activeTab, async (tab) => {
  if (tab === 'mailboxes' && !mailboxesLoaded) {
    mailboxesLoaded = true
    await loadMailboxes()
  }
  if (tab === 'aliases' && !aliasesLoaded) {
    aliasesLoaded = true
    await loadAliases()
  }
})
</script>
