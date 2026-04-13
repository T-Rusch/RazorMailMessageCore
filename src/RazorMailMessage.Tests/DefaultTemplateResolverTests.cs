using NUnit;
using NUnit.Framework;
using RazorMailMessage.Exceptions;
using RazorMailMessage.TemplateResolvers;
using System;
using System.Reflection;

namespace RazorMailMessage.Tests
{
    [TestFixture]
    class DefaultTemplateResolverTests
    {
        [Test]
        public void CanResolveTemplateWithDefaultAssemblyAndDefaultNameSpace()
        {
            var templateResolver = new DefaultTemplateResolver();

            var template = templateResolver.ResolveTemplate("TestTemplates.TestTemplate.cshtml", false);
            var textTemplate = templateResolver.ResolveTemplate("TestTemplates.TestTemplate.cshtml", true);
            var layout = templateResolver.ResolveLayout("TestTemplates.TestLayout.cshtml");

            Assert.AreEqual("<b>This is a test</b>", template);
            Assert.AreEqual("This is a test", textTemplate);
            Assert.AreEqual("Start layout @RenderBody End layout", layout);
        }

        [Test]
        public void CanResolveTemplateWithDefaultAssemblyAndSpecificNameSpace()
        {
            var templateResolver = new DefaultTemplateResolver("TestTemplates");

            var template = templateResolver.ResolveTemplate("TestTemplate.cshtml", false);
            var textTemplate = templateResolver.ResolveTemplate("TestTemplate.cshtml", true);
            var layout = templateResolver.ResolveLayout("TestLayout.cshtml");

            Assert.AreEqual("<b>This is a test</b>", template);
            Assert.AreEqual("This is a test", textTemplate);
            Assert.AreEqual("Start layout @RenderBody End layout", layout);
        }

        [Test]
        public void CanResolveTemplateWithSpecificAssemblyNameAndSpecificNameSpace()
        {
            var templateResolver = new DefaultTemplateResolver("RazorMailMessage.Tests", "TestTemplates");

            var template = templateResolver.ResolveTemplate("TestTemplate.cshtml", false);
            var textTemplate = templateResolver.ResolveTemplate("TestTemplate.cshtml", true);
            var layout = templateResolver.ResolveLayout("TestLayout.cshtml");

            Assert.AreEqual("<b>This is a test</b>", template);
            Assert.AreEqual("This is a test", textTemplate);
            Assert.AreEqual("Start layout @RenderBody End layout", layout);
        }

        [Test]
        public void CanResolveTemplateWithSpecificAssemblyAndSpecificNameSpace()
        {
            var templateResolver = new DefaultTemplateResolver(Assembly.Load("RazorMailMessage.Tests"), "TestTemplates");

            var template = templateResolver.ResolveTemplate("TestTemplate.cshtml", false);
            var textTemplate = templateResolver.ResolveTemplate("TestTemplate.cshtml", true);
            var layout = templateResolver.ResolveLayout("TestLayout.cshtml");

            Assert.AreEqual("<b>This is a test</b>", template);
            Assert.AreEqual("This is a test", textTemplate);
            Assert.AreEqual("Start layout @RenderBody End layout", layout);
        }

        [Test]
        public void ExceptionIsThrownWhenTemplateNameNotSpecified()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DefaultTemplateResolver().ResolveTemplate("", false));
        }

        [Test]
        public void IfLayoutIsNotFoundExceptionIsThrown()
        {
            Assert.Throws<TemplateNotFoundException>(() =>
                new DefaultTemplateResolver().ResolveLayout("BogusLayout.cshtml"));
        }

        [Test]
        public void IfAssemblyIsNullExceptionIsThrown()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DefaultTemplateResolver((Assembly)null, "TestTemplates"));
        }
    }
}
