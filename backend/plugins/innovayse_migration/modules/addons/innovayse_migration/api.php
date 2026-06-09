<?php
/**
 * Innovayse Migration — Pull API
 * Called by the Innovayse panel to pull WHMCS data page by page.
 */

// Bootstrap WHMCS (3 levels up: innovayse_migration/ → addons/ → modules/ → whmcs root)
$whmcsRoot = dirname(__DIR__, 3);
if (!file_exists($whmcsRoot . '/init.php')) {
    http_response_code(500);
    header('Content-Type: application/json');
    echo json_encode(['error' => 'WHMCS init not found.']);
    exit;
}

define('CLIENTAREA', true);
require $whmcsRoot . '/init.php';

use Illuminate\Database\Capsule\Manager as Capsule;

header('Content-Type: application/json');
header('X-Content-Type-Options: nosniff');

$body   = json_decode(file_get_contents('php://input'), true) ?? [];
$key    = trim($body['key'] ?? '');
$action = $body['action'] ?? '';

// Validate key against stored setting
$storedKey = (string) Capsule::table('tbladdonmodules')
    ->where('module', 'innovayse_migration')
    ->where('setting', 'migration_key')
    ->value('value');

if (!$key || !$storedKey || !hash_equals($storedKey, $key)) {
    http_response_code(401);
    echo json_encode(['error' => 'Invalid migration key.']);
    exit;
}

$page    = max(1, (int)($body['page'] ?? 1));
$perPage = max(10, min(500, (int)($body['perPage'] ?? 100)));
$offset  = ($page - 1) * $perPage;

switch ($action) {
    case 'ping':
        echo json_encode(['ok' => true]);
        break;

    case 'totals':
        echo json_encode([
            'clients'  => (int) Capsule::table('tblclients')->count(),
            'invoices' => (int) Capsule::table('tblinvoices')->count(),
            'services' => (int) Capsule::table('tblhosting')->count(),
            'domains'  => (int) Capsule::table('tbldomains')->count(),
            'tickets'  => (int) Capsule::table('tbltickets')->count(),
        ]);
        break;

    case 'clients':
        $total = (int) Capsule::table('tblclients')->count();
        $rows  = Capsule::table('tblclients')
            ->orderBy('id')->skip($offset)->take($perPage)->get();
        echo json_encode([
            'items'      => $rows->map(fn($r) => [
                'email'     => $r->email,
                'firstName' => $r->firstname,
                'lastName'  => $r->lastname,
                'company'   => $r->companyname ?: null,
                'phone'     => $r->phonenumber ?: null,
                'address'   => $r->address1 ?: null,
                'city'      => $r->city ?: null,
                'state'     => $r->state ?: null,
                'postCode'  => $r->postcode ?: null,
                'country'   => $r->country ?: null,
                'status'    => _mapClientStatus($r->status),
            ])->values()->all(),
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'invoices':
        $total = (int) Capsule::table('tblinvoices')->count();
        $rows  = Capsule::table('tblinvoices')
            ->orderBy('id')->skip($offset)->take($perPage)->get();
        $records = [];
        foreach ($rows as $row) {
            $client = Capsule::table('tblclients')->where('id', $row->userid)->first(['email']);
            if (!$client) continue;
            $lineItems = Capsule::table('tblinvoiceitems')
                ->where('invoiceid', $row->id)
                ->get(['description', 'amount'])
                ->map(fn($i) => [
                    'description' => $i->description,
                    'amount'      => (float) $i->amount,
                    'quantity'    => 1,
                ])->all();
            $records[] = [
                'clientEmail' => $client->email,
                'total'       => (float) $row->total,
                'status'      => _mapInvoiceStatus($row->status),
                'date'        => _toIso($row->date),
                'dueDate'     => _toIso($row->duedate),
                'items'       => $lineItems,
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'services':
        $total = (int) Capsule::table('tblhosting')->count();
        $rows  = Capsule::table('tblhosting')
            ->orderBy('id')->skip($offset)->take($perPage)->get();
        $records = [];
        foreach ($rows as $row) {
            $client  = Capsule::table('tblclients')->where('id', $row->userid)->first(['email']);
            if (!$client) continue;
            $product = Capsule::table('tblproducts')->where('id', $row->packageid)->first(['name']);
            $records[] = [
                'clientEmail'  => $client->email,
                'productName'  => $product->name ?? 'Unknown Product',
                'billingCycle' => $row->billingcycle ?? 'monthly',
                'status'       => $row->domainstatus ?? 'Active',
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'domains':
        $total = (int) Capsule::table('tbldomains')->count();
        $rows  = Capsule::table('tbldomains')
            ->orderBy('id')->skip($offset)->take($perPage)->get();
        $records = [];
        foreach ($rows as $row) {
            $client = Capsule::table('tblclients')->where('id', $row->userid)->first(['email']);
            if (!$client) continue;
            $records[] = [
                'clientEmail'  => $client->email,
                'domainName'   => $row->domain,
                'registeredAt' => _toIso($row->registrationdate),
                'expiresAt'    => _toIso($row->expirydate),
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'tickets':
        $total = (int) Capsule::table('tbltickets')->count();
        $rows  = Capsule::table('tbltickets')
            ->orderBy('id')->skip($offset)->take($perPage)->get();
        $records = [];
        foreach ($rows as $row) {
            $client = Capsule::table('tblclients')->where('id', $row->userid)->first(['email']);
            if (!$client) continue;
            $firstReply = Capsule::table('tblticketreplies')
                ->where('tid', $row->id)->orderBy('id')->first(['message']);
            $records[] = [
                'clientEmail' => $client->email,
                'subject'     => $row->title,
                'message'     => $firstReply->message ?? '',
                'status'      => _mapTicketStatus($row->status ?? 'Open'),
                'priority'    => $row->urgency ?? 'Medium',
                'createdAt'   => _toIso($row->date),
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    default:
        http_response_code(400);
        echo json_encode(['error' => "Unknown action: {$action}"]);
}

function _mapClientStatus(string $s): string {
    return match(strtolower($s)) { 'active'=>'Active','inactive'=>'Inactive','closed'=>'Closed',default=>'Active' };
}
function _mapInvoiceStatus(string $s): string {
    return match(strtolower($s)) { 'paid'=>'Paid','unpaid'=>'Unpaid','overdue'=>'Overdue','cancelled'=>'Cancelled',default=>'Unpaid' };
}
function _mapTicketStatus(string $s): string {
    return match(strtolower($s)) { 'open'=>'Open','answered'=>'Answered','customer-reply'=>'Open','closed'=>'Closed','on hold'=>'On Hold',default=>'Open' };
}
function _toIso(?string $d): string {
    if (!$d || $d === '0000-00-00') return date('c');
    return date('c', strtotime($d));
}
