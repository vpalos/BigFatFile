using BigFatFile.Contracts.Hashing;

namespace BigFatFile.Contracts.Manifests;

public sealed record ManifestChunk(ContentHash Hash, int Size);
