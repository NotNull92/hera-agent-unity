namespace HeraAgent
{
    internal sealed class ToolSafetyContract
    {
        public HeraRiskClass RiskClass { get; set; }
        public bool RequiresConfirmation { get; set; }
        public bool Reversible { get; set; }
        public bool SupportsCancellation { get; set; }
    }

    internal sealed class ToolSafetyRule
    {
        public string Path { get; set; }
        public string Equals { get; set; }
        public ToolSafetyContract Safety { get; set; }
    }

    internal static class ToolContractSafety
    {
        internal static ToolSafetyContract From(HeraToolAttribute attribute)
        {
            return new ToolSafetyContract
            {
                RiskClass = attribute?.RiskClass ?? HeraRiskClass.Unspecified,
                RequiresConfirmation = attribute?.RequiresConfirmation ?? false,
                Reversible = attribute?.Reversible ?? false,
                SupportsCancellation = attribute?.SupportsCancellation ?? false,
            };
        }
    }
}
