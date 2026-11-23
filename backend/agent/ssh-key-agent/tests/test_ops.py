"""
Unit tests for the 1.5.0 ops of hostpanel-ssh-key-agent: ensure-website, remove-website, ensure-database and
remove-database. Run: python3 -m unittest discover -s ssh-key-agent/tests

The agent side (validation, exit code mapping, RESULT schema, audit) runs against stand-in helpers, so no root and no
real web server or database is needed.
"""

import glob
import json
import os
import shutil
import stat

from test_agent import AgentTestCase, agent

PASSWORD = "0123456789abcdef0123456789abcdef01234567"

SITE = dict(op="ensure-website", account="alice", hostname="app.example.com", docroot="sites/app", php="8.3", link_root=True)
WEBSITE_REMOVE = dict(op="remove-website", account="alice", hostname="app.example.com")
DB = dict(op="ensure-database", account="alice", label="shop")
DB_REMOVE = dict(op="remove-database", account="alice", label="shop")


def write_executable(path, body):
    """Writes a script and makes it executable."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as handle:
        handle.write(body)
    os.chmod(path, os.stat(path).st_mode | stat.S_IXUSR)


class OpsTestCase(AgentTestCase):
    """The account tree of AgentTestCase plus a helper directory for stand-in helpers."""

    def setUp(self):
        super().setUp()
        self.helper_dir = os.path.join(self.tmp.name, "libexec")
        self.config["helper_dir"] = self.helper_dir

    def install_stub(self, op, exit_code=0, out=""):
        """A stand-in helper that records its arguments, prints `out` and exits with `exit_code`."""
        args_file = os.path.join(self.tmp.name, "args-" + op)
        write_executable(os.path.join(self.helper_dir, op),
                         "#!/bin/sh\necho \"$@\" > '%s'\ncat <<'EOF'\n%s\nEOF\nexit %d\n" % (args_file, out, exit_code))
        return args_file

    @staticmethod
    def read(path):
        """The text of a file."""
        with open(path) as handle:
            return handle.read()

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


class ContractVectorTests(OpsTestCase):
    """The golden vectors in tests/contract (the .NET adapter's fixtures) are exactly what the agent answers."""

    def prepare_account(self, case):
        """
        Gives a vector its own account state: no .ssh directory except the authorized_keys lines the vector lists, and
        the host key file's content when the vector names one (none otherwise, so the answer carries no host_key).
        """
        shutil.rmtree(os.path.join(self.home_root, "alice", ".ssh"), ignore_errors=True)
        if "authorized_keys" in case:
            ssh_dir = os.path.join(self.home_root, "alice", ".ssh")
            os.makedirs(ssh_dir, mode=0o700)
            with open(os.path.join(ssh_dir, "authorized_keys"), "w") as handle:
                handle.write("\n".join(case["authorized_keys"]) + "\n")
            os.chmod(os.path.join(ssh_dir, "authorized_keys"), 0o600)
        self.config.pop("host_key_file", None)
        if "host_key_file" in case:
            path = os.path.join(self.tmp.name, "ssh_host_ed25519_key.pub")
            with open(path, "w") as handle:
                handle.write(case["host_key_file"] + "\n")
            self.config["host_key_file"] = path

    def test_every_vector_is_what_the_agent_answers(self):
        for path in sorted(glob.glob(os.path.join(os.path.dirname(__file__), "contract", "*.json"))):
            for case in json.loads(self.read(path))["cases"]:
                with self.subTest(file=os.path.basename(path), case=case["name"]):
                    self.prepare_account(case)
                    shutil.rmtree(self.helper_dir, ignore_errors=True)
                    for op in case.get("installed", []):
                        self.install_stub(op)
                    if "php_versions_output" in case:
                        self.install_stub("php-versions", out="\n".join(case["php_versions_output"]))
                    if "capabilities_output" in case:
                        self.install_stub("capabilities", out="\n".join(case["capabilities_output"]))
                    if case.get("helper") is not None:
                        self.install_stub(case["request"]["op"], case["helper"]["exit"],
                                          "RESULT " + json.dumps(case["helper"]["result"]))
                    self.assertEqual(agent.handle(json.dumps(case["request"]), self.config), case["response"])
