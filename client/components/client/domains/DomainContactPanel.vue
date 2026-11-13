<template>
  <UiCard data-testid="domain-contact">
    <UiCardHeader :title="$t('client.domains.tabContactInfo')" :icon="UserCog" />

    <UiStack v-if="whoisLoading" gap="sm">
      <UiSkeleton v-for="i in 6" :key="i" shape="block" class="h-10" />
    </UiStack>

    <template v-else>
      <UiAlert variant="info" :icon-size="16" class="mb-5">{{ $t('client.domains.contactInfoNote') }}</UiAlert>

      <UiStack v-if="whois" gap="xs" class="mb-5" data-testid="domain-whois-record">
        <UiKeyValue :label="$t('client.domains.whoisRegistrar')" :value="whois.registrar" />
        <UiKeyValue :label="$t('client.domains.whoisRegistrant')" :value="whois.registrant" />
        <UiKeyValue :label="$t('client.domains.whoisCreated')" :value="formatDate(whois.createdAt)" />
        <UiKeyValue :label="$t('client.domains.whoisExpires')" :value="formatDate(whois.expiresAt)" />
      </UiStack>
      <UiAlert v-else variant="warning" :icon-size="16" class="mb-5" data-testid="domain-whois-unavailable">
        {{ $t('client.domains.whoisUnavailable') }}
      </UiAlert>

      <UiStack gap="sm" class="mb-5">
        <UiRadio
          name="contactMode"
          :value="true"
          :model-value="useExisting"
          :label="$t('client.domains.contactUseExisting')"
          testid="domain-contact-existing"
          @update:model-value="useExisting = true"
        />
        <UiSelect
          v-if="useExisting"
          :model-value="selectedId"
          :label="$t('client.domains.contactChoose')"
          :placeholder="$t('client.domains.contactChoosePlaceholder')"
          :options="contactOptions"
          size="sm"
          class="ml-8 !w-auto"
          testid="domain-contact-choose"
          @update:model-value="chooseContact(String($event))"
        />
        <UiRadio
          name="contactMode"
          :value="false"
          :model-value="useExisting"
          :label="$t('client.domains.contactSpecifyCustom')"
          testid="domain-contact-custom"
          @update:model-value="useExisting = false"
        />
      </UiStack>

      <UiForm data-testid="domain-contact-form" :error="whoisError" :success="success" spacing="sm" @submit="save">
        <ClientDomainsDomainContactForm :values="form" :disabled="useExisting" @change="editField" />
        <template #actions>
          <UiButton type="button" size="sm" variant="ghost" data-testid="domain-contact-cancel" @click="cancel">
            {{ $t('client.domains.cancelChanges') }}
          </UiButton>
          <UiButton type="submit" size="sm" :loading="whoisSaving" data-testid="domain-contact-save">
            <Save v-if="!whoisSaving" :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('client.domains.saveChanges') }}
          </UiButton>
        </template>
      </UiForm>
    </template>
  </UiCard>
</template>

<script setup lang="ts">
/**
 * DomainContactPanel: the WHOIS registrant contact of a domain, either pre-filled from the
 * registrar's current record, the account's profile or contacts, or typed in, beside the
 * registry's read-only WHOIS record (registrar, registrant, dates). The form starts from the
 * registrant the registrar holds and falls back to the account's primary profile when the
 * registrar has none or cannot be read. Reading, saving and the API's refusal belong to the
 * domains store.
 */
import { onMounted, reactive, ref, computed } from 'vue'
import { Save, UserCog } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useDomainsStore } from '~/stores/domains'
import { DomainActionOutcome } from '~/types/domainactionoutcome'
import type { DomainContactChoice } from '~/types/domaincontactchoice'
import type { DomainContactFields } from '~/types/domaincontactfields'
import type { WhoisRecord } from '~/types/whoisrecord'
import { contactFromProfile, emptyContact, PRIMARY_CONTACT_ID, toContactChoices } from '~/utils/domainContacts'
import { formatDate } from '~/utils/formatDate'

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of DomainContactPanel. */
interface Props {
  /** The id of the domain. */
  domainId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The domains store: the in-flight flags and the refusal sentence. */
const store = useDomainsStore()
/** Reactive state of the store this component renders. */
const { whoisLoading, whoisSaving, whoisError } = storeToRefs(store)

/** The registry's WHOIS record; null until read, or when it could not be. */
const whois = ref<WhoisRecord | null>(null)

/** The contact fields as edited. */
const form = reactive<DomainContactFields>(emptyContact())

/** The contact fields as loaded or last saved, so Cancel can restore them. */
const initial = reactive<DomainContactFields>(emptyContact())

/** Whether an existing contact is used (true) or custom fields are typed (false). */
const useExisting = ref(true)

/** The id of the contact the form is pre-filled from. */
const selectedId = ref(PRIMARY_CONTACT_ID)

/** The account's other contacts, as dropdown choices. */
const contacts = ref<DomainContactChoice[]>([])

/** The confirmation shown after a successful save. */
const success = ref('')

/** The dropdown: the primary profile first, then the other contacts. */
const contactOptions = computed(() => [
  { label: t('client.domains.contactPrimaryProfile'), value: PRIMARY_CONTACT_ID },
  ...contacts.value.map((contact) => ({ label: contact.label, value: contact.id })),
])

onMounted(async () => {
  const [record, contactsData, profile, registrant] = await Promise.all([
    store.loadWhois(props.domainId),
    store.loadContacts(),
    store.loadPrimaryProfile(),
    store.loadRegistrant(props.domainId),
  ])
  // Each read is optional: a failed one leaves its part empty and the rest usable.
  whois.value = record
  contacts.value = contactsData ? toContactChoices(contactsData) : []
  // The registrar's own record wins; the profile only stands in when there is none.
  if (registrant) {
    Object.assign(form, registrant)
    Object.assign(initial, form)
  } else if (profile) {
    Object.assign(form, contactFromProfile(profile))
    Object.assign(initial, form)
  }
})

/**
 * Pre-fills the form from the contact the reader picked.
 *
 * @param id - The chosen contact id, or {@link PRIMARY_CONTACT_ID} for the account's profile.
 * @returns Resolves when the form has been filled.
 */
const chooseContact = async (id: string): Promise<void> => {
  selectedId.value = id
  if (id === PRIMARY_CONTACT_ID) {
    const profile = await store.loadPrimaryProfile()
    if (profile) {
      Object.assign(form, contactFromProfile(profile))
    }
    return
  }
  const entry = contacts.value.find((contact) => contact.id === id)
  if (entry) {
    Object.assign(form, entry.fields)
  }
}

/**
 * Applies an edit of the contact form.
 *
 * @param key - The field that changed.
 * @param value - Its new value.
 */
const editField = (key: keyof DomainContactFields, value: string): void => {
  form[key] = value
}

/** Restores the form to its loaded or last saved values. */
const cancel = (): void => {
  Object.assign(form, initial)
  success.value = ''
}

/**
 * Saves the registrant contact through the store.
 *
 * @returns Resolves when the attempt has ended.
 */
const save = async (): Promise<void> => {
  success.value = ''
  if (await store.saveWhois(props.domainId, { ...form }) === DomainActionOutcome.Done) {
    // The saved state is the new baseline for Cancel.
    Object.assign(initial, form)
    success.value = t('client.domains.contactSaved')
  }
}
</script>
