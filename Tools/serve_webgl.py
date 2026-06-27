#!/usr/bin/env python3
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import argparse
import os


class UnityWebGLHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(".wasm.gz"):
            return "application/wasm"
        if path.endswith(".js.gz"):
            return "application/javascript"
        if path.endswith(".data.gz"):
            return "application/octet-stream"
        if path.endswith(".wasm"):
            return "application/wasm"
        if path.endswith(".data"):
            return "application/octet-stream"
        return super().guess_type(path)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        if self.path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        super().end_headers()


def main():
    parser = argparse.ArgumentParser(description="Serve a Unity WebGL build with gzip headers.")
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--directory", default="Builds/WebGL")
    args = parser.parse_args()

    root = Path(args.directory).resolve()
    os.chdir(root)
    server = ThreadingHTTPServer(("127.0.0.1", args.port), UnityWebGLHandler)
    print(f"Serving {root} at http://127.0.0.1:{args.port}/")
    server.serve_forever()


if __name__ == "__main__":
    main()
