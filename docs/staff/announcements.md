---
title: Announcements, knowledge base and downloads
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

A category that has sub-categories cannot be deleted (`KBCATEGORY_IN_USE`). A category that does not exist returns `KBCATEGORY_NOT_FOUND`.

### Articles

Click **Add Article** and fill in:
- **Title**
- **Category**
- **Content**
- **Published** — tick to make the article visible to clients

An article that does not exist returns `KBARTICLE_NOT_FOUND`.

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
