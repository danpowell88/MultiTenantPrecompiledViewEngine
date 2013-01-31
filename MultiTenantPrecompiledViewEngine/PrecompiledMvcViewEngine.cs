using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Compilation;
using System.Web.Mvc;
using System.Web.WebPages;
using RazorGenerator.Mvc;

namespace MultiTenantPrecompiledViewEngine
{
    public class PrecompiledMvcEngine : VirtualPathProviderViewEngine, IVirtualPathFactory
    {
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly IDictionary<string, Type> _mappings;

        private readonly Func<ControllerContext, IEnumerable<Assembly>, Assembly> _tenantFilter;

        public PrecompiledMvcEngine(IEnumerable<Assembly> assemblies, Func<ControllerContext, IEnumerable<Assembly>, Assembly> tenantFilter)
        {
            _assemblies = assemblies;
            _tenantFilter = tenantFilter;

            AreaViewLocationFormats = new[]
                                          {
                                              "~/Areas/{2}/Views/{1}/{0}.cshtml",
                                              "~/Areas/{2}/Views/{1}/{0}.vbhtml",
                                              "~/Areas/{2}/Views/Shared/{0}.cshtml",
                                              "~/Areas/{2}/Views/Shared/{0}.vbhtml"
                                          };

            AreaMasterLocationFormats = new[]
                                            {
                                                "~/Areas/{2}/Views/{1}/{0}.cshtml",
                                                "~/Areas/{2}/Views/{1}/{0}.vbhtml",
                                                "~/Areas/{2}/Views/Shared/{0}.cshtml",
                                                "~/Areas/{2}/Views/Shared/{0}.vbhtml"
                                            };

            AreaPartialViewLocationFormats = new[]
                                                 {
                                                     "~/Areas/{2}/Views/{1}/{0}.cshtml",
                                                     "~/Areas/{2}/Views/{1}/{0}.vbhtml",
                                                     "~/Areas/{2}/Views/Shared/{0}.cshtml",
                                                     "~/Areas/{2}/Views/Shared/{0}.vbhtml"
                                                 };
            ViewLocationFormats = new[]
                                      {
                                          "~/Views/{1}/{0}.cshtml",
                                          "~/Views/{1}/{0}.vbhtml",
                                          "~/Views/Shared/{0}.cshtml",
                                          "~/Views/Shared/{0}.vbhtml"
                                      };
            MasterLocationFormats = new[]
                                        {
                                            "~/Views/{1}/{0}.cshtml",
                                            "~/Views/{1}/{0}.vbhtml",
                                            "~/Views/Shared/{0}.cshtml",
                                            "~/Views/Shared/{0}.vbhtml"
                                        };
            PartialViewLocationFormats = new[]
                                             {
                                                 "~/Views/{1}/{0}.cshtml",
                                                 "~/Views/{1}/{0}.vbhtml",
                                                 "~/Views/Shared/{0}.cshtml",
                                                 "~/Views/Shared/{0}.vbhtml"
                                             };
            FileExtensions = new[]
                                 {
                                     "cshtml",
                                     "vbhtml"
                                 };

            var dictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in assemblies)
            {
                var name = asm.GetName().Name;
                foreach (var type in asm.GetTypes())
                {
                    if (typeof (WebPageRenderingBase).IsAssignableFrom(type))
                    {
                        var pageVirtualPath =
                            type.GetCustomAttributes(inherit: false).OfType<PageVirtualPathAttribute>().FirstOrDefault();
                        if (pageVirtualPath != null)
                        {
                            var pair = new KeyValuePair<string, Type>(pageVirtualPath.VirtualPath, type);
                            dictionary.Add(FormatKey(name, pair.Key), pair.Value);
                        }
                    }
                }
            }
            _mappings = dictionary;
        }


        /// <summary>
        ///     Determines if IVirtualPathFactory lookups returns files from assemblies regardless of whether physical files are available for the virtual path.
        /// </summary>
        public bool PreemptPhysicalFiles { get; set; }

        public object CreateInstance(string virtualPath)
        {
            Type type;

            if (!PreemptPhysicalFiles && VirtualPathProvider.FileExists(virtualPath))
            {
                // If we aren't pre-empting physical files, use the BuildManager to create _ViewStart instances if the file exists on disk. 
                return BuildManager.CreateInstanceFromVirtualPath(virtualPath, typeof (WebPageRenderingBase));
            }
            var asm = HttpContext.Current.Items["_assembly_"] as string;
            virtualPath = asm != null ? FormatKey(asm, virtualPath) : virtualPath;
            if (_mappings.TryGetValue(virtualPath, out type))
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }

        public bool Exists(string virtualPath)
        {
            return _mappings.ContainsKey(virtualPath);
        }

        protected override IView CreatePartialView(ControllerContext controllerContext, string partialPath)
        {
            Type type;

            var tenantAssembly = _tenantFilter(controllerContext, _assemblies);

            if (_mappings.TryGetValue(FormatKey(tenantAssembly.GetName().Name, partialPath), out type))
            {
                return new PrecompiledMvcView(partialPath, type, false, FileExtensions);
            }
            return null;
        }

        protected override IView CreateView(ControllerContext controllerContext, string viewPath, string masterPath)
        {
            Type type;
            var tenantAssembly = _tenantFilter(controllerContext, _assemblies);
            if (_mappings.TryGetValue(FormatKey(tenantAssembly.GetName().Name, viewPath), out type))
            {
                return new PrecompiledMvcView(viewPath, type, true, FileExtensions);
            }
            return null;
        }


        protected override bool FileExists(ControllerContext controllerContext, string virtualPath)
        {
            var tenantAssembly = _tenantFilter(controllerContext, _assemblies);

            return Exists(FormatKey(tenantAssembly.GetName().Name, virtualPath));
        }


        private string FormatKey(string asmName, string filePath)
        {
            return asmName + "!" + filePath;
        }

        //private string GetAssemblyName(ControllerContext ctx)
        //{
        //    var cache = ctx.HttpContext.Items["cview_assembly"] as string;
        //    if (cache != null) return cache;
        //    var asm = Assembly.GetAssembly(ctx.Controller.GetType()).GetName().Name;
        //    ctx.HttpContext.Items["cview_assembly"] = asm;
        //    return asm;
        //}
    }
}