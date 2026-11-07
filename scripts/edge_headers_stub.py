"""Stand-in for the three upstreams behind the edges (API, client portal, admin panel).

It answers only what the edge header check needs. The client portal's answer for a plugin page carries the narrow
`frame-src` policy that the portal's own server adds in production, so the check can prove an edge lets that second
Content-Security-Policy header through, neither stripped nor merged. The admin answer carries a marker policy so the
check can prove an edge does not replace the admin image's own.
"""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import threading

API_PORT = 8080
CLIENT_PORT = 3000
ADMIN_PORT = 80

#: The policy the portal's server adds to a plugin page document.
PLUGIN_FRAME_POLICY = "frame-src https://preview.example"

#: A policy of the admin upstream; the edge must hand it on untouched and alone.
ADMIN_POLICY = "default-src 'self'; script-src 'self'; admin-marker"


def make_handler(port):
    """Builds the request handler of one upstream.

    :param port: The port the upstream listens on, which decides its answers.
    :returns: A handler class.
    """

    class Handler(BaseHTTPRequestHandler):
        """Answers every GET with a short page and the headers its upstream would add."""

        def do_GET(self):
            """Answers one GET."""
            body = b"ok"
            self.send_response(200)
            self.send_header("Content-Type", "text/plain")
            self.send_header("Content-Length", str(len(body)))
            if port == CLIENT_PORT and "/client/apps/" in self.path:
                self.send_header("Content-Security-Policy", PLUGIN_FRAME_POLICY)
            if port == ADMIN_PORT:
                self.send_header("Content-Security-Policy", ADMIN_POLICY)
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *_args):
            """Keeps the container log quiet."""

    return Handler


def main():
    """Serves the three upstreams until the container stops."""
    servers = [ThreadingHTTPServer(("0.0.0.0", port), make_handler(port)) for port in (API_PORT, CLIENT_PORT, ADMIN_PORT)]
    for server in servers[1:]:
        threading.Thread(target=server.serve_forever, daemon=True).start()
    servers[0].serve_forever()


if __name__ == "__main__":
    main()
