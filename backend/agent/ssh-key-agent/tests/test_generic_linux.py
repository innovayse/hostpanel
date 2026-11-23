"""
Tests of the generic-linux provider helpers (detect, capabilities, php-versions, ensure-website, remove-website,
ensure-database, remove-database).
Run: python3 -m unittest discover -s ssh-key-agent/tests

The helpers run for real, through the agent, against a stand-in server tree in a temporary directory (HP_ROOT) with
fake systemctl, web server test commands, a2* tools, php-fpm binaries and a fake mariadb or mysql client, so no root and
no real server is needed. The matrix covers Apache on Debian, Apache on RHEL (Remi), nginx on RHEL (AppStream) and nginx on Debian.
"""

import os
import shutil
import subprocess

import test_ops
from test_ops import DB, DB_REMOVE, FAKE_MYSQL, FAKE_OPENSSL, PASSWORD, SITE, WEBSITE_REMOVE, write_executable

GENERIC_HELPERS = os.path.join(os.path.dirname(__file__), "..", "providers", "generic-linux")

FAKE_SYSTEMCTL = """#!/bin/sh
case "$1" in
  is-active) [ -e "$HP_ROOT/active/$3" ] ;;
  reload) echo "systemctl $*" >> "$HP_ROOT/calls.log"; [ ! -e "$HP_ROOT/fail-reload" ] ;;
esac
"""
FAKE_WEB_TEST = """#!/bin/sh
echo "$(basename "$0") $*" >> "$HP_ROOT/calls.log"
[ ! -e "$HP_ROOT/fail-web" ]
"""
FAKE_FPM = """#!/bin/sh
echo "fpm ${0#$HP_ROOT} $*" >> "$HP_ROOT/calls.log"
[ ! -e "$HP_ROOT/fail-fpm" ]
"""
FAKE_A2ENSITE = """#!/bin/sh
echo "a2ensite $*" >> "$HP_ROOT/calls.log"
mkdir -p "$HP_ROOT/etc/apache2/sites-enabled"
ln -sfn "../sites-available/$1.conf" "$HP_ROOT/etc/apache2/sites-enabled/$1.conf"
"""
FAKE_A2DISSITE = """#!/bin/sh
echo "a2dissite $*" >> "$HP_ROOT/calls.log"
rm -f "$HP_ROOT/etc/apache2/sites-enabled/$1.conf"
"""
FAKE_A2ENMOD = """#!/bin/sh
echo "a2enmod $*" >> "$HP_ROOT/calls.log"
mkdir -p "$HP_ROOT/etc/apache2/mods-enabled"
touch "$HP_ROOT/etc/apache2/mods-enabled/$1.load"
"""
FAKE_APPSTREAM_PHP = """#!/bin/sh
cat "$HP_ROOT/php-version"
"""

OPCACHE_LINES = ("opcache.validate_timestamps] = 1", "opcache.revalidate_freq] = 0", "opcache.revalidate_path] = 1",
                 "realpath_cache_size] = 0")


class Scenario:
    """One server layout: its web server, its PHP-FPM layout and the versions installed, and where each thing lives."""

    def __init__(self, name, unit, php, versions, nginx_user="nginx"):
        self.name = name
        self.unit = unit
        self.php = php
        self.versions = versions
        self.nginx_user = nginx_user

    @property
    def web_user(self):
        """The user the web server runs as, which owns the pool's socket."""
        return {"apache2": "www-data", "httpd": "apache", "nginx": self.nginx_user}[self.unit]

    @property
    def vhost_dir(self):
        """The directory (below the server root) the virtual hosts are written to."""
        return {"apache2": "etc/apache2/sites-available", "httpd": "etc/httpd/conf.d", "nginx": "etc/nginx/conf.d"}[self.unit]

    @property
    def test_call(self):
        """The recorded call of the web server's configuration test."""
        return {"apache2": "apache2ctl -t", "httpd": "httpd -t", "nginx": "nginx -t"}[self.unit]

    def nn(self, version):
        """The version without its dot."""
        return version.replace(".", "")

    def pool_dir(self, version):
        """The pool directory (below the server root) of a version."""
        return {"sury": "etc/php/%s/fpm/pool.d" % version, "remi": "etc/opt/remi/php%s/php-fpm.d" % self.nn(version),
                "appstream": "etc/php-fpm.d"}[self.php]

    def run_dir(self, version):
        """The logical socket directory of a version."""
        return {"sury": "/run/php", "remi": "/var/opt/remi/php%s/run/php-fpm" % self.nn(version),
                "appstream": "/run/php-fpm"}[self.php]

    def service(self, version):
        """The php-fpm unit of a version."""
        return {"sury": "php%s-fpm" % version, "remi": "php%s-php-fpm" % self.nn(version), "appstream": "php-fpm"}[self.php]

    def cli(self, version):
        """The logical path of a version's CLI."""
        return {"sury": "/usr/bin/php%s" % version, "remi": "/opt/remi/php%s/root/usr/bin/php" % self.nn(version),
                "appstream": "/usr/bin/php"}[self.php]

    def fpm_call(self, version):
        """The tail of the recorded php-fpm test call of a version."""
        return {"sury": "/usr/sbin/php-fpm%s -t" % version, "remi": "/opt/remi/php%s/root/usr/sbin/php-fpm -t" % self.nn(version),
                "appstream": "/usr/sbin/php-fpm -t"}[self.php]


SCENARIOS = [
    Scenario("debian-apache", "apache2", "sury", ["8.2", "8.3"]),
    Scenario("rhel-apache-remi", "httpd", "remi", ["8.1", "8.3"]),
    Scenario("rhel-nginx-appstream", "nginx", "appstream", ["8.2"], nginx_user="nginx"),
    Scenario("debian-nginx", "nginx", "sury", ["8.3"], nginx_user="www-data"),
]


class GenericLinuxCase(test_ops.OpsTestCase):
    """The stand-in server of OpsTestCase, rebuilt per scenario for the generic-linux helpers."""

    def build(self, scenario, active=True):
        """Installs the real helpers and a server tree for the scenario; the web server is running unless told not to."""
        self.scenario = scenario
        shutil.copytree(GENERIC_HELPERS, self.helper_dir)
        for name in os.listdir(self.helper_dir):
            os.chmod(os.path.join(self.helper_dir, name), 0o755)
        root = self.root
        write_executable(os.path.join(root, "usr/bin/systemctl"), FAKE_SYSTEMCTL)
        write_executable(os.path.join(root, "usr/bin/chown"), test_ops.FAKE_LOGGER)
        os.makedirs(os.path.join(root, scenario.vhost_dir))
        write_executable(os.path.join(root, "usr/sbin", {"apache2": "apache2ctl", "httpd": "httpd", "nginx": "nginx"}[scenario.unit]),
                         FAKE_WEB_TEST)
        if scenario.unit == "apache2":
            os.makedirs(os.path.join(root, "etc/apache2/sites-enabled"))
            for tool, body in (("a2ensite", FAKE_A2ENSITE), ("a2dissite", FAKE_A2DISSITE), ("a2enmod", FAKE_A2ENMOD)):
                write_executable(os.path.join(root, "usr/sbin", tool), body)
        if scenario.unit == "nginx":
            with open(os.path.join(root, "etc/nginx/nginx.conf"), "w") as handle:
                handle.write("user %s;\nworker_processes auto;\n" % scenario.nginx_user)
        for version in scenario.versions:
            self.install_php(scenario, version)
        os.makedirs(os.path.join(self.home_root, "alice"), exist_ok=True)
        if active:
            self.activate(scenario.unit)

    def install_php(self, scenario, version):
        """Adds one PHP version in the scenario's layout."""
        root, nn = self.root, scenario.nn(version)
        if scenario.php == "sury":
            write_executable(os.path.join(root, "usr/sbin/php-fpm" + version), FAKE_FPM)
            write_executable(os.path.join(root, "usr/bin/php" + version), test_ops.FAKE_LOGGER)
        elif scenario.php == "remi":
            write_executable(os.path.join(root, "opt/remi/php%s/root/usr/sbin/php-fpm" % nn), FAKE_FPM)
            write_executable(os.path.join(root, "opt/remi/php%s/root/usr/bin/php" % nn), test_ops.FAKE_LOGGER)
        else:
            write_executable(os.path.join(root, "usr/sbin/php-fpm"), FAKE_FPM)
            write_executable(os.path.join(root, "usr/bin/php"), FAKE_APPSTREAM_PHP)
            with open(os.path.join(root, "php-version"), "w") as handle:
                handle.write(version)
        os.makedirs(os.path.join(root, scenario.pool_dir(version)), exist_ok=True)

    def activate(self, unit):
        """Marks a systemd unit as running."""
        os.makedirs(os.path.join(self.root, "active"), exist_ok=True)
        open(os.path.join(self.root, "active", unit), "w").close()

    def set_agent_conf(self, text):
        """Writes the agent's own configuration file the helpers read `web_server` from."""
        path = os.path.join(self.root, "etc/hostpanel-ssh-key-agent/agent.conf")
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w") as handle:
            handle.write("[agent]\n" + text)

    def site(self, **changes):
        """The standard ensure-website request, for the scenario's newest PHP version unless changed."""
        request = dict(SITE, php=self.scenario.versions[-1])
        request.update(changes)
        return request

    def vhost_path(self, hostname="app.example.com"):
        """The file path of a hostname's virtual host."""
        return os.path.join(self.root, self.scenario.vhost_dir, "hp-%s.conf" % hostname)

    def vhost(self, hostname="app.example.com"):
        """The text of a hostname's virtual host."""
        return self.read(self.vhost_path(hostname))

    def pool_path(self, version=None):
        """The file path of the account's pool of a version (the scenario's last one by default)."""
        version = version or self.scenario.versions[-1]
        return os.path.join(self.root, self.scenario.pool_dir(version), "hp-alice.conf")

    def enabled_link(self, hostname="app.example.com"):
        """The path of the Debian Apache enabling link of a hostname."""
        return os.path.join(self.root, "etc/apache2/sites-enabled", "hp-%s.conf" % hostname)

    def all_files(self):
        """Every file or link the helpers may have written (config, pools, state), relative to the root; enabled modules are left out, a run never undoes those."""
        found = []
        for base in ("etc", "var"):
            for directory, _, names in os.walk(os.path.join(self.root, base)):
                for name in names:
                    if "mods-enabled" in directory:
                        continue
                    found.append(os.path.relpath(os.path.join(directory, name), self.root))
        return sorted(found)

    def run_helper(self, name, *args):
        """Runs a helper the way the agent does and returns its output."""
        done = subprocess.run([os.path.join(self.helper_dir, name), *args], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                              env=dict(test_ops.agent.JAIL_ENV), check=True, text=True)
        return done.stdout


class MatrixTests:
    """
    ensure-website and remove-website on one server layout. A mixin: one concrete test class per scenario is generated
    below, so each scenario gets a fresh stand-in server.
    """

    scenario_index = 0

    def setUp(self):
        """Builds the scenario's server."""
        super().setUp()
        self.build(SCENARIOS[self.scenario_index])

    def test_ensure_creates_everything_and_is_idempotent(self):
        scenario = self.scenario
        version = scenario.versions[-1]
        nn = scenario.nn(version)
        sock = "%s/hp-alice-%s.sock" % (scenario.run_dir(version), nn)
        result = self.call(**self.site())
        self.assertEqual(result, {"v": 1, "ok": True, "changed": True, "php_cli": scenario.cli(version)})
        vhost = self.vhost()
        self.assertTrue(vhost.startswith("# hostpanel-ssh-key-agent managed\n"))
        self.assertIn("/home/alice/sites/app", vhost)
        self.assertIn(sock, vhost)
        pool = self.read(self.pool_path())
        self.assertTrue(pool.startswith("; hostpanel-ssh-key-agent managed\n[hp-alice-%s]\n" % nn))
        self.assertIn("listen = %s\n" % sock, pool)
        self.assertIn("user = alice\n", pool)
        self.assertIn("listen.owner = %s\n" % scenario.web_user, pool)
        calls = self.calls()
        self.assertIn(scenario.test_call, calls)
        self.assertIn(scenario.fpm_call(version), calls)
        self.assertIn("systemctl reload %s\n" % scenario.service(version), calls)
        self.assertIn("systemctl reload %s\n" % scenario.unit, calls)
        if scenario.unit == "apache2":
            self.assertTrue(os.path.islink(self.enabled_link()))
            self.assertIn("a2enmod proxy_fcgi", calls)
        self.assertIn("website.app.example.com", os.listdir(self.registry))
        files, before = self.all_files(), (vhost, pool, calls.count("reload"))
        self.assertEqual(self.call(**self.site()), {"v": 1, "ok": True, "changed": False, "php_cli": scenario.cli(version)})
        self.assertEqual(self.all_files(), files)
        self.assertEqual((self.vhost(), self.read(self.pool_path()), self.calls().count("reload")), before)

    def test_remove_takes_back_only_what_ensure_made(self):
        before = self.all_files()
        self.call(**self.site())
        self.assertEqual(self.call(**WEBSITE_REMOVE), {"v": 1, "ok": True, "changed": True})
        self.assertEqual(self.all_files(), before)
        self.assertTrue(os.path.isdir(os.path.join(self.home_root, "alice", "sites")))
        self.assertEqual(self.call(**WEBSITE_REMOVE), {"v": 1, "ok": True, "changed": False})

    def test_remove_deletes_the_docroot_it_created_when_it_is_empty(self):
        self.call(**self.site(link_root=False))
        docroot = os.path.join(self.home_root, "alice", "sites/app")
        self.assertTrue(os.path.isdir(docroot))
        self.call(**WEBSITE_REMOVE)
        self.assertFalse(os.path.exists(docroot))
        self.call(**self.site(link_root=False))
        with open(os.path.join(docroot, "index.html"), "w") as handle:
            handle.write("kept")
        self.call(**WEBSITE_REMOVE)
        self.assertTrue(os.path.exists(os.path.join(docroot, "index.html")))

    def test_without_php_there_is_no_pool_and_no_cli_in_the_answer(self):
        request = dict(SITE)
        del request["php"], request["link_root"]
        self.assertEqual(self.call(**request), {"v": 1, "ok": True, "changed": True})
        self.assertFalse(os.path.exists(self.pool_path()))
        self.assertNotIn(".sock", self.vhost())
        self.assertNotIn("a2enmod", self.calls())
        self.assertNotIn("fpm", self.calls())
        self.assertEqual(self.call(**WEBSITE_REMOVE), {"v": 1, "ok": True, "changed": True})

    def test_docroot_is_owned_by_the_account_and_the_home_is_enterable(self):
        home = os.path.join(self.home_root, "alice")
        os.chmod(home, 0o750)
        self.call(**self.site(link_root=False))
        self.assertEqual(os.stat(home).st_mode & 0o001, 0o001)
        owner = os.stat(home)
        self.assertIn("chown -h %d:%d" % (owner.st_uid, owner.st_gid), self.calls())
        self.assertEqual(os.stat(os.path.join(home, "sites/app")).st_mode & 0o7777, 0o755)
        self.assertEqual(os.stat(os.path.join(home, "sites")).st_mode & 0o7777, 0o755)

    def test_link_root_is_per_server(self):
        self.call(**self.site())
        # The link point is left to the deployer; only its parent is made.
        self.assertFalse(os.path.lexists(os.path.join(self.home_root, "alice", "sites/app")))
        self.assertTrue(os.path.isdir(os.path.join(self.home_root, "alice", "sites")))
        vhost, pool = self.vhost(), self.read(self.pool_path())
        if self.scenario.unit == "nginx":
            self.assertIn("fastcgi_param SCRIPT_FILENAME $realpath_root$fastcgi_script_name;", vhost)
            self.assertIn("fastcgi_param DOCUMENT_ROOT $realpath_root;", vhost)
            for line in OPCACHE_LINES:
                self.assertNotIn(line, pool)
        else:
            self.assertNotIn("realpath_root", vhost)
            for line in OPCACHE_LINES:
                self.assertEqual(pool.count(line), 1, line)

    def test_without_link_root_the_pool_has_no_opcache_lines_and_the_docroot_is_created(self):
        self.call(**self.site(link_root=False))
        for line in OPCACHE_LINES:
            self.assertNotIn(line, self.read(self.pool_path()))
        self.assertTrue(os.path.isdir(os.path.join(self.home_root, "alice", "sites/app")))

    def test_a_failing_web_server_test_restores_everything(self):
        before = self.all_files()
        self.touch("fail-web")
        self.assertEqual(self.call(**self.site())["error"], "website_failed")
        self.assertEqual(self.all_files(), before)
        self.assertFalse(os.path.lexists(self.enabled_link()))
        self.assertFalse(os.path.exists(os.path.join(self.home_root, "alice", "sites")))
        self.assertNotIn("reload", self.calls())

    def test_a_failing_pool_test_restores_the_pool_and_the_vhost(self):
        before = self.all_files()
        self.touch("fail-fpm")
        self.assertEqual(self.call(**self.site())["error"], "website_failed")
        self.assertEqual(self.all_files(), before)
        self.assertFalse(os.path.lexists(self.enabled_link()))
        self.assertNotIn("reload", self.calls())

    def test_a_failing_test_on_remove_puts_the_website_back(self):
        self.call(**self.site())
        files, vhost, pool = self.all_files(), self.vhost(), self.read(self.pool_path())
        link = os.path.islink(self.enabled_link())
        self.touch("fail-web")
        self.assertEqual(self.call(**WEBSITE_REMOVE)["error"], "website_failed")
        self.assertEqual((self.all_files(), self.vhost(), self.read(self.pool_path())), (files, vhost, pool))
        self.assertEqual(os.path.islink(self.enabled_link()), link)
        self.assertTrue(os.path.exists(os.path.join(self.registry, "website.app.example.com")))

    def test_no_backup_is_left_behind_after_a_run(self):
        self.call(**self.site())
        self.call(**WEBSITE_REMOVE)
        self.touch("fail-web")
        self.call(**self.site())
        backups = os.path.join(self.root, "var/lib/hostpanel-ssh-key-agent/backups")
        self.assertEqual(os.listdir(backups) if os.path.isdir(backups) else [], [])


for _index, _scenario in enumerate(SCENARIOS):
    _name = "Matrix" + "".join(part.capitalize() for part in _scenario.name.split("-")) + "Tests"
    globals()[_name] = type(_name, (MatrixTests, GenericLinuxCase), {"scenario_index": _index, "__doc__": _scenario.name})


class NginxTests(GenericLinuxCase):
    """nginx specifics."""

    def test_the_vhost_uses_the_resolved_root_even_without_link_root(self):
        self.build(SCENARIOS[3])
        self.call(**self.site(link_root=False))
        self.assertIn("$realpath_root", self.vhost())

    def test_the_pool_socket_belongs_to_the_user_nginx_runs_as(self):
        self.build(SCENARIOS[2])
        self.call(**self.site())
        self.assertIn("listen.owner = nginx\n", self.read(self.pool_path()))


class SeveralPhpVersionsTests(GenericLinuxCase):
    """One pool per account and PHP version, on a server that has several versions."""

    def setUp(self):
        super().setUp()
        self.build(SCENARIOS[0])

    def test_each_version_gets_its_own_pool_and_socket(self):
        self.call(**self.site(php="8.2", hostname="old.example.com", docroot="sites/old", link_root=False))
        self.call(**self.site(php="8.3", hostname="new.example.com", docroot="sites/new", link_root=False))
        self.assertIn("[hp-alice-82]", self.read(self.pool_path("8.2")))
        self.assertIn("[hp-alice-83]", self.read(self.pool_path("8.3")))
        self.assertIn("/run/php/hp-alice-82.sock", self.vhost("old.example.com"))
        self.assertIn("/run/php/hp-alice-83.sock", self.vhost("new.example.com"))

    def test_websites_on_one_version_share_its_pool_until_the_last_one_goes(self):
        self.call(**self.site(hostname="a.example.com", docroot="sites/a", link_root=False))
        self.call(**self.site(hostname="b.example.com", docroot="sites/b", link_root=False))
        self.call(op="remove-website", account="alice", hostname="a.example.com")
        self.assertTrue(os.path.exists(self.pool_path()))
        self.call(op="remove-website", account="alice", hostname="b.example.com")
        self.assertFalse(os.path.exists(self.pool_path()))

    def test_moving_a_website_to_another_version_drops_the_pool_nobody_uses(self):
        self.call(**self.site(php="8.2", link_root=False))
        self.assertTrue(os.path.exists(self.pool_path("8.2")))
        self.call(**self.site(php="8.3", link_root=False))
        self.assertFalse(os.path.exists(self.pool_path("8.2")))
        self.assertTrue(os.path.exists(self.pool_path("8.3")))
        self.assertIn("/run/php/hp-alice-83.sock", self.vhost())
        self.assertIn("systemctl reload php8.2-fpm\n", self.calls())

    def test_a_sibling_with_link_root_gives_the_shared_pool_the_opcache_lines(self):
        self.call(**self.site(hostname="a.example.com", docroot="sites/a", link_root=False))
        self.call(**self.site(hostname="b.example.com", docroot="sites/b"))
        for line in OPCACHE_LINES:
            self.assertEqual(self.read(self.pool_path()).count(line), 1, line)

    def test_a_version_that_is_not_installed_is_refused_and_changes_nothing(self):
        before = self.all_files()
        self.assertEqual(self.call(**self.site(php="7.4"))["error"], "php_version_unsupported")
        self.assertEqual(self.all_files(), before)
        self.assertFalse(os.path.exists(os.path.join(self.home_root, "alice", "sites")))

    def test_ping_lists_every_version_with_its_cli(self):
        ping = self.call(op="ping")
        self.assertEqual(ping["php_versions"], ["8.2", "8.3"])
        self.assertEqual(ping["php_cli"], {"8.2": "/usr/bin/php8.2", "8.3": "/usr/bin/php8.3"})


class RegistryTests(GenericLinuxCase):
    """What the agent may touch: only what it registered, and only what it wrote."""

    def setUp(self):
        super().setUp()
        self.build(SCENARIOS[0])

    def test_remove_never_touches_a_vhost_it_did_not_write(self):
        foreign = self.vhost_path("hand.example.com")
        with open(foreign, "w") as handle:
            handle.write("<VirtualHost *:80>\n\tServerName hand.example.com\n</VirtualHost>\n")
        self.assertEqual(self.call(op="remove-website", account="alice", hostname="hand.example.com")["error"], "site_not_owned")
        self.assertTrue(os.path.exists(foreign))
        self.assertNotIn("reload", self.calls())

    def test_remove_never_touches_a_marked_vhost_without_a_registry_entry(self):
        self.call(**self.site())
        os.unlink(os.path.join(self.registry, "website.app.example.com"))
        self.assertEqual(self.call(**WEBSITE_REMOVE)["error"], "site_not_owned")
        self.assertTrue(os.path.exists(self.vhost_path()))

    def test_remove_of_an_unknown_hostname_is_a_no_op(self):
        self.assertEqual(self.call(op="remove-website", account="alice", hostname="none.example.com"), {"v": 1, "ok": True, "changed": False})
        self.assertNotIn("reload", self.calls())

    def test_remove_keeps_other_websites_and_foreign_files(self):
        other = os.path.join(self.root, self.scenario.vhost_dir, "000-default.conf")
        with open(other, "w") as handle:
            handle.write("# the administrator's site\n")
        self.call(**self.site())
        self.call(**self.site(hostname="www.example.com", docroot="sites/www"))
        self.call(**WEBSITE_REMOVE)
        self.assertTrue(os.path.exists(other))
        self.assertTrue(os.path.exists(self.vhost_path("www.example.com")))
        self.assertTrue(os.path.exists(self.pool_path()))

    def test_a_hostname_registered_to_another_account_is_refused(self):
        os.makedirs(os.path.join(self.root, "var/lib/hostpanel-ssh-key-agent/bob"))
        open(os.path.join(self.root, "var/lib/hostpanel-ssh-key-agent/bob/website.app.example.com"), "w").close()
        self.assertEqual(self.call(**self.site())["error"], "site_not_owned")
        self.assertEqual(self.call(**WEBSITE_REMOVE)["error"], "site_not_owned")
        self.assertFalse(os.path.exists(self.vhost_path()))

    def test_a_vhost_the_agent_did_not_write_is_a_conflict(self):
        with open(self.vhost_path(), "w") as handle:
            handle.write("# not ours\n")
        self.assertEqual(self.call(**self.site())["error"], "website_conflict")
        self.assertEqual(self.vhost(), "# not ours\n")
        self.assertFalse(os.path.exists(self.registry))

    def test_a_pool_the_agent_did_not_write_is_a_conflict(self):
        with open(self.pool_path(), "w") as handle:
            handle.write("[mine]\n")
        self.assertEqual(self.call(**self.site())["error"], "website_conflict")
        self.assertEqual(self.read(self.pool_path()), "[mine]\n")
        self.assertFalse(os.path.exists(self.vhost_path()))

    def test_a_pool_the_agent_did_not_write_survives_a_remove(self):
        self.call(**self.site())
        with open(self.pool_path(), "w") as handle:
            handle.write("[mine]\n")
        self.call(**WEBSITE_REMOVE)
        self.assertEqual(self.read(self.pool_path()), "[mine]\n")


class DetectionTests(GenericLinuxCase):
    """Which web server the helpers pick, and what ping reports about it."""

    def test_both_web_servers_running_is_refused_without_a_choice(self):
        self.build(SCENARIOS[0])
        self.activate("nginx")
        before = self.all_files()
        self.assertEqual(self.call(**self.site())["error"], "website_failed")
        self.assertEqual(self.call(**WEBSITE_REMOVE)["error"], "website_failed")
        self.assertEqual(self.all_files(), before)
        self.assertEqual(self.call(op="ping")["capabilities"], [])

    def test_no_web_server_running_is_refused_without_a_choice(self):
        self.build(SCENARIOS[0], active=False)
        self.assertEqual(self.call(**self.site())["error"], "website_failed")
        self.assertEqual(self.call(op="ping")["capabilities"], [])

    def test_web_server_in_the_agent_configuration_decides(self):
        self.build(SCENARIOS[0], active=False)
        self.set_agent_conf("web_server = apache\n")
        self.assertEqual(self.call(op="ping")["capabilities"], ["ensure-website", "remove-website"])
        self.assertTrue(self.call(**self.site())["ok"])
        self.assertTrue(os.path.exists(self.vhost_path()))

    def test_web_server_in_the_agent_configuration_breaks_a_tie(self):
        self.build(SCENARIOS[3])
        os.makedirs(os.path.join(self.root, "etc/httpd/conf.d"))
        write_executable(os.path.join(self.root, "usr/sbin/httpd"), FAKE_WEB_TEST)
        self.activate("httpd")
        self.set_agent_conf("web_server = nginx\n")
        self.assertTrue(self.call(**self.site())["ok"])
        self.assertTrue(os.path.exists(self.vhost_path()))
        self.assertFalse(os.path.exists(os.path.join(self.root, "etc/httpd/conf.d/hp-app.example.com.conf")))

    def test_an_unknown_web_server_setting_is_refused(self):
        self.build(SCENARIOS[0])
        self.set_agent_conf("web_server = caddy\n")
        self.assertEqual(self.call(**self.site())["error"], "website_failed")

    def test_ping_reports_only_what_the_server_can_do(self):
        self.build(SCENARIOS[2])
        ping = self.call(op="ping")
        self.assertEqual(ping["capabilities"], ["ensure-website", "remove-website"])
        self.assertEqual(ping["php_versions"], ["8.2"])
        self.assertEqual(ping["php_cli"], {"8.2": "/usr/bin/php"})

    def test_ping_without_the_web_server_tool_reports_no_website_ops(self):
        self.build(SCENARIOS[1])
        os.unlink(os.path.join(self.root, "usr/sbin/httpd"))
        self.assertEqual(self.call(op="ping")["capabilities"], [])

    def test_ping_lists_versions_oldest_first_from_every_layout(self):
        self.build(SCENARIOS[1])
        self.install_php(SCENARIOS[0], "8.10")
        self.install_php(SCENARIOS[2], "7.4")
        self.assertEqual(self.call(op="ping")["php_versions"], ["7.4", "8.1", "8.3", "8.10"])

    def test_a_version_without_its_cli_or_pool_directory_is_not_offered(self):
        self.build(SCENARIOS[0])
        os.unlink(os.path.join(self.root, "usr/bin/php8.2"))
        shutil.rmtree(os.path.join(self.root, "etc/php/8.3/fpm/pool.d"))
        self.assertEqual(self.call(op="ping")["php_versions"], [])
        self.assertEqual(self.call(**self.site(php="8.2"))["error"], "php_version_unsupported")

    def test_detect_describes_the_server(self):
        self.build(SCENARIOS[3])
        out = self.run_helper("detect")
        self.assertIn("web_server=nginx\n", out)
        self.assertIn("web_user=www-data\n", out)
        self.assertIn("vhost_dir=/etc/nginx/conf.d\n", out)
        self.assertIn("php=8.3 /usr/bin/php8.3\n", out)
        self.activate("apache2")
        self.assertIn("web_server=unknown\n", self.run_helper("detect"))


class HomeRootTests(GenericLinuxCase):
    """The helpers find the account's home under the agent's home_root setting, not under a fixed /home."""

    def setUp(self):
        super().setUp()
        self.build(SCENARIOS[0])


    def move_home(self, root):
        """Makes the homes reachable below another home_root (a link inside the server root) and sets it in the agent configuration."""
        target = os.path.join(self.root, root.strip("/"))
        os.makedirs(os.path.dirname(target))
        os.symlink(self.home_root, target)
        self.set_agent_conf("home_root = %s\n" % root)

    def test_the_vhost_and_directories_use_the_configured_home_root(self):
        self.move_home("/srv/homes/")
        self.assertTrue(self.call(**self.site(link_root=False))["ok"])
        self.assertIn("/srv/homes/alice/sites/app", self.vhost())
        self.assertTrue(os.path.isdir(os.path.join(self.root, "srv/homes/alice/sites/app")))
        self.assertEqual(self.call(**WEBSITE_REMOVE), {"v": 1, "ok": True, "changed": True})
        self.assertFalse(os.path.exists(os.path.join(self.root, "srv/homes/alice/sites/app")))

    def test_an_account_without_a_home_under_home_root_is_refused(self):
        self.set_agent_conf("home_root = /srv/other\n")
        self.assertEqual(self.call(**self.site())["error"], "site_not_owned")
        self.assertFalse(os.path.exists(self.vhost_path()))

    def test_a_home_root_that_is_not_a_plain_absolute_path_is_refused(self):
        for value in ("relative", "/a/../b", "/a b"):
            self.set_agent_conf("home_root = %s\n" % value)
            self.assertEqual(self.call(**self.site())["error"], "website_failed", value)


class DatabaseTests(GenericLinuxCase):
    """ensure-database and remove-database against a fake mariadb or mysql client and a fake systemctl."""

    def setUp(self):
        """A Debian Apache server that also runs MariaDB, with the fake client, openssl and the database state."""
        super().setUp()
        self.build(SCENARIOS[0])
        os.makedirs(os.path.join(self.root, "mysql"))
        self.install_client("mariadb")
        write_executable(os.path.join(self.root, "usr/bin/openssl"), FAKE_OPENSSL)
        self.activate("mariadb")

    def install_client(self, name):
        """Installs the fake client under a name; its calls are recorded under that name."""
        write_executable(os.path.join(self.root, "usr/bin", name), FAKE_MYSQL.replace('echo "mysql $*"', 'echo "%s $*"' % name))

    def exists(self, name="alice_shop"):
        """Whether the fake database server holds the schema."""
        return os.path.exists(os.path.join(self.root, "mysql", "db." + name))

    def entries(self):
        """The registry entries of the account."""
        return os.listdir(self.registry) if os.path.isdir(self.registry) else []

    def test_create_returns_the_password_once_and_is_idempotent(self):
        created = self.call(**DB)
        self.assertEqual(created, {"v": 1, "ok": True, "created": True, "database": "alice_shop", "username": "alice_shop",
                                   "password": PASSWORD})
        self.assertTrue(self.exists())
        self.assertEqual(self.entries(), ["database.shop"])
        again = self.call(**DB)
        self.assertEqual(again, {"v": 1, "ok": True, "created": False, "database": "alice_shop", "username": "alice_shop"})
        self.assertEqual(self.entries(), ["database.shop"])

    def test_the_password_is_never_logged_stored_or_put_on_a_command_line(self):
        self.call(**DB)
        self.assertNotIn(PASSWORD, self.calls())
        self.assertNotIn(PASSWORD, self.read(self.config["audit_log"]))
        for name in self.entries():
            self.assertNotIn(PASSWORD, self.read(os.path.join(self.registry, name)))

    def test_the_registry_entry_exists_before_the_database_is_created(self):
        real = os.path.join(self.root, "usr/bin/real-client")
        os.rename(os.path.join(self.root, "usr/bin/mariadb"), real)
        marker = os.path.join(self.root, "registered-before-create")
        entry = os.path.join(self.registry, "database.shop")
        write_executable(os.path.join(self.root, "usr/bin/mariadb"), "\n".join([
            "#!/bin/sh",
            "sql=$(cat)",
            'case "$sql" in *"CREATE DATABASE"*) if [ -e "%s" ]; then touch "%s"; fi ;; esac' % (entry, marker),
            'printf "%%s" "$sql" | "%s" "$@"' % real,
            ""]))
        self.assertTrue(self.call(**DB)["created"])
        self.assertTrue(os.path.exists(marker))

    def test_a_database_the_agent_did_not_create_is_refused_and_survives_a_remove(self):
        self.touch("mysql/db.alice_shop")
        self.assertEqual(self.call(**DB)["error"], "site_not_owned")
        self.assertEqual(self.call(**DB_REMOVE)["error"], "site_not_owned")
        self.assertTrue(self.exists())
        self.assertEqual(self.entries(), [])

    def test_a_foreign_user_of_that_name_is_refused(self):
        self.touch("mysql/user.alice_shop")
        self.assertEqual(self.call(**DB)["error"], "site_not_owned")
        self.assertEqual(self.call(**DB_REMOVE)["error"], "site_not_owned")
        self.assertFalse(self.exists())
        self.assertEqual(self.entries(), [])

    def test_remove_drops_and_forgets_and_is_idempotent(self):
        self.call(**DB)
        self.assertEqual(self.call(**DB_REMOVE), {"v": 1, "ok": True, "changed": True})
        self.assertFalse(self.exists())
        self.assertEqual(self.entries(), [])
        self.assertEqual(self.call(**DB_REMOVE), {"v": 1, "ok": True, "changed": False})
        self.assertTrue(self.call(**DB)["created"])

    def test_an_account_has_at_most_ten_databases(self):
        for index in range(10):
            self.assertTrue(self.call(op="ensure-database", account="alice", label="d%d" % index)["created"])
        self.assertEqual(self.call(op="ensure-database", account="alice", label="extra")["error"], "database_limit")
        self.assertFalse(self.exists("alice_extra"))
        self.assertEqual(len(self.entries()), 10)
        self.assertFalse(self.call(op="ensure-database", account="alice", label="d3")["created"])

    def test_a_failed_creation_leaves_nothing_and_no_registry_entry(self):
        self.touch("fail-create")
        self.assertEqual(self.call(**DB)["error"], "database_failed")
        self.assertFalse(self.exists())
        self.assertEqual(self.entries(), [])
        os.unlink(os.path.join(self.root, "fail-create"))
        self.assertTrue(self.call(**DB)["created"])

    def test_a_bad_label_is_refused(self):
        for label in ("Shop", "1shop", "shop_x", "a" * 16, ""):
            self.assertEqual(self.call(op="ensure-database", account="alice", label=label)["error"], "invalid_request", label)

    def test_the_mariadb_client_wins_over_mysql_and_mysql_is_used_alone(self):
        self.install_client("mysql")
        self.call(**DB)
        self.assertIn("mariadb -N -B", self.calls())
        self.assertNotIn("mysql -N -B", self.calls())
        os.unlink(os.path.join(self.root, "usr/bin/mariadb"))
        self.assertEqual(self.call(**DB_REMOVE), {"v": 1, "ok": True, "changed": True})
        self.assertIn("mysql -N -B", self.calls())

    def test_any_of_the_three_server_units_is_enough(self):
        for unit in ("mysql", "mysqld", "mariadb"):
            with self.subTest(unit=unit):
                shutil.rmtree(os.path.join(self.root, "active"))
                self.activate(unit)
                self.assertTrue(self.call(**DB)["created"])
                self.assertTrue(self.call(**DB_REMOVE)["changed"])

    def test_root_client_file_is_used_when_present_else_socket_authentication(self):
        self.call(**DB)
        self.assertNotIn("--defaults-file", self.calls())
        self.call(**DB_REMOVE)
        conf = os.path.join(self.root, "root/.my.cnf")
        os.makedirs(os.path.dirname(conf))
        open(conf, "w").close()
        self.call(**DB)
        self.assertIn("--defaults-file=%s -N -B" % conf, self.calls())

    def test_ping_lists_the_database_ops_only_with_a_client_and_a_running_server(self):
        self.assertEqual(self.call(op="ping")["capabilities"],
                         ["ensure-website", "remove-website", "ensure-database", "remove-database"])
        os.unlink(os.path.join(self.root, "active/mariadb"))
        self.assertEqual(self.call(op="ping")["capabilities"], ["ensure-website", "remove-website"])
        self.assertEqual(self.call(**DB)["error"], "database_failed")
        self.activate("mariadb")
        os.unlink(os.path.join(self.root, "usr/bin/mariadb"))
        self.assertEqual(self.call(op="ping")["capabilities"], ["ensure-website", "remove-website"])

    def test_detect_names_the_client_and_unit(self):
        self.assertIn("database_client=/usr/bin/mariadb\ndatabase_unit=mariadb\n", self.run_helper("detect"))
        os.unlink(os.path.join(self.root, "active/mariadb"))
        self.assertIn("database_client=unknown\n", self.run_helper("detect"))


FAKE_SURY_PHP = """#!/bin/sh
printf '/etc/php/8.3/cli\\n/etc/php/8.3/cli/conf.d\\n/usr/lib/php/20230831\\n'
"""
FAKE_GETENFORCE = """#!/bin/sh
echo "$(cat "$HP_ROOT/selinux")"
"""


class JailExtrasTests(GenericLinuxCase):
    """The jail-extras helper: the deploy tools, the PHP CLIs with their directories and the CA bundle, found on the server."""

    def setUp(self):
        super().setUp()
        self.build(SCENARIOS[3])
        self.tool("flock")
        self.tool("tar")
        self.tool("gzip")
        self.tool("curl")
        write_executable(os.path.join(self.root, "usr/bin/php8.3"), FAKE_SURY_PHP)
        for directory in ("etc/php/8.3/cli", "etc/php/8.3/mods-available", "usr/lib/php/20230831"):
            os.makedirs(os.path.join(self.root, directory))
        os.makedirs(os.path.join(self.root, "etc/php/8.3/cli/conf.d"))
        open(os.path.join(self.root, "etc/php/8.3/mods-available/curl.ini"), "w").close()
        os.symlink("../../mods-available/curl.ini", os.path.join(self.root, "etc/php/8.3/cli/conf.d/20-curl.ini"))

    def tool(self, name):
        """Installs a stand-in deploy tool in /usr/bin."""
        write_executable(os.path.join(self.root, "usr/bin", name), "#!/bin/sh\n")

    def paths(self):
        """The helper's output for the agent's trust paths, as a list of lines."""
        return self.run_helper("jail-extras", *test_ops.agent.SAFE_TRUST_PATHS).splitlines()

    def test_lists_the_installed_tools_the_php_cli_with_its_directories_and_the_bundle(self):
        os.makedirs(os.path.join(self.root, "etc/ssl/certs"))
        open(os.path.join(self.root, "etc/ssl/certs/ca-certificates.crt"), "w").close()
        self.assertEqual(self.paths(), [
            "/usr/bin/flock", "/usr/bin/tar", "/usr/bin/curl", "/usr/bin/gzip",
            "/usr/bin/php8.3", "/etc/php/8.3/cli", "/etc/php/8.3/cli/conf.d", "/usr/lib/php/20230831",
            "/etc/php/8.3/mods-available", "/etc/ssl/certs/ca-certificates.crt"])

    def test_rsync_and_unzip_are_listed_only_when_installed(self):
        self.assertNotIn("/usr/bin/rsync", self.paths())
        self.tool("rsync")
        self.tool("unzip")
        paths = self.paths()
        self.assertIn("/usr/bin/rsync", paths)
        self.assertIn("/usr/bin/unzip", paths)

    def test_a_bundle_that_is_a_link_lists_its_target_too(self):
        os.makedirs(os.path.join(self.root, "etc/pki/tls/certs"))
        os.makedirs(os.path.join(self.root, "etc/pki/ca-trust/extracted/pem"))
        open(os.path.join(self.root, "etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem"), "w").close()
        os.symlink("../../ca-trust/extracted/pem/tls-ca-bundle.pem", os.path.join(self.root, "etc/pki/tls/certs/ca-bundle.crt"))
        paths = self.paths()
        self.assertIn("/etc/pki/tls/certs/ca-bundle.crt", paths)
        self.assertIn("/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem", paths)

    def test_a_trust_directory_that_is_not_a_plain_path_is_refused(self):
        done = subprocess.run([os.path.join(self.helper_dir, "jail-extras"), "/etc/ssl/../x"], stdout=subprocess.PIPE,
                              stderr=subprocess.PIPE, env=dict(test_ops.agent.JAIL_ENV), text=True)
        self.assertEqual(done.returncode, 2)
        self.assertEqual(done.stdout, "")

    def test_the_agent_reads_the_helper_and_the_private_key_check_passes_over_the_union(self):
        os.makedirs(os.path.join(self.root, "etc/ssl/certs"))
        open(os.path.join(self.root, "etc/ssl/certs/ca-certificates.crt"), "w").close()
        self.config["jail_extras"] = ["/configured/by/hand"]
        union = test_ops.agent.jail_extras(self.config)
        self.assertEqual(union[0], "/configured/by/hand")
        self.assertEqual(union[1:], self.paths())
        self.assertEqual(test_ops.agent.unsafe_jail_extras(union), [])


class SelinuxTests(GenericLinuxCase):
    """ensure-website refuses while SELinux is enforcing, because the files it writes are not labelled for it."""

    def setUp(self):
        super().setUp()
        self.build(SCENARIOS[2])
        write_executable(os.path.join(self.root, "usr/sbin/getenforce"), FAKE_GETENFORCE)

    def set_mode(self, mode):
        """Sets what the stand-in getenforce reports."""
        with open(os.path.join(self.root, "selinux"), "w") as handle:
            handle.write(mode)

    def test_enforcing_refuses_the_website_and_changes_nothing(self):
        self.set_mode("Enforcing")
        before = self.all_files()
        self.assertEqual(self.call(**self.site())["error"], "website_failed")
        self.assertEqual(self.all_files(), before)
        self.assertFalse(os.path.exists(self.vhost_path()))

    def test_permissive_and_a_server_without_selinux_serve_the_website(self):
        self.set_mode("Permissive")
        self.assertTrue(self.call(**self.site())["ok"])
        os.unlink(os.path.join(self.root, "usr/sbin/getenforce"))
        self.assertTrue(self.call(**self.site(hostname="two.example.com", docroot="sites/two"))["ok"])
