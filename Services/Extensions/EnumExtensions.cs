using System;
using System.ComponentModel;
using System.Reflection;

namespace eInvWorld.Services.Extensions
{
    public static class EnumExtensions
    {
        /// <summary>
        /// Retrieves the description attribute of an enum value.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The description if available, otherwise the enum's name as a string.</returns>
        public static string GetDescription(this Enum value)
        {
            FieldInfo? field = value.GetType().GetField(value.ToString());
            DescriptionAttribute? attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }
    }
}
