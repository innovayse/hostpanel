/**
 * Fixtures for the plugin slot widget binding section of the plugin manager: an installed app
 * plugin that declares one admin slot widget, and two products the operator can bind it to.
 */
import type { PluginListItemDto, PluginSlotWidgetBindingDto } from '../../src/types/plugin'
import { PluginStatus } from '../../src/types/plugin'
import type { Product } from '../../src/types/product'
import { ProductStatus, ProductType } from '../../src/types/productenums'

/** An installed, loaded app plugin that declares slot widgets. */
export const widgetPlugin: PluginListItemDto = {
  id: 'sample-app',
  name: 'Sample app',
  version: '1.0.0',
  author: 'Hostpanel',
  description: 'A sample app plugin with slot widgets.',
  type: 'app',
  category: 'Add-ons',
  color: '#0f766e',
  isLoaded: true,
  iconUrl: null,
  isBuiltIn: false,
  status: PluginStatus.Loaded,
  refusedRule: null,
  refusedDetail: null,
  stagedVersion: null,
}

/**
 * Builds the widget the sample plugin declares, unbound.
 *
 * @returns A fresh binding the spec may mutate to play the server.
 */
export const unboundWidget = (): PluginSlotWidgetBindingDto => ({
  slotId: 'admin.service.panels',
  widgetId: 'usage',
  title: 'Usage panel',
  manifestProductIds: [],
  productIds: [],
  isBound: false,
})

/**
 * Builds one product the widget can be bound to.
 *
 * @param id - Product id.
 * @param name - Product name.
 * @returns The product with no prices.
 */
const productOf = (id: number, name: string): Product => ({
  id,
  groupId: 1,
  name,
  description: null,
  website: null,
  slug: null,
  packageName: null,
  type: ProductType.SharedHosting,
  status: ProductStatus.Active,
  pricing: { monthly: null, quarterly: null, semiAnnual: null, annual: null, biennial: null },
  prices: [],
  serverGroupId: null,
  isFeatured: false,
  sortOrder: id,
  sku: null,
  metadata: null,
  gracePeriodDays: 0,
})

/** The two products the operator can choose from. */
export const bindableProducts: Product[] = [productOf(3, 'Starter hosting'), productOf(4, 'Business hosting')]
