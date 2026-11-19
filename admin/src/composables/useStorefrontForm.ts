/**
 * The create / edit flow shared by the storefront content forms: edit mode from the route, loading
 * the stored entry into the form, the dirty flag, saving through the store and returning to the
 * list.
 */

import { computed, onMounted, ref } from 'vue'
import type { ComputedRef, Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useFormDirty } from '@/composables/useFormDirty'
import type { StorefrontCrud } from '@/composables/useStorefrontCrud'
import type { StorefrontEntity } from '@/types/storefront'

/** What the flow needs from the form that uses it. */
export interface StorefrontFormOptions<TItem extends StorefrontEntity, TPayload> {
  /** The content store. */
  store: {
    /** Finds the entry to edit. */
    fetchOne: StorefrontCrud<TItem, TPayload>['fetchOne']
    /** Creates an entry. */
    create: StorefrontCrud<TItem, TPayload>['create']
    /** Updates an entry. */
    update: StorefrontCrud<TItem, TPayload>['update']
    /** Sentence for the last refused create or update, as a store exposes it (unwrapped). */
    actionError: string | null
    /** Sentence for the last failed load, as a store exposes it (unwrapped). */
    error: string | null
    /** Clears every earlier refusal. */
    resetErrors: () => void
  }
  /** The reactive form state; any deep change marks the form dirty. */
  form: object
  /** Route of the list page, where a save or cancel returns. */
  listPath: string
  /** Copies a stored entry into the form. */
  populate: (item: TItem) => void
  /** Builds the payload from the form. */
  build: () => TPayload
}

/** What {@link useStorefrontForm} hands the form view. */
export interface StorefrontForm {
  /** Whether the route carries an entry id. */
  isEditMode: ComputedRef<boolean>
  /** True while the stored entry is loading. */
  loadingEntry: Ref<boolean>
  /** True while the form is being saved. */
  saving: Ref<boolean>
  /** Sentence for a failed load of the stored entry, null when none. */
  loadError: Ref<string | null>
  /** Sentence for a refused save, null when none. */
  saveError: Ref<string | null>
  /** Loads the stored entry again after a refused load. */
  reload: () => Promise<void>
  /** True when the form has edits not yet saved. */
  isDirty: Ref<boolean>
  /** Saves the form and, on success, resolves once the list is shown again. */
  save: () => Promise<void>
  /** Returns to the list; the unsaved-changes guard asks first when needed. */
  cancel: () => void
}

/**
 * Creates the create / edit flow of one content form.
 *
 * @param options - The store, the form state and the two converters.
 * @returns The flow's state and its two actions.
 */
export const useStorefrontForm = <TItem extends StorefrontEntity, TPayload>(
  options: StorefrontFormOptions<TItem, TPayload>,
): StorefrontForm => {
  const { t } = useI18n()
  const route = useRoute()
  const router = useRouter()

  /** Whether the route carries an entry id. */
  const isEditMode = computed<boolean>(() => !!route.params.id)

  /** True while the stored entry is loading. */
  const loadingEntry = ref(false)

  /** True while the form is being saved. */
  const saving = ref(false)

  /** Sentence for a failed load of the stored entry, null when none. */
  const loadError = ref<string | null>(null)

  /** Sentence for a refused save, null when none. */
  const saveError = ref<string | null>(null)

  /** The edits not yet saved, and the way to declare the form clean. */
  const { isDirty, settle } = useFormDirty(options.form)

  /** Loads the stored entry into the form; a refusal or a missing entry is kept in `loadError`. */
  const reload = async (): Promise<void> => {
    loadError.value = null
    loadingEntry.value = true
    const id = Number(route.params.id)
    const item = await options.store.fetchOne(id)
    loadingEntry.value = false
    if (!item) {
      // A refused load carries the API's own sentence; "not found" is only for a list that loaded without the entry.
      loadError.value = options.store.error ?? t('storefront.notFound', { id })
      return
    }
    options.populate(item)
    await settle()
  }

  onMounted(async () => {
    // Sentences left by another form or page must not show up on this one.
    options.store.resetErrors()
    if (isEditMode.value) {
      await reload()
    }
  })

  /** Saves the form through the store and returns to the list on success. */
  const save = async (): Promise<void> => {
    saving.value = true
    saveError.value = null
    const payload = options.build()
    let saved: boolean
    if (isEditMode.value) {
      saved = await options.store.update(Number(route.params.id), payload)
    } else {
      saved = (await options.store.create(payload)) !== null
    }
    saving.value = false
    if (saved) {
      await settle()
      await router.push(options.listPath)
    } else {
      saveError.value = options.store.actionError
    }
  }

  /** Returns to the list; the unsaved-changes guard asks first if needed. */
  const cancel = (): void => {
    void router.push(options.listPath)
  }

  return { isEditMode, loadingEntry, saving, loadError, saveError, isDirty, reload, save, cancel }
}
