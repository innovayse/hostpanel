---
title: Announcements, knowledge base and downloads
description: "Publish announcements, knowledge base articles and download files for clients from the Hostpanel admin panel."
keywords: "announcements, knowledge base, downloads, support content"
---

# Announcements, knowledge base and downloads <Label type="admin" />

## Announcements

Go to **Support → Announcements**. Subtitle: "Manage client-facing announcements."

**Columns:** Date, Title, Published.

Click **Add New Announcement** and fill in the form:

| Field | Notes |
|-------|-------|
| Title | Announcement title |
| Announcement | The announcement body |
| Published | Tick to show this announcement in the client portal |

The **Date** is set automatically and shown read-only when editing.

Only published announcements appear in the client portal. An announcement that does not exist returns `ANNOUNCEMENT_NOT_FOUND`.

Clients can read announcements at **Announcements** in the client portal.

## Knowledge base

Go to **Support → Knowledgebase**. Subtitle: "Manage knowledgebase articles and categories."

### Categories

Click **Add Category** and enter:
- **Category Name**
- **Check to Hide** — hides the category from the client portal
- **Description** — shown on the category page

Every category gets a short address (its **slug**) from its name when it is created, for example `billing-payments`. The slug stays the same when the category is renamed, so links to it keep working.

A category that still has sub-categories or articles cannot be deleted (`KBCATEGORY_IN_USE`): move or delete them first. A category that does not exist returns `KBCATEGORY_NOT_FOUND`.

### Articles

Click **Add Article** and fill in:
- **Category** — choose one of the existing categories
- **Title** and **Content** — written per language on the **English**, **Russian** and **Armenian** tabs. English is required; the other languages are optional, and a client who reads in a language the article is not written in sees the English text.
- **Published** — tick to make the article visible to clients

The article gets a short address (its **slug**) from its English title, for example `getting-started`. The slug is fixed once the article exists, even when the title is changed, so links to the article keep working. Clients can open an article by its slug or by its number.

Below the form, **Articles** lists every article with its title, slug, category and status (Published or Draft). Use **Edit** on a row to change its category or add and change its languages; the **Published** box is not part of editing.

An article that does not exist returns `KBARTICLE_NOT_FOUND`.

**Articles written before languages were added.** When languages were introduced, each existing article kept its text as one translation, in the language the text is written in (Armenian, Russian or English, recognised by its letters). An article that has no English text is still shown to clients, in the language it has; add the English text when you next edit it.

### Browse

The knowledgebase home shows **Browse by Category** and **Browse by Tag**. Tags come from articles and cannot be managed directly.

Clients can read the knowledgebase at **Knowledgebase** in the client portal.

## Downloads

Go to **Support → Downloads**. Subtitle: "Manage downloadable files and categories."

### Categories

Click **Add Category** and enter a Category Name. A category that has files or sub-categories cannot be deleted (`DOWNLOAD_CATEGORY_IN_USE`). A category that does not exist returns `DOWNLOAD_CATEGORY_NOT_FOUND`.

### Files

Click **Add Download** and fill in the form:

| Field | Notes |
|-------|-------|
| Type | ZIP File, Executable, PDF, or Other |
| Title | Display name shown to clients |
| Description | Short description |
| Filename | The filename to serve |
| Category | The category this download belongs to |
| Clients Only | Restricts download to logged-in clients |
| Product Download | Requires a product or add-on purchase to access |
| Hidden | Removes the download from the client area |

A download that does not exist returns `DOWNLOAD_NOT_FOUND`.

<Callout type="note">
Downloads are managed here but are not yet shown in the client portal.
</Callout>
