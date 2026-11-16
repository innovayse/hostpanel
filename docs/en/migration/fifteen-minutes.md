---
title: Migration checklist
description: A short checklist for moving from WHMCS into Hostpanel. The steps you do by hand take about fifteen minutes, while the import itself runs in the background.
keywords: "migration checklist, whmcs import, quick migration, import checklist"
---

# Migration checklist {#migration-checklist}

<Label type="admin" />

This is the short route for moving from WHMCS. The steps you do by hand take about fifteen minutes. The import then runs in the background, so how long it takes depends on how many records you move. Each item links to the section of [Moving from WHMCS](./from-another-system) that explains it.

- [ ] Read [What you need](./from-another-system#what-you-need) and [What is not imported](./from-another-system#what-is-not-imported).
- [ ] Back up the database and `.env` and prepare currencies, servers and departments, see [Before you start](./from-another-system#before-you-start).
- [ ] Create the job, install the addon and check the connection, see [Create a migration](./from-another-system#create-a-migration).
- [ ] Start the import and watch the counters, see [Job statuses](./from-another-system#job-statuses).
- [ ] Fix skipped and failed records and retry, see [Import report and skip reasons](./import-report) and [Run it again safely](./from-another-system#run-it-again-safely).
- [ ] Verify the data, see [Check the result](./from-another-system#check-the-result).
- [ ] Remove the key, the addon and the finished job, see [After the move](./from-another-system#after-the-move).
