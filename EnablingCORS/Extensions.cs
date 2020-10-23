using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel.Description;

namespace EnablingCORS
{
    public static class Extensions
    {
        public static T Clone<T>(this T obj){
            var t = typeof(T);
            var method=t.GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method != null)
            {
                var res = method.Invoke(obj, null);
                return (T)res;
            }
            return default(T);
        }
    }
}
