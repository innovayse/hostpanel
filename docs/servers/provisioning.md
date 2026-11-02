---
title: Provisioning
---

# Provisioning <Label type="admin" />

Provisioning is the process by which Hostpanel creates, suspends and removes hosting accounts on control-panel servers automatically.

## When an order is paid

When a client pays an order, Hostpanel accepts it and marks each service line as **Pending**. It does not create the hosting account at that moment. The account is created in one of two ways:

- **Client completes the setup wizard** — the client opens the service in the client portal and clicks "Set Up My Service" to enter the domain, username and billing details. Provisioning runs once the setup is saved.
- **Staff press Create** — on a Pending service in the admin panel, click **Create** in the Module Commands card.

The setup wizard is refused until the service's first invoice is paid in full (`SERVICE_FIRST_INVOICE_UNPAID`).

### Server selection

The provisioner selects a server as follows:

1. If the product has a **Server Group**, a server is chosen from that group. Disabled servers and full servers are skipped. The server with the lowest fill ratio wins; ties go to the server with the higher account limit, then by name.
2. If the product has no server group, the module must be unambiguous: if only one supported module is in use across all servers, that module's least-full server is used. If more than one module is in use, the order fails with `PRODUCT_SERVER_GROUP_REQUIRED`.
3. If no eligible server exists, the operation fails with `NO_ELIGIBLE_SERVER`.

### What happens on provisioning

<Steps>
<li>

Hostpanel reads the service's domain, username and password. If no password was set, one is generated and saved to the service record before the panel is called.

</li>
<li>

Hostpanel calls the server's control panel API to create the hosting account with the product's **Package Name**. If the product has no package name, the operation fails with `PRODUCT_PACKAGE_MISSING`.

</li>
<li>

On success the service becomes **Active**, the billing period starts, and the provisioning reference is stored on the service record.

</li>
</Steps>

A retry is safe: if the same account already exists on the panel under the same username and domain, Hostpanel adopts it. If the username is taken by a different domain, provisioning fails with `PROVISIONING_ACCOUNT_CONFLICT`. Other panel failures return `PROVISIONING_UPSTREAM_FAILED`.

If the service record has no domain or username yet, provisioning fails with `SERVICE_HOSTING_DETAILS_MISSING`. Attempting to run provisioning on a service that is not in Pending status returns `SERVICE_NOT_PENDING`.

On success the client receives the `service-provisioned` email.

### Failed domain registrations on the same order

When an order contains both a hosting service and a domain, and the domain registration fails, the domain's cost is refunded automatically (through the original gateway or as credit). The hosting service is still provisioned.

## The Module Commands card

The **Module Commands** card on a service's detail page shows the actions available for the current status.

| Service status | Available commands |
|----------------|--------------------|
| Pending | **Create**, **Terminate** |
| Active | **Suspend**, **Terminate**, **Change Package**, **Change Password** |
| Suspended | **Unsuspend**, **Terminate** |
| Terminated | (shows Termination Date field only) |

The **Login to Control Panel** button (top of the service page) is available on Active and Suspended services. It opens a single-sign-on URL to the client's account on the control panel (`SERVICE_CREDENTIALS_UPSTREAM_FAILED` if the panel does not answer).

**Change Package** and **Change Password** require a provisioning reference to be set (i.e. the account was created successfully). Without one: `SERVICE_NOT_PROVISIONED`. Without a server assigned: `SERVICE_NO_SERVER_ASSIGNED`. Panel refusals return `SERVICE_PACKAGE_UPSTREAM_FAILED` or `SERVICE_PASSWORD_UPSTREAM_FAILED`.

**Terminate** always shows a confirmation dialog before proceeding.

Staff can also change the status directly with the **Status** drop-down in the same card without calling the panel, useful for correcting a record.

## Suspension

A service is suspended in two ways:

- **Manually by staff** — click **Suspend** in Module Commands.
- **Automatically by the billing job** — the **Suspend Overdue Services** job suspends services whose invoices are overdue beyond the `billing.renewal.suspend_after_overdue_days` grace period.

Hostpanel calls the panel first and updates its own record only after the panel confirms. A panel refusal returns `SERVICE_SUSPEND_UPSTREAM_FAILED`.

When a service is suspended, any SSH keys the client had installed on the server are removed automatically.

<Callout type="warning">
Suspend through Hostpanel, not directly in the control panel. A panel-side suspension locks the password but does not stop a key from logging in. Suspending in Hostpanel removes the keys.
</Callout>

**Unsuspend** re-installs the SSH keys on the server after the panel confirms. Panel refusal returns `SERVICE_UNSUSPEND_UPSTREAM_FAILED`.

## Termination

A service is terminated in three ways:

- **Manually by staff** — click **Terminate** in Module Commands.
- **Automatically at end of billing cycle** — tick **Auto-Terminate End of Cycle** on the service's Auto-Management tab and optionally set a **Termination Reason**.
- **Automatically after long suspension** — the **Auto-Terminate Suspended** job terminates services that have been suspended for more than `billing.renewal.terminate_after_suspended_days` days.

When a service is terminated manually, Hostpanel contacts the panel first. If the panel refuses, it returns `SERVICE_TERMINATE_UPSTREAM_FAILED` and the service remains unchanged — no status change is written. If the panel accepts, the service is set to Terminated and the SSH keys are revoked.

If the async removal of the hosting account fails after the service has been set to Terminated, the daily auto-terminate job retries automatically and staff receive the `service-termination-failed-admin` email.

`SERVICE_TERMINATE_NOT_ALLOWED` is returned when attempting to terminate a service that is already Terminated.

## Auto-Management settings

On the service's **Auto-Management** tab:

| Setting | What it does |
|---------|--------------|
| Automatic billing | Enables renewal invoice generation, overdue suspension and auto-termination for this service |
| Override Auto-Suspend | Prevents the billing job from suspending this service automatically |
| Suspend Until | Date after which auto-suspend can resume (only when Override Auto-Suspend is on) |
| Auto-Terminate End of Cycle | Terminates the service at the end of the current billing period |
| Termination Reason | Note recorded with the auto-termination (only when Auto-Terminate End of Cycle is on) |

## Scheduled jobs

| Job (`Cron` class) | Default time (UTC) | What it does |
|--------------------|--------------------|--------------|
| Sync Server Account Counts (`SyncServerAccountCountsCron`) | 06:00 | Updates the live account count shown on each server row |
| Auto-Terminate Suspended (`AutoTerminateSuspendedCron`) | 08:00 | Terminates services suspended for non-payment past `billing.renewal.terminate_after_suspended_days` days, then terminates services set to auto-terminate at end of cycle |
| Suspend Overdue Services (`SuspendOverdueServicesCron`) | 09:00 | Suspends services whose invoice has been overdue for more than `billing.renewal.suspend_after_overdue_days` days |

The job times are set in `ScheduledJobs` options. Go to **Settings → System Settings** to change the billing thresholds:

<SettingTable :settings="[
  { name: 'billing.renewal.suspend_after_overdue_days', type: 'number', default: '3', description: 'Days an invoice may stay overdue before the services on it are suspended. Range: 0 to 90.' },
  { name: 'billing.renewal.terminate_after_suspended_days', type: 'number', default: '14', description: 'Days a service suspended for non-payment stays suspended before it is terminated and its hosting account removed. Range: 1 to 90.' }
]" />
