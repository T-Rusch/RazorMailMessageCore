using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Mail;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using RazorLight;
using RazorMailMessage.Exceptions;
using RazorMailMessage.TemplateBase;
using RazorMailMessage.TemplateCache;
using RazorMailMessage.TemplateResolvers;
using ITemplateResolver = RazorMailMessage.TemplateResolvers.ITemplateResolver;

namespace RazorMailMessage
{
    public class RazorMailMessageFactory : IRazorMailMessageFactory, IDisposable
    {
        private readonly ITemplateResolver _templateResolver;
        private readonly ITemplateCache _templateCache;
        private readonly IRazorLightEngine _engine;
        private readonly Type _templateBase;

        public RazorMailMessageFactory() : this(new DefaultTemplateResolver(Assembly.GetCallingAssembly(), string.Empty), typeof(DefaultTemplateBase<>), null, new InMemoryTemplateCache()) { }

        public RazorMailMessageFactory(ITemplateResolver templateResolver) : this(templateResolver, typeof(DefaultTemplateBase<>), null, new InMemoryTemplateCache()) { }

        public RazorMailMessageFactory(ITemplateResolver templateResolver, Type templateBase) : this(templateResolver, templateBase, null, new InMemoryTemplateCache()) { }

        public RazorMailMessageFactory(ITemplateResolver templateResolver, Type templateBase, Func<Type, object> dependencyResolver) : this(templateResolver, templateBase, dependencyResolver, new InMemoryTemplateCache()) { }

        public RazorMailMessageFactory(ITemplateResolver templateResolver, Type templateBase, Func<Type, object> dependencyResolver, ITemplateCache templateCache)
        {
            if (templateResolver == null)
                throw new ArgumentNullException("templateResolver");
            if (templateCache == null)
                throw new ArgumentNullException("templateCache");
            if (templateBase == null)
                throw new ArgumentNullException("templateBase");

            _templateResolver = templateResolver;
            _templateCache = templateCache;
            _templateBase = templateBase;

            var project = new RazorMailMessageProject(templateResolver, templateCache);

            var builder = new RazorLightEngineBuilder()
                .UseProject(project)
                .UseMemoryCachingProvider();

            // When a custom template base class is used, add the necessary assembly references
            // so RazorLight's compiler can resolve the type used in the injected @inherits directive.
            if (templateBase != typeof(DefaultTemplateBase<>))
            {
                var refs = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(DefaultTemplateBase<>).Assembly.Location)
                };
                if (templateBase.Assembly != typeof(DefaultTemplateBase<>).Assembly)
                    refs.Add(MetadataReference.CreateFromFile(templateBase.Assembly.Location));

                builder = builder.AddMetadataReferences(refs.ToArray());
            }

            _engine = builder.Build();
        }

        public virtual MailMessage Create<TModel>(string templateName, TModel model)
        {
            return Create(templateName, model, null, null);
        }

        public virtual MailMessage Create<TModel>(string templateName, TModel model, ExpandoObject viewBag)
        {
            return Create(templateName, model, viewBag, null);
        }

        public virtual MailMessage Create<TModel>(string templateName, TModel model, IEnumerable<LinkedResource> linkedResources)
        {
            return Create(templateName, model, null, linkedResources);
        }

        public virtual MailMessage Create<TModel>(string templateName, TModel model, ExpandoObject viewBag, IEnumerable<LinkedResource> linkedResources)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentNullException("templateName");

            linkedResources = linkedResources ?? new List<LinkedResource>();

            var htmlTemplate = ParseTemplate(templateName, model, false, viewBag);
            var textTemplate = ParseTemplate(templateName, model, true, viewBag);

            var hasHtmlTemplate = !string.IsNullOrWhiteSpace(htmlTemplate);
            var hasTextTemplate = !string.IsNullOrWhiteSpace(textTemplate);

            if (!hasHtmlTemplate && !hasTextTemplate)
                throw new RazorLight.TemplateNotFoundException(templateName);

            var mailMessage = new MailMessage { BodyEncoding = Encoding.UTF8 };

            if (hasTextTemplate)
            {
                // Plain-text body; HTML goes in an alternate view
                // http://msdn.microsoft.com/en-us/library/system.net.mail.mailmessage.alternateviews.aspx
                mailMessage.Body = textTemplate;
            }

            if (hasHtmlTemplate)
            {
                // Always use an alternate view for HTML so linked resources can be attached
                mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlTemplate, Encoding.UTF8, MediaTypeNames.Text.Html));

                foreach (var linkedResource in linkedResources)
                    mailMessage.AlternateViews[0].LinkedResources.Add(linkedResource);

                // If no plain-text template, also set the HTML body directly
                if (!hasTextTemplate)
                    mailMessage.Body = htmlTemplate;
            }

            mailMessage.IsBodyHtml = !hasTextTemplate;
            return mailMessage;
        }

        private string ParseTemplate<TModel>(string templateName, TModel model, bool plainText, ExpandoObject viewBag)
        {
            var templateCacheName = ResolveTemplateCacheName(templateName, plainText);

            // Resolve template content (use cache to avoid repeated resolution)
            var template = _templateCache.Get(templateCacheName);
            if (template == null)
            {
                template = _templateResolver.ResolveTemplate(templateName, plainText);
                // Cache empty string for missing plain-text variants so we don't retry
                _templateCache.Add(templateCacheName, template ?? "");
            }

            if (string.IsNullOrWhiteSpace(template)) return null;

            // Prepend @inherits directive for custom base classes so helper methods are accessible
            var templateContent = PrepareTemplate(template);

            return _engine.CompileRenderStringAsync(templateCacheName, templateContent, model, viewBag)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Prepends the @inherits directive when a non-default base class is configured,
        /// giving compiled templates access to custom helper methods defined on the base.
        /// </summary>
        private string PrepareTemplate(string template)
        {
            if (_templateBase == typeof(DefaultTemplateBase<>))
                return template;

            // Build a fully-qualified type name with <dynamic> for the generic parameter.
            // e.g. "MyApp.CustomTemplateBase`1" → "MyApp.CustomTemplateBase<dynamic>"
            var typeName = _templateBase.FullName.Replace("`1", "<dynamic>");
            return $"@inherits {typeName}{Environment.NewLine}{template}";
        }

        private static string ResolveTemplateCacheName(string templateName, bool plainText)
        {
            var parts = new List<string> { templateName };
            if (plainText) parts.Add("text");
            return string.Join(".", parts);
        }

        public void Dispose()
        {
            // RazorLightEngine does not implement IDisposable
        }
    }
}
