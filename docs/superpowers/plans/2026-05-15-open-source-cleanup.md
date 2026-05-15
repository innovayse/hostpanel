# Open-Source Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove all company-specific hardcoded values and exclude unnecessary files before pushing to GitHub.

**Architecture:** Replace hardcoded Innovayse emails/URLs with generic placeholders so contributors can drop in their own values. Exclude log files and local env files from git.

**Tech Stack:** .gitignore, JSON locale files, TypeScript, C# appsettings.json

---

### Task 1: Update .gitignore

**Files:**
- Modify: `.gitignore`

- [ ] Add exclusions for logs, playwright artifacts, admin env files

---

### Task 2: Fix useSchemaOrg.ts

**Files:**
- Modify: `client/composables/useSchemaOrg.ts`

- [ ] Replace `contact@innovayse.com` with `contact@yourdomain.com`

---

### Task 3: Fix locale files — seo.json (en, ru, hy)

**Files:**
- Modify: `client/locales/en/seo.json`
- Modify: `client/locales/ru/seo.json`
- Modify: `client/locales/hy/seo.json`

- [ ] Replace `contact@innovayse.com` and phone `+374 33 73 16 73` with placeholders

---

### Task 4: Fix locale files — refundPolicy.json (en, ru, hy)

**Files:**
- Modify: `client/locales/en/refundPolicy.json`
- Modify: `client/locales/ru/refundPolicy.json`
- Modify: `client/locales/hy/refundPolicy.json`

- [ ] Replace `billing@innovayse.com` with `billing@yourdomain.com`

---

### Task 5: Fix data.ts demoUrls

**Files:**
- Modify: `client/lib/data.ts`

- [ ] Replace all `https://*.innovayse.com` demoUrls with `#`

---

### Task 6: Fix whmcs.ts demoUrls

**Files:**
- Modify: `client/utils/whmcs.ts`

- [ ] Replace all `https://*.innovayse.com` demoUrls with `#`

---

### Task 7: Fix appsettings.json

**Files:**
- Modify: `backend/src/Innovayse.API/appsettings.json`

- [ ] Replace `noreply@innovayse.com` with `noreply@yourdomain.com`
- [ ] Replace `Innovayse` FromName with `YourBrand`
