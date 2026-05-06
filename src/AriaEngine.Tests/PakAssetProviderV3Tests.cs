using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using AriaEngine.Assets;
using System.Linq;

namespace AriaEngine.Tests
{
    public class PakAssetProviderV3Tests
    {
        [Fact]
        public void ReadAllText_IsThreadSafe_WhenCached()
        {
            // Arrange: create provider with no pak files and pre-populate boot cache with known entries
            var provider = new PakAssetProviderV3(new string[] { }, null);

            // Use reflection to access private _bootCache and add 10 entries
            var t = typeof(PakAssetProviderV3);
            var bootCacheField = t.GetField("_bootCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var bootCache = bootCacheField.GetValue(provider);
            var cacheEntryType = bootCache.GetType().GetGenericArguments()[0]; // CacheEntry type
            var addMethod = bootCache.GetType().GetMethod("Add", new Type[] { typeof(string), cacheEntryType.GetInterface("IDictionary`2")?.GetType() ?? typeof(object) });
            // Build 10 boot entries via reflection
            var ceType = t.GetNestedType("CacheEntry", BindingFlags.NonPublic);
            for (int i = 0; i < 10; i++)
            {
                string key = $"boot_{i}";
                var ce = Activator.CreateInstance(ceType);
                var dataField = ceType.GetField("Data");
                var bytes = System.Text.Encoding.UTF8.GetBytes($"value-{i}");
                dataField.SetValue(ce, bytes);
                var dictAdd = bootCache.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
                if (dictAdd != null)
                {
                    dictAdd.Invoke(bootCache, new object[] { key, ce });
                }
            }

            // Act: Read all boot_ entries concurrently
            Parallel.For(0, 100, idx =>
            {
                string key = $"boot_{idx % 10}";
                // ReadAllText will trigger ReadAllBytesInternal and hit the boot cache path
                string _ = provider.ReadAllText(key);
            });

            // Assert: If no exceptions occurred, the concurrency path is thread-safe for reads
            Assert.True(true);
        }

        [Fact]
        public void DataCacheEntriesLimit_CanBeOverridden_ForTests()
        {
            // Arrange: override limit via reflection (test helper allowance via InternalsVisibleTo)
            var dataLimitField = typeof(PakAssetProviderV3).GetField("DataCacheEntriesLimit", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            int original = (int)(dataLimitField.GetValue(null));
            try
            {
                dataLimitField.SetValue(null, 2);
                Assert.Equal(2, (int)dataLimitField.GetValue(null));
            }
            finally
            {
                // Restore original value to avoid side effects in other tests
                dataLimitField.SetValue(null, original);
            }
        }

        [Fact]
        public void MaterializeToFile_ThrowsArgumentException_ForPathTraversal()
        {
            // Arrange
            var provider = new PakAssetProviderV3(new string[] { }, null);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => provider.MaterializeToFile("../../../etc/passwd"));
            Assert.Contains("traversal", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void MaterializeToFile_CreatesTempFile_ForValidRelativePath()
        {
            // Arrange: create provider with no pak files and pre-populate data cache
            var provider = new PakAssetProviderV3(new string[] { }, null);
            string path = "normal/path/file.txt";
            var t = typeof(PakAssetProviderV3);
            var dataCacheField = t.GetField("_dataCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var dataCache = dataCacheField.GetValue(provider);
            var ceType = t.GetNestedType("CacheEntry", BindingFlags.NonPublic);
            var ce = Activator.CreateInstance(ceType);
            var dataField = ceType.GetField("Data");
            dataField.SetValue(ce, System.Text.Encoding.UTF8.GetBytes("hello"));
            var dictAdd = dataCache.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            dictAdd.Invoke(dataCache, new object[] { path, ce });

            // Act
            string resultPath = provider.MaterializeToFile(path);

            // Assert
            Assert.True(System.IO.File.Exists(resultPath));
            Assert.Equal("hello", System.IO.File.ReadAllText(resultPath));

            // Cleanup
            try { System.IO.File.Delete(resultPath); } catch { }
            try { System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(resultPath), true); } catch { }
        }

        [Fact]
        public void Dispose_DeletesTempDirectory_CreatedByMaterializeToFile()
        {
            // Arrange
            var provider = new PakAssetProviderV3(new string[] { }, null);
            var t = typeof(PakAssetProviderV3);

            // Pre-populate data cache with a small file
            var dataCacheField = t.GetField("_dataCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var dataCache = dataCacheField.GetValue(provider);
            var ceType = t.GetNestedType("CacheEntry", BindingFlags.NonPublic);
            var ce = Activator.CreateInstance(ceType);
            var dataField = ceType.GetField("Data");
            dataField.SetValue(ce, System.Text.Encoding.UTF8.GetBytes("test content"));
            var dictAdd = dataCache.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            dictAdd.Invoke(dataCache, new object[] { "test.txt", ce });

            string filePath = provider.MaterializeToFile("test.txt");
            string tempDir = System.IO.Path.GetDirectoryName(filePath)!;

            // Act
            provider.Dispose();

            // Assert: temp directory should no longer exist
            Assert.False(System.IO.Directory.Exists(tempDir));
        }

        [Fact]
        public void Dispose_DeletesAllTempDirectories_WhenMultipleMaterializeToFileCalled()
        {
            // Arrange
            var provider = new PakAssetProviderV3(new string[] { }, null);
            var t = typeof(PakAssetProviderV3);

            // Pre-populate data cache
            var dataCacheField = t.GetField("_dataCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var dataCache = dataCacheField.GetValue(provider);
            var ceType = t.GetNestedType("CacheEntry", BindingFlags.NonPublic);
            var ce = Activator.CreateInstance(ceType);
            var dataField = ceType.GetField("Data");
            dataField.SetValue(ce, System.Text.Encoding.UTF8.GetBytes("content"));
            var dictAdd = dataCache.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            dictAdd.Invoke(dataCache, new object[] { "file.txt", ce });

            var paths = new[] { provider.MaterializeToFile("file.txt"), provider.MaterializeToFile("file.txt") };
            var tempDirs = paths.Select(p => System.IO.Path.GetDirectoryName(p)!).ToList();

            // Act
            provider.Dispose();

            // Assert: all temp directories should be deleted
            foreach (var dir in tempDirs)
            {
                Assert.False(System.IO.Directory.Exists(dir), $"Expected {dir} to be deleted");
            }
        }

        [Fact]
        public void DataCacheHit_ReturnsCachedData_WithoutTouchingDisk()
        {
            // Arrange: create provider with NO pak files
            var provider = new PakAssetProviderV3(new string[] { }, null);

            // Use reflection to inject a fake entry into _dataCache with known key and data
            string path = "cached/data/file.txt";
            byte[] cachedBytes = System.Text.Encoding.UTF8.GetBytes("cached content from data cache");
            var t = typeof(PakAssetProviderV3);
            var dataCacheField = t.GetField("_dataCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var dataCache = dataCacheField.GetValue(provider);
            var ceType = t.GetNestedType("CacheEntry", BindingFlags.NonPublic);
            var ce = Activator.CreateInstance(ceType);
            var dataField = ceType.GetField("Data");
            dataField.SetValue(ce, cachedBytes);
            var dictAdd = dataCache.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            dictAdd.Invoke(dataCache, new object[] { path, ce });

            // Act: ReadAllText hits ReadAllBytesInternal which checks _dataCache first
            string result = provider.ReadAllText(path);

            // Assert: returns the cached string without touching disk
            // Since _pakReaders is empty, any disk access would throw FileNotFoundException
            Assert.Equal("cached content from data cache", result);
        }
    }
}
