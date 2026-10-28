# BigFatFile (BFF): Transport Layer

Version: 2025-07-27
Author: Valeriu Paloș <vp@gcas.ro>
Status: Planning / Roadmap

> [!IMPORTANT]
> This paper was generated with the help of AI based on my own technical research.
> It is at the very least work-in-progress and designed to set the "vision" of where
> the BFF architecture intends to evolve towards.

## 1. Overview

This document outlines the long-term technical strategy for the communication protocol between the BFF backend and the BFF agent utility. To deliver on our core promise of "near-local speed" and exceptional reliability, especially on variable networks, our roadmap includes a planned evolution from standard TCP-based protocols (HTTP/2) to **QUIC**.

This strategy focuses on optimizing the two most critical communication paths:

1. **Index Synchronization:** The frequent, small metadata updates between the agent's local index (LMDB) and the remote PostgreSQL database.
2. **Data Transfer:** The bulk upload and download of file data (or data blocks) between the agent and cloud storage (Cloudflare R2).

## 2. Why QUIC?

While traditional TCP is reliable, QUIC (Quick UDP Internet Connections) is a modern transport protocol designed to solve the specific challenges faced by applications like the BFF agent. It offers significant advantages over TCP for our use case.

## 3. Core Benefits for the BFF Agent

Adopting QUIC will provide three game-changing benefits that directly enhance the user experience:

- **1. Seamless Connection Migration (The Killer Feature):**
  - **Problem:** A user on a laptop moves from their office Wi-Fi to a mobile hotspot. With TCP, the connection breaks, interrupting sync and failing large transfers.
  - **QUIC's Solution:** QUIC connections are identified by a unique ID, not the underlying IP address. The agent can seamlessly transition between networks without dropping the connection. Sync continues uninterrupted, and large file transfers do not need to be restarted. This is critical for reliability.
- **2. Elimination of Head-of-Line Blocking:**
  - **Problem:** When transferring a file as multiple chunks over TCP, a single lost packet for one chunk stalls the _entire connection_, blocking all other chunks until it's recovered.
  - **QUIC's Solution:** QUIC uses independent streams within a single connection. A lost packet on one stream does not block any others. This leads to significantly faster file assembly, especially on networks with minor packet loss.
- **3. Reduced Latency for Sync:**
  - **Problem:** The agent needs to constantly check in with the server for updates. Each check-in with TCP requires a new connection handshake, adding latency.
  - **QUIC's Solution:** QUIC supports 0-RTT (Zero Round-Trip Time) connection resumption, making the frequent "is anything new?" checks nearly instantaneous and the entire system feel more responsive.

## 4. Implementation Strategy: A Phased Approach

Implementing a custom QUIC transport layer is a significant engineering effort. Therefore, we will adopt a phased approach.

- **Phase 1 (MVP):** The initial version of the BFF agent will use a standard, robust HttpClient over **HTTP/2 (using TCP)**. This is well-supported, reliable, and allows for a faster time-to-market.
- **Phase 2 (Optimization):** Once the core product is established, we will dedicate engineering resources to build a QUIC-based transport layer.
  - **Runtime Considerations:** As noted, the initial implementation can be single-threaded for simplicity. The architecture will be designed to evolve into a multi-threaded model to handle higher concurrency and more complex background tasks as needed.
  - **Focus:** The initial QUIC implementation will focus on the **index synchronization** channel, as this is where the low-latency benefits will be most immediately felt by the user. Bulk data transfer will follow.

## 5. Conclusion

By planning to evolve our transport layer to QUIC, we are building a roadmap for a technically superior product. This strategy ensures that the BFF agent will be faster, more reliable, and more resilient than competitors, directly supporting our goal of delivering a true near-local speed virtual drive experience.
