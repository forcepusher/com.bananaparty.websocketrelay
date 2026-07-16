import { RelayServer } from "./RelayServer";

const port = Number(process.env.RELAY_PORT) || 80;
const certPath = process.env.RELAY_TLS_CERT;
const keyPath = process.env.RELAY_TLS_KEY;
const tls = certPath && keyPath ? { cert: certPath, key: keyPath } : undefined;

const server = new RelayServer(port, tls);
server.start();
