---
title: Network issues
---

# Network issues <Label type="admin" />

Network issues let you tell clients about outages and planned maintenance.

## The list

Go to **Support → Network Issues**. Subtitle: "Manage network status and incident reports."

Three tabs:

| Tab | Shows |
|-----|-------|
| Open | Issues with status Reported or Investigating |
| Scheduled | Issues with status Scheduled |
| Resolved | Issues with status Resolved |

An issue that does not exist returns `NETWORKISSUE_NOT_FOUND`.

## Report an issue

Click **(+) Create New** and fill in the form:

| Field | Options |
|-------|---------|
| Title | Issue title |
| Type | Server, Other |
| Server | Server name (optional) |
| Priority | Low, Medium, High, Critical |
| Start Date | When the issue started |
| Description | Details about the issue |

New issues always start with status **Reported**. Click **Create Issue** to save.

## Update an issue

Click an issue to edit it. The edit form adds two fields:

| Field | Notes |
|-------|-------|
| Status | Reported, Investigating, Scheduled, Resolved |
| End Date | When it ended or is expected to end |

## Planned maintenance

<Steps>
<li>

Click **(+) Create New** and fill in the Title, Type, Priority, Start Date and Description. Save as **Reported**.

</li>
<li>

Open the issue and set **Status** to **Scheduled** and set the **End Date**.

</li>
<li>

Once work is complete, edit the issue again and set **Status** to **Resolved**.

</li>
</Steps>

## What customers see

Network issues are not yet shown in the client portal.
