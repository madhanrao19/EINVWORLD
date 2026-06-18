using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EINVWORLD.Helpers
{
    /// <summary>
    /// Newtonsoft contract resolver that skips EMPTY collections (in addition to nulls, which
    /// <c>NullValueHandling.Ignore</c> already handles). This keeps the submitted MyInvois document to
    /// only the fields that actually have data — matching LHDN's expectation that optional structures are
    /// OMITTED rather than sent as empty arrays. An empty array (e.g. <c>"Percent": []</c>) is read by
    /// LHDN as "TooFewItems" and rejected; omitting it entirely is valid.
    ///
    /// Property names are preserved (PascalCase) — this only adds a ShouldSerialize guard for collections.
    /// </summary>
    public class SkipEmptyCollectionsContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            // Strings are IEnumerable but must never be treated as a collection here.
            if (property.PropertyType != null
                && property.PropertyType != typeof(string)
                && typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                var basePredicate = property.ShouldSerialize;
                property.ShouldSerialize = instance =>
                {
                    if (basePredicate != null && !basePredicate(instance))
                        return false;

                    if (property.ValueProvider?.GetValue(instance) is not IEnumerable value)
                        return false; // null collection → omit

                    return value.GetEnumerator().MoveNext(); // serialize only if it has at least one item
                };
            }

            return property;
        }
    }
}
