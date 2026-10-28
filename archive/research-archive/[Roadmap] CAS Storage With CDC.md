# BigFatFile (BFF) Content-Addressable Storage via CDC

Version: 2025-07-26
Author: Valeriu Paloș <vp@gcas.ro>
Status: Vision / Roadmap

> [!IMPORTANT]
> This paper was generated with the help of AI based on my own technical research.
> It is at the very least work-in-progress and designed to set the "vision" of where
> the BFF architecture intends to evolve towards.

## 1. Overview

This document outlines the future technical architecture for BigFatFile's storage backend. The goal is to evolve from a simple full-snapshot model to a highly efficient **Content-Addressable Storage (CAS)** system that uses **Content-Defined Chunking (CDC)**.

This architecture is designed to provide a "near-local" user experience by dramatically optimizing bandwidth usage and storage costs for large, frequently modified files, while also providing robust versioning, deduplication, and client-side security.

### Core Features Enabled:

- **Deduplication:** Identical blocks of data are stored only once across the entire system.
- **Bandwidth Optimization:** Only new or modified blocks are transferred during an update.
- **Client-Side Encryption:** Data is encrypted on the client before upload, ensuring zero-knowledge privacy.
- **Compression:** Blocks are compressed client-side to reduce storage footprint and transfer times.
- **Efficient Versioning:** New file versions are created by creating a new list of block references, not by re-uploading the entire file.

## 2. High-Level Architecture

The system is composed of three main parts: the **Client Application**, the **BFF Backend (API & Database)**, and the **Block Storage (S3)**. The client is responsible for all "heavy" processing (chunking, hashing, encryption), ensuring the backend remains scalable and stateless.

### Upload / File Update Flow:

1. **Chunking:** The client reads the file and uses a **Content-Defined Chunking** algorithm (e.g., FastCDC) to split it into variable-sized blocks.
2. **Hashing:** For each block, the client calculates a **strong cryptographic hash** (e.g., BLAKE3) which will serve as the block's unique, content-derived ID.
3. **Deduplication Check:** The client sends the list of block hashes to the BFF backend. The backend checks which of these blocks it has already seen and stored.
4. **Compress & Encrypt:** For the new blocks that the backend has not seen, the client compresses (e.g., with Zstandard) and encrypts them using a key derived from the drive's settings.
5. **Upload:** The client uploads the encrypted, compressed blocks directly to S3, using the block's hash as the object key.
6. **Update Manifest:** The client sends the final, ordered list of block hashes (the "fingerprint") to the BFF backend. The backend saves this list as the storage_manifest for the new file version in the database.

### Download / File Restore Flow:

1. **Fetch Manifest:** The client requests a specific file version from the BFF backend.
2. **Receive Manifest:** The backend returns the storage_manifest for that version, which contains the ordered list of block hashes.
3. **Fetch Blocks:** The client requests the required blocks from S3 in parallel, using their hashes as keys.
4. **Decrypt, Decompress & Reassemble:** The client decrypts and decompresses each block, then reassembles them in the correct order to reconstruct the original file.

## 3. Core Concepts & Technical Details

### 3.1. Content-Defined Chunking (CDC)

Instead of fixed-size blocks, we use CDC to create variable-size blocks based on the file's content.

- **Algorithm:** FastCDC
- **Block Size:** Target average block size of \~4MB, with a defined min (\~1MB) and max (\~16MB).
- **Benefit:** This ensures that an insertion or deletion in the middle of a file only affects the blocks immediately surrounding the change, maximizing deduplication efficiency.

### 3.2. Block Hashing & Identification

Each block is identified by its strong cryptographic hash.

- **Algorithm:** BLAKE3 (256-bit)
- **Process:** After a block is chunked, its content is hashed. This hash becomes the block's universal ID and its object key in S3. This guarantees that identical content results in an identical hash, forming the basis of our deduplication.

### 3.3. The storage_manifest (The Fingerprint)

This is the key data structure stored in the nodes and node_versions tables in our PostgreSQL database. It replaces a simple file path with a rich descriptor.

```json
{
  "type": "chunked",
  "checksum": "blake3-hash-of-reconstructed-file",
  "size_bytes": 104857600,
  "blocks": [
    { "hash": "blake3-of-block-1", "size": 4194304 },
    { "hash": "blake3-of-block-2", "size": 4194304 },
    { "hash": "blake3-of-block-3", "size": 2097152 }
  ]
}
```

### 3.4. Critical System Process: Garbage Collection

This architecture's primary challenge is identifying and deleting orphaned blocks from S3 that are no longer referenced by any file version. This requires a robust, system-wide garbage collection process.

- **Method:** Mark and Sweep.
- **Process:**
  1. **Mark:** A background job periodically scans the entire nodes and node_versions tables, reading every storage_manifest. It builds a complete set of all unique block hashes that are currently "active".
  2. **Sweep:** The job then lists all objects in the S3 block storage bucket. For each object, it checks if its key (the block hash) exists in the active set.
  3. **Delete:** If a block hash is not in the active set, it is an orphan and can be safely deleted from S3.
- **Implementation Note:** This process must be designed with extreme care to prevent race conditions and ensure data integrity.

## 4. Technology Stack

- **Chunking:** FastCDC (Rust library with C\# bindings or a WASM module for web clients).
- **Hashing:** BLAKE3 (highly parallel and fast).
- **Compression:** Zstandard (excellent compression ratio and speed).
- **Encryption:** AES-256 GCM using client-side keys.
- **Database:** PostgreSQL with LTREE and TimescaleDB.
- **Block Storage:** Any S3-compatible object store.

This architecture represents a significant engineering investment but provides a powerful foundation for a highly efficient, secure, and scalable file storage platform.
