"""Asks the admin image's nginx for the pages it serves and checks that each carries exactly one policy, its own.

Usage: edge_headers_admin_check.py HOST PORT VHOST

Run against the admin image's real configuration and snippets (`docker/nginx/admin.conf`), which every location
includes by name because a location with an `add_header` of its own drops the inherited ones.
"""
import http.client
import sys


def main():
    """Runs the checks.

    :returns: The process exit code.
    """
    host, port, vhost = sys.argv[1], int(sys.argv[2]), sys.argv[3]
    failures = []
    for path in ("/", "/index.html", "/config.js", "/theme-boot.js", "/assets/none.js"):
        connection = http.client.HTTPConnection(host, port, timeout=10)
        connection.request("GET", path, headers={"Host": vhost})
        response = connection.getresponse()
        response.read()
        policies = [value for name, value in response.getheaders() if name.lower() == "content-security-policy"]
        # A missing file still answers with the policy: the 404 page of an asset is a document too.
        ok = (
            len(policies) == 1
            and policies[0].startswith("default-src 'self'")
            and "script-src 'self';" in policies[0]
            and "frame-ancestors 'none'" in policies[0]
        )
        if not ok:
            failures.append(f"admin image {path}: {policies}")
    for failure in failures:
        print(f"FAIL {failure}", file=sys.stderr)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
