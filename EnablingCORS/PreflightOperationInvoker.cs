using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Text;

namespace EnablingCORS
{
    internal class PreflightOperationInvoker : IOperationInvoker
    {
        private string replyAction;
        List<string> allowedHttpMethods;

        public PreflightOperationInvoker(string replyAction, List<string> allowedHttpMethods)
        {
            this.replyAction = replyAction;
            this.allowedHttpMethods = allowedHttpMethods;
        }

        public object[] AllocateInputs()
        {
            return new object[1];
        }

        public object Invoke(object instance, object[] inputs, out object[] outputs)
        {
            Message input = (Message)inputs[0];
            outputs = null;
            return HandlePreflight(input);
        }

        public IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state)
        {
            throw new NotSupportedException("Only synchronous invocation");
        }

        public object InvokeEnd(object instance, out object[] outputs, IAsyncResult result)
        {
            throw new NotSupportedException("Only synchronous invocation");
        }

        public bool IsSynchronous
        {
            get { return true; }
        }

        Message HandlePreflight(Message input)
        {
            HttpRequestMessageProperty httpRequest = (HttpRequestMessageProperty)input.Properties[HttpRequestMessageProperty.Name];
            string origin = httpRequest.Headers["Origin"];
            string requestMethod = httpRequest.Headers["Access-Control-Request-Method"];
            string requestHeaders = httpRequest.Headers["Access-Control-Request-Headers"];

            Message reply = Message.CreateMessage(MessageVersion.None, replyAction);
            HttpResponseMessageProperty httpResponse = new HttpResponseMessageProperty();
            reply.Properties.Add(HttpResponseMessageProperty.Name, httpResponse);

            httpResponse.SuppressEntityBody = true;
            httpResponse.StatusCode = HttpStatusCode.OK;
            if (origin != null)
            {
                httpResponse.Headers.Add("Access-Control-Allow-Origin", origin);
            }

            if (requestMethod != null && this.allowedHttpMethods.Contains(requestMethod))
            {
                httpResponse.Headers.Add("Access-Control-Allow-Methods", string.Join(",", this.allowedHttpMethods));
            }

            if (requestHeaders != null)
            {
                httpResponse.Headers.Add("Access-Control-Allow-Headers", requestHeaders);
                httpResponse.Headers.Add("Access-Control-Allow-Credentials", "true");
            }

            return reply;
        }
    }
}
