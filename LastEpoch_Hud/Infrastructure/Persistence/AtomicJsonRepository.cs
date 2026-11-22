using System;
using System.IO;
using System.Text;
using LastEpoch_Hud.Scripts.Core.Persistence;
using Newtonsoft.Json;

namespace LastEpoch_Hud.Scripts.Infrastructure.Persistence;

/// <summary>
/// Stages and flushes a complete document beside its destination before a single atomic rename.
/// Corruption and I/O errors propagate; a caller must explicitly choose how to recover.
/// </summary>
public sealed class AtomicJsonRepository<T> : IJsonRepository<T>
{
    private readonly string path;
    private readonly JsonStorePolicy policy;
    private readonly Action<T> validate;
    private readonly object gate = new();

    public AtomicJsonRepository(string path, JsonStorePolicy policy = null, Action<T> validate = null)
    {
        this.path = Path.GetFullPath(path);
        this.policy = policy ?? new JsonStorePolicy();
        this.validate = validate;
    }

    public bool TryLoad(out T value)
    {
        lock (gate)
        {
            string json;
            try { json = File.ReadAllText(path); }
            catch (FileNotFoundException) { value = default; return false; }
            catch (DirectoryNotFoundException) { value = default; return false; }
            // Empty documents and JSON null must never turn an existing save into defaults.
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                throw new JsonSerializationException("Missing JSON data: " + path);
            value = JsonConvert.DeserializeObject<T>(json);
            Check(value);
            return true;
        }
    }

    public void Save(T value)
    {
        lock (gate)
        {
            Check(value);
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(value, policy.Formatting));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Exclusive creation also protects against another repository/process writing this path.
            // A staging file left by a crash is retained for explicit recovery, never overwritten.
            string temporary = path + ".tmp";
            bool created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, policy.KeepBackup ? path + ".bak" : null);
                else File.Move(temporary, path);
            }
            finally
            {
                // Cleanup must not mask the original failure or report failure after a successful commit.
                if (created)
                    try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }

    private void Check(T value)
    {
        if (value is null) throw new JsonSerializationException("Missing JSON data: " + path);
        validate?.Invoke(value);
    }
}
