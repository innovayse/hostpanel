<?php

declare(strict_types=1);

namespace HostpanelMigration\Tests\Kernel;

use HostpanelMigration\ApiKernel;
use HostpanelMigration\Auth\AttemptLog;
use HostpanelMigration\Auth\FailedAttemptThrottle;
use HostpanelMigration\Database\SourceDatabase;
use HostpanelMigration\Database\Table;
use HostpanelMigration\Http\HttpStatus;
use HostpanelMigration\Http\JsonResponse;
use HostpanelMigration\Http\Request;
use HostpanelMigration\Module;
use HostpanelMigration\Tests\Fixtures\InMemoryAttemptLog;
use HostpanelMigration\Tests\Fixtures\InMemoryDatabase;
use HostpanelMigration\Tests\Fixtures\UnavailableAttemptLog;
use Illuminate\Database\Connection;
use Illuminate\Database\Schema\Blueprint;
use PHPUnit\Framework\TestCase;

/**
 * The whole request path of the addon API: the key is checked before anything else, a locked
 * address is refused even with the right key, and an unknown action is reported only to a caller
 * with the right key.
 */
final class ApiKernelTest extends TestCase
{
    private const KEY = '0123456789abcdef0123456789abcdef';
    private const ADDRESS = '203.0.113.7';
    private const NOW = 1_700_000_000;

    public function testRightKeyAndPingIsAnswered(): void
    {
        $response = self::kernel(self::KEY)->handle(self::request(self::KEY, 'ping'), self::ADDRESS, self::NOW);

        self::assertSame(HttpStatus::Ok, $response->status);
        self::assertSame(['ok' => true], $response->body);
    }

    public function testWrongKeyIsRefusedWith401(): void
    {
        $response = self::kernel(self::KEY)->handle(self::request('wrong', 'ping'), self::ADDRESS, self::NOW);

        self::assertError($response, HttpStatus::Unauthorized, 'Invalid migration key.');
    }

    public function testNoKeyConfiguredRefusesEveryKey(): void
    {
        $response = self::kernel('')->handle(self::request('', 'ping'), self::ADDRESS, self::NOW);

        self::assertError($response, HttpStatus::Unauthorized, 'Invalid migration key.');
    }

    public function testWrongKeyWithAnUnknownActionStillAnswers401(): void
    {
        $response = self::kernel(self::KEY)->handle(self::request('wrong', 'no_such_action'), self::ADDRESS, self::NOW);

        self::assertError($response, HttpStatus::Unauthorized, 'Invalid migration key.');
    }

    public function testRightKeyWithAnUnknownActionAnswers400(): void
    {
        $response = self::kernel(self::KEY)->handle(self::request(self::KEY, 'no_such_action'), self::ADDRESS, self::NOW);

        self::assertError($response, HttpStatus::BadRequest, 'Unknown action.');
    }

    public function testLockedAddressIsRefusedWith429EvenWithTheRightKey(): void
    {
        $kernel = self::kernel(self::KEY);
        for ($i = 0; $i < FailedAttemptThrottle::MAX_FAILURES; $i++) {
            $kernel->handle(self::request('wrong', 'ping'), self::ADDRESS, self::NOW);
        }

        $response = $kernel->handle(self::request(self::KEY, 'ping'), self::ADDRESS, self::NOW + 1);

        self::assertError($response, HttpStatus::TooManyRequests, 'Too many failed attempts.');
    }

    public function testLockOfOneAddressDoesNotRefuseAnother(): void
    {
        $kernel = self::kernel(self::KEY);
        for ($i = 0; $i < FailedAttemptThrottle::MAX_FAILURES; $i++) {
            $kernel->handle(self::request('wrong', 'ping'), self::ADDRESS, self::NOW);
        }

        $response = $kernel->handle(self::request(self::KEY, 'ping'), '198.51.100.9', self::NOW + 1);

        self::assertSame(HttpStatus::Ok, $response->status);
    }

    public function testUnusableAttemptLogAnswers503(): void
    {
        $response = self::kernel(self::KEY, new UnavailableAttemptLog())
            ->handle(self::request(self::KEY, 'ping'), self::ADDRESS, self::NOW);

        self::assertError($response, HttpStatus::ServiceUnavailable, 'Attempt log unavailable.');
    }

    /**
     * Builds the kernel the way api.php does, over a database whose addon settings hold the key.
     */
    private static function kernel(string $configuredKey, ?AttemptLog $attempts = null): ApiKernel
    {
        return ApiKernel::create(new SourceDatabase(self::databaseWithKey($configuredKey)), $attempts ?? new InMemoryAttemptLog());
    }

    /**
     * An in-memory database with the addon settings table, holding the key when one is given.
     */
    private static function databaseWithKey(string $key): Connection
    {
        $connection = InMemoryDatabase::connection();
        $connection->getSchemaBuilder()->create(Table::ADDON_MODULES, static function (Blueprint $table): void {
            $table->string('module');
            $table->string('setting');
            $table->text('value');
        });
        if ($key !== '') {
            $connection->table(Table::ADDON_MODULES)->insert(['module' => Module::ID, 'setting' => Module::KEY_SETTING, 'value' => $key]);
        }

        return $connection;
    }

    /**
     * A request body as Hostpanel sends it.
     */
    private static function request(string $key, string $action): Request
    {
        return Request::fromJson((string) json_encode(['key' => $key, 'action' => $action]));
    }

    /**
     * Asserts the response is the given error and carries nothing else.
     */
    private static function assertError(JsonResponse $response, HttpStatus $status, string $message): void
    {
        self::assertSame($status, $response->status);
        self::assertSame(['error' => $message], $response->body);
    }
}
