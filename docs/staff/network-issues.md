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
| Status | Reported, Investigating, Scheduled, Resolved |
| Start Date | When the issue started or is scheduled to start |
| End Date | When it ended or is expected to end |
| Description | Details about the issue |

Click **Create Issue** to save.

## Planned maintenance

To schedule maintenance, create an issue with status **Scheduled** and set a start and end date. Once work is complete, edit the issue and set the status to **Resolved**.

## What customers see

Network issues are not yet shown in the client portal.
