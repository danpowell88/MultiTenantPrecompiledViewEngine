using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Compilation;
using System.Web.Mvc;
using System.Web.WebPages;

namespace PrecompiledMvcViewEngineContrib
{
	public class PrecompiledMvcEngine : VirtualPathProviderViewEngine, IVirtualPathFactory
	{
		private readonly IDictionary<string, Type> _mappings;
		public PrecompiledMvcEngine(IEnumerable<Assembly> assembly)
		{
			base.AreaViewLocationFormats = new[] {
                "~/Areas/{2}/Views/{1}/{0}.cshtml", 
                "~/Areas/{2}/Views/{1}/{0}.vbhtml", 
                "~/Areas/{2}/Views/Shared/{0}.cshtml", 
                "~/Areas/{2}/Views/Shared/{0}.vbhtml"
            };

			base.AreaMasterLocationFormats = new[] {
                "~/Areas/{2}/Views/{1}/{0}.cshtml", 
                "~/Areas/{2}/Views/{1}/{0}.vbhtml", 
                "~/Areas/{2}/Views/Shared/{0}.cshtml", 
                "~/Areas/{2}/Views/Shared/{0}.vbhtml"
            };

			base.AreaPartialViewLocationFormats = new[] {
                "~/Areas/{2}/Views/{1}/{0}.cshtml", 
                "~/Areas/{2}/Views/{1}/{0}.vbhtml", 
                "~/Areas/{2}/Views/Shared/{0}.cshtml", 
                "~/Areas/{2}/Views/Shared/{0}.vbhtml"
            };
			base.ViewLocationFormats = new[] {
                "~/Views/{1}/{0}.cshtml", 
                "~/Views/{1}/{0}.vbhtml", 
                "~/Views/Shared/{0}.cshtml", 
                "~/Views/Shared/{0}.vbhtml"
            };
			base.MasterLocationFormats = new[] {
                "~/Views/{1}/{0}.cshtml", 
                "~/Views/{1}/{0}.vbhtml", 
                "~/Views/Shared/{0}.cshtml", 
                "~/Views/Shared/{0}.vbhtml"
            };
			base.PartialViewLocationFormats = new[] {
                "~/Views/{1}/{0}.cshtml", 
                "~/Views/{1}/{0}.vbhtml", 
                "~/Views/Shared/{0}.cshtml", 
                "~/Views/Shared/{0}.vbhtml"
            };
			base.FileExtensions = new[] {
                "cshtml", 
                "vbhtml"
            };

			Dictionary<string, Type> dictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
			foreach(var asm in assembly)
			{
				var name = asm.GetName().Name;
				foreach (Type type in asm.GetTypes())
				{
					if (typeof(WebPageRenderingBase).IsAssignableFrom(type))
					{
						PageVirtualPathAttribute pageVirtualPath = type.GetCustomAttributes(inherit: false).OfType<PageVirtualPathAttribute>().FirstOrDefault();
						if (pageVirtualPath != null)
						{
							KeyValuePair<string, Type> pair = new KeyValuePair<string, Type>(pageVirtualPath.VirtualPath, type);
							dictionary.Add(FormatKey(name, pair.Key), pair.Value);
						}
					}
				}
			}
			_mappings = dictionary;
		}

	
		/// <summary>
		/// Determines if IVirtualPathFactory lookups returns files from assembly regardless of whether physical files are available for the virtual path.
		/// </summary>
		public bool PreemptPhysicalFiles
		{
			get;
			set;
		}

        
		protected override bool FileExists(ControllerContext controllerContext, string virtualPath)
		{
		    return Exists(FormatKey(GetAssemblyName(controllerContext),virtualPath));
		}


        string FormatKey(string asmName, string filePath)
        {
            return asmName + "!" + filePath;
        }

        string GetAssemblyName(ControllerContext ctx)
        {
            var cache = ctx.HttpContext.Items["cview_assembly"] as string;
            if (cache!=null) return cache;
            var asm = Assembly.GetAssembly(ctx.Controller.GetType()).GetName().Name;
            ctx.HttpContext.Items["cview_assembly"] = asm;
            return asm;
        }

		protected override IView CreatePartialView(ControllerContext controllerContext, string partialPath)
		{
			Type type;

            if (_mappings.TryGetValue(FormatKey(GetAssemblyName(controllerContext), partialPath), out type))
			{
				return new PrecompiledMvcView(partialPath, type, false, base.FileExtensions);
			}
			return null;
		}

		protected override IView CreateView(ControllerContext controllerContext, string viewPath, string masterPath)
		{
			Type type;
            if (_mappings.TryGetValue(FormatKey(GetAssemblyName(controllerContext), viewPath), out type))
			{
				return new PrecompiledMvcView(viewPath, type, true, base.FileExtensions);
			}
			return null;
		}

		public object CreateInstance(string virtualPath)
		{
			Type type;

			if (!PreemptPhysicalFiles && VirtualPathProvider.FileExists(virtualPath))
			{
				// If we aren't pre-empting physical files, use the BuildManager to create _ViewStart instances if the file exists on disk. 
				return BuildManager.CreateInstanceFromVirtualPath(virtualPath, typeof(WebPageRenderingBase));
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
	}
}