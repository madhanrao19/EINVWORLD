using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace eInvWorld.Helpers
{
    /// <summary>
    /// A user's column choices for one list table (which columns are hidden, and their order), as kept
    /// in <c>ApplicationUser.UserPreferences</c> under <c>tableColumns:&lt;key&gt;</c> — the same JSON
    /// blob Invoice Lists keeps its own column keys in, which this never touches.
    /// Column names are the table's header labels (with "#2" etc. for repeats); they are only ever
    /// compared with the page's own labels, never rendered.
    /// </summary>
    public sealed class TableColumnPreference
    {
        public List<string> Hidden { get; set; } = new();
        public List<string> Order { get; set; } = new();
    }

    /// <summary>Validation and read/write of <see cref="TableColumnPreference"/> entries.</summary>
    public static class TablePreferences
    {
        public const string KeyPrefix = "tableColumns:";
        public const int MaxNamesPerList = 60;
        public const int MaxNameLength = 120;
        public const int MaxTablesPerUser = 60;
        /// <summary>Upper bound for the whole UserPreferences JSON after a write (it's loaded with the user).</summary>
        public const int MaxTotalJsonLength = 64 * 1024;

        private static readonly Regex KeyPattern = new("^[a-z0-9][a-z0-9:_-]{0,63}$", RegexOptions.CultureInvariant);

        /// <summary>Table keys come from <c>data-einv-table</c> attributes, e.g. "items", "code-taxtypes".</summary>
        public static bool IsValidKey(string? key) => key != null && KeyPattern.IsMatch(key);

        /// <summary>
        /// Trims and de-duplicates the names. Returns null (reject the request) when a list is too long
        /// or a name is empty/too long, rather than silently storing part of what the user chose.
        /// </summary>
        public static TableColumnPreference? Normalize(TableColumnPreference? input)
        {
            if (input == null) return null;
            var hidden = Clean(input.Hidden);
            var order = Clean(input.Order);
            return hidden == null || order == null ? null : new TableColumnPreference { Hidden = hidden, Order = order };
        }

        private static List<string>? Clean(List<string>? names)
        {
            if (names == null) return new List<string>();
            if (names.Count > MaxNamesPerList) return null;
            var result = new List<string>(names.Count);
            foreach (var raw in names)
            {
                var name = raw?.Trim();
                if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength) return null;
                if (!result.Contains(name, StringComparer.Ordinal)) result.Add(name);
            }
            return result;
        }

        /// <summary>The saved entries for the requested keys (invalid or unsaved keys are left out).</summary>
        public static Dictionary<string, TableColumnPreference> Read(string? userPreferencesJson, IEnumerable<string> keys)
        {
            var result = new Dictionary<string, TableColumnPreference>(StringComparer.Ordinal);
            var root = Parse(userPreferencesJson);
            foreach (var key in keys.Where(IsValidKey).Distinct(StringComparer.Ordinal))
            {
                if (root[KeyPrefix + key] is not JObject entry) continue;
                try
                {
                    var pref = Normalize(entry.ToObject<TableColumnPreference>());
                    if (pref != null) result[key] = pref;
                }
                catch (JsonException) { /* a damaged entry reads as "no preference" */ }
            }
            return result;
        }

        /// <summary>
        /// Returns the updated JSON with this table's entry set (or removed, when both lists are empty —
        /// "Reset to default"). Every other preference is kept as-is. Null when the user already has the
        /// maximum number of saved tables and this would add another, or the result would be too large.
        /// </summary>
        public static string? Write(string? userPreferencesJson, string key, TableColumnPreference pref)
        {
            if (!IsValidKey(key)) throw new ArgumentException("Invalid table key.", nameof(key));
            var root = Parse(userPreferencesJson);
            var name = KeyPrefix + key;
            if (pref.Hidden.Count == 0 && pref.Order.Count == 0)
            {
                root.Remove(name);
            }
            else
            {
                var count = root.Properties().Count(p => p.Name.StartsWith(KeyPrefix, StringComparison.Ordinal));
                if (root[name] == null && count >= MaxTablesPerUser) return null;
                root[name] = JObject.FromObject(new { hidden = pref.Hidden, order = pref.Order });
            }
            var result = root.ToString(Formatting.None);
            return result.Length > MaxTotalJsonLength ? null : result;
        }

        // Unreadable JSON is replaced rather than failing every save (Invoice Lists does the same).
        private static JObject Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new JObject();
            try
            {
                // DateParseHandling.None: other preferences are written back exactly as stored.
                using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
                return JToken.ReadFrom(reader) as JObject ?? new JObject();
            }
            catch (JsonException) { return new JObject(); }
        }
    }
}
