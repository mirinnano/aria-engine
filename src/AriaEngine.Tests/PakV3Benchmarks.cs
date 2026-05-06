using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

// Ensure benchmark tests run sequentially to avoid MemoryMappedFile contention across tests
using AriaEngine.Packaging;

namespace AriaEngine.Tests
{
    [CollectionDefinition("PakBenchmarkCollection", DisableParallelization = true)]
    public class PakBenchmarkCollection { }

    [Collection("PakBenchmarkCollection")]
    public class PakV3Benchmarks
    {
        // Helper to create a dataset of 100 random-sized payloads (1KB - 100KB)
        private static void CreateBenchmarkDataset(out List<string> paths, out List<byte[]> data)
        {
            paths = new List<string>(100);
            data = new List<byte[]>(100);
            var rand = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                int size = rand.Next(1024, 1024 * 100 + 1); // 1KB - 100KB
                var bytes = new byte[size];
                rand.NextBytes(bytes);
                string p = $"data/file_{i + 1:D3}.bin";
                paths.Add(p);
                data.Add(bytes);
            }
        }

        [Fact]
        public void ReadSpeedBenchmark()
        {
            // Prepare dataset
            CreateBenchmarkDataset(out var paths, out var payloads);

            // V2 Pak -> read all entries in order
            string tempV2 = Path.Combine(Path.GetTempPath(), $"pak_v2_bench_{Guid.NewGuid()}.pak");
            if (File.Exists(tempV2)) File.Delete(tempV2);
            PakArchive.Write(tempV2, paths.Select((p, idx) => (p, "data", payloads[idx])), null);

            // V3 Pak (V3 writer) -> manifest built with path-hash ordering to preserve offsets
            // Build a sorted-by-PathHash order with corresponding payloads
            // Compute PathHash using the same xxHash64 approach as the production code
            ulong ComputePathHash(string path) {
                var b = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
                var hash = System.IO.Hashing.XxHash64.Hash(b);
                return BitConverter.ToUInt64(hash);
            }
            var pathHashPairs = paths.Select((p, idx) => new { Path = p, Data = payloads[idx], Hash = ComputePathHash(p) }).ToList();
            pathHashPairs.Sort((a, b) => a.Hash.CompareTo(b.Hash));

            var manifestEntries = new List<PakManifestEntryV3>();
            var pathStrings = new List<string>();
            ulong accOffset = 0;
            foreach (var item in pathHashPairs)
            {
                manifestEntries.Add(new PakManifestEntryV3
                {
                    PathHash = item.Hash,
                    Offset = accOffset,
                    Size = (uint)item.Data.Length,
                    OriginalSize = (uint)item.Data.Length,
                    Flags = 0
                });
                pathStrings.Add(item.Path);
                accOffset += (ulong)item.Data.Length;
            }
            var manifestV3 = new PakManifestV3 { Entries = manifestEntries, PathStrings = pathStrings };
            string tempV3 = Path.Combine(Path.GetTempPath(), $"pak_v3_bench_{Guid.NewGuid()}.pak");
            if (File.Exists(tempV3)) File.Delete(tempV3);
            {
                using var v3Stream = File.OpenWrite(tempV3);
                PakArchiveV3.Write(
                    v3Stream,
                    manifestV3,
                    pathHashPairs.Select(p => p.Data).ToArray());
            }

            // Read all entries for V2 and V3 and measure elapsed time
            var sw = new Stopwatch();
            // V2 read
            var readerV2 = PakArchive.Open(tempV2, null);
            sw.Start();
            foreach (var e in readerV2.GetAllEntries())
            {
                // Read using the path directly; hash verification happens inside
                var _ = readerV2.ReadAllBytes(e.Path);
            }
            sw.Stop();
            Console.WriteLine($"V2 ReadAllBytes total for 100 entries: {sw.Elapsed.TotalMilliseconds} ms");

            // V3 read
            using var readerV3 = PakArchiveV3Reader.Open(tempV3);
            sw.Start();
            foreach (var p in readerV3.PathStrings)
            {
                var _ = readerV3.ReadAllBytes(p);
            }
            sw.Stop();
            Console.WriteLine($"V3 ReadAllBytes total for 100 entries: {sw.Elapsed.TotalMilliseconds} ms");
            System.Threading.Thread.Sleep(100);
        }

        [Fact]
        public void MemoryUsageBenchmark()
        {
            // Prepare dataset
            CreateBenchmarkDataset(out var paths, out var payloads);
            string tempV2 = Path.Combine(Path.GetTempPath(), $"pak_v2_mem_bench_{Guid.NewGuid()}.pak");
            PakArchive.Write(tempV2, paths.Select((p, idx) => (p, "data", payloads[idx])), null);
            string tempV3 = Path.Combine(Path.GetTempPath(), $"pak_v3_mem_bench_{Guid.NewGuid()}.pak");
            ulong ComputePathHash(string path) {
                var b = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
                var hash = System.IO.Hashing.XxHash64.Hash(b);
                return BitConverter.ToUInt64(hash);
            }
            var pathHashPairs = paths.Select((p, idx) => new { Path = p, Data = payloads[idx], Hash = ComputePathHash(p) }).ToList();
            pathHashPairs.Sort((a, b) => a.Hash.CompareTo(b.Hash));
            var manifestEntries = new List<PakManifestEntryV3>();
            var pathStrings = new List<string>();
            ulong accOffset = 0;
            foreach (var item in pathHashPairs)
            {
                manifestEntries.Add(new PakManifestEntryV3
                {
                    PathHash = item.Hash,
                    Offset = accOffset,
                    Size = (uint)item.Data.Length,
                    OriginalSize = (uint)item.Data.Length,
                    Flags = 0
                });
                pathStrings.Add(item.Path);
                accOffset += (ulong)item.Data.Length;
            }
            var manifestV3 = new PakManifestV3 { Entries = manifestEntries, PathStrings = pathStrings };
            {
                using var v3memStream = File.OpenWrite(tempV3);
                PakArchiveV3.Write(v3memStream, manifestV3, pathHashPairs.Select(p => p.Data).ToArray());
            }

            // V2 memory measurement
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);
            var r2 = PakArchive.Open(tempV2, null);
            foreach (var e in r2.GetAllEntries())
            {
                _ = r2.ReadAllBytes(e.Path);
            }
            GC.Collect();
            long memAfterV2 = GC.GetTotalMemory(true);

            Console.WriteLine($"V2 memory delta after reads: {memAfterV2 - memBefore} bytes");

            // V3 memory measurement
            GC.Collect();
            long memBeforeV3 = GC.GetTotalMemory(true);
            using var r3 = PakArchiveV3Reader.Open(tempV3);
            foreach (var e in r3.Entries)
            {
                _ = r3.ReadAllBytes(r3.PathStrings[r3.Entries.IndexOf(e)]);
            }
            System.Threading.Thread.Sleep(100);
            GC.Collect();
            long memAfterV3 = GC.GetTotalMemory(true);
            Console.WriteLine($"V3 memory delta after reads: {memAfterV3 - memBeforeV3} bytes");
        }

        [Fact]
        public void StreamingLatencyBenchmark()
        {
            // Prepare dataset including a 16MB streaming entry
            // Build 16MB payload divided into 4MB chunks
            const int streamingSize = 16 * 1024 * 1024;
            const int chunk = 4 * 1024 * 1024;
            var data16mb = new byte[streamingSize];
            new Random(7).NextBytes(data16mb);

            // Create V2 pak with 16MB streaming entry
            string v2pak = Path.Combine(Path.GetTempPath(), $"pak_v2_stream_{Guid.NewGuid()}.pak");
            string pathStreaming = "stream/stream_16mb.bin";
            PakArchive.Write(v2pak, new[] { (pathStreaming, "stream", data16mb) }.AsEnumerable(), null);
            // Create V3 pak with same 16MB streaming entry, in single-element manifest
            ulong ComputePathHash(string path) {
                var b = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
                var hash = System.IO.Hashing.XxHash64.Hash(b);
                return BitConverter.ToUInt64(hash);
            }
            var manifestEntries = new List<PakManifestEntryV3> {
                new PakManifestEntryV3 { PathHash = ComputePathHash(pathStreaming), Offset = 0, Size = (uint)streamingSize, OriginalSize = (uint)streamingSize, Flags = 0 }
            };
            var pathStrings = new List<string> { pathStreaming };
            var manifestV3 = new PakManifestV3 { Entries = manifestEntries, PathStrings = pathStrings };
            string v3pak = Path.Combine(Path.GetTempPath(), $"pak_v3_stream_{Guid.NewGuid()}.pak");
            {
                using var v3stream = File.OpenWrite(v3pak);
                PakArchiveV3.Write(v3stream, manifestV3, new[] { data16mb });
            }

            // Helper to read chunk from V2 pak
            void ReadChunkV2(long basePos, int toRead, byte[] buffer, int off)
            {
                using var fs = File.OpenRead(v2pak);
                fs.Position = basePos;
                int read = fs.Read(buffer, off, toRead);
                if (read != toRead) throw new EndOfStreamException();
            }

            // Helper to read chunk from V3 pak (via direct file access using PayloadOffset + entry.Offset)
            void ReadChunkV3(long basePos, int toRead, byte[] buffer, int off)
            {
                using var fs = File.OpenRead(v3pak);
                fs.Position = basePos;
                int read = fs.Read(buffer, off, toRead);
                if (read != toRead) throw new EndOfStreamException();
            }

            // Determine dataStart for V2 by reading header/manifest length
            using var fV2 = File.OpenRead(v2pak);
            // magic + manifestLen (4 bytes) + manifestBytes
            var magic = new byte[5];
            fV2.ReadExactly(magic);
            var lenBuf = new byte[4];
            fV2.ReadExactly(lenBuf);
            int manifestLen = BitConverter.ToInt32(lenBuf, 0);
            long dataStartV2 = 5 + 4 + manifestLen;
            // Read using the single 16MB entry
            var entryV2 = PakArchive.Open(v2pak, null).GetAllEntries().First(); // only one entry
            // Read and time chunks
            var stopwatch = new Stopwatch();
            var chunkBuf = new byte[chunk];
            // First chunk
            stopwatch.Start();
            ReadChunkV2(dataStartV2 + (long)entryV2.Offset, chunk, chunkBuf, 0);
            stopwatch.Stop();
            Console.WriteLine($"V2 streaming first chunk: {stopwatch.Elapsed.TotalMilliseconds} ms");

            // Middle chunk
            stopwatch.Restart();
            ReadChunkV2(dataStartV2 + (long)entryV2.Offset + chunk, chunk, chunkBuf, 0);
            stopwatch.Stop();
            Console.WriteLine($"V2 streaming middle chunk: {stopwatch.Elapsed.TotalMilliseconds} ms");

            // End chunk
            stopwatch.Restart();
            ReadChunkV2(dataStartV2 + (long)entryV2.Offset + 2L * chunk, chunk, chunkBuf, 0);
            stopwatch.Stop();
            Console.WriteLine($"V2 streaming end chunk: {stopwatch.Elapsed.TotalMilliseconds} ms");

            // V3: determine PayloadOffset and perform similar chunk reads
            using var r3 = PakArchiveV3Reader.Open(v3pak);
            var payloadOffset = r3.PayloadOffset;
            var entryV3 = r3.FindEntry(pathStreaming);
            stopwatch.Reset();
            stopwatch.Start();
            ReadChunkV3((long)payloadOffset + (long)entryV3.Offset, chunk, chunkBuf, 0);
            stopwatch.Stop();
            Console.WriteLine($"V3 streaming first chunk: {stopwatch.Elapsed.TotalMilliseconds} ms");

            stopwatch.Restart();
            ReadChunkV3((long)payloadOffset + (long)entryV3.Offset + chunk, chunk, chunkBuf, 0);
            stopwatch.Stop();
            Console.WriteLine($"V3 streaming middle chunk: {stopwatch.Elapsed.TotalMilliseconds} ms");

            stopwatch.Restart();
            ReadChunkV3((long)payloadOffset + (long)entryV3.Offset + 2L * chunk, chunk, chunkBuf, 0);
            stopwatch.Stop();
            Console.WriteLine($"V3 streaming end chunk: {stopwatch.Elapsed.TotalMilliseconds} ms");
            System.Threading.Thread.Sleep(100);
        }
    }
}
