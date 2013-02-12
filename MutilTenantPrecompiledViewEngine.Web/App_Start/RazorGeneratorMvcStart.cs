using System;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using System.Web.WebPages;
using MVC.Tenant.Infrastructure;
using MultiTenantPrecompiledViewEngine;
using MutilTenantPrecompiledViewEngine.Web.App_Start;
using WebActivator;

[assembly: PostApplicationStartMethod(typeof (RazorGeneratorMvcStart), "Start")]

namespace MutilTenantPrecompiledViewEngine.Web.App_Start
{
    public static class RazorGeneratorMvcStart
    {
        public static void Start()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                                      .AsQueryable()
                                      .Where(a => a.IsDefined(typeof (PluginAssemblyAttribute), false));

            var engine = new MultiTenantPrecompiledMvcEngine(assemblies,
                                                             (routes, tasms) =>

                                                                 {
                                                                     object tenantId;

                                                                     var canParse =routes.TryGetValue("tenant",out tenantId);

                                                                     Assembly asm =null;
                                                                     if (canParse)
                                                                     {
                                                                         asm = tasms.SingleOrDefault(a =>
                                                                         {
                                                                             var attr =a.GetCustomAttributes(typeof(PluginAssemblyAttribute),false).OfType<PluginAssemblyAttribute>().Single();
                                                                             
                                                                             return attr.Tenant ==tenantId as string;
                                                                         });
                                                                     }
                                                                     return asm;
                                                                 });


            //UsePhysicalViewsIfNewer = HttpContext.Current.Request.IsLocal
            //    };

            ViewEngines.Engines.Insert(0, engine);

            // StartPage lookups are done by WebPages. 
            VirtualPathFactoryManager.RegisterVirtualPathFactory(engine);
        }
    }
}