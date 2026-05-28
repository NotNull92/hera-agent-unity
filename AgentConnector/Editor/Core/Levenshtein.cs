using System;

namespace HeraAgent
{
    /// <summary>
    /// Plain edit-distance helper shared by every "did you mean" suggester
    /// in the connector (CommandRouter typo hints, ComponentTypeResolver,
    /// UnityDocsStore). Was duplicated three times before extraction.
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

        /// <summary>
        /// Same as Distance but bails out the moment the best path through a
        /// row is already worse than <paramref name="maxDistance"/>. Returns
        /// <c>maxDistance + 1</c> as a sentinel for "exceeded the budget" so
        /// callers can branch on a single comparison.
        ///
        /// Cheap pre-filter: length difference alone is a lower bound on the
        /// edit distance, so a string pair whose lengths differ by more than
        /// the budget can't possibly fit. Skipping those without populating
        /// the DP table is the bulk of the speedup on 31k-key scans.
        /// </summary>
        public static int DistanceBounded(string a, string b, int maxDistance)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;
            if (Math.Abs(a.Length - b.Length) > maxDistance) return maxDistance + 1;

            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                int rowMin = int.MaxValue;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int del = d[i - 1, j] + 1;
                    int ins = d[i, j - 1] + 1;
                    int sub = d[i - 1, j - 1] + cost;
                    int min = del < ins ? del : ins;
                    int cell = min < sub ? min : sub;
                    d[i, j] = cell;
                    if (cell < rowMin) rowMin = cell;
                }
                // If even the best path through this row already exceeds the
                // budget, no further row can pull the final distance back
                // under maxDistance.
                if (rowMin > maxDistance) return maxDistance + 1;
            }
            return d[a.Length, b.Length];
        }
    }
}
