using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVC.Tenant.Infrastructure
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = true)]
    public class PluginAssemblyAttribute : Attribute
    {
        public PluginAssemblyAttribute(string tenant)
        {
            Tenant = tenant;
        }

        public String Tenant { get; private set; }
    }
}
