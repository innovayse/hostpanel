"""Asks one running edge for the pages whose Content-Security-Policy matters and checks the headers it answers with.

Usage: edge_headers_check.py HOST PORT PORTAL_VHOST ADMIN_VHOST

The edge is the real nginx template or the real Caddyfile of this repository, run in front of
`edge_headers_stub.py`. Exits 1 with every failed expectation printed on stderr. The portal's policy, with its
whitespace normalised, is printed on stdout so the caller can compare the two edges.
"""
import http.client
import sys

#: The narrow policy the stub's portal adds on a plugin page; keep in step with `edge_headers_stub.py`.
PLUGIN_FRAME_POLICY = "frame-src https://preview.example"

#: What the stub's admin upstream sends; the edge must pass it through alone.
ADMIN_POLICY = "default-src 'self'; script-src 'self'; admin-marker"

#: What an uploaded file is served under.
UPLOAD_POLICY = "default-src 'none'; sandbox"


def fetch(host, port, vhost, path):
    """Requests a path and returns the status and the response headers in the order received.

    :param host: The edge's address.
    :param port: The edge's port.
    :param vhost: The Host header, which picks the portal or the admin site.
    :param path: The request path.
    :returns: The status and a list of (lower-case name, value) pairs; a repeated header appears repeatedly.
    """
    connection = http.client.HTTPConnection(host, port, timeout=10)
    connection.request("GET", path, headers={"Host": vhost})
    response = connection.getresponse()
    headers = [(name.lower(), value) for name, value in response.getheaders()]
    response.read()
    connection.close()
    return response.status, headers


def values(headers, name):
    """Lists every value of one header.

    :param headers: The (name, value) pairs.
    :param name: The lower-case header name.
    :returns: The values, one per occurrence.
    """
    return [value for header, value in headers if header == name]


def main():
    """Runs the checks.

    :returns: The process exit code.
    """
    host, port, portal, admin = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]
    failures = []

    def expect(condition, message):
        """Records a failed expectation.

        :param condition: What must hold.
        :param message: What to print when it does not.
        """
        if not condition:
            failures.append(message)

    status, headers = fetch(host, port, portal, "/")
    csp = values(headers, "content-security-policy")
    expect(status == 200, f"/ answered {status}")
    expect(len(csp) == 1, f"/ carries {len(csp)} Content-Security-Policy headers, expected 1")
    portal_policy = csp[0] if csp else ""
    expect(portal_policy.startswith("default-src 'self'"), "/ policy does not start from 'self'")
    expect("frame-ancestors 'none'" in portal_policy, "/ policy lets the portal be framed")
    expect(values(headers, "x-content-type-options") == ["nosniff"], "/ lacks X-Content-Type-Options: nosniff")
    expect(values(headers, "x-frame-options") == ["DENY"], "/ lacks X-Frame-Options: DENY")
    expect(not any(value.lower().startswith("nginx/") or value.lower().startswith("caddy/") for value in values(headers, "server")),
           "/ names the proxy's version")
    print(" ".join(portal_policy.split()))

    # A plugin page gets a second, narrow policy from the portal; both must reach the browser, as two headers.
    for path in ("/client/apps/demo", "/hy/client/apps/demo/page"):
        status, headers = fetch(host, port, portal, path)
        csp = values(headers, "content-security-policy")
        expect(status == 200, f"{path} answered {status}")
        expect(len(csp) == 2, f"{path} carries {len(csp)} Content-Security-Policy headers, expected 2")
        expect(PLUGIN_FRAME_POLICY in csp, f"{path} lost the plugin's narrow frame-src policy")
        expect(any(value.startswith("default-src 'self'") for value in csp), f"{path} lost the edge policy")

    status, headers = fetch(host, port, portal, "/uploads/logo.png")
    expect(values(headers, "content-security-policy") == [UPLOAD_POLICY], "/uploads/ is not served under its own policy")

    status, headers = fetch(host, port, admin, "/")
    csp = values(headers, "content-security-policy")
    expect(csp == [ADMIN_POLICY], f"admin / does not carry the admin upstream's policy alone: {csp}")

    for failure in failures:
        print(f"FAIL {host}:{port}: {failure}", file=sys.stderr)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
