namespace HeraAgent
{
    /// <summary>
    /// The documentation bucket shipped with the package. The Connector supports Unity 6.6+ only,
    /// and the shipped ScriptReference corpus is the 6.6 documentation set, so there is no
    /// per-version bucket selection left.
    /// </summary>
    internal static class UnityVersionCompat
    {
        public const string DocsVersion = "6000.6";

        public static string CurrentDocsVersion()
        {
            return DocsVersion;
        }
    }
}
