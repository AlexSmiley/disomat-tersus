"""Standard-library smoke tests for the synthetic terminal."""
import pathlib
import socket
import subprocess
import sys
import time
import unittest

SERVER = pathlib.Path(__file__).resolve().parents[1] / "simulator" / "server.py"
REQUEST = bytes.fromhex("0000010000240155650004017270746f204223422334362331233430233230303123312336312123aa84")


class SimulatorTests(unittest.TestCase):
    def test_two_requests_on_same_connection(self):
        import socketserver
        # Choose a free port before starting the test subprocess.
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0))
            port = probe.getsockname()[1]
        proc = subprocess.Popen([sys.executable, str(SERVER), "--port", str(port)],
                                stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        try:
            client = None
            for _ in range(50):
                try:
                    client = socket.create_connection(("127.0.0.1", port), timeout=1)
                    break
                except OSError:
                    time.sleep(0.05)
            self.assertIsNotNone(client)
            with client:
                client.settimeout(2)
                buffer = bytearray()
                for _ in range(2):
                    client.sendall(REQUEST)
                    while buffer.count(b"#") < 5:
                        buffer.extend(client.recv(128))
                    split_at = self.fifth_hash(buffer)
                    value = buffer[:split_at + 1].split(b"#")[4]
                    self.assertGreater(int(value), 0)
                    del buffer[:split_at + 1]
                    # The synthetic trailer is not formally framed and may remain buffered.
                    buffer.clear()
        finally:
            proc.terminate()
            try:
                proc.wait(timeout=3)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait()

    @staticmethod
    def fifth_hash(data):
        n = 0
        for i, char in enumerate(data):
            if char == ord("#"):
                n += 1
                if n == 5:
                    return i
        raise AssertionError("incomplete frame")


if __name__ == "__main__":
    unittest.main()
