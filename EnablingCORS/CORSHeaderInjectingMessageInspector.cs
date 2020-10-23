using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;

namespace EnablingCORS
{
    internal class CORSHeaderInjectingMessageInspector : IDispatchMessageInspector
    {


        public object AfterReceiveRequest(
          ref Message request,
          IClientChannel channel,
          InstanceContext instanceContext)
        {
            HttpRequestMessageProperty requestProperty = null;
            if (request.Properties.ContainsKey(HttpRequestMessageProperty.Name))
            {
                requestProperty = request.Properties[HttpRequestMessageProperty.Name] as HttpRequestMessageProperty;
            }

            if (requestProperty != null)
            {
                var origin = requestProperty.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin))
                {
                    // if a cors options request (preflight) is detected, 
                    // we create our own reply message and don't invoke any 
                    // operation at all.
                    if (requestProperty.Method.ToUpperInvariant() == "OPTIONS")
                    {
                        instanceContext.Abort();
                    }
                }
            }

            return requestProperty;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {

            HttpResponseMessageProperty responseProperty = null;
            HttpRequestMessageProperty requestProperty = correlationState as HttpRequestMessageProperty;

            var origin = requestProperty == null ? "*" : (requestProperty.Headers["Origin"] ?? "*");



            if (reply.Properties.ContainsKey(HttpResponseMessageProperty.Name))
            {
                responseProperty = reply.Properties[HttpResponseMessageProperty.Name]
                                   as HttpResponseMessageProperty;
            }

            if (responseProperty == null)
            {
                responseProperty = new HttpResponseMessageProperty();
                reply.Properties.Add(HttpResponseMessageProperty.Name,
                                     responseProperty);
            }

            // Access-Control-Allow-Origin should be added for all cors responses
            responseProperty.Headers.Add("Access-Control-Allow-Origin", origin);
            responseProperty.Headers.Add("Access-Control-Allow-Credentials", "true");
            responseProperty.Headers.Add("Access-Control-Allow-Headers", "Origin, Content-Type, Accept, Authorization, x-requested-with");
            responseProperty.Headers.Add("Access-Control-Allow-Method", "POST,GET,PUT,DELETE,OPTIONS");

            if (requestProperty !=null && requestProperty.Method.ToUpperInvariant() == "OPTIONS")
            {
                responseProperty.StatusCode = HttpStatusCode.NoContent;                
            }
            else if (requestProperty != null && requestProperty.Headers[HttpRequestHeader.Authorization] == null)
            {
                responseProperty.StatusDescription = "Unauthorized";
                responseProperty.StatusCode = HttpStatusCode.Unauthorized;
            }

        }
    }
}
