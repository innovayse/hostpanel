---
title: Services and SSH keys
description: "Manage your hosting services in the Hostpanel client portal: set up a new service, see its status and add SSH keys to your hosting account."
keywords: "hosting services, service setup, ssh keys, client portal"
---

# Services and SSH keys

## Your services

<!-- See it in the demo: link goes here (#615) -->

Go to **My Services**. Each service shows its status:

| Status | What it means |
|--------|--------------|
| Pending | The order is placed but the service is not yet set up |
| Active | The service is running |
| Suspended | The service is paused. You cannot use it until it is reactivated. |
| Ended | The service period is over (also shown as Terminated) |

## Set up a new service

When a service needs setup, it shows **Setup Required**.

<Steps>
<li>

Click **Complete Setup** on the service.

</li>
<li>

Follow the setup steps: choose a domain, set credentials and review.

</li>
<li>

The service becomes active once setup is complete.

</li>
</Steps>

Setup becomes available once the first invoice is paid in full.

## Manage a service

Click a service to open it. The **Overview** tab shows your plan, billing details and quick links.

- **Change Password**: enter a new password and click **Change Password**. Requires the **View & Modify Product Passwords** permission.
- **Login to Control Panel**: opens your hosting control panel directly.
- **Request Cancellation**: choose **Immediate** (cancels straight away) or **End of Billing Period** (runs until the end of the current period), enter a reason and click **Submit Cancellation Request**. Requires the **Place New Orders/Upgrades/Cancellations** permission.

## SSH keys

An SSH key is a pair of files: a private key that stays on your computer and a public key that you add to a server. When you connect, the server checks that your private key matches the public key on file. You never need a password, and your private key never leaves your computer.

The **SSH Keys** tab lets you manage the public keys on your hosting account.

To see this tab your account needs the **View & Modify Product Passwords** permission.

<!-- See it in the demo: link goes here (#615) -->

### Connect to your server

The tab shows **Hostname**, **Port** and **Username** for your account. Click **Connect with** to copy the ready-made SSH command.

### Add a key

<Steps>
<li>

Go to the **SSH Keys** tab. The tab shows **Your keys (count/max)**.

</li>
<li>

Scroll to **Add a key**. Paste your public key into **Public key (ED25519)** and enter a **Label** (for example "Work laptop").

</li>
<li>

Click **Add key**.

</li>
</Steps>

<Callout type="danger">
Never paste your private key (the file <em>without</em> .pub). The page rejects it, but keep the habit on any site.
</Callout>

Only ED25519 public keys are accepted. If you do not have one yet, the **How do I create an SSH key?** guide on the same tab walks you through creating one.

### Deploy automatically from GitHub or GitLab

The **Deploy automatically from GitHub or GitLab** guide on the SSH Keys tab shows how to set up automatic deployments on every push to your main branch.

### Revoke a key

Click **Revoke** next to the key, then confirm with **Revoke this key?**. The key is removed from the server.

### Key status badges

| Badge | What it means |
|-------|--------------|
| Pending | The key is being added to the server |
| Install not confirmed | The key was sent but the server did not confirm it was added |
| Revoke not confirmed | Revocation was sent but not confirmed |
| Off the server while the service is suspended | The key is stored but inactive until the service is reactivated |
| Revoked | The key has been removed |
| Previous server | The key was added on a previous server and is no longer active |
| Not found on server | The key is not on the server. Click Retry to try again. |

### When SSH keys are not available

| Message | What to do |
|---------|-----------|
| "SSH access is disabled for this hosting account. Contact support to enable it." | Ask your hosting provider to enable SSH for your account |
| "SSH key management is not available for this type of hosting." | This hosting type does not support SSH key management |
| "SSH key management is not enabled on this service's server yet." | The server is not yet configured for SSH key management. Check back later or contact support. |
| "SSH keys can only be managed while the service is active." | Reactivate the service first |
| "The hosting server could not be reached. Please try again later." | A temporary network issue. Try again in a moment. |

