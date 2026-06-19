using Newtonsoft.Json.Linq;

namespace HeraAgent.Tools
{
    [HeraTool(Description = "Refresh Unity assets and optionally request script compilation.")]
    public static class RefreshUnity
    {
        public class Parameters
        {
            [ToolParameter("Refresh mode: if_dirty (default) or force")]
            public string Mode { get; set; }

            [ToolParameter("Allow refresh while the editor is in or entering play mode.")]
            public bool Force { get; set; }

            [ToolParameter("Scope: all (default) or specific path")]
            public string Scope { get; set; }

            [ToolParameter("Compile mode: none (default) or request")]
            public string Compile { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params ?? new JObject());
            string mode = p.Get("mode", "if_dirty");
            string compile = p.Get("compile", "none");
            bool force = p.GetBool("force");

            var result = AssetRefresh.Refresh(mode, compile, force);
            if (result.error != null)
                return result.error;

            return new SuccessResponse("Refresh requested.", new
            {
                refresh_triggered = result.refreshTriggered,
                compile_requested = result.compileRequested,
                force = result.force,
            });
        }
    }
}
