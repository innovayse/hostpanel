"""Unit tests for hostpanel-ssh-key-agent. Run: python3 -m unittest discover -s ssh-key-agent/tests"""

import base64
import importlib.machinery
import importlib.util
import json
import os
import pwd
import shutil
import subprocess
import sys
import time
import tempfile
import threading
import unittest
from types import SimpleNamespace

AGENT_PATH = os.path.join(os.path.dirname(__file__), "..", "hostpanel-ssh-key-agent")
_loader = importlib.machinery.SourceFileLoader("agent", AGENT_PATH)
_spec = importlib.util.spec_from_loader("agent", _loader)
agent = importlib.util.module_from_spec(_spec)
_loader.exec_module(agent)


def make_key(seed):
    """A syntactically valid ED25519 public key line for a 32-byte seed value."""
    blob = b"\x00\x00\x00\x0bssh-ed25519" + b"\x00\x00\x00\x20" + bytes([seed]) * 32
    return "ssh-ed25519 " + base64.b64encode(blob).decode() + " someone@laptop"


KEY_ID_A = "11111111-1111-4111-8111-111111111111"
KEY_ID_B = "22222222-2222-4222-8222-222222222222"


class AgentTestCase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.home_root = os.path.join(self.tmp.name, "home")
        os.makedirs(os.path.join(self.home_root, "alice"))
        self.config = {
            "min_uid": 0,
            "home_root": os.path.realpath(self.home_root),
            "deny_accounts": {"root"},
            "max_keys": 3,
            "audit_log": os.path.join(self.tmp.name, "audit.log"),
            "lock_dir": os.path.join(self.tmp.name, "locks"),
        }
        me = pwd.getpwuid(os.geteuid())
        self.shell = "/bin/bash"
        home = os.path.join(self.home_root, "alice")

        self.shadow = {"alice": "$6$salt$hash"}
        self.groups = {}

        def fake_getpwnam(name):
            if name != "alice":
                raise KeyError(name)
            return pwd.struct_passwd(("alice", "x", me.pw_uid, max(me.pw_gid, 1), "", home, self.shell))

        def fake_getspnam(name):
            if name not in self.shadow:
                raise KeyError(name)
            return SimpleNamespace(sp_pwdp=self.shadow[name])

        def fake_getgrnam(name):
            if name not in self.groups:
                raise KeyError(name)
            return SimpleNamespace(gr_mem=self.groups[name], gr_gid=424242)

        self._orig = agent.resolve_account
        agent.resolve_account = lambda name, config, **_: self._orig(
            name, config, getpwnam=fake_getpwnam, getspnam=fake_getspnam, getgrnam=fake_getgrnam, run=self.run)
        self.run = None

    def tearDown(self):
        agent.resolve_account = self._orig
        self.tmp.cleanup()

    def call(self, **request):
        request.setdefault("v", 1)
        return agent.handle(json.dumps(request), self.config)

    def authorized_keys(self):
        with open(os.path.join(self.home_root, "alice", ".ssh", "authorized_keys")) as handle:
            return handle.read()


class KeyParsingTests(unittest.TestCase):
    def test_valid_key_has_openssh_fingerprint(self):
        _, fingerprint = agent.parse_ed25519(make_key(7))
        self.assertTrue(fingerprint.startswith("SHA256:"))
        self.assertEqual(len(fingerprint), len("SHA256:") + 43)

    def test_rejects_other_algorithms_and_garbage(self):
        for bad in ["ssh-rsa AAAAB3NzaC1yc2E= x", "ssh-ed25519 !!!", "", "ssh-ed25519 " + "A" * 68,
                    make_key(1) + "\nssh-ed25519 injected", "-----BEGIN OPENSSH PRIVATE KEY-----"]:
            with self.assertRaises(agent.AgentError, msg=bad):
                agent.parse_ed25519(bad)


class OperationTests(AgentTestCase):
    def test_add_list_revoke_roundtrip(self):
        added = self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        self.assertTrue(added["ok"], added)
        self.assertNotIn("someone@laptop", self.authorized_keys())
        self.assertIn("hostpanel:" + KEY_ID_A, self.authorized_keys())
        listed = self.call(op="list", account="alice")
        self.assertEqual([k["key_id"] for k in listed["keys"]], [KEY_ID_A])
        self.assertEqual(listed["keys"][0]["fingerprint"], added["fingerprint"])
        revoked = self.call(op="revoke", account="alice", key_id=KEY_ID_A)
        self.assertTrue(revoked["changed"])
        self.assertNotIn(KEY_ID_A, self.authorized_keys())

    def test_permissions_are_private(self):
        self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        ssh_dir = os.path.join(self.home_root, "alice", ".ssh")
        self.assertEqual(os.stat(ssh_dir).st_mode & 0o777, 0o700)
        self.assertEqual(os.stat(os.path.join(ssh_dir, "authorized_keys")).st_mode & 0o777, 0o600)

    def test_add_is_idempotent_for_same_key_id(self):
        self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        again = self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        self.assertTrue(again["ok"])
        self.assertFalse(again["changed"])
        self.assertEqual(self.authorized_keys().count("ssh-ed25519"), 1)

    def test_duplicate_key_under_new_id_refused(self):
        self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        dup = self.call(op="add", account="alice", key_id=KEY_ID_B, public_key=make_key(1))
        self.assertEqual(dup["error"], "duplicate_key")

    def test_unmanaged_keys_are_preserved_and_never_listed(self):
        ssh_dir = os.path.join(self.home_root, "alice", ".ssh")
        os.mkdir(ssh_dir, 0o700)
        with open(os.path.join(ssh_dir, "authorized_keys"), "w") as handle:
            handle.write(make_key(9) + "\n")
        self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        self.call(op="revoke", account="alice", key_id=KEY_ID_A)
        listed = self.call(op="list", account="alice")
        self.assertEqual(listed["keys"], [])
        self.assertEqual(listed["unmanaged_keys"], 1)
        self.assertIn("someone@laptop", self.authorized_keys())

    def test_max_keys_enforced(self):
        for i, seed in enumerate([1, 2, 3]):
            self.call(op="add", account="alice", key_id="%08d-1111-4111-8111-111111111111" % i, public_key=make_key(seed))
        over = self.call(op="add", account="alice", key_id=KEY_ID_B, public_key=make_key(4))
        self.assertEqual(over["error"], "too_many_keys")

    def test_revoke_unknown_is_noop(self):
        result = self.call(op="revoke", account="alice", key_id=KEY_ID_B)
        self.assertTrue(result["ok"])
        self.assertFalse(result["changed"])

    def test_shell_disabled_refuses_add_but_reports_status(self):
        self.shell = "/sbin/nologin"
        self.assertEqual(self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))["error"], "ssh_disabled")
        self.assertFalse(self.call(op="status", account="alice")["shell_enabled"])

    def test_audit_log_records_fingerprint_not_key(self):
        self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        with open(self.config["audit_log"]) as handle:
            line = json.loads(handle.read().splitlines()[-1])
        self.assertEqual(line["outcome"], "ok")
        self.assertTrue(line["fingerprint"].startswith("SHA256:"))
        self.assertNotIn("public_key", line)

    def test_concurrent_adds_lose_nothing(self):
        ids = ["%08d-2222-4222-8222-222222222222" % i for i in range(3)]
        threads = [threading.Thread(target=self.call, kwargs=dict(op="add", account="alice", key_id=k, public_key=make_key(10 + i)))
                   for i, k in enumerate(ids)]
        [t.start() for t in threads]
        [t.join() for t in threads]
        self.assertEqual(sorted(k["key_id"] for k in self.call(op="list", account="alice")["keys"]), sorted(ids))


class BoundaryTests(AgentTestCase):
    def test_symlinked_ssh_dir_refused(self):
        target = os.path.join(self.tmp.name, "elsewhere")
        os.mkdir(target)
        os.symlink(target, os.path.join(self.home_root, "alice", ".ssh"))
        result = self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        self.assertFalse(result["ok"])
        self.assertEqual(os.listdir(target), [])

    def test_symlinked_authorized_keys_refused(self):
        ssh_dir = os.path.join(self.home_root, "alice", ".ssh")
        os.mkdir(ssh_dir, 0o700)
        victim = os.path.join(self.tmp.name, "victim")
        open(victim, "w").close()
        os.symlink(victim, os.path.join(ssh_dir, "authorized_keys"))
        result = self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        self.assertEqual(result["error"], "unsafe_path")
        self.assertEqual(os.path.getsize(victim), 0)

    def test_bad_account_names_refused(self):
        for name in ["../etc", "root", "Alice", "a b", "alice;id", "", "x" * 40, "bob"]:
            result = self.call(op="list", account=name)
            self.assertFalse(result["ok"], name)

    def test_unknown_fields_and_ops_refused(self):
        self.assertEqual(self.call(op="list", account="alice", path="/etc")["error"], "invalid_request")
        self.assertEqual(self.call(op="exec", account="alice")["error"], "unknown_operation")
        self.assertEqual(agent.handle(json.dumps({"v": 2, "op": "ping"}), self.config)["error"], "unsupported_version")
        self.assertEqual(agent.handle("not json", self.config)["error"], "invalid_request")

    def test_bad_key_id_refused(self):
        result = self.call(op="revoke", account="alice", key_id="../../x")
        self.assertEqual(result["error"], "invalid_request")

    def test_home_outside_root_refused(self):
        me = pwd.getpwuid(os.geteuid())
        entry = pwd.struct_passwd(("alice", "x", me.pw_uid, 1, "", "/etc", "/bin/bash"))
        with self.assertRaises(agent.AgentError):
            self._orig("alice", self.config, getpwnam=lambda _n: entry, getspnam=lambda _n: SimpleNamespace(sp_pwdp="x"))

    def test_ping(self):
        self.assertEqual(self.call(op="ping")["agent_version"], agent.AGENT_VERSION)

    def test_ping_reports_provider_and_binding(self):
        self.config["provider"] = "cwp"
        self.config["binding"] = KEY_ID_A
        pong = self.call(op="ping")
        self.assertEqual((pong["provider"], pong["binding"]), ("cwp", KEY_ID_A))

    def test_profiles_load_and_bad_binding_refused(self):
        root = os.path.join(os.path.dirname(__file__), "..", "profiles")
        for name in ("generic-linux.conf", "cwp.conf"):
            loaded = agent.load_config(os.path.join(root, name))
            self.assertEqual(loaded["binding"], "")
        broken = os.path.join(self.tmp.name, "broken.conf")
        with open(broken, "w") as handle:
            handle.write("[agent]\nbinding = ../../etc\n")
        with self.assertRaises(agent.AgentError):
            agent.load_config(broken)


class PolicyTests(AgentTestCase):
    """Per-server policy: suspension, group, opt-in list and the panel account check."""

    def test_suspended_account_cannot_receive_keys_but_can_be_cleaned(self):
        self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        self.shadow["alice"] = "!$6$salt$hash"  # usermod -L
        self.assertEqual(self.call(op="add", account="alice", key_id=KEY_ID_B, public_key=make_key(2))["error"], "account_suspended")
        status = self.call(op="status", account="alice")
        self.assertFalse(status["shell_enabled"])
        self.assertEqual(status["reason"], "account_suspended")
        self.assertTrue(self.call(op="revoke", account="alice", key_id=KEY_ID_A)["changed"])

    def test_no_password_is_not_a_lock(self):
        self.shadow["alice"] = "!!"
        self.assertTrue(self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))["ok"])

    def test_group_required(self):
        self.config["ssh_group"] = "sftp-ssh"
        self.groups["sftp-ssh"] = []
        self.assertEqual(self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))["error"], "account_not_eligible")
        self.groups["sftp-ssh"] = ["alice"]
        self.assertTrue(self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))["ok"])

    def test_eligible_list_must_be_root_owned_and_name_the_account(self):
        path = os.path.join(self.tmp.name, "eligible.conf")
        with open(path, "w") as handle:
            handle.write("bob\n")
        self.config["eligible_list"] = path
        result = self.call(op="add", account="alice", key_id=KEY_ID_A, public_key=make_key(1))
        # Not root-owned in the test sandbox: refused as a fault, never treated as permission.
        self.assertFalse(result["ok"])
        if os.geteuid() != 0:
            self.assertEqual(result["error"], "internal_error")

    def test_account_check_command(self):
        self.config["account_check_command"] = "/usr/local/libexec/check"
        calls = []

        def run(argv, **kwargs):
            calls.append((argv, kwargs.get("env")))
            return SimpleNamespace(returncode=self.check_code)

        self.run = run
        self.check_code = 1
        self.assertEqual(self.call(op="list", account="alice")["error"], "account_not_found")
        self.check_code = 0
        self.assertTrue(self.call(op="list", account="alice")["ok"])
        self.check_code = 5
        self.assertEqual(self.call(op="list", account="alice")["error"], "internal_error")
        self.assertEqual(calls[0][0], ["/usr/local/libexec/check", "alice"])
        self.assertEqual(calls[0][1], {"PATH": "/usr/bin:/bin"})

    def test_relative_check_command_refused(self):
        self.config["account_check_command"] = "check"
        self.assertEqual(self.call(op="list", account="alice")["error"], "internal_error")



class JailEnrolTests(AgentTestCase):
    """jail_enrol: the first key jails the account for SSH, the last managed key withdraws it."""

    def setUp(self):
        super().setUp()
        self.shell = "/sbin/nologin"
        self.jail_root = os.path.join(self.tmp.name, "jail")
        self.unit_dir = os.path.join(self.tmp.name, "units")
        os.makedirs(self.unit_dir)
        self.config.update({
            "jail_enrol": True,
            "jail_root": self.jail_root,
            "jail_group": "sftp-ssh",
            "jail_sections": ["basicshell", "editors"],
            "jail_shell": "/bin/bash",
            "systemd_unit_dir": self.unit_dir,
        })
        self.groups["sftp-ssh"] = []
        self.calls = []

        def jail_run(argv, **kwargs):
            self.calls.append(argv)
            self.assertEqual(kwargs.get("env"), {"PATH": "/usr/sbin:/usr/bin:/sbin:/bin"})
            self.assertTrue(os.path.isabs(argv[0]))
            if argv[0] == "/usr/sbin/jk_init":
                os.makedirs(os.path.join(argv[argv.index("-j") + 1], "etc"), exist_ok=True)
            if argv[0] == "/usr/sbin/jk_cp":
                # Copies like jk_cp does: the host path recreated under the jail, times kept.
                jail = argv[argv.index("-j") + 1]
                for source in argv[argv.index("-j") + 2:]:
                    target = os.path.join(jail, source.lstrip("/"))
                    if os.path.isdir(source):
                        shutil.copytree(source, target, symlinks=True, dirs_exist_ok=True)
                    else:
                        os.makedirs(os.path.dirname(target), exist_ok=True)
                        shutil.copy2(source, target)
            if argv[0] == "/usr/bin/systemd-escape":
                return SimpleNamespace(returncode=0, stdout=argv[-1].strip("/").replace("/", "-") + ".mount")
            if argv[:2] == ["/usr/sbin/usermod", "-aG"]:
                self.groups["sftp-ssh"].append(argv[-1])
            if argv[:2] == ["/usr/sbin/usermod", "-s"]:
                self.shell = argv[2]
            if argv[0] == "/usr/bin/gpasswd":
                self.groups["sftp-ssh"].remove(argv[2])
            return SimpleNamespace(returncode=0, stdout="")

        agent.JAIL_RUN = jail_run

    def tearDown(self):
        agent.JAIL_RUN = subprocess.run
        super().tearDown()

    def add(self, seed=1, key_id=KEY_ID_A):
        return self.call(op="add", account="alice", key_id=key_id, public_key=make_key(seed))

    def test_not_yet_enrolled_account_reports_ssh_available(self):
        status = self.call(op="status", account="alice")
        self.assertTrue(status["shell_enabled"])
        self.assertEqual(self.calls, [])

    def test_first_key_builds_jail_mount_group_and_shell(self):
        self.assertTrue(self.add()["ok"])
        programs = [c[0] for c in self.calls]
        self.assertIn("/usr/sbin/jk_init", programs)
        jk = next(c for c in self.calls if c[0] == "/usr/sbin/jk_init")
        self.assertEqual(jk[jk.index("-j") + 1], os.path.join(self.jail_root, "alice"))
        self.assertEqual(jk[-2:], ["basicshell", "editors"])
        self.assertIn(["/usr/sbin/usermod", "-aG", "sftp-ssh", "alice"], self.calls)
        self.assertIn(["/usr/sbin/usermod", "-s", "/bin/bash", "alice"], self.calls)
        self.assertTrue(os.path.isdir(os.path.join(self.jail_root, "alice", "home", "alice")))
        units = os.listdir(self.unit_dir)
        self.assertEqual(len(units), 1)
        with open(os.path.join(self.unit_dir, units[0])) as handle:
            unit = handle.read()
        self.assertIn("What=" + self.config["home_root"] + "/alice", unit)
        self.assertIn("Where=" + os.path.join(self.jail_root, "alice", "home", "alice"), unit)
        self.assertIn("Options=bind", unit)
        self.assertIn(["/usr/bin/systemctl", "enable", "--now", units[0]], self.calls)
        with open(os.path.join(self.jail_root, "alice", "etc", "passwd")) as handle:
            self.assertIn("alice:x:", handle.read())
        self.assertTrue(self.call(op="status", account="alice")["shell_enabled"])

    def jk_cp_calls(self):
        return [c for c in self.calls if c[0] == "/usr/sbin/jk_cp"]

    def host_file(self, name, text="x"):
        path = os.path.join(self.tmp.name, "host", name)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w") as handle:
            handle.write(text)
        return path

    def test_jail_gets_login_comforts(self):
        # Missing in a bare jailkit tree on srv4: uname (a profile.d script calls it at login)
        # and /etc/bashrc (where `ll` is defined). Only paths present on the host are copied.
        extra = self.host_file("bashrc")
        missing = os.path.join(self.tmp.name, "absent")
        self.config["jail_extras"] = [extra, missing]
        self.assertTrue(self.add(1, KEY_ID_A)["ok"])
        cp = self.jk_cp_calls()
        self.assertEqual(len(cp), 1)
        self.assertEqual(cp[0][:4], ["/usr/sbin/jk_cp", "-f", "-j", os.path.join(self.jail_root, "alice")])
        self.assertEqual(cp[0][4:], [extra])

    def test_extra_added_later_reaches_a_built_jail(self):
        first = self.host_file("bashrc")
        self.config["jail_extras"] = [first]
        self.assertTrue(self.add(1, KEY_ID_A)["ok"])
        later = self.host_file("uname")
        self.config["jail_extras"] = [first, later]
        self.calls.clear()
        self.assertTrue(self.add(2, KEY_ID_B)["ok"])
        cp = self.jk_cp_calls()
        self.assertEqual(len(cp), 1, "a jail built before an extra was added gets it on the next key")
        self.assertEqual(cp[0][4:], [later], "only the extra the jail lacks is copied")

    def test_extras_already_in_the_jail_are_not_copied_again(self):
        # jk_cp resolves and copies every binary's libraries: with git and its helpers in the list
        # that took ~20 s of CPU per key on srv4, longer than HostPanel waits for one SSH call.
        tree = os.path.join(self.tmp.name, "host", "git-core")
        os.makedirs(os.path.join(tree, "mergetools"))
        for name in ("git-add", "mergetools/vimdiff"):
            with open(os.path.join(tree, name), "w") as handle:
                handle.write("bin")
        os.symlink("git-add", os.path.join(tree, "git-stage"))
        self.config["jail_extras"] = [self.host_file("bashrc"), tree]
        self.assertTrue(self.add(1, KEY_ID_A)["ok"])
        self.calls.clear()
        self.assertTrue(self.add(2, KEY_ID_B)["ok"])
        self.assertEqual(self.jk_cp_calls(), [])

    def test_extra_changed_on_the_host_is_copied_again(self):
        unchanged = self.host_file("bashrc")
        updated = self.host_file("rsync", "v1")
        tree = os.path.join(self.tmp.name, "host", "zoneinfo")
        os.makedirs(tree)
        zone = os.path.join(tree, "Yerevan")
        with open(zone, "w") as handle:
            handle.write("old")
        self.config["jail_extras"] = [unchanged, updated, tree]
        self.assertTrue(self.add(1, KEY_ID_A)["ok"])
        later = time.time() + 60
        with open(updated, "w") as handle:
            handle.write("v2, a package update")
        os.utime(updated, (later, later))
        with open(zone, "w") as handle:
            handle.write("new rules")
        os.utime(zone, (later, later))
        self.calls.clear()
        self.assertTrue(self.add(2, KEY_ID_B)["ok"])
        cp = self.jk_cp_calls()
        self.assertEqual(len(cp), 1)
        self.assertEqual(cp[0][4:], [updated, tree])

    def test_a_copy_whose_time_lost_its_fraction_is_not_stale(self):
        # jk_cp keeps a file's time but drops the fraction of a second (seen on srv4), so a copy
        # is up to date when its whole second is not older than the host's.
        extra = self.host_file("bashrc")
        os.utime(extra, (1792956992.5939536, 1792956992.5939536))
        self.config["jail_extras"] = [extra]
        self.assertTrue(self.add(1, KEY_ID_A)["ok"])
        copy = os.path.join(self.jail_root, "alice", extra.lstrip("/"))
        os.utime(copy, (1792956992.0, 1792956992.0))
        self.calls.clear()
        self.assertTrue(self.add(2, KEY_ID_B)["ok"])
        self.assertEqual(self.jk_cp_calls(), [])

    def test_a_file_missing_inside_a_copied_directory_is_copied_again(self):
        tree = os.path.join(self.tmp.name, "host", "pki")
        os.makedirs(tree)
        with open(os.path.join(tree, "cert.pem"), "w") as handle:
            handle.write("pem")
        self.config["jail_extras"] = [tree]
        self.assertTrue(self.add(1, KEY_ID_A)["ok"])
        with open(os.path.join(tree, "new.pem"), "w") as handle:
            handle.write("pem")
        self.calls.clear()
        self.assertTrue(self.add(2, KEY_ID_B)["ok"])
        self.assertEqual([c[4:] for c in self.jk_cp_calls()], [[tree]])

    def test_enrolment_is_idempotent(self):
        self.assertTrue(self.add(1, KEY_ID_A)["ok"])
        self.calls.clear()
        self.assertTrue(self.add(2, KEY_ID_B)["ok"])
        self.assertNotIn("/usr/sbin/jk_init", [c[0] for c in self.calls])
        self.assertFalse([c for c in self.calls if c[0] == "/usr/sbin/usermod"])
        with open(os.path.join(self.jail_root, "alice", "etc", "passwd")) as handle:
            self.assertEqual(handle.read().count("alice:x:"), 1)

    def test_last_managed_key_withdraws_group_and_shell(self):
        self.add(1, KEY_ID_A)
        self.add(2, KEY_ID_B)
        self.calls.clear()
        self.call(op="revoke", account="alice", key_id=KEY_ID_A)
        self.assertEqual(self.calls, [])
        self.call(op="revoke", account="alice", key_id=KEY_ID_B)
        self.assertIn(["/usr/bin/gpasswd", "-d", "alice", "sftp-ssh"], self.calls)
        self.assertIn(["/usr/sbin/usermod", "-s", "/sbin/nologin", "alice"], self.calls)
        self.assertNotIn("alice", self.groups["sftp-ssh"])
        self.assertTrue(os.path.isdir(os.path.join(self.jail_root, "alice")))

    def test_withdrawal_spares_hand_installed_keys_and_other_shells(self):
        self.add()
        with open(os.path.join(self.home_root, "alice", ".ssh", "authorized_keys"), "a") as handle:
            handle.write(make_key(9) + "\n")
        self.calls.clear()
        self.call(op="revoke", account="alice", key_id=KEY_ID_A)
        self.assertEqual(self.calls, [])

    def test_failed_enrolment_adds_no_key(self):
        def failing(argv, **kwargs):
            return SimpleNamespace(returncode=1, stdout="")
        agent.JAIL_RUN = failing
        self.assertEqual(self.add()["error"], "internal_error")
        self.assertEqual(self.call(op="list", account="alice")["keys"], [])

    def test_suspended_or_denied_accounts_are_never_enrolled(self):
        self.shadow["alice"] = "!$6$salt$hash"
        self.assertEqual(self.add()["error"], "account_suspended")
        self.config["deny_accounts"] = {"alice"}
        self.assertEqual(self.add()["error"], "account_not_eligible")
        self.assertEqual(self.calls, [])

    def test_off_by_default(self):
        cfg = agent.load_config(os.path.join(self.tmp.name, "missing.conf"))
        self.assertFalse(cfg["jail_enrol"])

class TimeoutTests(unittest.TestCase):
    def test_silent_client_does_not_pin_the_process(self):
        code = ("import importlib.machinery as m, importlib.util as u, sys;"
                "l=m.SourceFileLoader('a', sys.argv[1]); a=u.module_from_spec(u.spec_from_loader('a', l)); l.exec_module(a);"
                "a.STDIN_TIMEOUT_SECONDS=1; sys.exit(a.main())")
        proc = subprocess.Popen([sys.executable, "-c", code, AGENT_PATH], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
        started = time.monotonic()
        out = proc.stdout.read()
        proc.wait(timeout=10)
        self.assertLess(time.monotonic() - started, 5)
        self.assertEqual(json.loads(out)["error"], "timeout")
        proc.stdin.close()


if __name__ == "__main__":
    unittest.main()
