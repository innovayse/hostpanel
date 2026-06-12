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

function _safeCount(string $table): int {
    try {
        return (int) Capsule::table($table)->count();
    } catch (\Throwable $e) {
        return 0;
    }
}
function _safeRows(string $table, callable $fn): \Illuminate\Support\Collection {
    try {
        return $fn(Capsule::table($table));
    } catch (\Throwable $e) {
        return collect([]);
    }
}

switch ($action) {
    case 'ping':
        echo json_encode(['ok' => true]);
        break;

    case 'totals':
        echo json_encode([
            'clients'            => _safeCount('tblclients'),
            'invoices'           => _safeCount('tblinvoices'),
            'services'           => _safeCount('tblhosting'),
            'domains'            => _safeCount('tbldomains'),
            'tickets'            => _safeCount('tbltickets'),
            'products'           => _safeCount('tblproducts'),
            'orders'             => _safeCount('tblorders'),
            'transactions'       => _safeCount('tblaccounts'),
            'quotes'             => _safeCount('tblquotes'),
            'knowledgebase'      => _safeCount('tblknowledgebase'),
            'contacts'           => _safeCount('tblcontacts'),
            'ticketReplies'      => _safeCount('tblticketreplies'),
            'announcements'      => _safeCount('tblannouncements'),
            'downloads'          => _safeCount('tbldownloads'),
            'downloadCategories' => _safeCount('tbldownloadcats'),
            'networkIssues'      => _safeCount('tblnetworkissues'),
        ]);
        break;

    case 'clients':
        $total = (int) Capsule::table('tblclients')->count();
        $rows  = Capsule::table('tblclients')
            ->orderBy('id')->skip($offset)->take($perPage)->get();
        // Build currency ID → code map from tblcurrencies
        $currencies = [];
        try {
            Capsule::table('tblcurrencies')->get(['id', 'code'])->each(function($c) use (&$currencies) {
                $currencies[(int)$c->id] = $c->code;
            });
        } catch (\Throwable $e) {}
        echo json_encode([
            'items'      => $rows->map(fn($r) => [
                'email'         => $r->email,
                'firstName'     => $r->firstname,
                'lastName'      => $r->lastname,
                'company'       => $r->companyname ?: null,
                'phone'         => $r->phonenumber ?: null,
                'address'       => $r->address1 ?: null,
                'city'          => $r->city ?: null,
                'state'         => $r->state ?: null,
                'postCode'      => $r->postcode ?: null,
                'country'       => $r->country ?: null,
                'status'        => _mapClientStatus($r->status),
                'currency'      => $currencies[(int)($r->currency ?? 0)] ?? null,
                'taxExempt'     => (bool)($r->taxexempt ?? false),
                'creditBalance' => (float)($r->credit ?? 0),
                'signupDate'    => ($r->datecreated && $r->datecreated !== '0000-00-00 00:00:00') ? _toIso($r->datecreated) : null,
                'lastLogin'     => ($r->lastlogin && $r->lastlogin !== '0000-00-00 00:00:00') ? _toIso($r->lastlogin) : null,
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
            $lineItems = _safeRows('tblinvoiceitems', fn($q) => $q->where('invoiceid', $row->id)->get(['description', 'amount']))
                ->map(fn($i) => [
                    'description' => $i->description,
                    'amount'      => (float) $i->amount,
                    'quantity'    => 1,
                ])->all();
            $records[] = [
                'clientEmail'   => $client->email,
                'externalId'    => (string)$row->id,
                'total'         => (float) $row->total,
                'status'        => _mapInvoiceStatus($row->status),
                'date'          => _toIso($row->date),
                'dueDate'       => _toIso($row->duedate),
                'paymentMethod' => $row->paymentmethod ?: null,
                'items'         => $lineItems,
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
                'clientEmail'        => $client->email,
                'productName'        => $product->name ?? 'Unknown Product',
                'billingCycle'       => $row->billingcycle ?? 'monthly',
                'status'             => $row->domainstatus ?? 'Active',
                'domain'             => $row->domain ?: null,
                'username'           => $row->username ?: null,
                'nextDueDate'        => ($row->nextduedate && $row->nextduedate !== '0000-00-00') ? _toIso($row->nextduedate) : null,
                'terminatedAt'       => ($row->terminationdate && $row->terminationdate !== '0000-00-00 00:00:00') ? _toIso($row->terminationdate) : null,
                'subscriptionId'     => $row->subscriptionid ?: null,
                'adminNotes'         => $row->notes ?: null,
                'recurringAmount'    => (float)($row->amount ?? 0),
                'firstPaymentAmount' => (float)($row->firstpaymentamount ?? 0),
                'paymentMethod'      => $row->paymentmethod ?: null,
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
            $nameservers = array_filter([
                $row->ns1 ?: null,
                $row->ns2 ?: null,
                $row->ns3 ?: null,
                $row->ns4 ?: null,
            ]);
            $records[] = [
                'clientEmail'        => $client->email,
                'domainName'         => $row->domain,
                'registeredAt'       => _toIso($row->registrationdate),
                'expiresAt'          => _toIso($row->expirydate),
                'nextDueDate'        => ($row->nextduedate && $row->nextduedate !== '0000-00-00') ? _toIso($row->nextduedate) : null,
                'registrar'          => $row->registrar ?: null,
                'nameservers'        => array_values($nameservers),
                'status'             => $row->status ?: 'Active',
                'autoRenew'          => (bool)($row->donotrenew ? false : true),
                'recurringAmount'    => (float)($row->recurringamount ?? 0),
                'firstPaymentAmount' => (float)($row->firstpaymentamount ?? 0),
                'paymentMethod'      => $row->paymentmethod ?: null,
                'subscriptionId'     => $row->subscriptionid ?: null,
                'adminNotes'         => $row->notes ?: null,
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
            // tbltickets.message is the original opening message — use it directly
            $message = $row->message ?? '';
            if (empty(trim($message))) {
                try {
                    $firstReply = Capsule::table('tblticketreplies')
                        ->where('tid', $row->id)->orderBy('id')->first(['message']);
                    $message = $firstReply?->message ?? '';
                } catch (\Throwable $e) {
                    $message = '';
                }
            }
            try { $dept = Capsule::table('tblticketdepartments')->where('id', $row->did)->first(['name']); } catch (\Throwable $e) { $dept = null; }
            $records[] = [
                'clientEmail'    => $client->email,
                'subject'        => $row->title,
                'message'        => $message,
                'status'         => _mapTicketStatus($row->status ?? 'Open'),
                'priority'       => $row->urgency ?? 'Medium',
                'departmentName' => $dept->name ?? null,
                'createdAt'      => _toIso($row->date),
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'product_groups':
        $total = _safeCount('tblproductgroups');
        $rows = _safeRows('tblproductgroups', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        echo json_encode([
            'items' => $rows->map(fn($r) => [
                'id'          => $r->id,
                'name'        => $r->name,
                'description' => $r->description ?: null,
            ])->values()->all(),
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'products':
        $total = _safeCount('tblproducts');
        $rows = _safeRows('tblproducts', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        echo json_encode([
            'items' => $rows->map(fn($r) => [
                'id'           => $r->id,
                'groupId'      => $r->gid,
                'name'         => $r->name,
                'description'  => $r->description ?: null,
                'type'         => $r->type ?? 'hostingaccount',
                'monthlyPrice' => (float)($r->pricing_monthly ?? 0),
                'annualPrice'  => (float)($r->pricing_annually ?? 0),
            ])->values()->all(),
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'orders':
        $total = _safeCount('tblorders');
        $rows = _safeRows('tblorders', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        $records = [];
        foreach ($rows as $row) {
            $client = Capsule::table('tblclients')->where('id', $row->userid)->first(['email']);
            if (!$client) continue;
            try {
                $orderItemsRaw = Capsule::table('tblorderitems')->where('orderid', $row->id)->get();
            } catch (\Throwable $e) {
                $orderItemsRaw = collect([]);
            }
            $orderItems = $orderItemsRaw
                ->map(fn($i) => [
                    'productId'          => $i->relid ? (int)$i->relid : null,
                    'productName'        => $i->type ?? 'Product',
                    'billingCycle'       => $i->billingcycle ?? 'monthly',
                    'firstPaymentAmount' => (float)($i->amount ?? 0),
                    'recurringAmount'    => (float)($i->amount ?? 0),
                    'domain'             => $i->domain ?: null,
                    'hostname'           => null,
                ])->values()->all();
            $records[] = [
                'clientEmail'  => $client->email,
                'orderNumber'  => (string)$row->id,
                'paymentMethod' => $row->paymentmethod ?? 'bank_transfer',
                'status'        => _mapOrderStatus($row->status ?? 'Pending'),
                'amount'        => (float)$row->amount,
                'createdAt'     => _toIso($row->date),
                'items'         => $orderItems,
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'transactions':
        $total = _safeCount('tblaccounts');
        $rows = _safeRows('tblaccounts', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        $records = [];
        foreach ($rows as $row) {
            $uid = $row->userid ?? $row->clientid ?? null;
            $client = null;
            if ($uid) {
                // Try tblclients first, then tblusers (WHMCS 8+)
                $client = Capsule::table('tblclients')->where('id', $uid)->first(['email']);
                if (!$client) {
                    $user = Capsule::table('tblusers')->where('id', $uid)->first(['email']);
                    if ($user) {
                        $linked = Capsule::table('tblusers')
                            ->join('tblclients', 'tblclients.id', '=', 'tblusers.id')
                            ->where('tblusers.id', $uid)
                            ->first(['tblclients.id as cid', 'tblusers.email']);
                        $client = $linked ?: $user;
                    }
                }
            }
            if (!$client) continue;
            $amountIn  = (float)($row->amountin  ?? 0);
            $amountOut = (float)($row->amountout ?? 0);
            if ($amountIn <= 0 && $amountOut <= 0) $amountIn = 0.01;
            $records[] = [
                'clientEmail'   => $client->email,
                'date'          => _toIso($row->date),
                'description'   => $row->description ?? '',
                'transactionId' => $row->transid ?? (string)$row->id,
                'invoiceId'     => $row->invoiceid ? (int)$row->invoiceid : null,
                'paymentMethod' => $row->gateway ?? 'bank_transfer',
                'amountIn'      => $amountIn,
                'amountOut'     => $amountOut,
                'fees'          => (float)($row->fees ?? 0),
                'currency'      => (!empty($row->currency) && $row->currency !== '0') ? $row->currency : null,
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'quotes':
        $total = _safeCount('tblquotes');
        $rows = _safeRows('tblquotes', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        $records = [];
        foreach ($rows as $row) {
            $client = Capsule::table('tblclients')->where('id', $row->userid)->first(['email']);
            if (!$client) continue;
            $items = _safeRows('tblquoteitems', fn($q) => $q->where('quoteid', $row->id)->get(['item_desc', 'item_unit_cost', 'item_qty', 'item_discount']))
                ->map(fn($i) => [
                    'description'     => $i->item_desc ?? '',
                    'unitPrice'       => (float)($i->item_unit_cost ?? 0),
                    'quantity'        => (int)($i->item_qty ?? 1),
                    'discountPercent' => (float)($i->item_discount ?? 0),
                ])->all();
            $records[] = [
                'clientEmail'  => $client->email,
                'subject'      => $row->subject ?? 'Quote',
                'stage'        => _mapQuoteStage($row->stage ?? 'Draft'),
                'expiryDate'   => _toIso($row->validuntil),
                'proposalText' => $row->proposal_text ?: null,
                'customerNotes'=> $row->notes ?: null,
                'items'        => $items,
                'createdAt'    => _toIso($row->datecreated),
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'knowledgebase_categories':
        $total = _safeCount('tblknowledgebasecats');
        $rows = _safeRows('tblknowledgebasecats', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        echo json_encode([
            'items' => $rows->map(fn($r) => [
                'id'          => $r->id,
                'name'        => $r->name,
                'description' => $r->description ?? '',
                'parentId'    => $r->parentid ? (int)$r->parentid : null,
            ])->values()->all(),
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'knowledgebase':
        $total = _safeCount('tblknowledgebase');
        $rows = _safeRows('tblknowledgebase', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        $records = [];
        foreach ($rows as $row) {
            $records[] = [
                'title'      => $row->title ?? 'Article',
                'content'    => $row->article ?? '',
                'categoryId' => $row->catid ? (int)$row->catid : null,
                'published'  => (bool)($row->active ?? false),
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'contacts':
        $total = (int) Capsule::table('tblcontacts')->count();
        $rows  = Capsule::table('tblcontacts')->orderBy('id')->skip($offset)->take($perPage)->get();
        $records = [];
        foreach ($rows as $row) {
            $client = Capsule::table('tblclients')->where('id', $row->userid)->first(['email']);
            if (!$client) continue;
            $records[] = [
                'clientEmail' => $client->email,
                'firstName'   => $row->firstname ?? '',
                'lastName'    => $row->lastname  ?? '',
                'company'     => $row->companyname ?: null,
                'email'       => $row->email ?? '',
                'phone'       => $row->phonenumber ?: null,
                'address'     => $row->address1 ?: null,
                'city'        => $row->city ?: null,
                'state'       => $row->state ?: null,
                'postCode'    => $row->postcode ?: null,
                'country'     => $row->country ?: null,
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'ticket_replies':
        $total = _safeCount('tblticketreplies');
        $rows  = _safeRows('tblticketreplies', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        $records = [];
        foreach ($rows as $row) {
            $ticket = Capsule::table('tbltickets')->where('id', $row->tid)->first(['userid', 'title']);
            if (!$ticket) continue;
            $client = Capsule::table('tblclients')->where('id', $ticket->userid)->first(['email']);
            if (!$client) continue;
            $records[] = [
                'clientEmail'  => $client->email,
                'ticketSubject'=> $ticket->title,
                'message'      => $row->message ?? '',
                'authorName'   => $row->name ?? 'Unknown',
                'isStaffReply' => (bool)($row->admin ?? false),
            ];
        }
        echo json_encode([
            'items'      => $records,
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'announcements':
        $total = _safeCount('tblannouncements');
        $rows  = _safeRows('tblannouncements', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        echo json_encode([
            'items' => $rows->map(fn($r) => [
                'title'       => $r->title ?? '',
                'content'     => $r->announcement ?? '',
                'published'   => (bool)($r->published ?? false),
                'createdAt'   => ($r->date && $r->date !== '0000-00-00') ? _toIso($r->date) : null,
            ])->values()->all(),
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'download_categories':
        $total = _safeCount('tbldownloadcats');
        $rows  = _safeRows('tbldownloadcats', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        echo json_encode([
            'items' => $rows->map(fn($r) => [
                'id'          => $r->id,
                'name'        => $r->name ?? '',
                'description' => $r->description ?: null,
                'hidden'      => (bool)($r->hidden ?? false),
                'parentId'    => $r->parentid ? (int)$r->parentid : null,
            ])->values()->all(),
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'downloads':
        $total = _safeCount('tbldownloads');
        $rows  = _safeRows('tbldownloads', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        echo json_encode([
            'items' => $rows->map(fn($r) => [
                'title'          => $r->name ?? '',
                'description'    => $r->description ?: null,
                'type'           => $r->type ?? 'file',
                'filename'       => $r->filename ?? '',
                'categoryId'     => $r->catid ? (int)$r->catid : null,
                'clientsOnly'    => (bool)($r->clientsonly ?? false),
                'hidden'         => (bool)($r->hidden ?? false),
            ])->values()->all(),
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    case 'network_issues':
        $total = _safeCount('tblnetworkissues');
        $rows  = _safeRows('tblnetworkissues', fn($q) => $q->orderBy('id')->skip($offset)->take($perPage)->get());
        echo json_encode([
            'items' => $rows->map(fn($r) => [
                'title'       => $r->title ?? '',
                'type'        => $r->type ?? 'Outage',
                'status'      => $r->status ?? 'Active',
                'priority'    => $r->priority ?? 'Medium',
                'description' => $r->body ?? '',
                'startDate'   => ($r->date && $r->date !== '0000-00-00 00:00:00') ? _toIso($r->date) : null,
                'endDate'     => ($r->enddate && $r->enddate !== '0000-00-00 00:00:00') ? _toIso($r->enddate) : null,
            ])->values()->all(),
            'totalPages' => $perPage > 0 ? (int) ceil($total / $perPage) : 1,
        ]);
        break;

    default:
        http_response_code(400);
        echo json_encode(['error' => "Unknown action: {$action}"]);
}

function _mapOrderStatus(string $s): string {
    return match(strtolower($s)) { 'active'=>'Active','cancelled'=>'Cancelled','fraud'=>'Fraud',default=>'Pending' };
}
function _mapQuoteStage(string $s): string {
    return match(strtolower($s)) { 'delivered'=>'Delivered','accepted'=>'Accepted','lost'=>'Lost','dead'=>'Dead',default=>'Draft' };
}

function _mapClientStatus(string $s): string {
    return match(strtolower($s)) { 'active'=>'Active','inactive'=>'Inactive','closed'=>'Closed',default=>'Active' };
}
function _mapInvoiceStatus(string $s): string {
    return match(strtolower($s)) {
        'paid'            => 'Paid',
        'unpaid'          => 'Unpaid',
        'overdue'         => 'Overdue',
        'cancelled'       => 'Cancelled',
        'draft'           => 'Draft',
        'refunded'        => 'Refunded',
        'collections'     => 'Collections',
        'payment pending' => 'PaymentPending',
        default           => 'Unpaid',
    };
}
function _mapTicketStatus(string $s): string {
    return match(strtolower($s)) { 'open'=>'Open','answered'=>'Answered','customer-reply'=>'Open','closed'=>'Closed','on hold'=>'On Hold',default=>'Open' };
}
function _toIso(?string $d): string {
    if (!$d || $d === '0000-00-00') return date('c');
    return date('c', strtotime($d));
}
