<?php

namespace Innovayse\Migration;

/**
 * Exports WHMCS data and sends it to the Innovayse Migration API in batches.
 */
class Exporter
{
    private string $panelUrl;
    private string $key;
    private int $batchSize;
    private bool $totalsSent = false;

    private bool $doClients  = true;
    private bool $doInvoices = true;
    private bool $doServices = true;
    private bool $doDomains  = true;
    private bool $doTickets  = true;

    /** @var array<string, int> */
    private array $stats = [
        'clients'  => 0,
        'invoices' => 0,
        'services' => 0,
        'domains'  => 0,
        'tickets'  => 0,
    ];

    public function __construct(string $panelUrl, string $key, int $batchSize = 100, ?array $config = null)
    {
        $this->panelUrl  = rtrim($panelUrl, '/');
        $this->key       = $key;
        $this->batchSize = $batchSize;

        if ($config !== null) {
            $sel = $config['entitySelection'] ?? [];
            $this->doClients  = (bool)($sel['clients']  ?? true);
            $this->doInvoices = (bool)($sel['invoices'] ?? true);
            $this->doServices = (bool)($sel['services'] ?? true);
            $this->doDomains  = (bool)($sel['domains']  ?? true);
            $this->doTickets  = (bool)($sel['tickets']  ?? true);
        }
    }

    /**
     * Runs the full export: counts totals, then exports each entity type in batches.
     *
     * @return array{success: bool, message: string, stats: array, error: string}
     */
    public function run(): array
    {
        try {
            $totals = $this->countTotals();
            if ($this->doClients)  $this->exportClients($totals);
            if ($this->doInvoices) $this->exportInvoices($totals);
            if ($this->doServices) $this->exportServices($totals);
            if ($this->doDomains)  $this->exportDomains($totals);
            $this->exportTickets($totals); // always last — triggers job completion

            // If no batches were sent at all (every table was empty),
            // send one synthetic Tickets batch to start + complete the job.
            if (!$this->totalsSent) {
                $this->post('/api/migrations/import', [
                    'key'        => $this->key,
                    'entityType' => 'Tickets',
                    'page'       => 1,
                    'totalPages' => 1,
                    'totals'     => [
                        'clients'  => 0,
                        'invoices' => 0,
                        'services' => 0,
                        'domains'  => 0,
                        'tickets'  => 0,
                    ],
                    'tickets'    => [],
                ]);
            }

            return [
                'success' => true,
                'message' => 'All data exported successfully.',
                'stats'   => $this->stats,
                'error'   => '',
            ];
        } catch (\Throwable $e) {
            return [
                'success' => false,
                'message' => 'Export failed: ' . $e->getMessage(),
                'stats'   => $this->stats,
                'error'   => $e->getMessage(),
            ];
        }
    }

    // ── Count totals ─────────────────────────────────────────────────────────

    /** @return array<string, int> */
    private function countTotals(): array
    {
        return [
            'clients'  => (int)\Illuminate\Database\Capsule\Manager::table('tblclients')->count(),
            'invoices' => (int)\Illuminate\Database\Capsule\Manager::table('tblinvoices')->count(),
            'services' => (int)\Illuminate\Database\Capsule\Manager::table('tblhosting')->count(),
            'domains'  => (int)\Illuminate\Database\Capsule\Manager::table('tbldomains')->count(),
            'tickets'  => (int)\Illuminate\Database\Capsule\Manager::table('tbltickets')->count(),
        ];
    }

    // ── Clients ──────────────────────────────────────────────────────────────

    private function exportClients(array $totals): void
    {
        $this->paginate('tblclients', $totals, 'clients', function (array $rows): array {
            $records = [];
            foreach ($rows as $row) {
                $records[] = [
                    'email'     => $row->email,
                    'firstName' => $row->firstname,
                    'lastName'  => $row->lastname,
                    'company'   => $row->companyname ?: null,
                    'phone'     => $row->phonenumber ?: null,
                    'address'   => $row->address1 ?: null,
                    'city'      => $row->city ?: null,
                    'state'     => $row->state ?: null,
                    'postCode'  => $row->postcode ?: null,
                    'country'   => $row->country ?: null,
                    'status'    => $this->mapClientStatus($row->status),
                ];
            }
            return $records;
        });
    }

    private function mapClientStatus(string $status): string
    {
        return match (strtolower($status)) {
            'active'   => 'Active',
            'inactive' => 'Inactive',
            'closed'   => 'Closed',
            default    => 'Active',
        };
    }

    // ── Invoices ─────────────────────────────────────────────────────────────

    private function exportInvoices(array $totals): void
    {
        $this->paginate('tblinvoices', $totals, 'invoices', function (array $rows): array {
            $records = [];
            foreach ($rows as $row) {
                // Load client email
                $client = \Illuminate\Database\Capsule\Manager::table('tblclients')
                    ->where('id', $row->userid)
                    ->first(['email']);

                if (!$client) continue;

                // Load invoice items
                $items = \Illuminate\Database\Capsule\Manager::table('tblinvoiceitems')
                    ->where('invoiceid', $row->id)
                    ->get(['description', 'amount', 'quantity'])
                    ->map(fn($i) => [
                        'description' => $i->description,
                        'amount'      => (float)$i->amount,
                        'quantity'    => max(1, (int)$i->quantity),
                    ])->toArray();

                $records[] = [
                    'clientEmail' => $client->email,
                    'total'       => (float)$row->total,
                    'status'      => $this->mapInvoiceStatus($row->status),
                    'date'        => $this->toIso($row->date),
                    'dueDate'     => $this->toIso($row->duedate),
                    'items'       => $items,
                ];
            }
            return $records;
        });
    }

    private function mapInvoiceStatus(string $status): string
    {
        return match (strtolower($status)) {
            'paid'       => 'Paid',
            'unpaid'     => 'Unpaid',
            'overdue'    => 'Overdue',
            'cancelled'  => 'Cancelled',
            default      => 'Unpaid',
        };
    }

    // ── Services ─────────────────────────────────────────────────────────────

    private function exportServices(array $totals): void
    {
        $this->paginate('tblhosting', $totals, 'services', function (array $rows): array {
            $records = [];
            foreach ($rows as $row) {
                $client = \Illuminate\Database\Capsule\Manager::table('tblclients')
                    ->where('id', $row->userid)
                    ->first(['email']);

                if (!$client) continue;

                $product = \Illuminate\Database\Capsule\Manager::table('tblproducts')
                    ->where('id', $row->packageid)
                    ->first(['name']);

                $records[] = [
                    'clientEmail'  => $client->email,
                    'productName'  => $product->name ?? 'Unknown Product',
                    'billingCycle' => $row->billingcycle ?? 'monthly',
                    'status'       => $row->domainstatus ?? 'Active',
                ];
            }
            return $records;
        });
    }

    // ── Domains ──────────────────────────────────────────────────────────────

    private function exportDomains(array $totals): void
    {
        $this->paginate('tbldomains', $totals, 'domains', function (array $rows): array {
            $records = [];
            foreach ($rows as $row) {
                $client = \Illuminate\Database\Capsule\Manager::table('tblclients')
                    ->where('id', $row->userid)
                    ->first(['email']);

                if (!$client) continue;

                $records[] = [
                    'clientEmail'  => $client->email,
                    'domainName'   => $row->domain,
                    'registeredAt' => $this->toIso($row->registrationdate),
                    'expiresAt'    => $this->toIso($row->expirydate),
                ];
            }
            return $records;
        });
    }

    // ── Tickets ──────────────────────────────────────────────────────────────

    private function exportTickets(array $totals): void
    {
        // If tickets table is empty, still send one batch so the job completes.
        if ($totals['tickets'] === 0) {
            $this->sendBatch('tickets', [], 1, 1, $totals);
            return;
        }

        $this->paginate('tbltickets', $totals, 'tickets', function (array $rows): array {
            $records = [];
            foreach ($rows as $row) {
                $client = \Illuminate\Database\Capsule\Manager::table('tblclients')
                    ->where('id', $row->userid)
                    ->first(['email']);

                if (!$client) continue;

                // WHMCS stores the opening message in tblticketreplies, not tbltickets.
                $firstReply = \Illuminate\Database\Capsule\Manager::table('tblticketreplies')
                    ->where('tid', $row->id)
                    ->orderBy('id')
                    ->first(['message']);

                $records[] = [
                    'clientEmail' => $client->email,
                    'subject'     => $row->title,
                    'message'     => $firstReply->message ?? '',
                    'status'      => $this->mapTicketStatus($row->status ?? 'Open'),
                    'priority'    => $row->urgency ?? 'Medium',
                    'createdAt'   => $this->toIso($row->date),
                ];
            }
            return $records;
        });
    }

    private function mapTicketStatus(string $status): string
    {
        return match (strtolower($status)) {
            'open'           => 'Open',
            'answered'       => 'Answered',
            'customer-reply' => 'Open',
            'closed'         => 'Closed',
            'on hold'        => 'On Hold',
            'in progress'    => 'Open',
            default          => 'Open',
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /** Sends a single batch directly (used when a table is empty but we still need to signal the API). */
    private function sendBatch(string $entityType, array $records, int $page, int $totalPages, array $totals): void
    {
        $sendTotals = null;
        if (!$this->totalsSent) {
            $sendTotals = [
                'clients'  => $totals['clients'],
                'invoices' => $totals['invoices'],
                'services' => $totals['services'],
                'domains'  => $totals['domains'],
                'tickets'  => $totals['tickets'],
            ];
            $this->totalsSent = true;
        }

        $this->post('/api/migrations/import', [
            'key'        => $this->key,
            'entityType' => $this->capitalise($entityType),
            'page'       => $page,
            'totalPages' => $totalPages,
            'totals'     => $sendTotals,
            $entityType  => $records,
        ]);
    }

    /**
     * Paginates a WHMCS table and sends each batch to the API.
     *
     * @param callable(array): array $mapper Maps raw DB rows to API record format.
     */
    private function paginate(
        string $table,
        array $totals,
        string $entityType,
        callable $mapper
    ): void {
        $total      = $totals[$entityType] ?? 0;
        $totalPages = max(1, (int)ceil($total / $this->batchSize));
        $page       = 1;

        \Illuminate\Database\Capsule\Manager::table($table)
            ->orderBy('id')
            ->chunk($this->batchSize, function ($rows) use (
                $entityType, $totalPages, &$page, $totals, $mapper
            ) {
                $records = $mapper($rows->all());

                // Send totals with the very first batch we actually send,
                // regardless of entity type — handles the case where earlier
                // entity tables are empty and their chunk callbacks never fire.
                $sendTotals = null;
                if (!$this->totalsSent) {
                    $sendTotals = [
                        'clients'  => $totals['clients'],
                        'invoices' => $totals['invoices'],
                        'services' => $totals['services'],
                        'domains'  => $totals['domains'],
                        'tickets'  => $totals['tickets'],
                    ];
                    $this->totalsSent = true;
                }

                $payload = [
                    'key'        => $this->key,
                    'entityType' => $this->capitalise($entityType),
                    'page'       => $page,
                    'totalPages' => $totalPages,
                    'totals'     => $sendTotals,
                    $entityType  => $records,
                ];

                $this->post('/api/migrations/import', $payload);
                $this->stats[$entityType] += \count($records);
                $page++;
            });
    }

    /** Sends a JSON POST request to the Innovayse panel API. */
    private function post(string $path, array $payload): void
    {
        $url  = $this->panelUrl . $path;
        $json = json_encode($payload);

        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_POST           => true,
            CURLOPT_POSTFIELDS     => $json,
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT        => 30,
            CURLOPT_HTTPHEADER     => [
                'Content-Type: application/json',
                'Content-Length: ' . strlen($json),
            ],
        ]);

        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        $curlErr  = curl_error($ch);
        curl_close($ch);

        if ($curlErr) {
            throw new \RuntimeException("cURL error: $curlErr");
        }

        if ($httpCode < 200 || $httpCode >= 300) {
            $body = json_decode($response, true);
            $msg  = $body['error'] ?? $response;
            throw new \RuntimeException("API error $httpCode: $msg");
        }
    }

    /** Converts a WHMCS date string to ISO 8601. */
    private function toIso(?string $date): string
    {
        if (!$date || $date === '0000-00-00') {
            return date('c');
        }
        return date('c', strtotime($date));
    }

    /** Capitalises first letter (Clients, Invoices, etc.) */
    private function capitalise(string $str): string
    {
        return ucfirst($str);
    }
}
