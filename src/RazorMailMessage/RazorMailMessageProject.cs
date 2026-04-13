using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RazorLight;
using RazorLight.Razor;
using RazorMailMessage.TemplateCache;
using RazorMailMessage.TemplateResolvers;

namespace RazorMailMessage
{
    internal class RazorMailMessageProject : RazorLightProject
    {
        private readonly ITemplateResolver _templateResolver;
        private readonly ITemplateCache _templateCache;

        public RazorMailMessageProject(ITemplateResolver templateResolver, ITemplateCache templateCache)
        {
            _templateResolver = templateResolver;
            _templateCache = templateCache;
        }

        /// <summary>
        /// Called by RazorLight when it needs to resolve a template by key — used primarily for layouts.
        /// Main template content is provided directly via CompileRenderStringAsync, but layouts
        /// referenced with @{ Layout = "LayoutName"; } are resolved through this method.
        /// </summary>
        public override Task<RazorLightProjectItem> GetItemAsync(string templateKey)
        {
            // Check cache first (layouts may have been cached already)
            var cached = _templateCache.Get(templateKey);
            if (cached != null)
                return Task.FromResult<RazorLightProjectItem>(new TextSourceRazorProjectItem(templateKey, cached));

            // Try to resolve as a layout via the template resolver
            string layout = null;
            try { layout = _templateResolver.ResolveLayout(templateKey); }
            catch { /* Layout not found — return not-found item below */ }

            if (layout != null)
            {
                _templateCache.Add(templateKey, layout);
                return Task.FromResult<RazorLightProjectItem>(new TextSourceRazorProjectItem(templateKey, layout));
            }

            // Return an item with Exists = false
            return Task.FromResult<RazorLightProjectItem>(new TextSourceRazorProjectItem(templateKey, null));
        }

        public override Task<IEnumerable<RazorLightProjectItem>> GetImportsAsync(string templateKey)
        {
            return Task.FromResult(Enumerable.Empty<RazorLightProjectItem>());
        }
    }
}
