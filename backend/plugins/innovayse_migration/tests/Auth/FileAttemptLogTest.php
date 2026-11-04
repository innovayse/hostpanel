<?php

declare(strict_types=1);

namespace HostpanelMigration\Tests\Auth;

use HostpanelMigration\Auth\AttemptWindow;
use HostpanelMigration\Auth\FileAttemptLog;
use PHPUnit\Framework\TestCase;

/**
 * The attempt log api.php keeps on disk: a window survives between requests, a damaged file reads
 * as no window instead of failing every request, and clearing removes it.
 */
final class FileAttemptLogTest extends TestCase
{
    private const ADDRESS = '203.0.113.7';

    private string $directory;

    protected function setUp(): void
    {
        $this->directory = sys_get_temp_dir() . DIRECTORY_SEPARATOR . 'hostpanel-migration-test-' . bin2hex(random_bytes(6));
    }

    protected function tearDown(): void
    {
        foreach (glob($this->directory . DIRECTORY_SEPARATOR . '*') ?: [] as $file) {
            unlink($file);
        }
        if (is_dir($this->directory)) {
            rmdir($this->directory);
        }
    }

    public function testWindowIsReadBackByANewInstance(): void
    {
        (new FileAttemptLog($this->directory))->write(self::ADDRESS, new AttemptWindow(3, 1_700_000_000));

        $window = (new FileAttemptLog($this->directory))->read(self::ADDRESS);

        self::assertNotNull($window);
        self::assertSame(3, $window->failures);
        self::assertSame(1_700_000_000, $window->startedAt);
    }

    public function testUnknownAddressReadsAsNoWindow(): void
    {
        self::assertNull((new FileAttemptLog($this->directory))->read(self::ADDRESS));
    }

    public function testDamagedFileReadsAsNoWindow(): void
    {
        $log = new FileAttemptLog($this->directory);
        $log->write(self::ADDRESS, new AttemptWindow(1, 1));
        foreach (glob($this->directory . DIRECTORY_SEPARATOR . '*') ?: [] as $file) {
            file_put_contents($file, '{not json');
        }

        self::assertNull($log->read(self::ADDRESS));
    }

    public function testFileNameDoesNotHoldTheAddress(): void
    {
        (new FileAttemptLog($this->directory))->write(self::ADDRESS, new AttemptWindow(1, 1));

        $names = implode(' ', glob($this->directory . DIRECTORY_SEPARATOR . '*') ?: []);
        self::assertStringNotContainsString(self::ADDRESS, $names);
    }

    public function testClearRemovesTheWindow(): void
    {
        $log = new FileAttemptLog($this->directory);
        $log->write(self::ADDRESS, new AttemptWindow(2, 1));

        $log->clear(self::ADDRESS);

        self::assertNull($log->read(self::ADDRESS));
    }
}
