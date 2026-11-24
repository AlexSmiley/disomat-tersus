"""Synthetic DISOMAT weighing terminal for development; not hardware-verified."""
import argparse
import socketserver
import time

REQUEST = bytes.fromhex("0000010000240155650004017270746f204223422334362331233430233230303123312336312123aa84")


class Handler(socketserver.BaseRequestHandler):
    def handle(self):
        pending = bytearray()
        count = 0
        while True:
            chunk = self.request.recv(4096)
            if not chunk:
                return
            pending.extend(chunk)
            while len(pending) >= len(REQUEST):
                if pending[:len(REQUEST)] != REQUEST:
                    self.request.sendall(b"ERROR: unexpected command")
                    return
                del pending[:len(REQUEST)]
                weight = 28450 + (count % 7) * 3
                count += 1
                # Fourth/fifth '#' delimit the numeric weight field.
                response = f"SIM#B#46#1#{weight}#END".encode("ascii")
                mid = len(response) // 2
                self.request.sendall(response[:mid])
                time.sleep(0.01)
                self.request.sendall(response[mid:])


class Server(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", default=1502, type=int)
    args = parser.parse_args()
    with Server((args.host, args.port), Handler) as server:
        print(f"Simulated DISOMAT listening at {args.host}:{args.port}", flush=True)
        server.serve_forever()
