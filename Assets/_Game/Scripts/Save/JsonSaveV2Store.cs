using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TYCOON
{
    public static class JsonSaveV2Store
    {
        public static void Save(string path, GameSaveDataV2 data,
            System.Collections.Generic.IReadOnlyDictionary<string, ItemDefinition> definitions)
        {
            if (!GameSaveDataV2Validation.TryValidate(data, definitions, out var error))
                throw new ArgumentException(error, nameof(data));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is empty.", nameof(path));
            var destination = Path.GetFullPath(path);
            var folder = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(folder);
            var temporary = Path.Combine(folder, ".tycoon-save-v2-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                        writer.Write(JsonUtility.ToJson(data, true));
                    stream.Flush(true);
                }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static bool TryLoad(string path,
            System.Collections.Generic.IReadOnlyDictionary<string, ItemDefinition> definitions,
            out GameSaveDataV2 data, out string error)
        {
            data = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return Fail("Save path is empty.", out error);
                var json = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}')
                    return Fail("Save must be a JSON object.", out error);
                var candidate = new GameSaveDataV2 { version = 0, world = null, orders = null };
                JsonUtility.FromJsonOverwrite(json, candidate);
                if (!GameSaveDataV2Validation.TryValidate(candidate, definitions, out error)) return false;
                data = candidate;
                return true;
            }
            catch (Exception exception) when (exception is IOException ||
                exception is UnauthorizedAccessException || exception is ArgumentException ||
                exception is NotSupportedException)
            {
                return Fail(exception.Message, out error);
            }
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
