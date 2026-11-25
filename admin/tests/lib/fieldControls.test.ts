/**
 * Unit tests for the one field type to kit control mapping: every plugin field type and every integration
 * field type has a rendering, each maps to the control the kit draws it with, and the input type is the
 * rendering's own or plain text.
 */
import { describe, expect, it } from 'vitest'
import { fieldControlOf, fieldInputType, INTEGRATION_FIELD_RENDERINGS, PLUGIN_FIELD_RENDERINGS } from '@/lib/fieldControls'
import { FieldControl } from '@/types/fieldcontrol'
import { IntegrationFieldType } from '@/types/integration'
import { PluginFieldType } from '@/types/pluginview'
import { UiInputType } from '@/utils/ui/input'

describe('PLUGIN_FIELD_RENDERINGS', () => {
  it('has a rendering for every plugin field type', () => {
    expect(Object.keys(PLUGIN_FIELD_RENDERINGS).sort()).toEqual(Object.values(PluginFieldType).sort())
  })

  it('maps each type to its one kit control', () => {
    expect(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Text].control).toBe(FieldControl.Input)
    expect(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Number].control).toBe(FieldControl.Number)
    expect(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Url].control).toBe(FieldControl.Input)
    expect(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Textarea].control).toBe(FieldControl.Textarea)
    expect(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Select].control).toBe(FieldControl.Select)
    expect(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Boolean].control).toBe(FieldControl.Toggle)
    expect(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Secret].control).toBe(FieldControl.Secret)
  })
})

describe('INTEGRATION_FIELD_RENDERINGS', () => {
  it('has a rendering for every integration field type', () => {
    expect(Object.keys(INTEGRATION_FIELD_RENDERINGS).sort()).toEqual(Object.values(IntegrationFieldType).sort())
  })

  it('maps each type to its one kit control, both secret kinds to the secret field', () => {
    expect(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Text].control).toBe(FieldControl.Input)
    expect(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Url].control).toBe(FieldControl.Input)
    expect(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Number].control).toBe(FieldControl.Number)
    expect(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Textarea].control).toBe(FieldControl.Textarea)
    expect(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Select].control).toBe(FieldControl.Select)
    expect(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Toggle].control).toBe(FieldControl.Toggle)
    expect(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Password].control).toBe(FieldControl.Secret)
    expect(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Secret].control).toBe(FieldControl.Secret)
  })
})

describe('fieldInputType', () => {
  it('gives an address its native input type and every other rendering plain text', () => {
    expect(fieldInputType(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Url])).toBe(UiInputType.Url)
    expect(fieldInputType(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Url])).toBe(UiInputType.Url)
    expect(fieldInputType(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Text])).toBe(UiInputType.Text)
  })
})

describe('fieldControlOf', () => {
  it('draws a field the API marks secret as the secret field whatever its type says', () => {
    expect(fieldControlOf(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Text], true)).toBe(FieldControl.Secret)
    expect(fieldControlOf(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Textarea], true)).toBe(FieldControl.Secret)
    expect(fieldControlOf(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Url], true)).toBe(FieldControl.Secret)
  })

  it('takes the control of the type for a field that is not secret', () => {
    expect(fieldControlOf(INTEGRATION_FIELD_RENDERINGS[IntegrationFieldType.Text], false)).toBe(FieldControl.Input)
    expect(fieldControlOf(PLUGIN_FIELD_RENDERINGS[PluginFieldType.Number], false)).toBe(FieldControl.Number)
  })
})
