<?php

declare(strict_types=1);

namespace HostpanelMigration\Tests\Fixtures;

use HostpanelMigration\Auth\AttemptLog;
use HostpanelMigration\Auth\AttemptLogUnavailable;
use HostpanelMigration\Auth\AttemptWindow;

/**
 * An attempt log whose storage cannot be used, as when the temp folder is not writable.
 */
final class UnavailableAttemptLog implements AttemptLog
{
    /** {@inheritDoc} */
    public function read(string $address): ?AttemptWindow
    {
        throw new AttemptLogUnavailable('The attempt log cannot be read.');
    }

    /** {@inheritDoc} */
    public function write(string $address, AttemptWindow $window): void
    {
        throw new AttemptLogUnavailable('The attempt log cannot be written.');
    }

    /** {@inheritDoc} */
    public function clear(string $address): void
    {
        throw new AttemptLogUnavailable('The attempt log cannot be cleared.');
    }
}
