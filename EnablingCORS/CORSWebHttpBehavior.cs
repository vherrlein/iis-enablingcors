using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;

namespace EnablingCORS
{
    public class CORSWebHttpDispatchOperationSelector : WebHttpDispatchOperationSelector
    {

        private WebHttpDispatchOperationSelector target;
        private ServiceEndpoint endpoint;

        String optionOperationName;

        public CORSWebHttpDispatchOperationSelector(ServiceEndpoint endpoint, WebHttpDispatchOperationSelector target)
        {
            this.target = target;
            this.endpoint = endpoint;

            foreach (var item in this.endpoint.Contract.Operations)
            {
                var webInvoke = item.Behaviors.OfType<WebInvokeAttribute>().FirstOrDefault();
                if (webInvoke != null && webInvoke.Method.Equals("options", StringComparison.OrdinalIgnoreCase) && webInvoke.UriTemplate == "*")
                {
                    optionOperationName = item.Name;
                    break;
                }
            }
        }
        #region IDispatchOperationSelector Members

        protected override string SelectOperation(ref Message message, out bool uriMatched)
        {
            var result = target.SelectOperation(ref message);

            var matched = message.Properties["UriMatched"] as bool?;
            message.Properties.Remove("UriMatched");
            message.Properties.Remove("HttpOperationName");
            uriMatched = matched.HasValue && matched.Value;

            var httpRequest = message.Properties["httpRequest"] as HttpRequestMessageProperty;

            var cond = string.IsNullOrEmpty(result) &&
                            httpRequest != null &&
                            httpRequest.Method.Equals("options", StringComparison.OrdinalIgnoreCase);

            if (cond && !string.IsNullOrEmpty(optionOperationName))
            {
                result = optionOperationName;
                uriMatched = true;
            }

            return result;
        }
        #endregion
    }

    public class CORSWebHttpBehavior : WebHttpBehavior
    {
        protected override WebHttpDispatchOperationSelector GetOperationSelector(ServiceEndpoint endpoint)
        {
            return new CORSWebHttpDispatchOperationSelector(endpoint, base.GetOperationSelector(endpoint));
        }
    }
}
