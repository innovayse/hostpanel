<template>
  <div>
    <div class="mb-6 flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ $t('client.account.title') }}</h1>
        <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.account.subtitle') }}</p>
      </div>
      <!-- Edit button — only on profile tab -->
      <UiButton v-if="activeTab === 'details' && !editing && store.user" size="sm" variant="subtle" @click="startEdit">
        <Pencil :size="14" :stroke-width="2" class="mr-1.5" />
        {{ $t('client.profile.edit') }}
      </UiButton>
    </div>

    <UiTabs v-model="activeTab" :tabs="tabs" class="mb-8" />

    <!-- ── Account Details ─────────────────────────────────── -->
    <div v-if="activeTab === 'details'">
      <div v-if="store.userLoading" class="space-y-4">
        <div class="h-24 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
        <div class="h-64 rounded-2xl bg-white/5 border border-white/10 animate-pulse" />
      </div>

      <div v-else-if="store.user">
        <!-- Avatar + name -->
        <UiCard class="mb-6 flex items-center gap-5">
          <div class="w-16 h-16 rounded-2xl bg-gradient-to-br from-cyan-500/30 to-primary-500/20 flex items-center justify-center text-white font-bold text-2xl border border-cyan-500/30 flex-shrink-0">
            {{ store.userInitial }}
          </div>
          <div>
            <h2 class="text-xl font-bold text-gray-900 dark:text-white">{{ store.fullName }}</h2>
            <p class="text-gray-500 dark:text-gray-400 text-sm mt-0.5">{{ store.user.email }}</p>
          </div>
        </UiCard>

        <!-- View mode -->
        <template v-if="!editing">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <UiCard :title="$t('client.profile.accountInfo')">
              <dl class="grid grid-cols-1 gap-4">
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.firstName') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.firstname }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.lastName') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.lastname }}</dd>
                </div>
                <div v-if="store.user.companyname">
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.companyName') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.companyname }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.email') }}</dt>
                  <dd class="text-white font-medium break-all">{{ store.user.email }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.paymentMethod') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ paymentMethodLabel }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.language') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ languageLabel }}</dd>
                </div>
              </dl>
            </UiCard>

            <UiCard :title="$t('client.profile.billingAddress')">
              <dl class="grid grid-cols-1 gap-4">
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.address1') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.address1 || '—' }}</dd>
                </div>
                <div v-if="store.user.address2">
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.address2') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.address2 }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.city') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.city || '—' }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.state') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.state || '—' }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.postcode') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.postcode || '—' }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.country') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.countryname || store.user.country || '—' }}</dd>
                </div>
                <div>
                  <dt class="text-xs text-gray-500 mb-1">{{ $t('client.profile.phone') }}</dt>
                  <dd class="text-gray-900 dark:text-white font-medium">{{ store.user.phonenumber || '—' }}</dd>
                </div>
              </dl>
            </UiCard>
          </div>

          <!-- Email preferences view -->
          <UiCard class="mt-6" :title="$t('client.profile.emailPrefs')">
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <UiCheckbox
                v-for="pref in emailPrefList"
                :key="pref.key"
                :id="`view-pref-${pref.key}`"
                :label="pref.label"
                :description="pref.description"
                :model-value="currentEmailPrefs[pref.key]"
                :disabled="true"
              />
            </div>
          </UiCard>
        </template>

        <!-- Edit mode -->
        <UiForm v-else spacing="none" :error="saveError" @submit="saveEdit">
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
            <UiCard :title="$t('client.profile.accountInfo')">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <UiInput v-model="form.firstname" :label="$t('client.profile.firstName')" type="text" size="sm" required />
                <UiInput v-model="form.lastname" :label="$t('client.profile.lastName')" type="text" size="sm" required />
                <div class="sm:col-span-2">
                  <UiInput v-model="form.companyname" :label="$t('client.profile.companyName')" type="text" size="sm" />
                </div>
                <div class="sm:col-span-2">
                  <UiInput v-model="form.email" :label="$t('client.profile.emailAddress')" type="email" size="sm" required />
                </div>
                <div class="sm:col-span-2">
                  <UiSelect v-model="form.paymentmethod" :label="$t('client.profile.paymentMethod')" size="sm" :options="paymentOptions" />
                </div>
                <div class="sm:col-span-2">
                  <UiSelect v-model="form.language" :label="$t('client.profile.language')" size="sm" :options="languageOptions" />
                </div>
              </div>
            </UiCard>

            <UiCard :title="$t('client.profile.billingAddress')">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div class="sm:col-span-2">
                  <UiInput v-model="form.address1" :label="$t('client.profile.address1')" type="text" size="sm" />
                </div>
                <div class="sm:col-span-2">
                  <UiInput v-model="form.address2" :label="$t('client.profile.address2')" type="text" size="sm" />
                </div>
                <UiInput v-model="form.city" :label="$t('client.profile.city')" type="text" size="sm" />
                <UiInput v-model="form.state" :label="$t('client.profile.state')" type="text" size="sm" />
                <UiInput v-model="form.postcode" :label="$t('client.profile.postcode')" type="text" size="sm" />
                <div class="sm:col-span-2">
                  <UiSelect v-model="form.country" :label="$t('client.profile.country')" size="sm" :options="countryOptions || []" />
                </div>
                <div class="sm:col-span-2">
                  <UiInput v-model="form.phonenumber" :label="$t('client.profile.phone')" type="tel" size="sm" />
                </div>
              </div>
            </UiCard>
          </div>

          <UiCard :title="$t('client.profile.emailPrefs')">
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <UiCheckbox
                v-for="pref in emailPrefList"
                :key="pref.key"
                :id="`edit-pref-${pref.key}`"
                :label="pref.label"
                :description="pref.description"
                :model-value="emailprefs[pref.key]"
                @update:model-value="emailprefs[pref.key] = $event"
              />
            </div>
          </UiCard>

          <div class="flex gap-3 justify-end mt-6">
            <UiButton type="button" size="sm" variant="subtle" @click="cancelEdit">
              {{ $t('client.profile.cancel') }}
            </UiButton>
            <UiButton type="submit" size="sm" :loading="saving">
              <Check v-if="!saving" :size="14" :stroke-width="2" class="mr-1.5" />
              {{ saving ? $t('client.profile.saving') : $t('client.profile.save') }}
            </UiButton>
          </div>
        </UiForm>
      </div>

      <div v-else class="text-center py-20">
        <AlertCircle :size="48" :stroke-width="2" class="text-red-400 mx-auto mb-4" />
        <p class="text-gray-400">{{ $t('client.profile.failedToLoad') }}</p>
      </div>
    </div>

    <!-- ── User Management ─────────────────────────────────── -->
    <div v-else-if="activeTab === 'users'" class="space-y-8">
      <UiCard :title="$t('client.users.currentUsers')">
        <div v-if="usersPending" class="space-y-3">
          <div v-for="i in 2" :key="i" class="h-16 rounded-xl bg-white/5 animate-pulse" />
        </div>
        <div v-else-if="!users.length" class="text-center py-10">
          <Users :size="40" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-3" />
          <p class="text-gray-400 text-sm">{{ $t('client.users.empty') }}</p>
        </div>
        <div v-else class="divide-y divide-gray-100 dark:divide-white/5">
          <div
            v-for="user in users"
            :key="user.id"
            class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 py-4 first:pt-0 last:pb-0"
          >
            <div class="flex items-center gap-3">
              <div class="w-9 h-9 rounded-full bg-gradient-to-br from-primary-500/30 to-secondary-500/20 flex items-center justify-center text-white font-bold text-sm border border-primary-500/20 flex-shrink-0">
                {{ (user.name || user.email)[0].toUpperCase() }}
              </div>
              <div>
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="text-gray-900 dark:text-white font-medium text-sm">{{ user.email }}</span>
                  <span v-if="user.isOwner" class="px-2 py-0.5 rounded-full text-xs font-semibold bg-primary-500/20 text-primary-300 border border-primary-500/30">
                    {{ $t('client.users.owner') }}
                  </span>
                </div>
                <div v-if="user.lastLogin" class="text-xs text-gray-500 mt-0.5">
                  {{ $t('client.users.lastLogin') }}: {{ user.lastLogin }}
                </div>
              </div>
            </div>
            <div v-if="!user.isOwner" class="flex-shrink-0">
              <UiButton variant="outline" size="sm" :loading="removingUserId === user.id" @click="confirmRemove(user)">
                <UserMinus :size="14" :stroke-width="2" class="mr-1.5" />
                {{ $t('client.users.removeAccess') }}
              </UiButton>
            </div>
            <span v-else class="text-xs text-gray-600 italic flex-shrink-0">{{ $t('client.users.ownerNote') }}</span>
          </div>
        </div>
      </UiCard>

      <UiCard :title="$t('client.users.inviteTitle')">
        <p class="text-gray-500 dark:text-gray-400 text-sm mb-6">{{ $t('client.users.inviteDesc') }}</p>
        <UiForm :error="inviteError" :success="inviteSuccess" spacing="md" @submit="sendInvite">
          <UiInput v-model="inviteEmail" type="email" :label="$t('client.users.emailLabel')" placeholder="name@example.com" required />
          <div>
            <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">{{ $t('client.users.permissions') }}</p>
            <div class="flex gap-6 mb-4">
              <label class="flex items-center gap-2.5 cursor-pointer group">
                <input v-model="invitePermissions" type="radio" value="all" class="w-4 h-4 accent-primary-500 cursor-pointer" />
                <span class="text-sm text-gray-700 dark:text-gray-300 group-hover:text-gray-900 dark:group-hover:text-white transition-colors">{{ $t('client.users.permAll') }}</span>
              </label>
              <label class="flex items-center gap-2.5 cursor-pointer group">
                <input v-model="invitePermissions" type="radio" value="choose" class="w-4 h-4 accent-primary-500 cursor-pointer" />
                <span class="text-sm text-gray-700 dark:text-gray-300 group-hover:text-gray-900 dark:group-hover:text-white transition-colors">{{ $t('client.users.permChoose') }}</span>
              </label>
            </div>

            <!-- Granular permissions (shown when Choose is selected) -->
            <div v-if="invitePermissions === 'choose'" class="space-y-2 pl-1">
              <div
                v-for="perm in permissionsList"
                :key="perm.key"
                class="flex items-start gap-3 py-1.5"
              >
                <UiCheckbox
                  :id="`perm-${perm.key}`"
                  v-model="chosenPerms[perm.key]"
                  :label="perm.label"
                  :description="perm.desc"
                />
              </div>
            </div>
          </div>
          <template #actions>
            <UiButton type="submit" :loading="inviting">
              <Send v-if="!inviting" :size="15" :stroke-width="2" class="mr-1.5" />
              {{ inviting ? $t('client.users.sending') : $t('client.users.sendInvite') }}
            </UiButton>
          </template>
        </UiForm>
      </UiCard>
    </div>

    <!-- ── Contacts ──────────────────────────────────────────── -->
    <div v-else-if="activeTab === 'contacts'">
      <UiCard>
        <UiCardHeader :title="$t('client.contacts.title')">
          <UiButton size="sm" variant="subtle" @click="openAddContact">
            <Plus :size="12" :stroke-width="2" class="mr-1" />
            {{ $t('client.contacts.addNew') }}
          </UiButton>
        </UiCardHeader>

        <div v-if="contactsPending" class="space-y-3">
          <div v-for="i in 2" :key="i" class="h-16 rounded-xl bg-white/5 animate-pulse" />
        </div>

        <div v-else-if="!contacts.length" class="text-center py-10">
          <Users :size="40" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-3" />
          <p class="text-gray-500 dark:text-gray-400 text-sm font-medium">{{ $t('client.contacts.empty') }}</p>
          <p class="text-gray-400 dark:text-gray-500 text-xs mt-1">{{ $t('client.contacts.emptyDesc') }}</p>
          <UiButton size="sm" variant="subtle" class="mt-4" @click="openAddContact">
            {{ $t('client.contacts.addNew') }}
          </UiButton>
        </div>

        <div v-else class="divide-y divide-gray-100 dark:divide-white/5">
          <div
            v-for="contact in contacts"
            :key="contact.id"
            class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 py-4 first:pt-0 last:pb-0"
          >
            <div class="flex items-center gap-3">
              <div class="w-9 h-9 rounded-full bg-gradient-to-br from-primary-500/30 to-secondary-500/20 flex items-center justify-center text-white font-bold text-sm border border-primary-500/20 flex-shrink-0">
                {{ (contact.firstname || contact.email)[0]?.toUpperCase() }}
              </div>
              <div>
                <div class="flex items-center gap-2 flex-wrap">
                  <span class="text-gray-900 dark:text-white font-medium text-sm">
                    {{ contact.firstname }} {{ contact.lastname }}
                  </span>
                  <span v-if="contact.companyname" class="text-gray-400 dark:text-gray-500 text-sm">· {{ contact.companyname }}</span>
                </div>
                <div class="text-xs text-gray-500 mt-0.5">
                  <span>{{ contact.email }}</span>
                  <span v-if="contact.phonenumber"> · {{ contact.phonenumber }}</span>
                </div>
              </div>
            </div>
            <div class="flex items-center gap-2 flex-shrink-0">
              <UiButton size="sm" variant="subtle" @click="openEditContact(contact)">
                <Pencil :size="13" :stroke-width="2" class="mr-1" />
                {{ $t('client.profile.edit') }}
              </UiButton>
              <UiButton size="sm" variant="subtle" @click="confirmDeleteContact(contact)">
                <Trash2 :size="13" :stroke-width="2" class="mr-1" />
                {{ $t('client.contacts.delete') }}
              </UiButton>
            </div>
          </div>
        </div>
      </UiCard>
    </div>

    <!-- ── Payment Methods ────────────────────────────────── -->
    <div v-else-if="activeTab === 'payment'">
      <div v-if="paymentPending" class="space-y-3">
        <div v-for="i in 3" :key="i" class="h-16 rounded-xl bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 animate-pulse" />
      </div>

      <template v-else>
        <UiCard class="mb-4">
          <UiCardHeader :title="$t('client.payment.title')">
            <UiButton size="sm" variant="subtle" @click="openAddForm">
              <Plus :size="12" :stroke-width="2" class="mr-1" />
              {{ $t('client.payment.addNew') }}
            </UiButton>
          </UiCardHeader>

          <div v-if="!paymentMethods.length" class="py-10 text-center">
            <CreditCard :size="40" :stroke-width="1.5" class="text-gray-300 dark:text-gray-600 mx-auto mb-3" />
            <p class="text-gray-500 dark:text-gray-400 text-sm font-medium">{{ $t('client.payment.empty') }}</p>
            <p class="text-gray-400 dark:text-gray-500 text-xs mt-1">{{ $t('client.payment.emptyDesc') }}</p>
            <UiButton size="sm" variant="subtle" class="mt-4" @click="openAddForm">
              {{ $t('client.payment.addNew') }}
            </UiButton>
          </div>

          <div v-else class="divide-y divide-gray-100 dark:divide-white/5">
            <div
              v-for="method in paymentMethods"
              :key="method.id"
              class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 py-4 first:pt-0 last:pb-0"
            >
              <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-xl bg-gray-100 dark:bg-white/5 border border-gray-200 dark:border-white/10 flex items-center justify-center flex-shrink-0">
                  <CreditCard :size="18" :stroke-width="1.5" class="text-gray-500 dark:text-gray-400" />
                </div>
                <div>
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="text-gray-900 dark:text-white font-medium text-sm">{{ method.description || method.gateway_name }}</span>
                    <span class="px-2 py-0.5 rounded-full text-xs border bg-green-500/10 text-green-600 dark:text-green-400 border-green-500/20">
                      {{ $t('client.payment.statusActive') }}
                    </span>
                  </div>
                  <div class="text-xs text-gray-500 mt-0.5">
                    <span>{{ method.type }}</span>
                    <span v-if="method.card_last_four"> · {{ $t('client.payment.cardEnding') }} {{ method.card_last_four }}</span>
                    <span v-if="method.card_expiry"> · {{ $t('client.payment.expires') }} {{ method.card_expiry }}</span>
                    <span v-if="method.bank_name"> · {{ method.bank_name }}</span>
                  </div>
                </div>
              </div>

              <div class="flex items-center gap-2 flex-shrink-0">
                <UiButton
                  size="sm"
                  variant="outline"
                  :loading="settingDefaultId === method.id"
                  @click="setDefaultPaymentMethod(method.id)"
                >
                  <Star :size="13" :stroke-width="2" class="mr-1" />
                  {{ $t('client.payment.setDefault') }}
                </UiButton>
                <UiButton
                  size="sm"
                  variant="subtle"
                  :loading="removingId === method.id"
                  @click="removePaymentMethod(method.id)"
                >
                  <Trash2 :size="13" :stroke-width="2" class="mr-1" />
                  {{ $t('client.payment.remove') }}
                </UiButton>
              </div>
            </div>
          </div>
        </UiCard>


        <UiAlert v-if="paymentError" variant="error">{{ paymentError }}</UiAlert>
        <UiAlert v-if="paymentSuccess" variant="success">{{ paymentSuccess }}</UiAlert>
      </template>
    </div>

    <!-- ── Email History ───────────────────────────────────── -->
    <div v-else-if="activeTab === 'emails'">
      <div v-if="emailsPending" class="space-y-3">
        <div v-for="i in 5" :key="i" class="h-16 rounded-xl bg-white/5 border border-white/10 animate-pulse" />
      </div>
      <template v-else>
        <!-- Toolbar: showing info + search -->
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-4">
          <p class="text-sm text-gray-500 dark:text-gray-400">
            {{ $t('ui.pagination.showing', { from: emailFrom, to: emailTo, total: emailFiltered.length }) }}
          </p>
          <UiSearchInput
            v-model="emailSearch"
            :placeholder="$t('client.emails.search')"
            class="sm:w-64"
            @update:model-value="emailPage = 1"
          />
        </div>

        <div v-if="!emailFiltered.length" class="text-center py-20">
          <Mail :size="48" :stroke-width="2" class="text-gray-300 dark:text-gray-600 mx-auto mb-4" />
          <p class="text-gray-400">{{ emailSearch ? $t('client.emails.noResults') : $t('client.emails.empty') }}</p>
        </div>

        <UiTable v-else>
          <UiTableHead>
            <UiTableRow :hoverable="false">
              <UiTableTh>{{ $t('client.emails.colDate') }}</UiTableTh>
              <UiTableTh>{{ $t('client.emails.colSubject') }}</UiTableTh>
              <UiTableTh />
            </UiTableRow>
          </UiTableHead>
          <UiTableBody>
            <UiTableRow v-for="email in emailPaged" :key="email.id">
              <UiTableTd class="text-gray-500 dark:text-gray-400 whitespace-nowrap text-sm">{{ email.date }}</UiTableTd>
              <UiTableTd class="text-gray-900 dark:text-white font-medium">{{ email.subject }}</UiTableTd>
              <UiTableTd align="right">
                <NuxtLink
                  :to="`/client/emails/${email.id}`"
                  class="px-3 py-1.5 rounded-lg border border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 text-xs hover:border-primary-500/30 hover:text-gray-900 dark:hover:text-white transition-all"
                >
                  {{ $t('client.emails.view') }}
                </NuxtLink>
              </UiTableTd>
            </UiTableRow>
          </UiTableBody>
        </UiTable>

        <UiPagination
          v-if="emailFiltered.length > 0"
          v-model="emailPage"
          v-model:per-page="emailPerPage"
          :total-pages="emailTotalPages"
        />
      </template>
    </div>
  </div>

  <!-- ── Edit Payment Method Modal ─────────────────────────────────── -->
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
        v-if="editModal.open"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        @click.self="closeEditModal"
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
            v-if="editModal.open"
            class="w-full max-w-2xl rounded-2xl bg-white dark:bg-[#13131a] border border-gray-200 dark:border-white/10 shadow-2xl flex flex-col max-h-[90vh]"
          >
            <!-- Header -->
            <div class="flex items-center justify-between px-6 py-5 border-b border-gray-100 dark:border-white/10">
              <h2 class="text-lg font-bold text-gray-900 dark:text-white">
                {{ $t('client.payment.editTitle') }}
              </h2>
              <button
                type="button"
                class="p-1.5 rounded-lg text-gray-400 hover:text-gray-700 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
                @click="closeEditModal"
              >
                <X :size="18" :stroke-width="2" />
              </button>
            </div>

            <!-- Body -->
            <div class="divide-y divide-gray-100 dark:divide-white/10 overflow-y-auto flex-1">

              <!-- Type (read-only) -->
              <div class="flex items-center gap-4 px-6 py-4">
                <span class="w-32 flex-shrink-0 text-sm text-gray-500 dark:text-gray-400">{{ $t('client.payment.typeLabel') }}</span>
                <div class="flex items-center gap-2">
                  <span class="w-5 h-5 rounded-full bg-cyan-500 flex items-center justify-center flex-shrink-0">
                    <Check :size="11" :stroke-width="3" class="text-white" />
                  </span>
                  <span class="text-sm font-medium text-gray-900 dark:text-white capitalize">{{ editModal.gateway_name }}</span>
                </div>
              </div>

              <!-- Description — read-only (WHMCS UpdatePayMethod does not accept description) -->
              <div class="flex items-center gap-4 px-6 py-4">
                <span class="w-32 flex-shrink-0 text-sm text-gray-500 dark:text-gray-400">{{ $t('client.payment.descriptionLabel') }}</span>
                <span class="flex-1 px-3 py-2 rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5 text-gray-500 dark:text-gray-400 text-sm">
                  {{ editModal.description || '—' }}
                </span>
              </div>

              <!-- Card Number (read-only, masked) -->
              <div v-if="editModal.card_last_four" class="flex items-center gap-4 px-6 py-4">
                <span class="w-32 flex-shrink-0 text-sm text-gray-500 dark:text-gray-400">{{ $t('client.payment.cardNumber') }}</span>
                <div class="flex-1 flex items-center justify-between px-3 py-2 rounded-xl border border-gray-200 dark:border-white/10 bg-gray-50 dark:bg-white/5">
                  <span class="text-sm font-mono text-gray-500 dark:text-gray-400 tracking-widest">
                    ············{{ editModal.card_last_four }}
                  </span>
                  <span class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-wider">{{ editModal.card_type || 'CARD' }}</span>
                </div>
              </div>

              <!-- Expiry Date — editable for all card types (CreditCard + RemoteCreditCard) -->
              <div v-if="editModal.card_last_four" class="flex items-center gap-4 px-6 py-4">
                <label class="w-32 flex-shrink-0 text-sm text-gray-500 dark:text-gray-400">{{ $t('client.payment.expiryDate') }}</label>
                <input
                  :value="editModal.card_expiry"
                  type="text"
                  inputmode="numeric"
                  maxlength="7"
                  :placeholder="$t('client.payment.expiryPlaceholder')"
                  class="w-36 px-3 py-2 rounded-xl border border-gray-300 dark:border-white/10 bg-white dark:bg-white/5 text-gray-900 dark:text-white text-sm font-mono focus:outline-none focus:ring-2 focus:ring-cyan-500/30 focus:border-cyan-500 transition-colors"
                  @input="formatEditExpiry"
                />
              </div>

              <!-- Billing Address — full-width section -->
              <div class="px-6 py-4 space-y-2">
                <p class="text-sm text-gray-500 dark:text-gray-400 mb-3">{{ $t('client.payment.billingAddress') }}</p>

                <!-- Skeleton while loading -->
                <template v-if="addressesLoading">
                  <div v-for="i in 2" :key="i" class="h-14 rounded-xl bg-gray-100 dark:bg-white/5 animate-pulse" />
                </template>

                <!-- Saved address radio list -->
                <template v-else>
                  <!-- Billing address is read-only — WHMCS UpdatePayMethod does not support billingaddressid -->
                  <ClientAddressCard
                    v-for="addr in savedAddresses"
                    :key="addr.id"
                    :addr="addr"
                    :selected="editModal.billingAddressId === addr.id"
                    :readonly="true"
                  />

                  <!-- Add new address link -->
                  <button
                    type="button"
                    class="flex items-center gap-1.5 text-sm text-cyan-600 dark:text-cyan-400 hover:text-cyan-500 dark:hover:text-cyan-300 transition-colors px-1 py-1"
                    @click="openEditAddressModal"
                  >
                    <Plus :size="14" :stroke-width="2.5" />
                    {{ $t('client.payment.addNewAddress') }}
                  </button>
                </template>
              </div>
            </div>

            <UiAlert v-if="editModal.error" variant="error" class="mx-6 mb-4">{{ editModal.error }}</UiAlert>

            <!-- Footer -->
            <div class="flex gap-3 px-6 py-4 border-t border-gray-100 dark:border-white/10 flex-shrink-0">
              <UiButton type="button" variant="outline" class="flex-1" @click="closeEditModal">
                {{ $t('client.profile.cancel') }}
              </UiButton>
              <UiButton type="button" class="flex-1" :loading="editModal.saving" @click="savePaymentEdit">
                <Check v-if="!editModal.saving" :size="14" :stroke-width="2.5" class="mr-1.5" />
                {{ editModal.saving ? $t('client.profile.saving') : $t('client.payment.saveChanges') }}
              </UiButton>
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>

  <!-- ── Contact Form Modal (Add / Edit) ──────────────────────────────────── -->
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
        v-if="contactModal.open"
        class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        @click.self="contactModal.open = false"
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
            v-if="contactModal.open"
            class="w-full max-w-lg rounded-2xl bg-white dark:bg-[#13131a] border border-gray-200 dark:border-white/10 shadow-2xl"
          >
            <!-- Header -->
            <div class="flex items-center justify-between px-6 py-5 border-b border-gray-100 dark:border-white/10">
              <h2 class="text-lg font-bold text-gray-900 dark:text-white">
                {{ contactModal.mode === 'add' ? $t('client.contacts.addTitle') : $t('client.contacts.editTitle') }}
              </h2>
              <button
                type="button"
                class="p-1.5 rounded-lg text-gray-400 hover:text-gray-700 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
                @click="contactModal.open = false"
              >
                <X :size="18" :stroke-width="2" />
              </button>
            </div>

            <!-- Body -->
            <div class="px-6 py-5 grid grid-cols-1 sm:grid-cols-2 gap-4 max-h-[65vh] overflow-y-auto">
              <UiInput v-model="contactModal.firstname" :label="$t('client.profile.firstName')" type="text" size="sm" required />
              <UiInput v-model="contactModal.lastname"  :label="$t('client.profile.lastName')"  type="text" size="sm" required />
              <div class="sm:col-span-2">
                <UiInput v-model="contactModal.companyname" :label="$t('client.profile.companyName')" type="text" size="sm" />
              </div>
              <div class="sm:col-span-2">
                <UiInput v-model="contactModal.email" :label="$t('client.profile.emailAddress')" type="email" size="sm" />
              </div>
              <div class="sm:col-span-2">
                <UiInput v-model="contactModal.phonenumber" :label="$t('client.profile.phone')" type="tel" size="sm" />
              </div>

              <!-- Email notifications -->
              <div class="sm:col-span-2 pt-1">
                <p class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">{{ $t('client.contacts.emailNotifications') }}</p>
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  <UiCheckbox id="cm-general"  v-model="contactModal.generalemails"  :label="$t('client.contacts.generalEmails')" />
                  <UiCheckbox id="cm-invoice"  v-model="contactModal.invoiceemails"  :label="$t('client.contacts.invoiceEmails')" />
                  <UiCheckbox id="cm-domain"   v-model="contactModal.domainemails"   :label="$t('client.contacts.domainEmails')" />
                  <UiCheckbox id="cm-product"  v-model="contactModal.productemails"  :label="$t('client.contacts.productEmails')" />
                  <UiCheckbox id="cm-support"  v-model="contactModal.supportemails"  :label="$t('client.contacts.supportEmails')" />
                </div>
              </div>
            </div>

            <UiAlert v-if="contactModal.error" variant="error" class="mx-6 mb-0 mt-1">{{ contactModal.error }}</UiAlert>

            <!-- Footer -->
            <div class="flex gap-3 px-6 py-4 border-t border-gray-100 dark:border-white/10">
              <UiButton type="button" variant="outline" class="flex-1" @click="contactModal.open = false">
                {{ $t('common.cancel') }}
              </UiButton>
              <UiButton type="button" class="flex-1" :loading="contactModal.saving" @click="saveContact">
                <Check v-if="!contactModal.saving" :size="14" :stroke-width="2.5" class="mr-1.5" />
                {{ contactModal.saving ? $t('client.profile.saving') : $t('client.contacts.save') }}
              </UiButton>
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>

  <!-- ── Address Sub-Modal (within Edit Payment Method) ───────────────────── -->
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
        v-if="addrModal.open"
        class="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
        @click.self="addrModal.open = false"
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
            v-if="addrModal.open"
            class="w-full max-w-lg rounded-2xl bg-white dark:bg-[#13131a] border border-gray-200 dark:border-white/10 shadow-2xl"
          >
            <!-- Header -->
            <div class="flex items-center justify-between px-6 py-5 border-b border-gray-100 dark:border-white/10">
              <h2 class="text-lg font-bold text-gray-900 dark:text-white">{{ $t('client.payment.addAddressTitle') }}</h2>
              <button
                type="button"
                class="p-1.5 rounded-lg text-gray-400 hover:text-gray-700 dark:hover:text-white hover:bg-gray-100 dark:hover:bg-white/10 transition-colors"
                @click="addrModal.open = false"
              >
                <X :size="18" :stroke-width="2" />
              </button>
            </div>

            <!-- Body -->
            <div class="px-6 py-5 grid grid-cols-1 sm:grid-cols-2 gap-4">
              <UiInput v-model="addrModal.form.firstname" :label="$t('client.profile.firstName')" type="text" size="sm" required />
              <UiInput v-model="addrModal.form.lastname"  :label="$t('client.profile.lastName')"  type="text" size="sm" required />
              <div class="sm:col-span-2">
                <UiInput v-model="addrModal.form.address1" :label="$t('client.profile.address1')" type="text" size="sm" required />
              </div>
              <div class="sm:col-span-2">
                <UiInput v-model="addrModal.form.address2" :label="$t('client.profile.address2')" type="text" size="sm" />
              </div>
              <UiInput v-model="addrModal.form.city"     :label="$t('client.profile.city')"     type="text" size="sm" />
              <UiInput v-model="addrModal.form.state"    :label="$t('client.profile.state')"    type="text" size="sm" />
              <UiInput v-model="addrModal.form.postcode" :label="$t('client.profile.postcode')" type="text" size="sm" />
              <UiSelect v-model="addrModal.form.country" :label="$t('client.profile.country')"  size="sm" :options="countryOptions ?? []" />
            </div>

            <UiAlert v-if="addrModal.error" variant="error" class="mx-6 mb-0 mt-1">{{ addrModal.error }}</UiAlert>

            <!-- Footer -->
            <div class="flex gap-3 px-6 py-4 border-t border-gray-100 dark:border-white/10">
              <UiButton type="button" variant="outline" class="flex-1" :disabled="addrModal.saving" @click="addrModal.open = false">
                {{ $t('common.cancel') }}
              </UiButton>
              <UiButton type="button" class="flex-1" :loading="addrModal.saving" @click="saveAddrModal">
                <Check v-if="!addrModal.saving" :size="14" :stroke-width="2.5" class="mr-1.5" />
                {{ addrModal.saving ? $t('client.profile.saving') : $t('common.confirm') }}
              </UiButton>
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>

  <!-- ── Confirm Dialog ──────────────────────────────────────────────── -->
  <UiConfirmModal
    :open="confirmDialog.open"
    :title="confirmDialog.title"
    :description="confirmDialog.description"
    :confirm-label="confirmDialog.confirmLabel"
    :loading="confirmDialog.loading"
    variant="danger"
    @confirm="confirmDialog.onConfirm()"
    @cancel="confirmDialog.open = false"
  />
</template>

<script setup lang="ts">
import { Pencil, Check, AlertCircle, Users, UserMinus, Send, Mail, CreditCard, Plus, Trash2, Star, X } from 'lucide-vue-next'
import { useClientStore, type ClientUser } from '~/stores/client'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const route = useRoute()
const config = useRuntimeConfig()
const whmcsUrl = config.public.whmcsUrl

// Active tab — driven by ?tab= query param for direct linking
const activeTab = ref((route.query.tab as string) || 'details')
watch(activeTab, tab => {
  useRouter().replace({ query: { ...route.query, tab } })
})

const tabs = computed(() => [
  { value: 'details',  label: t('client.account.tabDetails') },
  { value: 'contacts', label: t('client.contacts.title') },
  { value: 'users',    label: t('client.account.tabUsers') },
  { value: 'emails',   label: t('client.account.tabEmails') },
  { value: 'payment',  label: t('client.account.tabPayment') }
])

// ── Profile ──────────────────────────────────────────────────────────────────
const store = useClientStore()
await useAsyncData('client-user', () => store.fetchUser())

const [{ data: countryOptions }, { data: rawPaymentMethods }] = await Promise.all([
  useApi<{ value: string; label: string }[]>('/api/portal/public/countries', { default: () => [] }),
  useApi<{ module: string; displayname: string }[]>('/api/portal/order/payment-methods', { default: () => [] })
])

const paymentOptions = computed(() => [
  { value: '', label: t('client.profile.paymentDefault') },
  ...(rawPaymentMethods.value ?? []).map(g => ({ value: g.module, label: g.displayname }))
])
const languageOptions = computed(() => [
  { value: '', label: t('client.profile.langDefault') },
  { value: 'english',  label: t('client.profile.langEnglish') },
  { value: 'russian',  label: t('client.profile.langRussian') },
  { value: 'armenian', label: t('client.profile.langArmenian') }
])
const paymentMethodLabel = computed(() => {
  const gw = store.user?.defaultgateway
  if (!gw) return t('client.profile.paymentDefault')
  return (rawPaymentMethods.value ?? []).find(m => m.module === gw)?.displayname ?? gw
})
const languageLabel = computed(() => {
  const lang = store.user?.language
  if (!lang) return t('client.profile.langDefault')
  return lang.charAt(0).toUpperCase() + lang.slice(1)
})

const emailPrefList = computed(() => [
  { key: 'general'   as const, label: t('client.profile.prefGeneral'),   description: t('client.profile.prefGeneralDesc')   },
  { key: 'invoice'   as const, label: t('client.profile.prefInvoice'),   description: t('client.profile.prefInvoiceDesc')   },
  { key: 'support'   as const, label: t('client.profile.prefSupport'),   description: t('client.profile.prefSupportDesc')   },
  { key: 'product'   as const, label: t('client.profile.prefProduct'),   description: t('client.profile.prefProductDesc')   },
  { key: 'domain'    as const, label: t('client.profile.prefDomain'),    description: t('client.profile.prefDomainDesc')    },
  { key: 'affiliate' as const, label: t('client.profile.prefAffiliate'), description: t('client.profile.prefAffiliateDesc') }
])
type EmailPrefKey = 'general' | 'invoice' | 'support' | 'product' | 'domain' | 'affiliate'
function decodeEmailPrefs(prefs: ClientUser['email_preferences']) {
  // WHMCS may return values as strings ("1"/"0") or numbers (1/0); Number() handles both
  const p = (v: unknown) => Number(v ?? 1) !== 0
  return {
    general:   p(prefs?.general),
    invoice:   p(prefs?.invoice),
    support:   p(prefs?.support),
    product:   p(prefs?.product),
    domain:    p(prefs?.domain),
    affiliate: p(prefs?.affiliate)
  }
}
const emailprefs = reactive(decodeEmailPrefs(store.user?.email_preferences))
const currentEmailPrefs = computed(() =>
  editing.value ? emailprefs : decodeEmailPrefs(store.user?.email_preferences)
)

const editing  = ref(false)
const saving   = ref(false)
const saveError = ref('')
const form = reactive({
  firstname: '', lastname: '', companyname: '', email: '',
  phonenumber: '', address1: '', address2: '', city: '',
  state: '', postcode: '', country: '', paymentmethod: '', language: ''
})

function startEdit() {
  const u = store.user!
  Object.assign(form, {
    firstname: u.firstname, lastname: u.lastname, companyname: u.companyname ?? '',
    email: u.email, phonenumber: u.phonenumber ?? '', address1: u.address1 ?? '',
    address2: u.address2 ?? '', city: u.city ?? '', state: u.state ?? '',
    postcode: u.postcode ?? '', country: u.country ?? '',
    paymentmethod: u.defaultgateway ?? '', language: u.language ?? ''
  })
  Object.assign(emailprefs, decodeEmailPrefs(u.email_preferences))
  saveError.value = ''
  editing.value = true
}
function cancelEdit() {
  editing.value = false
  saveError.value = ''
  Object.assign(emailprefs, decodeEmailPrefs(store.user?.email_preferences))
}
async function saveEdit() {
  saving.value = true
  saveError.value = ''
  try {
    await apiFetch('/api/portal/client/me', { method: 'PUT', body: { ...form, emailprefs } })
    await store.fetchUser(true)
    editing.value = false
  } catch (err: any) {
    saveError.value = err?.data?.message || err?.message || 'Failed to save changes.'
  } finally {
    saving.value = false
  }
}

// ── Users ─────────────────────────────────────────────────────────────────────
interface ClientUserItem {
  id: string; name: string; email: string
  isOwner: boolean; lastLogin: string | null; permissions: string
}
const { data: usersRaw, pending: usersPending, refresh: refreshUsers } = await useApi<ClientUserItem[]>(
  '/api/portal/client/users', { default: () => [] }
)
const users = computed<ClientUserItem[]>(() => usersRaw.value ?? [])

const inviteEmail       = ref('')
const invitePermissions = ref<'all' | 'choose'>('all')
const inviting          = ref(false)
const inviteError       = ref('')
const inviteSuccess     = ref('')

const permissionsList = computed(() => [
  { key: 'profile',       label: t('client.users.permProfile'),    desc: t('client.users.permProfileDesc')    },
  { key: 'contacts',      label: t('client.users.permContacts'),   desc: t('client.users.permContactsDesc')   },
  { key: 'products',      label: t('client.users.permProducts'),   desc: t('client.users.permProductsDesc')   },
  { key: 'passwords',     label: t('client.users.permPasswords'),  desc: t('client.users.permPasswordsDesc')  },
  { key: 'sso',           label: t('client.users.permSso'),        desc: t('client.users.permSsoDesc')        },
  { key: 'domains',       label: t('client.users.permDomains'),    desc: t('client.users.permDomainsDesc')    },
  { key: 'domainsettings',label: t('client.users.permDomainMgmt'),desc: t('client.users.permDomainMgmtDesc') },
  { key: 'invoices',      label: t('client.users.permInvoices'),   desc: t('client.users.permInvoicesDesc')   },
  { key: 'quotes',        label: t('client.users.permQuotes'),     desc: t('client.users.permQuotesDesc')     },
  { key: 'tickets',       label: t('client.users.permTickets'),    desc: t('client.users.permTicketsDesc')    },
  { key: 'affiliate',     label: t('client.users.permAffiliate'),  desc: t('client.users.permAffiliateDesc')  },
  { key: 'emails',        label: t('client.users.permEmails'),     desc: t('client.users.permEmailsDesc')     },
  { key: 'orders',        label: t('client.users.permOrders'),     desc: t('client.users.permOrdersDesc')     },
])

const chosenPerms = reactive<Record<string, boolean>>({
  profile: false, contacts: false, products: false, passwords: false,
  sso: false, domains: false, domainsettings: false, invoices: false,
  quotes: false, tickets: false, affiliate: false, emails: false, orders: false
})

async function sendInvite() {
  inviting.value = true; inviteError.value = ''; inviteSuccess.value = ''
  try {
    const permissions = invitePermissions.value === 'all'
      ? 'all'
      : Object.entries(chosenPerms).filter(([, v]) => v).map(([k]) => k).join(',')
    await apiFetch('/api/portal/client/users/invite', {
      method: 'POST', body: { email: inviteEmail.value, permissions }
    })
    inviteSuccess.value = t('client.users.inviteSent')
    inviteEmail.value = ''; invitePermissions.value = 'all'
    Object.keys(chosenPerms).forEach(k => { chosenPerms[k] = false })
    refreshUsers()
  } catch (err: any) {
    inviteError.value = err?.data?.statusMessage || t('client.users.inviteError')
  } finally {
    inviting.value = false
  }
}
// ── Confirm Dialog ────────────────────────────────────────────────────────────
const confirmDialog = reactive({
  open:         false,
  title:        '',
  description:  '',
  confirmLabel: '',
  loading:      false,
  onConfirm:    () => {}
})

function openConfirm(opts: { title: string; description?: string; confirmLabel?: string; onConfirm: () => void }) {
  confirmDialog.title        = opts.title
  confirmDialog.description  = opts.description ?? ''
  confirmDialog.confirmLabel = opts.confirmLabel ?? ''
  confirmDialog.loading      = false
  confirmDialog.onConfirm    = opts.onConfirm
  confirmDialog.open         = true
}

const removingUserId = ref<string | null>(null)

function confirmRemove(user: ClientUserItem) {
  openConfirm({
    title:        t('client.users.removeConfirm', { email: user.email }),
    description:  t('client.users.removeConfirmDesc'),
    confirmLabel: t('client.users.removeAccess'),
    onConfirm:    () => doRemoveUser(user)
  })
}

async function doRemoveUser(user: ClientUserItem) {
  confirmDialog.loading = true
  removingUserId.value  = user.id
  try {
    await apiFetch(`/api/portal/client/users/${user.id}`, { method: 'DELETE' })
    confirmDialog.open = false
    refreshUsers()
  } catch (err: any) {
    inviteError.value = err?.data?.statusMessage || t('client.users.removeError')
    confirmDialog.open = false
  } finally {
    removingUserId.value  = null
    confirmDialog.loading = false
  }
}

// ── Contacts ──────────────────────────────────────────────────────────────────
interface ContactItem {
  id: string
  firstname: string
  lastname: string
  companyname?: string
  email: string
  phonenumber?: string
  generalemails: boolean
  invoiceemails: boolean
  domainemails: boolean
  productemails: boolean
  supportemails: boolean
}

const { data: contactsRaw, pending: contactsPending, refresh: refreshContacts } = await useApi<ContactItem[]>(
  '/api/portal/client/contacts', { default: () => [] }
)
const contacts = computed<ContactItem[]>(() => contactsRaw.value ?? [])

const contactModal = reactive({
  open:           false,
  mode:           'add' as 'add' | 'edit',
  id:             null as string | null,
  firstname:      '',
  lastname:       '',
  companyname:    '',
  email:          '',
  phonenumber:    '',
  generalemails:  true,
  invoiceemails:  true,
  domainemails:   false,
  productemails:  true,
  supportemails:  true,
  saving:         false,
  error:          ''
})

function openAddContact() {
  Object.assign(contactModal, {
    mode: 'add', id: null,
    firstname: '', lastname: '', companyname: '', email: '', phonenumber: '',
    generalemails: true, invoiceemails: true, domainemails: false, productemails: true, supportemails: true,
    saving: false, error: '', open: true
  })
}

function openEditContact(c: ContactItem) {
  Object.assign(contactModal, {
    mode: 'edit', id: c.id,
    firstname: c.firstname, lastname: c.lastname, companyname: c.companyname ?? '',
    email: c.email, phonenumber: c.phonenumber ?? '',
    generalemails: c.generalemails, invoiceemails: c.invoiceemails,
    domainemails: c.domainemails, productemails: c.productemails, supportemails: c.supportemails,
    saving: false, error: '', open: true
  })
}

async function saveContact() {
  if (!contactModal.firstname || !contactModal.lastname) {
    contactModal.error = t('client.contacts.saveError')
    return
  }
  contactModal.saving = true
  contactModal.error  = ''
  const body = {
    firstname:     contactModal.firstname,
    lastname:      contactModal.lastname,
    companyname:   contactModal.companyname,
    email:         contactModal.email,
    phonenumber:   contactModal.phonenumber,
    generalemails: contactModal.generalemails,
    invoiceemails: contactModal.invoiceemails,
    domainemails:  contactModal.domainemails,
    productemails: contactModal.productemails,
    supportemails: contactModal.supportemails
  }
  try {
    if (contactModal.mode === 'add') {
      await apiFetch('/api/portal/client/contacts', { method: 'POST', body })
    } else {
      await apiFetch(`/api/portal/client/contacts/${contactModal.id}`, { method: 'PUT', body })
    }
    contactModal.open = false
    refreshContacts()
  } catch (err: any) {
    contactModal.error = err?.data?.statusMessage || t('client.contacts.saveError')
  } finally {
    contactModal.saving = false
  }
}

function confirmDeleteContact(c: ContactItem) {
  openConfirm({
    title:        t('client.contacts.deleteConfirm', { name: `${c.firstname} ${c.lastname}` }),
    description:  t('client.contacts.deleteConfirmDesc'),
    confirmLabel: t('client.contacts.delete'),
    onConfirm:    () => doDeleteContact(c.id)
  })
}

async function doDeleteContact(id: string) {
  confirmDialog.loading = true
  try {
    await apiFetch(`/api/portal/client/contacts/${id}`, { method: 'DELETE' })
    confirmDialog.open = false
    refreshContacts()
  } catch {
    confirmDialog.open = false
  } finally {
    confirmDialog.loading = false
  }
}

// ── Emails ────────────────────────────────────────────────────────────────────
const { data: emailsRaw, pending: emailsPending } = await useApi<{ id: string; date: string; subject: string }[]>(
  '/api/portal/client/emails', { default: () => [] }
)
const emails = computed(() => emailsRaw.value ?? [])

const emailSearch  = ref('')
const emailPerPage = ref(10)
const emailPage    = ref(1)

const emailFiltered = computed(() => {
  const q = emailSearch.value.trim().toLowerCase()
  return q ? emails.value.filter(e => e.subject.toLowerCase().includes(q)) : emails.value
})
const emailTotalPages = computed(() => Math.max(1, Math.ceil(emailFiltered.value.length / emailPerPage.value)))
const emailPaged = computed(() => {
  const start = (emailPage.value - 1) * emailPerPage.value
  return emailFiltered.value.slice(start, start + emailPerPage.value)
})
const emailFrom = computed(() => emailFiltered.value.length === 0 ? 0 : (emailPage.value - 1) * emailPerPage.value + 1)
const emailTo   = computed(() => Math.min(emailPage.value * emailPerPage.value, emailFiltered.value.length))

watch(emailSearch, () => { emailPage.value = 1 })

// ── Payment Methods ───────────────────────────────────────────────────────────
interface PayMethod {
  id: number
  type: string
  description: string
  gateway_name: string
  contact_id: number
  card_last_four?: string | null
  card_expiry?: string | null
  card_type?: string | null
  bank_name?: string | null
}

const { data: paymentRaw, pending: paymentPending, refresh: refreshPayment } = await useApi<PayMethod[]>(
  '/api/portal/client/payment-methods', { default: () => [] }
)
const paymentMethods = computed<PayMethod[]>(() => paymentRaw.value ?? [])

const removingId      = ref<number | null>(null)
const settingDefaultId = ref<number | null>(null)
const paymentError    = ref('')
const paymentSuccess  = ref('')

function openAddForm() {
  // Adding cards via API is not supported for tokenised gateways (e.g. Stripe).
  // Redirect to WHMCS native credit card page.
  const whmcsUrl = useRuntimeConfig().public.whmcsUrl
  window.open(`${whmcsUrl}/clientarea.php?action=creditcard`, '_blank')
}

// ── Edit Modal ────────────────────────────────────────────────────────────────
interface SavedAddress {
  id: string
  firstname: string
  lastname: string
  companyname?: string
  address1?: string
  address2?: string
  city?: string
  state?: string
  postcode?: string
  country?: string
  countryname?: string
  phonenumber?: string
}

const editModal = reactive({
  open:           false,
  id:             null as number | null,
  type:           '',
  gateway_name:   '',
  card_last_four: null as string | null,
  card_type:      null as string | null,
  description:    '',
  card_expiry:    '',
  originalExpiry: '',
  saving:         false,
  error:          '',
  billingAddressId: null as string | null
})

const savedAddresses   = ref<SavedAddress[]>([])
const addressesLoading = ref(false)


async function loadAddresses() {
  addressesLoading.value = true
  try {
    savedAddresses.value = await apiFetch<SavedAddress[]>('/api/portal/client/addresses')
    // auto-select first address if none is selected yet
    if (!editModal.billingAddressId && savedAddresses.value.length) {
      editModal.billingAddressId = savedAddresses.value[0].id
    }
  } catch (err: any) {
    console.error('[addresses] failed to load:', err?.data?.statusMessage ?? err?.message)
    savedAddresses.value = []
  } finally {
    addressesLoading.value = false
  }
}

function openEditModal(method: PayMethod) {
  editModal.id             = method.id
  editModal.type           = method.type
  editModal.gateway_name   = method.gateway_name ?? ''
  editModal.card_last_four = method.card_last_four ?? null
  editModal.card_type      = method.card_type ?? null
  editModal.description = method.description ?? ''
  const rawExp = (method.card_expiry ?? '').replace(/\D/g, '').slice(0, 4)
  const fmtExp = rawExp.length > 2 ? rawExp.slice(0, 2) + ' / ' + rawExp.slice(2) : rawExp
  editModal.card_expiry    = fmtExp
  editModal.originalExpiry = fmtExp
  editModal.error  = ''
  editModal.saving = false
  // Pre-select billing address from WHMCS contact_id:
  // contact_id === 0 means the client's own profile address; otherwise it's a sub-contact
  if (method.contact_id && method.contact_id !== 0) {
    editModal.billingAddressId = String(method.contact_id)
  } else {
    editModal.billingAddressId = store.user ? `profile_${store.user.id}` : null
  }
  editModal.open = true
  loadAddresses()
}

function closeEditModal() {
  editModal.open = false
}

function formatEditExpiry(e: Event) {
  const input = e.target as HTMLInputElement
  const raw = input.value.replace(/\D/g, '').slice(0, 4)
  const formatted = raw.length > 2 ? raw.slice(0, 2) + ' / ' + raw.slice(2) : raw
  editModal.card_expiry = formatted
  nextTick(() => { input.value = formatted; input.setSelectionRange(formatted.length, formatted.length) })
}

// ── Address sub-modal (Add new address within Edit Payment Method) ───────────
const addrModal = reactive({
  open:   false,
  saving: false,
  error:  '',
  form: {
    firstname: '', lastname: '', companyname: '', email: '',
    address1: '', address2: '', city: '', state: '', postcode: '', country: ''
  }
})

function openEditAddressModal() {
  Object.assign(addrModal.form, {
    firstname: '', lastname: '', companyname: '', email: '',
    address1: '', address2: '', city: '', state: '', postcode: '', country: ''
  })
  addrModal.error  = ''
  addrModal.saving = false
  addrModal.open   = true
}

async function saveAddrModal() {
  if (!addrModal.form.firstname || !addrModal.form.lastname) return
  addrModal.saving = true
  addrModal.error  = ''
  try {
    const res = await apiFetch<{ addressid: string }>('/api/portal/client/addresses', {
      method: 'POST', body: addrModal.form
    })
    addrModal.open = false
    await loadAddresses()
    // auto-select the newly created address
    editModal.billingAddressId = res.addressid
  } catch (err: any) {
    addrModal.error = err?.data?.statusMessage || 'Failed to save address.'
  } finally {
    addrModal.saving = false
  }
}

async function savePaymentEdit() {
  if (!editModal.id) return

  const expiryChanged = !!editModal.card_last_four && editModal.card_expiry !== editModal.originalExpiry

  // Nothing changed — close without hitting the API
  if (!expiryChanged) {
    editModal.open = false
    return
  }

  editModal.saving = true
  editModal.error  = ''
  try {
    await apiFetch(`/api/portal/client/payment-methods/${editModal.id}`, {
      method: 'PUT',
      body: {
        card_expiry: editModal.card_expiry,
      }
    })
    paymentSuccess.value = t('client.payment.editSuccess')
    editModal.open = false
    refreshPayment()
  } catch (err: any) {
    editModal.error = err?.data?.statusMessage || t('client.payment.editError')
  } finally {
    editModal.saving = false
  }
}

async function setDefaultPaymentMethod(id: number) {
  settingDefaultId.value = id
  paymentError.value = ''
  paymentSuccess.value = ''
  try {
    await apiFetch(`/api/portal/client/payment-methods/${id}`, { method: 'PUT', body: { set_as_default: true } })
    paymentSuccess.value = t('client.payment.defaultSet')
    refreshPayment()
  } catch {
    paymentError.value = t('client.payment.defaultError')
  } finally {
    settingDefaultId.value = null
  }
}

function removePaymentMethod(id: number) {
  openConfirm({
    title:        t('client.payment.removeConfirm'),
    description:  t('client.payment.removeConfirmDesc'),
    confirmLabel: t('client.payment.remove'),
    onConfirm:    () => doRemovePaymentMethod(id)
  })
}

async function doRemovePaymentMethod(id: number) {
  confirmDialog.loading = true
  removingId.value      = id
  paymentError.value    = ''
  paymentSuccess.value  = ''
  try {
    await apiFetch(`/api/portal/client/payment-methods/${id}`, { method: 'DELETE' })
    paymentSuccess.value = t('client.payment.removeSuccess')
    confirmDialog.open   = false
    refreshPayment()
  } catch {
    paymentError.value = t('client.payment.removeError')
    confirmDialog.open = false
  } finally {
    removingId.value      = null
    confirmDialog.loading = false
  }
}
</script>
