/**
 * Unit tests for the storefront test-id factory.
 */
import { describe, expect, it } from 'vitest'
import { storefrontTestIds } from '@/types/storefront'

describe('storefrontTestIds', () => {
  it('uses the plural prefix for page ids and the singular for row ids', () => {
    expect(storefrontTestIds('faqs')).toEqual({
      view: 'faqs-view',
      add: 'faqs-add',
      error: 'faqs-error',
      actionError: 'faqs-action-error',
      savingOrder: 'faqs-saving-order',
      row: 'faq-row',
      actions: 'faq-actions',
      edit: 'faq-edit',
      delete: 'faq-delete',
      moveUp: 'faq-move-up',
      moveDown: 'faq-move-down',
    })
  })

  it('matches the ids of every storefront list page', () => {
    expect(storefrontTestIds('testimonials').row).toBe('testimonial-row')
    expect(storefrontTestIds('partners').moveUp).toBe('partner-move-up')
    expect(storefrontTestIds('projects').delete).toBe('project-delete')
  })
})
