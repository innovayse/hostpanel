# Apps & Integrations — Design Spec

**Date:** 2026-04-19  
**Branch:** feature/csharp-backend  
**Scope:** Admin panel — new `/integrations` module

---

## Overview

A new **Apps & Integrations** section in the admin panel that allows administrators to view, enable/disable, and configure all third-party service integrations in one place.

---

## Layout

**Section Groups** — all categories on a single page, each as a white card with a header row and a grid of integration items inside. No tabs, no sidebar — scroll-based browsing.

---

## Categories & Integrations

| Category | Integrations |
|---|---|
| 💳 Payment Gateways | Stripe, PayPal, Bank Transfer |
| 🌐 Domain Registrars | Namecheap, ResellerClub, ENOM |
| 🖥️ Hosting / Provisioning | cPanel WHM, Plesk |
| 📧 Email / SMTP | SMTP Server (MailKit) |
| 🛡️ Fraud Protection | MaxMind |

---

## Main Page (`/integrations`)

- Page header: title + subtitle
- Each category renders as a white rounded card with:
  - Section header: icon + category name + "N active" badge
  - Integration rows: logo color block, name, description, Active/Inactive badge, "Configure →" link
  - Inactive integrations shown at reduced opacity (0.65)
- Email and Fraud sections shown side-by-side (2-column grid) since they have fewer items

---

## Configure UX — Dedicated Page (`/integrations/:slug`)

When the admin clicks "Configure →", they navigate to a dedicated settings page.

### Layout
- **Breadcrumb**: Apps & Integrations › [Integration Name]
- **Left (2/3)**: Config form card
  - Integration logo + name + description in header
  - Enable/Disable toggle (top-right of card)
  - Fields: integration-specific (e.g., Secret Key, Publishable Key, Webhook Secret, Mode for Stripe)
  - Actions: "Save Changes" + "Test Connection"
- **Right (1/3)**: Info sidebar
  - Connection status (last tested timestamp)
  - Contextual hints (e.g., webhook URL for Stripe)

### Fields per integration

**Stripe:** Secret Key, Publishable Key, Webhook Secret, Mode (Live/Test)  
**PayPal:** Client ID, Client Secret, Mode (Live/Sandbox)  
**Bank Transfer:** Account Name, IBAN, Bank Name, Instructions (textarea)  
**Namecheap:** API Key, API Username, Client IP  
**ResellerClub:** Reseller ID, API Key  
**ENOM:** Account ID, API Key  
**cPanel WHM:** Host, Port, Username, API Token  
**Plesk:** Host, Port, Username, Password  
**SMTP:** Host, Port, Username, Password, From Address, Encryption (TLS/SSL/None)  
**MaxMind:** Account ID, License Key  

---

## Routing

```
/integrations                    → IntegrationsView.vue
/integrations/:slug              → IntegrationDetailView.vue
  slugs: stripe, paypal, bank-transfer,
         namecheap, resellerclub, enom,
         cpanel, plesk, smtp, maxmind
```

---

## State Management

**`useIntegrationsStore`** (Pinia):
- `integrations: IntegrationDto[]` — list with status
- `fetchAll()` — GET /api/admin/integrations
- `fetchOne(slug)` — GET /api/admin/integrations/:slug
- `saveConfig(slug, config)` — PUT /api/admin/integrations/:slug
- `testConnection(slug)` — POST /api/admin/integrations/:slug/test
- `loading`, `error` states

---

## API Endpoints (existing backend)

The backend already has `IPaymentGateway`, `IRegistrarProvider`, `IProvisioningProvider` interfaces in Domain. New endpoints needed:

```
GET    /api/admin/integrations           → list all with status
GET    /api/admin/integrations/:slug     → get config (masked secrets)
PUT    /api/admin/integrations/:slug     → save config
POST   /api/admin/integrations/:slug/test → test connection
```

Secrets returned masked (`sk_live_••••`) — full value only on explicit edit.

---

## File Structure

```
admin/src/modules/integrations/
  stores/
    integrationsStore.ts
  views/
    IntegrationsView.vue          ← main page (all sections)
    IntegrationDetailView.vue     ← config page per integration
  components/
    IntegrationSection.vue        ← one category section card
    IntegrationRow.vue            ← single integration row
    IntegrationConfigForm.vue     ← dynamic form by slug
    IntegrationStatusSidebar.vue  ← right sidebar (status + hints)
  types/
    integration.types.ts          ← IntegrationDto, IntegrationConfig
```

---

## Sidebar Navigation

Add to `AppSidebar.vue`:
```
{ to: '/integrations', label: 'Apps & Integrations' }
```

---

## Conventions

- Follows existing module pattern: store + views, same as `billing`, `domains`, etc.
- All API calls via `integrationsStore`, never direct `useApi` in components
- Tailwind only, no inline styles
- Full JSDoc on all exported functions, composables, store actions
