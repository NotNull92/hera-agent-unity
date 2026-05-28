namespace HeraAgent
{
    /// <summary>
    /// Plain edit-distance helper shared by every "did you mean" suggester
    /// in the connector (CommandRouter typo hints, ComponentTypeResolver,
    /// UnityDocsIndex). Was duplicated three times before extraction.
    /// </summary>
    public static class Levenshtein
    {
        public static int Distance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int del = d[i - 1, j] + 1;
                    int ins = d[i, j - 1] + 1;
                    int sub = d[i - 1, j - 1] + cost;
                    int min = del < ins ? del : ins;
                    d[i, j] = min < sub ? min : sub;
                }
            }
            return d[a.Length, b.Length];
        }
    }
}
