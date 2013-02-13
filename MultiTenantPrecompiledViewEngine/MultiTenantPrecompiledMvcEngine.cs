/** 
 * This is based on PrecompiledViews written by Chris van de Steeg. 
 * Code and discussions for Chris's changes are available at (http://www.chrisvandesteeg.nl/2010/11/22/embedding-pre-compiled-razor-views-in-your-dll/)
 **/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Compilation;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.WebPages;
using RazorGenerator.Mvc;

namespace MultiTenantPrecompiledViewEngine
{
    public class MultiTenantPrecompiledMvcEngine : VirtualPathProviderViewEngine, IVirtualPathFactory
    {
        private readonly IEnumerable<Assembly> _assemblies;
        private readonly IDictionary<string, Type> _mappings;
        private readonly string _baseVirtualPath;
        private readonly IDictionary<Assembly,Lazy<DateTime>> _assemblyLastWriteTimes;

        private readonly Func<RouteValueDictionary, IEnumerable<Assembly>, Assembly> _tenantFilter;

        public MultiTenantPrecompiledMvcEngine(IEnumerable<Assembly> assemblies,
                                               Func<RouteValueDictionary, IEnumerable<Assembly>, Assembly> tenantFilter):this(assemblies,null,tenantFilter)
        {
        }

        public MultiTenantPrecompiledMvcEngine(IEnumerable<Assembly> assemblies, string baseVirtualPath, Func<RouteValueDictionary, IEnumerable<Assembly>, Assembly> tenantFilter)
        {
            _assemblies = assemblies.ToList();
            _tenantFilter = tenantFilter;

            _assemblyLastWriteTimes = _assemblies.ToDictionary(k => k,
                                                              v =>
                                                              new Lazy<DateTime>(
                                                                  () =>
                                                                  v.GetLastWriteTimeUtc(fallback: DateTime.MaxValue)));
           
            _baseVirtualPath = NormalizeBaseVirtualPath(baseVirtualPath);

            base.AreaViewLocationFormats = new[] {
                "~/Areas/{2}/Views/{1}/{0}.cshtml", 
                "~/Areas/{2}/Views/Shared/{0}.cshtml", 
            };

            base.AreaMasterLocationFormats = new[] {
                "~/Areas/{2}/Views/{1}/{0}.cshtml", 
                "~/Areas/{2}/Views/Shared/{0}.cshtml", 
            };

            base.AreaPartialViewLocationFormats = new[] {
                "~/Areas/{2}/Views/{1}/{0}.cshtml", 
                "~/Areas/{2}/Views/Shared/{0}.cshtml", 
            };
            base.ViewLocationFormats = new[] {
                "~/Views/{1}/{0}.cshtml", 
                "~/Views/Shared/{0}.cshtml", 
            };
            base.MasterLocationFormats = new[] {
                "~/Views/{1}/{0}.cshtml", 
                "~/Views/Shared/{0}.cshtml", 
            };
            base.PartialViewLocationFormats = new[] {
                "~/Views/{1}/{0}.cshtml", 
                "~/Views/Shared/{0}.cshtml", 
            };
            base.FileExtensions = new[] {
                "cshtml", 
            };


            var dictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in _assemblies)
            {
                var name = asm.GetName().Name;
                foreach (var pair in from type in asm.GetTypes() 
                                     where typeof(WebPageRenderingBase).IsAssignableFrom(type) 
                                     let pageVirtualPath = type.GetCustomAttributes(inherit: false).OfType<PageVirtualPathAttribute>().FirstOrDefault() 
                                     where pageVirtualPath != null select new KeyValuePair<string, Type>(pageVirtualPath.VirtualPath, type))
                {
                    dictionary.Add(FormatKey(name, pair.Key), pair.Value);
                }
            }
            _mappings = dictionary;
            //this.ViewLocationCache = new PrecompiledViewLocationCache(assembly.FullName, this.ViewLocationCache);
        }

        private string FormatKey(string asmName, string filePath)
        {
            return asmName + "!" + filePath;
        }

        /// <summary>
        /// Determines if IVirtualPathFactory lookups returns files from assembly regardless of whether physical files are available for the virtual path.
        /// </summary>
        public bool PreemptPhysicalFiles
        {
            get;
            set;
        }

        /// <summary>
        /// Determines if the view engine uses views on disk if it's been changed since the view assembly was last compiled.
        /// </summary>
        /// <remarks>
        /// What an awful name!
        /// </remarks>
        public bool UsePhysicalViewsIfNewer
        {
            get;
            set;
        }

        protected override bool FileExists(ControllerContext controllerContext, string virtualPath)
        {
            var tenantAssembly = _tenantFilter(controllerContext.RouteData.Values, _assemblies);

            if (UsePhysicalViewsIfNewer && IsPhysicalFileNewer(virtualPath, tenantAssembly))
            {
                // If the physical file on disk is newer and the user's opted in this behavior, serve it instead.
                return false;
            }
            

            return tenantAssembly != null && Exists(FormatKey(tenantAssembly.GetName().Name, virtualPath));
        }

        protected override IView CreatePartialView(ControllerContext controllerContext, string partialPath)
        {
            return CreateViewInternal(controllerContext,partialPath, masterPath: null, runViewStartPages: false);
        }

        protected override IView CreateView(ControllerContext controllerContext, string viewPath, string masterPath)
        {
            return CreateViewInternal(controllerContext,viewPath, masterPath, runViewStartPages: true);
        }

        private IView CreateViewInternal(ControllerContext controllerContext,string viewPath, string masterPath, bool runViewStartPages)
        {
            Type type;

            var tenantAssembly = _tenantFilter(controllerContext.RouteData.Values, _assemblies);

            if (_mappings.TryGetValue(FormatKey(tenantAssembly.GetName().Name, viewPath), out type))
            {
                return new PrecompiledMvcView(viewPath, masterPath, type, runViewStartPages, base.FileExtensions);
            }
            return null;
        }

        public object CreateInstance(string virtualPath)
        {
            Type type;
            var routeData = ((MvcHandler)HttpContext.Current.Handler).RequestContext.RouteData;

            var asm = _tenantFilter(routeData.Values, _assemblies);


            if (!PreemptPhysicalFiles && VirtualPathProvider.FileExists(virtualPath))
            {
                // If we aren't pre-empting physical files, use the BuildManager to create _ViewStart instances if the file exists on disk. 
                return BuildManager.CreateInstanceFromVirtualPath(virtualPath, typeof(WebPageRenderingBase));
            }

            if (UsePhysicalViewsIfNewer && IsPhysicalFileNewer(virtualPath,asm))
            {
                // If the physical file on disk is newer and the user's opted in this behavior, serve it instead.
                return BuildManager.CreateInstanceFromVirtualPath(virtualPath, typeof(WebViewPage));
            }


            virtualPath = asm != null ? FormatKey(asm.GetName().Name, virtualPath) : virtualPath;

            if (_mappings.TryGetValue(virtualPath, out type))
            {
                if (DependencyResolver.Current != null)
                {
                    return DependencyResolver.Current.GetService(type);
                }
                return Activator.CreateInstance(type);
            }
            return null;
        }

        public bool Exists(string virtualPath)
        {
            return _mappings.ContainsKey(virtualPath);
        }

        private bool IsPhysicalFileNewer(string virtualPath, Assembly tenantAssembly)
        {
            if (virtualPath.StartsWith(_baseVirtualPath ?? String.Empty, StringComparison.Ordinal))
            {
                // If a base virtual path is specified, we should remove it as a prefix. Everything that follows should map to a view file on disk.
                if (!String.IsNullOrEmpty(_baseVirtualPath))
                {
                    virtualPath = '~' + virtualPath.Substring(_baseVirtualPath.Length);
                }

                string path = HttpContext.Current.Request.MapPath(virtualPath);
                return File.Exists(path) && File.GetLastWriteTimeUtc(path) > _assemblyLastWriteTimes[tenantAssembly].Value;
            }
            return false;
        }

        private static string NormalizeBaseVirtualPath(string virtualPath)
        {
            if (!String.IsNullOrEmpty(virtualPath))
            {
                // For a virtual path to combine properly, it needs to start with a ~/ and end with a /.
                if (!virtualPath.StartsWith("~/", StringComparison.Ordinal))
                {
                    virtualPath = "~/" + virtualPath;
                }
                if (!virtualPath.EndsWith("/", StringComparison.Ordinal))
                {
                    virtualPath += "/";
                }
            }
            return virtualPath;
        }

        private static string CombineVirtualPaths(string baseVirtualPath, string virtualPath)
        {
            if (!String.IsNullOrEmpty(baseVirtualPath))
            {
                return VirtualPathUtility.Combine(baseVirtualPath, virtualPath.Substring(2));
            }
            return virtualPath;
        }
    }
}
