using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace EnablingCORS
{
    public class CORSServiceAuthorizationManager : ServiceAuthorizationManager
    {
        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            bool ret = base.CheckAccessCore(operationContext);
            var requestProperty = operationContext.RequestContext.RequestMessage.Properties[HttpRequestMessageProperty.Name] as HttpRequestMessageProperty;

            if (!ret && requestProperty != null && requestProperty.Method.Equals("options", StringComparison.InvariantCultureIgnoreCase))
            {
                ret = true;
            }

            return ret;
        }
    }
}
