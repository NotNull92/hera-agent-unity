using System.Text.RegularExpressions;

namespace HeraAgent
{
    public static class StringCaseUtility
    {
        public static string ToSnakeCase(string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            var result = Regex.Replace(str, "([a-z0-9])([A-Z])", "$1_$2");
            result = Regex.Replace(result, "([A-Z]+)([A-Z][a-z])", "$1_$2");
            return result.ToLowerInvariant();
        }
    }
}
