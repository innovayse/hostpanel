<?php

/**
 * Innovayse Migration — WHMCS Addon Module
 *
 * Exposes a pull API so the Innovayse panel can fetch data from this WHMCS instance.
 *
 * Installation:
 *   1. Upload this folder to /modules/addons/innovayse_migration/
 *   2. Activate in WHMCS Admin → Setup → Addon Modules
 *   3. Enter the Migration Key from your Innovayse panel in the settings
 */

if (!defined('WHMCS')) {
    die('This file cannot be accessed directly');
}

// ── Module metadata ──────────────────────────────────────────────────────────

function innovayse_migration_config(): array
{
    return [
        'name'        => 'Innovayse Migration',
        'description' => 'Connect your WHMCS instance to the Innovayse hosting panel.',
        'version'     => '1.1.0',
        'author'      => 'Innovayse',
        'fields'      => [
            'migration_key' => [
                'FriendlyName' => 'Migration Key',
                'Type'         => 'password',
                'Size'         => '60',
                'Description'  => 'Secret key from your Innovayse panel → Migration Tools → New Migration',
                'Default'      => '',
            ],
        ],
    ];
}

function innovayse_migration_activate(): array
{
    return ['status' => 'success', 'description' => 'Innovayse Migration activated.'];
}

function innovayse_migration_deactivate(): array
{
    return ['status' => 'success', 'description' => 'Innovayse Migration deactivated.'];
}

// ── Admin UI ─────────────────────────────────────────────────────────────────

function innovayse_migration_output(array $vars): void
{
    $migrationKey = $vars['migration_key'] ?? '';
    $configured   = !empty($migrationKey);

    $protocol    = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http';
    $host        = $_SERVER['HTTP_HOST'] ?? '';
    $apiEndpoint = $protocol . '://' . $host . '/modules/addons/innovayse_migration/api.php';
    ?>
    <div style="font-family:sans-serif;max-width:620px;padding:20px">

        <h2 style="margin-top:0">Innovayse Migration</h2>

        <?php if (!$configured): ?>
        <div style="background:#fef2f2;border:1px solid #fca5a5;border-radius:8px;
                    padding:14px 18px;color:#991b1b;font-weight:600;margin-bottom:20px">
            &#9888; Not configured — enter your Migration Key in the settings above and save.
        </div>
        <?php else: ?>
        <div style="background:#f0fdf4;border:1px solid #86efac;border-radius:8px;
                    padding:14px 18px;color:#166534;font-weight:600;margin-bottom:20px">
            &#10003; Plugin is ready. The Innovayse panel can now connect and pull your data.
        </div>
        <?php endif; ?>

        <div style="background:#f8f9fa;border:1px solid #dee2e6;border-radius:8px;padding:20px">
            <p style="margin:0 0 6px 0;font-weight:600;font-size:14px">Source URL</p>
            <p style="margin:0 0 10px 0;color:#666;font-size:13px">
                Enter this URL as the <strong>Source URL</strong> when creating a migration in your Innovayse panel:
            </p>
            <code style="display:block;background:#e9ecef;padding:8px 12px;border-radius:6px;
                          font-size:13px;word-break:break-all">
                <?= htmlspecialchars($apiEndpoint) ?>
            </code>
        </div>

    </div>
    <?php
}
