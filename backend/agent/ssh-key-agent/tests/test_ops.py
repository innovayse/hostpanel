"""
Unit tests for the 1.5.0 ops of hostpanel-ssh-key-agent: ensure-website, remove-website, ensure-database and
remove-database. Run: python3 -m unittest discover -s ssh-key-agent/tests

The agent side (validation, exit code mapping, RESULT schema, audit) runs against stand-in helpers. The CWP helpers
run for real against a stand-in server tree in a temporary directory (HP_ROOT) with fake tools, so no root and no
real Apache, php-fpm or MySQL is needed.
"""

import glob
import json
import os
import shutil
import stat

from test_agent import AgentTestCase, agent

CWP_HELPERS = os.path.join(os.path.dirname(__file__), "..", "providers", "cwp")
OPS = ("ensure-website", "remove-website", "ensure-database", "remove-database")
PASSWORD = "0123456789abcdef0123456789abcdef01234567"

SITE = dict(op="ensure-website", account="alice", hostname="app.example.com", docroot="sites/app", php="8.3", link_root=True)
WEBSITE_REMOVE = dict(op="remove-website", account="alice", hostname="app.example.com")
DB = dict(op="ensure-database", account="alice", label="shop")
DB_REMOVE = dict(op="remove-database", account="alice", label="shop")

FAKE_APACHECTL = """#!/bin/sh
echo "apachectl $*" >> "$HP_ROOT/calls.log"
if [ "$1" = "-t" ] && [ -e "$HP_ROOT/fail-apache" ]; then exit 1; fi
exit 0
"""
FAKE_PHP_FPM = """#!/bin/sh
echo "php-fpm $*" >> "$HP_ROOT/calls.log"
if [ -e "$HP_ROOT/fail-fpm" ]; then exit 1; fi
exit 0
"""
FAKE_LOGGER = """#!/bin/sh
echo "$(basename "$0") $*" >> "$HP_ROOT/calls.log"
"""
FAKE_OPENSSL = """#!/bin/sh
echo %s
""" % PASSWORD
# Knows just the statements the helpers send: the CWP domain lookup, the two existence checks and create/drop.
FAKE_MYSQL = r"""#!/bin/sh
echo "mysql $*" >> "$HP_ROOT/calls.log"
sql="$* $(cat)"
state="$HP_ROOT/mysql"
case "$sql" in
  *"FROM root_cwp.user"*)
    user=$(echo "$sql" | sed -n "s/.*username='\([a-z0-9]*\)'.*/\1/p"); cat "$state/domain.$user" 2>/dev/null ;;
  *"information_schema.SCHEMATA"*)
    name=$(echo "$sql" | sed -n "s/.*SCHEMA_NAME='\([a-z0-9_]*\)'.*/\1/p"); [ -e "$state/db.$name" ] && echo 1 || echo 0 ;;
  *"FROM mysql.user"*)
    name=$(echo "$sql" | sed -n "s/.*User='\([a-z0-9_]*\)'.*/\1/p"); [ -e "$state/user.$name" ] && echo 1 || echo 0 ;;
  *"CREATE DATABASE"*)
    if [ -e "$HP_ROOT/fail-create" ]; then exit 1; fi
    name=$(echo "$sql" | sed -n 's/.*CREATE DATABASE `\([a-z0-9_]*\)`.*/\1/p'); touch "$state/db.$name" "$state/user.$name" ;;
  *"DROP DATABASE"*)
    name=$(echo "$sql" | sed -n 's/.*DROP DATABASE IF EXISTS `\([a-z0-9_]*\)`.*/\1/p'); rm -f "$state/db.$name" "$state/user.$name" ;;
esac
exit 0
"""


def write_executable(path, body):
    """Writes a script and makes it executable."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as handle:
        handle.write(body)
    os.chmod(path, os.stat(path).st_mode | stat.S_IXUSR)


class OpsTestCase(AgentTestCase):
    """The account tree of AgentTestCase plus a stand-in CWP server around it, reachable through HP_ROOT."""

    def setUp(self):
        super().setUp()
        self.root = self.tmp.name
        self.vhosts = os.path.join(self.root, "usr/local/apache/conf.d/vhosts")
        self.pool = os.path.join(self.root, "opt/alt/php-fpm83/usr/etc/php-fpm.d/users/alice.conf")
        self.registry = os.path.join(self.root, "var/lib/hostpanel-ssh-key-agent/alice")
        self.log = os.path.join(self.root, "calls.log")
        self.helper_dir = os.path.join(self.tmp.name, "libexec")
        self.config["helper_dir"] = self.helper_dir
        self._jail_env = agent.JAIL_ENV
        agent.JAIL_ENV = dict(self._jail_env, HP_ROOT=self.root)

    def tearDown(self):
        agent.JAIL_ENV = self._jail_env
        super().tearDown()

    def install_cwp_helpers(self):
        """Copies the real CWP helpers (mode bits are not relied on) and builds the stand-in server tree."""
        shutil.copytree(CWP_HELPERS, self.helper_dir)
        for name in os.listdir(self.helper_dir):
            os.chmod(os.path.join(self.helper_dir, name), 0o755)
        os.makedirs(self.vhosts)
        os.makedirs(os.path.join(self.root, "mysql"))
        os.makedirs(os.path.join(self.root, "opt/alt/php-fpm83/usr/etc/php-fpm.d/users"))
        os.makedirs(os.path.join(self.root, "opt/alt/php-fpm83/usr/var/sockets"))
        os.makedirs(os.path.join(self.home_root, "alice", "public_html"))
        write_executable(os.path.join(self.root, "usr/local/apache/bin/apachectl"), FAKE_APACHECTL)
        write_executable(os.path.join(self.root, "opt/alt/php-fpm83/usr/sbin/php-fpm"), FAKE_PHP_FPM)
        write_executable(os.path.join(self.root, "usr/bin/mysql"), FAKE_MYSQL)
        write_executable(os.path.join(self.root, "usr/bin/openssl"), FAKE_OPENSSL)
        for tool in ("chown", "chgrp", "systemctl"):
            write_executable(os.path.join(self.root, "usr/bin", tool), FAKE_LOGGER)
        with open(os.path.join(self.root, "mysql/domain.alice"), "w") as handle:
            handle.write("example.com\n")

    def install_stub(self, op, exit_code=0, out=""):
        """A stand-in helper that records its arguments, prints `out` and exits with `exit_code`."""
        args_file = os.path.join(self.tmp.name, "args-" + op)
        write_executable(os.path.join(self.helper_dir, op),
                         "#!/bin/sh\necho \"$@\" > '%s'\ncat <<'EOF'\n%s\nEOF\nexit %d\n" % (args_file, out, exit_code))
        return args_file

    def calls(self):
        """The recorded fake tool calls, empty when there were none."""
        if not os.path.exists(self.log):
            return ""
        return self.read(self.log)

    @staticmethod
    def read(path):
        """The text of a file."""
        with open(path) as handle:
            return handle.read()

    def touch(self, name):
        """Creates a marker file the fake tools react to (fail-apache, fail-fpm, fail-create) or a fake MySQL object."""
        open(os.path.join(self.root, name), "w").close()

    def audit_lines(self):
        """The audit log entries written so far."""
        return [json.loads(line) for line in self.read(self.config["audit_log"]).splitlines()]


class OpsRefusalTests(OpsTestCase):
    """Requests the agent refuses itself, before or without any helper."""

    def test_new_ops_are_not_supported_without_a_helper(self):
        for request in (SITE, WEBSITE_REMOVE, DB, DB_REMOVE):
            self.assertEqual(self.call(**request), {"v": 1, "ok": False, "error": "not_supported"}, request["op"])

    def test_ping_reports_only_the_ops_with_a_helper(self):
        self.assertEqual(self.call(op="ping")["capabilities"], [])
        self.install_stub("ensure-website")
        self.install_stub("remove-database")
        self.assertEqual(self.call(op="ping")["capabilities"], ["ensure-website", "remove-database"])

    def test_ping_lists_the_php_versions_of_the_helper_sorted_and_well_formed(self):
        self.assertEqual(self.call(op="ping")["php_versions"], [])
        self.install_stub("php-versions", out="8.10\n8.2\nnot a version\n8.2\n7.4\n8.1\n")
        self.assertEqual(self.call(op="ping")["php_versions"], ["7.4", "8.1", "8.2", "8.10"])

    def test_ping_offers_no_php_when_the_helper_fails(self):
        self.install_stub("php-versions", exit_code=1, out="8.3")
        self.assertEqual(self.call(op="ping")["php_versions"], [])

    def test_ping_lists_the_cli_of_each_php_version_the_helper_names(self):
        self.install_stub("php-versions", out="8.4\t/usr/bin/php8.4\n8.2\t/opt/remi/php82/root/usr/bin/php\n8.3\n8.5\trelative/php\n8.6\t/bad path\n")
        ping = self.call(op="ping")
        self.assertEqual(ping["php_versions"], ["8.2", "8.3", "8.4"])
        self.assertEqual(ping["php_cli"], {"8.2": "/opt/remi/php82/root/usr/bin/php", "8.4": "/usr/bin/php8.4"})

    def test_ping_has_an_empty_cli_map_without_a_php_versions_helper(self):
        self.assertEqual(self.call(op="ping")["php_cli"], {})

    def test_ping_narrows_the_ops_with_a_helper_to_the_ones_the_capabilities_helper_lists(self):
        for op in ("ensure-website", "remove-website", "ensure-database"):
            self.install_stub(op)
        self.install_stub("capabilities", out="ensure-website\nremove-website\nremove-database\nbogus\n")
        self.assertEqual(self.call(op="ping")["capabilities"], ["ensure-website", "remove-website"])

    def test_ping_offers_no_op_when_the_capabilities_helper_fails(self):
        self.install_stub("ensure-website")
        self.install_stub("capabilities", exit_code=1, out="ensure-website")
        self.assertEqual(self.call(op="ping")["capabilities"], [])

    def test_a_relative_helper_dir_is_a_broken_config(self):
        self.config["helper_dir"] = "libexec"
        self.assertEqual(self.call(**DB)["error"], "internal_error")

    def test_invalid_fields_are_refused_before_the_helper(self):
        args_file = self.install_stub("ensure-website", out='RESULT {"changed":true}')
        bad = [
            dict(SITE, hostname="App.example.com"), dict(SITE, hostname="x"), dict(SITE, hostname="a.com\n"),
            dict(SITE, hostname=7), dict(SITE, docroot="/etc"), dict(SITE, docroot="../x"), dict(SITE, docroot="a/../b"),
            dict(SITE, docroot=".ssh"), dict(SITE, docroot="public_html/.ssh"), dict(SITE, docroot=".conf/x"),
            dict(SITE, docroot=""), dict(SITE, docroot="a//b"), dict(SITE, docroot="a b"), dict(SITE, docroot="a" * 201),
            dict(SITE, docroot="a/b/c/d/e/f/g"), dict(SITE, docroot=None), dict(SITE, php="8"), dict(SITE, php="8.3.1"),
            dict(SITE, php=8.3), dict(SITE, link_root="yes"), dict(SITE, link_root=1),
            {k: v for k, v in SITE.items() if k != "php"},  # link_root without a php version
            {k: v for k, v in SITE.items() if k != "docroot"}, dict(SITE, extra="x"), dict(SITE, key_id="x"),
        ]
        for request in bad:
            self.assertEqual(self.call(**request)["error"], "invalid_request", request)
        self.assertFalse(os.path.exists(args_file))

    def test_label_table(self):
        self.install_stub("ensure-database", out="RESULT {}")
        for label in ("", "A", "1a", "a-b", "a_b", "a" * 16, "shop\n", 5, None):
            self.assertEqual(self.call(op="ensure-database", account="alice", label=label)["error"], "invalid_request", label)
        for label in ("a", "shop", "a" * 15, "x1"):
            self.assertNotEqual(self.call(op="ensure-database", account="alice", label=label).get("error"), "invalid_request", label)

    def test_docroot_table(self):
        self.install_stub("ensure-website", out='RESULT {"changed":true}')
        for docroot in ("public_html", "sites/app", "a-b/c_d/e.f", "_x", "a" * 64):
            request = dict(SITE, docroot=docroot, php="8.3", link_root=False)
            self.assertTrue(self.call(**request)["ok"], docroot)

    def test_optional_fields_may_be_left_out(self):
        args_file = self.install_stub("ensure-website", out='RESULT {"changed":true}')
        self.assertTrue(self.call(op="ensure-website", account="alice", hostname="app.example.com", docroot="public_html")["ok"])
        self.assertEqual(self.read(args_file).split("\n")[0].split(" ")[:3], ["alice", "app.example.com", "public_html"])

    def test_unknown_account_never_reaches_the_helper(self):
        args_file = self.install_stub("ensure-database", out="RESULT {}")
        self.assertFalse(self.call(op="ensure-database", account="mallory", label="shop")["ok"])
        self.assertFalse(os.path.exists(args_file))


class HelperResultTests(OpsTestCase):
    """The agent's handling of a helper's exit code and RESULT line, with stand-in helpers."""

    def test_exit_codes_map_to_stable_errors(self):
        table = {
            "ensure-website": ({3: "site_not_owned", 4: "website_conflict", 5: "php_version_unsupported", 1: "website_failed",
                                2: "website_failed", 6: "website_failed", 99: "website_failed"}, SITE),
            "remove-website": ({3: "site_not_owned", 4: "website_failed", 1: "website_failed"}, WEBSITE_REMOVE),
            "ensure-database": ({3: "site_not_owned", 6: "database_limit", 4: "database_failed", 1: "database_failed"}, DB),
            "remove-database": ({3: "site_not_owned", 6: "database_failed", 1: "database_failed"}, DB_REMOVE),
        }
        for op, (codes, request) in table.items():
            for code, error in codes.items():
                self.install_stub(op, exit_code=code, out="RESULT {}")
                self.assertEqual(self.call(**request), {"v": 1, "ok": False, "error": error}, "%s exit %d" % (op, code))

    def test_arguments_are_fixed_and_come_from_the_validated_fields(self):
        args_file = self.install_stub("ensure-website", out='RESULT {"changed":true}')
        self.call(**SITE)
        self.assertEqual(self.read(args_file).strip(), "alice app.example.com sites/app 8.3 1")
        self.call(**dict(SITE, php="8.4", link_root=False))
        self.assertEqual(self.read(args_file).strip(), "alice app.example.com sites/app 8.4 0")
        self.call(op="ensure-website", account="alice", hostname="app.example.com", docroot="public_html")
        self.assertEqual(self.read(args_file).strip(), "alice app.example.com public_html  0")

    def test_success_bodies_are_relayed_only_when_they_match_the_schema(self):
        self.install_stub("ensure-website", out='noise\nRESULT {"changed":true}\n[hostpanel] more noise')
        self.assertEqual(self.call(**SITE), {"v": 1, "ok": True, "changed": True})
        self.install_stub("remove-database", out='RESULT {"changed":false}')
        self.assertEqual(self.call(**DB_REMOVE), {"v": 1, "ok": True, "changed": False})

    def test_the_php_cli_of_an_ensure_website_answer_is_relayed_when_it_is_an_absolute_path(self):
        self.install_stub("ensure-website", out='RESULT {"changed":false,"php_cli":"/usr/bin/php8.3"}')
        self.assertEqual(self.call(**SITE), {"v": 1, "ok": True, "changed": False, "php_cli": "/usr/bin/php8.3"})

    def test_a_malformed_php_cli_fails_the_op(self):
        for cli in ('"php"', '"relative/php"', '"/bad path"', '"/a\nb"', "7", "null", '""'):
            self.install_stub("ensure-website", out='RESULT {"changed":true,"php_cli":%s}' % cli)
            self.assertEqual(self.call(**SITE)["error"], "website_failed", cli)
        self.install_stub("ensure-website", out='RESULT {"changed":true,"php_cli":"/usr/bin/php","x":1}')
        self.assertEqual(self.call(**SITE)["error"], "website_failed")

    def test_php_cli_is_refused_without_a_php_version_and_on_other_ops(self):
        self.install_stub("ensure-website", out='RESULT {"changed":true,"php_cli":"/usr/bin/php"}')
        request = dict(SITE)
        del request["php"], request["link_root"]
        self.assertEqual(self.call(**request)["error"], "website_failed")
        self.install_stub("remove-website", out='RESULT {"changed":true,"php_cli":"/usr/bin/php"}')
        self.assertEqual(self.call(**WEBSITE_REMOVE)["error"], "website_failed")

    def test_malformed_results_fail_the_op(self):
        website_bad = ["", "no result here", 'RESULT {"changed":true}\nRESULT {"changed":true}', "RESULT not json",
                       "RESULT []", "RESULT {}", 'RESULT {"changed":"yes"}', 'RESULT {"changed":true,"extra":1}',
                       ' RESULT {"changed":true}']
        for out in website_bad:
            self.install_stub("ensure-website", out=out)
            self.assertEqual(self.call(**SITE)["error"], "website_failed", out)
        name = "alice_shop"
        database_bad = [
            'RESULT {"created":true,"database":"%s","username":"%s"}' % (name, name),
            'RESULT {"created":true,"database":"%s","username":"%s","password":"short"}' % (name, name),
            'RESULT {"created":true,"database":"%s","username":"%s","password":"%s"}' % (name, name, PASSWORD.upper()),
            'RESULT {"created":false,"database":"%s","username":"%s","password":"%s"}' % (name, name, PASSWORD),
            'RESULT {"created":true,"database":"bob_shop","username":"%s","password":"%s"}' % (name, PASSWORD),
            'RESULT {"created":true,"database":"%s","username":"root","password":"%s"}' % (name, PASSWORD),
            'RESULT {"created":"true","database":"%s","username":"%s","password":"%s"}' % (name, name, PASSWORD),
            'RESULT {"created":true,"database":"%s","username":"%s","password":"%s","host":"x"}' % (name, name, PASSWORD),
        ]
        for out in database_bad:
            self.install_stub("ensure-database", out=out)
            self.assertEqual(self.call(**DB)["error"], "database_failed", out)

    def test_password_is_returned_on_creation_only_and_never_audited(self):
        name = "alice_shop"
        self.install_stub("ensure-database", out='RESULT {"created":true,"database":"%s","username":"%s","password":"%s"}'
                          % (name, name, PASSWORD))
        created = self.call(**DB)
        self.assertEqual(created, {"v": 1, "ok": True, "created": True, "database": name, "username": name, "password": PASSWORD})
        self.install_stub("ensure-database", out='RESULT {"created":false,"database":"%s","username":"%s"}' % (name, name))
        again = self.call(**DB)
        self.assertEqual(again, {"v": 1, "ok": True, "created": False, "database": name, "username": name})
        audit_text = self.read(self.config["audit_log"])
        self.assertNotIn(PASSWORD, audit_text)
        self.assertNotIn("password", audit_text)
        entry = self.audit_lines()[0]
        self.assertEqual((entry["op"], entry["account"], entry["target"], entry["outcome"]), ("ensure-database", "alice", "shop", "ok"))

    def test_refusals_are_audited_with_their_code(self):
        self.install_stub("remove-website", exit_code=3, out="RESULT {}")
        self.call(**WEBSITE_REMOVE)
        entry = self.audit_lines()[-1]
        self.assertEqual((entry["op"], entry["target"], entry["outcome"]), ("remove-website", "app.example.com", "site_not_owned"))


class WebsiteHelperTests(OpsTestCase):
    """ensure-website and remove-website, the real CWP helpers against the stand-in server."""

    def setUp(self):
        super().setUp()
        self.install_cwp_helpers()

    def vhost(self, hostname="app.example.com"):
        """The text of a vhost file."""
        return self.read(os.path.join(self.vhosts, hostname + ".conf"))

    def pool_text(self):
        """The text of the account's php-fpm pool."""
        return self.read(self.pool)

    def cwp_vhost(self, name="example.com.conf", docroot="/home/alice/public_html", owner="alice"):
        """A vhost as CWP writes it for the account's own domain."""
        with open(os.path.join(self.vhosts, name), "w") as handle:
            handle.write("<VirtualHost 1.2.3.4:80>\n\tServerName example.com\n\tDocumentRoot %s\n"
                         "\tSuexecUserGroup %s %s\n</VirtualHost>\n" % (docroot, owner, owner))

    def test_ensure_creates_everything_and_is_idempotent(self):
        self.assertEqual(self.call(**SITE), {"v": 1, "ok": True, "changed": True})
        vhost = self.vhost()
        self.assertTrue(vhost.startswith("# hostpanel-ssh-key-agent managed\n"))
        self.assertIn("DocumentRoot /home/alice/sites/app", vhost)
        self.assertIn("SuexecUserGroup alice alice", vhost)
        self.assertIn("proxy:unix:/opt/alt/php-fpm83/usr/var/sockets/alice.sock|fcgi://localhost", vhost)
        self.assertTrue(os.path.isdir(os.path.join(self.home_root, "alice", "sites")))
        self.assertIn('"php-fpm_ver": "83"',
                      self.read(os.path.join(self.home_root, "alice", ".conf/webservers/app.example.com.conf")))
        calls = self.calls()
        self.assertIn("apachectl -t", calls)
        self.assertIn("php-fpm -t", calls)
        self.assertIn("systemctl reload php-fpm83", calls)
        self.assertIn("apachectl graceful", calls)
        before = (vhost, self.pool_text(), calls)
        self.assertEqual(self.call(**SITE), {"v": 1, "ok": True, "changed": False})
        self.assertEqual((self.vhost(), self.pool_text(), self.calls().count("graceful")), (before[0], before[1], 1))

    def test_pool_lines_for_symlink_deploys_only_with_link_root(self):
        lines = ("opcache.validate_timestamps] = 1", "opcache.revalidate_freq] = 0", "opcache.revalidate_path] = 1",
                 "realpath_cache_size] = 0")
        self.call(**dict(SITE, link_root=False))
        for line in lines:
            self.assertNotIn(line, self.pool_text())
        self.assertEqual(self.call(**SITE), {"v": 1, "ok": True, "changed": True})
        for line in lines:
            self.assertEqual(self.pool_text().count(line), 1, line)
        self.call(**SITE)
        for line in lines:
            self.assertEqual(self.pool_text().count(line), 1, line)

    def test_docroot_is_owned_by_the_account_group_nobody_and_left_to_deploys_with_link_root(self):
        self.call(**dict(SITE, link_root=False))
        self.assertTrue(os.path.isdir(os.path.join(self.home_root, "alice", "sites/app")))
        self.assertIn("chown -h %d:nobody" % os.stat(os.path.join(self.home_root, "alice")).st_uid, self.calls())
        self.assertEqual(os.stat(os.path.join(self.home_root, "alice", "sites/app")).st_mode & 0o7777, 0o2750)
        self.call(**dict(SITE, hostname="www.example.com", docroot="releases/live"))
        self.assertTrue(os.path.isdir(os.path.join(self.home_root, "alice", "releases")))
        self.assertFalse(os.path.exists(os.path.join(self.home_root, "alice", "releases/live")))

    def test_without_php_there_is_no_pool_and_no_handler(self):
        self.assertTrue(self.call(op="ensure-website", account="alice", hostname="app.example.com", docroot="sites/app")["ok"])
        self.assertNotIn("SetHandler", self.vhost())
        self.assertFalse(os.path.exists(self.pool))

    def test_switching_php_off_strips_the_handler_again(self):
        self.call(**SITE)
        self.call(op="ensure-website", account="alice", hostname="app.example.com", docroot="sites/app")
        self.assertNotIn("SetHandler", self.vhost())
        self.assertTrue(self.vhost().startswith("# hostpanel-ssh-key-agent managed\n"))

    def test_ping_lists_the_installed_php_fpm_versions_as_dotted_and_sorted(self):
        for name in ("php-fpm81", "php-fpm810"):
            write_executable(os.path.join(self.root, "opt/alt", name, "usr/sbin/php-fpm"), FAKE_PHP_FPM)
        os.makedirs(os.path.join(self.root, "opt/alt/php-fpm74/usr/sbin"))  # no binary: not installed
        os.makedirs(os.path.join(self.root, "opt/alt/php-fpm-extra/usr/sbin"))
        self.assertEqual(self.call(op="ping")["php_versions"], ["8.1", "8.3", "8.10"])

    def test_php_version_that_is_not_installed_is_refused_and_changes_nothing(self):
        self.assertEqual(self.call(**dict(SITE, php="7.4"))["error"], "php_version_unsupported")
        self.assertFalse(os.path.exists(os.path.join(self.vhosts, "app.example.com.conf")))
        self.assertFalse(os.path.exists(os.path.join(self.home_root, "alice", "sites")))

    def test_foreign_hostnames_are_refused(self):
        for hostname in ("other.com", "notexample.com", "example.com.evil.com", "example.org"):
            self.assertEqual(self.call(**dict(SITE, hostname=hostname))["error"], "site_not_owned", hostname)
        self.assertEqual(os.listdir(self.vhosts), [])
        self.assertTrue(self.call(**dict(SITE, hostname="example.com"))["ok"])
        self.assertTrue(self.call(**dict(SITE, hostname="a.b.example.com"))["ok"])

    def test_docroot_cannot_leave_the_home_through_a_symlink(self):
        os.symlink("/etc", os.path.join(self.home_root, "alice", "out"))
        self.assertFalse(self.call(**dict(SITE, docroot="out/site"))["ok"])
        os.symlink(os.path.join(self.home_root, "alice", ".ssh"), os.path.join(self.home_root, "alice", "keys"))
        self.assertFalse(self.call(**dict(SITE, docroot="keys"))["ok"])
        self.assertEqual(os.listdir(self.vhosts), [])

    def test_vhost_of_another_account_is_not_the_accounts_own(self):
        self.cwp_vhost("app.example.com.conf", docroot="/home/bob/app", owner="bob")
        self.assertEqual(self.call(**SITE)["error"], "site_not_owned")

    def test_cwp_vhost_serving_another_docroot_is_a_conflict(self):
        self.cwp_vhost()
        self.assertEqual(self.call(**dict(SITE, hostname="example.com", docroot="sites/app"))["error"], "website_conflict")

    def test_cwp_vhost_keeps_its_content_and_only_gets_the_handler(self):
        self.cwp_vhost()
        original = self.read(os.path.join(self.vhosts, "example.com.conf"))
        self.assertTrue(self.call(**dict(SITE, hostname="example.com", docroot="public_html", link_root=False))["ok"])
        text = self.vhost("example.com")
        self.assertTrue(text.startswith("<VirtualHost 1.2.3.4:80>"))
        self.assertIn("SetHandler", text)
        self.assertEqual(text.count("</VirtualHost>"), 1)
        self.assertEqual(self.call(op="remove-website", account="alice", hostname="example.com")["changed"], True)
        self.assertEqual(self.vhost("example.com"), original)

    def test_a_failing_apache_test_restores_everything(self):
        self.touch("fail-apache")
        self.assertEqual(self.call(**SITE)["error"], "website_failed")
        self.assertEqual(os.listdir(self.vhosts), [])
        self.assertFalse(os.path.exists(self.pool))
        self.assertFalse(os.path.exists(os.path.join(self.home_root, "alice", "sites")))
        self.assertFalse(os.path.exists(os.path.join(self.home_root, "alice", ".conf/webservers/app.example.com.conf")))
        self.assertFalse(os.path.exists(self.registry))
        self.assertNotIn("graceful", self.calls())

    def test_a_failing_pool_test_restores_the_pool_and_vhost_edits(self):
        self.call(**dict(SITE, link_root=False))
        pool_before, vhost_before = self.pool_text(), self.vhost()
        self.touch("fail-fpm")
        self.assertEqual(self.call(**SITE)["error"], "website_failed")
        self.assertEqual((self.pool_text(), self.vhost()), (pool_before, vhost_before))

    def test_remove_takes_back_only_what_ensure_made(self):
        self.call(**SITE)
        self.assertEqual(self.call(**WEBSITE_REMOVE), {"v": 1, "ok": True, "changed": True})
        self.assertEqual(os.listdir(self.vhosts), [])
        self.assertFalse(os.path.exists(self.pool))
        self.assertFalse(os.path.exists(os.path.join(self.home_root, "alice", ".conf/webservers/app.example.com.conf")))
        self.assertEqual([n for n in os.listdir(self.registry) if n.startswith("website.")], [])
        self.assertEqual(self.call(**WEBSITE_REMOVE), {"v": 1, "ok": True, "changed": False})

    def test_remove_deletes_the_docroot_it_made_when_it_is_empty(self):
        self.call(**dict(SITE, link_root=False))
        self.call(**WEBSITE_REMOVE)
        self.assertFalse(os.path.exists(os.path.join(self.home_root, "alice", "sites/app")))

    def test_remove_never_deletes_customer_files(self):
        self.call(**dict(SITE, link_root=False))
        page = os.path.join(self.home_root, "alice", "sites/app", "index.html")
        open(page, "w").close()
        self.assertEqual(self.call(**WEBSITE_REMOVE)["changed"], True)
        self.assertTrue(os.path.exists(page))

    def test_remove_keeps_a_directory_the_customer_already_had(self):
        os.makedirs(os.path.join(self.home_root, "alice", "mine"))
        self.call(**dict(SITE, docroot="mine", link_root=False))
        self.call(**dict(WEBSITE_REMOVE))
        self.assertTrue(os.path.isdir(os.path.join(self.home_root, "alice", "mine")))

    def test_remove_keeps_the_pool_while_another_website_uses_it(self):
        self.call(**SITE)
        self.call(**dict(SITE, hostname="two.example.com", docroot="sites/two"))
        self.call(**WEBSITE_REMOVE)
        self.assertTrue(os.path.exists(self.pool))
        self.call(**dict(WEBSITE_REMOVE, hostname="two.example.com"))
        self.assertFalse(os.path.exists(self.pool))

    def test_remove_never_removes_a_pool_it_did_not_write(self):
        with open(self.pool, "w") as handle:
            handle.write("[alice]\nlisten = x\n")
        self.call(**SITE)
        self.call(**WEBSITE_REMOVE)
        self.assertTrue(os.path.exists(self.pool))

    def test_remove_refuses_a_vhost_the_agent_did_not_create(self):
        self.cwp_vhost("shop.example.com.conf")
        self.assertEqual(self.call(op="remove-website", account="alice", hostname="shop.example.com")["error"], "site_not_owned")
        self.assertTrue(os.path.exists(os.path.join(self.vhosts, "shop.example.com.conf")))
        self.assertEqual(self.call(op="remove-website", account="alice", hostname="never.example.com")["changed"], False)

    def test_remove_refuses_a_vhost_of_another_account(self):
        self.call(**SITE)
        path = os.path.join(self.vhosts, "app.example.com.conf")
        text = self.read(path).replace("SuexecUserGroup alice alice", "SuexecUserGroup bob bob")
        with open(path, "w") as handle:
            handle.write(text)
        self.assertEqual(self.call(**WEBSITE_REMOVE)["error"], "site_not_owned")

    def test_remove_restores_when_the_apache_test_fails(self):
        self.call(**SITE)
        self.touch("fail-apache")
        self.assertEqual(self.call(**WEBSITE_REMOVE)["error"], "website_failed")
        self.assertTrue(os.path.exists(os.path.join(self.vhosts, "app.example.com.conf")))
        self.assertTrue(os.path.exists(os.path.join(self.registry, "website.app.example.com")))


class DatabaseHelperTests(OpsTestCase):
    """ensure-database and remove-database, the real CWP helpers against the stand-in MySQL."""

    def setUp(self):
        super().setUp()
        self.install_cwp_helpers()

    def exists(self, name="alice_shop"):
        """Whether the fake MySQL holds the schema."""
        return os.path.exists(os.path.join(self.root, "mysql", "db." + name))

    def test_create_returns_the_password_once_and_is_idempotent(self):
        created = self.call(**DB)
        self.assertEqual(created, {"v": 1, "ok": True, "created": True, "database": "alice_shop", "username": "alice_shop",
                                   "password": PASSWORD})
        self.assertTrue(self.exists())
        again = self.call(**DB)
        self.assertEqual(again, {"v": 1, "ok": True, "created": False, "database": "alice_shop", "username": "alice_shop"})

    def test_password_never_reaches_a_command_line_or_the_audit_log(self):
        self.call(**DB)
        self.assertNotIn(PASSWORD, self.calls())
        self.assertNotIn(PASSWORD, self.read(self.config["audit_log"]))
        for name in os.listdir(self.registry):
            self.assertNotIn(PASSWORD, self.read(os.path.join(self.registry, name)))

    def test_a_database_the_agent_did_not_create_is_refused(self):
        self.touch("mysql/db.alice_shop")
        self.assertEqual(self.call(**DB)["error"], "site_not_owned")
        self.assertEqual(self.call(**DB_REMOVE)["error"], "site_not_owned")
        self.assertTrue(self.exists())

    def test_a_foreign_user_of_that_name_is_refused(self):
        self.touch("mysql/user.alice_shop")
        self.assertEqual(self.call(**DB)["error"], "site_not_owned")
        self.assertFalse(self.exists())

    def test_remove_drops_and_forgets_and_is_idempotent(self):
        self.call(**DB)
        self.assertEqual(self.call(**DB_REMOVE), {"v": 1, "ok": True, "changed": True})
        self.assertFalse(self.exists())
        self.assertEqual(self.call(**DB_REMOVE), {"v": 1, "ok": True, "changed": False})
        self.assertTrue(self.call(**DB)["created"])

    def test_database_limit(self):
        for index in range(10):
            self.assertTrue(self.call(op="ensure-database", account="alice", label="d%d" % index)["created"])
        self.assertEqual(self.call(op="ensure-database", account="alice", label="extra")["error"], "database_limit")
        self.assertFalse(self.exists("alice_extra"))
        self.assertFalse(self.call(op="ensure-database", account="alice", label="d3")["created"])

    def test_a_failed_creation_leaves_nothing_and_no_registry_entry(self):
        self.touch("fail-create")
        self.assertEqual(self.call(**DB)["error"], "database_failed")
        self.assertFalse(self.exists())
        self.assertFalse(os.path.exists(os.path.join(self.registry, "database.shop")))
        os.unlink(os.path.join(self.root, "fail-create"))
        self.assertTrue(self.call(**DB)["created"])


class ContractVectorTests(OpsTestCase):
    """The golden vectors in tests/contract (the .NET adapter's fixtures) are exactly what the agent answers."""

    def test_every_vector_is_what_the_agent_answers(self):
        for path in sorted(glob.glob(os.path.join(os.path.dirname(__file__), "contract", "*.json"))):
            for case in json.loads(self.read(path))["cases"]:
                with self.subTest(file=os.path.basename(path), case=case["name"]):
                    shutil.rmtree(self.helper_dir, ignore_errors=True)
                    for op in case.get("installed", []):
                        self.install_stub(op)
                    if "php_versions_output" in case:
                        self.install_stub("php-versions", out="\n".join(case["php_versions_output"]))
                    if "capabilities_output" in case:
                        self.install_stub("capabilities", out="\n".join(case["capabilities_output"]))
                    if case["helper"] is not None:
                        self.install_stub(case["request"]["op"], case["helper"]["exit"],
                                          "RESULT " + json.dumps(case["helper"]["result"]))
                    self.assertEqual(agent.handle(json.dumps(case["request"]), self.config), case["response"])
