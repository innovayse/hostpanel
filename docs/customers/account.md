---
title: Your account and signing in
description: "Create a Hostpanel client account, sign in, reset a forgotten password, edit your account details and manage users and permissions."
keywords: "client account, sign in, forgot password, account details, users and permissions"
---

# Your account and signing in

<Callout type="note">
If your hosting provider uses single sign-on, signing in and password management are handled by your sign-on service. The sections marked <strong>local accounts only</strong> do not apply.
</Callout>

## Create an account <Label type="local" />

<!-- See it in the demo: link goes here (#615) -->

<Steps>
<li>

Go to the sign-in page and click **Create account**.

</li>
<li>

Fill in **First Name**, **Last Name**, **Email Address** and **Password** (at least 8 characters), then click **Create Account**.

</li>
<li>

A confirmation email is sent to the address you entered. A "Verify your email" banner appears at the top of the page. Click the link in the email to verify your address. If it does not arrive, click **Resend verification email** in the banner.

</li>
</Steps>

## Sign in

<Steps>
<li>

Go to the sign-in page. The page is titled **Sign in to your account**.

</li>
<li>

**Local accounts:** Enter your **Email address** and **Password**, then click **Sign in**.<br>
**Single sign-on:** Click **Continue with single sign-on**. <Label type="sso" />

</li>
<li>

If two-factor authentication is on, you are taken to the two-factor step. See [Two-factor authentication](./two-factor).

</li>
</Steps>

## Forgot your password <Label type="local" />

<Steps>
<li>

On the sign-in page click **Forgot your password?**

</li>
<li>

Enter your email address and click **Send Reset Link**. The page shows "Check your inbox".

</li>
<li>

Open the email and click the link. The page is titled **Set a new password**.

</li>
<li>

Enter **New Password** and **Confirm Password**, then click **Reset Password**.

</li>
</Steps>

If the link has expired the page shows "Invalid reset link". Click **Request a new reset link** to start over.

## Your account details

Go to **Account**. The tabs you see depend on your permissions. The full set is: **Account Details**, **User Management**, **Email History** and **Payment Methods**.

### Account Details

The **Account Details** tab has two sections: **Account Information** (name, company, phone, address) and **Billing Address**. Click **Edit** to change them, then **Save Changes**.

**Email Preferences**: six toggles control which notification emails you receive:

| Toggle | What it controls |
|--------|-----------------|
| General Emails | Account notices and updates |
| Invoice Emails | New invoices, reminders and overdue notices |
| Support Emails | Copies of support ticket communications |
| Product Emails | Welcome emails, suspensions and other service lifecycle notifications |
| Domain Emails | Domain registration, transfer and renewal notices |
| Affiliate Emails | Affiliate account notifications |

### Change your sign-in email <Label type="local" />

The **Account Details** tab shows a **Sign-in email** card with your current email address. To change it:

<Steps>
<li>

Click **Change** next to your email address.

</li>
<li>

Enter the **New email address** and your **Current password**, then click **Send confirmation link**.

</li>
<li>

Open the email sent to your new address and click the confirmation link.

</li>
</Steps>

Every signed-in session ends after the change, including the current one. Sign in again with the new address.

### Contacts

The **Account Details** tab includes a **Contacts** section. Contacts receive email notifications but cannot sign in. Click **Add Contact** to add one.

Each contact has five email notification toggles: General Emails, Invoice Emails, Product & Service Emails, Domain Emails, Support Ticket Emails.

Click the edit icon to change a contact's details, or **Delete** to remove them.

### Two-factor authentication

The **Two-Factor Authentication** card lets you enable or disable 2FA. See [Two-factor authentication](./two-factor) for details.

### Email History and Payment Methods

**Email History** shows emails sent to your account. **Payment Methods** lists saved cards; you can set a default card or remove one. To see the **Payment Methods** tab your account needs the **View & Pay Invoices** permission.

## Users and permissions

Go to **Account**, then **User Management**. You can invite other people to access your account and choose what they can see or do.

### Invite a user

<Steps>
<li>

Click **Invite New User**.

</li>
<li>

Enter the person's **First Name**, **Last Name** and **Email Address**.

</li>
<li>

Choose **All Permissions** or **Choose Permissions** and tick what they should have access to.

</li>
<li>

Click **Send Invite**. The person receives an invitation email.

</li>
</Steps>

The person receives an email. When they click the link the **Accept Your Invitation** page opens. They click **Accept Invitation** to link their account. If they are not signed in they are asked to sign in first, then returned to the invitation. If they already have an account they use their existing credentials; otherwise they create one.

### Permissions

| Permission | What it allows |
|-----------|---------------|
| Modify Master Account Profile | Edit account information and billing address |
| View & Manage Contacts | View and manage contacts |
| View Products & Services | View services and addons |
| View & Modify Product Passwords | Change service passwords and manage SSH keys |
| Perform Single Sign-On | Log in to services directly |
| View Domains | View domain registrations |
| Manage Domain Settings | Change nameservers, lock, transfer and contact details |
| View & Pay Invoices | View and pay invoices; access the Payment Methods tab |
| View & Accept Quotes | View and accept quotes |
| View & Open Support Tickets | Open, reply to and manage support tickets |
| View & Manage Affiliate Account | View affiliate reports and request withdrawals |
| View Emails | View account email history |
| Place New Orders/Upgrades/Cancellations | Place orders and request cancellations |

The account owner always has full permissions and cannot be removed.
