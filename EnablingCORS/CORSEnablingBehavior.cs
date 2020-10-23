using System;
using System.Linq;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Web;

namespace EnablingCORS
{
    public class CORSEnablingBehavior : BehaviorExtensionElement, IServiceBehavior
    {

        public override Type BehaviorType { get { return typeof(CORSEnablingBehavior); } }

        protected override object CreateBehavior() { return new CORSEnablingBehavior(); }

        void IServiceBehavior.AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters) { }
        
        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            var annoEndpoints = new List<ServiceEndpoint>();
            foreach (ServiceEndpoint endPoint in serviceHostBase.Description.Endpoints)
            {

                //Rebind right WebHttpBehavior (Bypass issue related to finding right operation for the OPTIONS verb)
                var httpBehavior = endPoint.Behaviors.Find<WebHttpBehavior>();
                if (httpBehavior != null)
                {
                    var index = endPoint.Behaviors.IndexOf(httpBehavior);

                    var behavior = new CORSWebHttpBehavior();
                    behavior.DefaultBodyStyle = httpBehavior.DefaultBodyStyle;
                    behavior.DefaultOutgoingRequestFormat = httpBehavior.DefaultOutgoingRequestFormat;
                    behavior.DefaultOutgoingResponseFormat = httpBehavior.DefaultOutgoingResponseFormat;
                    behavior.AutomaticFormatSelectionEnabled = httpBehavior.AutomaticFormatSelectionEnabled;
                    
                    
                    endPoint.Behaviors.Insert(index, behavior);
                    endPoint.Behaviors.Remove(httpBehavior);
                }
                else {
                    //endPoint.Behaviors.Add(new CORSWebHttpBehavior());
                }
                                                
                //Add CORS behavior for all requests (Update response headers)
                endPoint.Behaviors.Add(new CORSEnablingEndpointBehavior());

                
                //AnnoEndpoint
                var binding = new WebHttpBinding();
                binding.Security.Mode = WebHttpSecurityMode.None;
                //binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.None;
                binding.CrossDomainScriptAccessEnabled = true;
                var contractDescription = ContractDescription.GetContract(endPoint.Contract.ContractType, serviceDescription.ServiceType);
                contractDescription.Name = "Anno_" + contractDescription.Name;
                var annoEndpoint = new ServiceEndpoint(contractDescription, binding, endPoint.Address);
                annoEndpoint.Behaviors.Add(new CORSWebHttpBehavior());
                
                //Add preflight operations according to each existing operation.
                try
                {
                    var tmpls = new Dictionary<string, PreflightOperationBehavior>();
                    var count = annoEndpoint.Contract.Operations.Count;
                    for (int i = 0; i < count; i++)
                    {
                        AddPreflightOperation(annoEndpoint.Contract.Operations[i], tmpls);
                    }
                }
                catch { }

                foreach (var op in annoEndpoint.Contract.Operations.Where(o => o.Behaviors.Find<PreflightOperationBehavior>() != null).ToList())
                {
                    annoEndpoint.Contract.Operations.Remove(op);
                }
                
                annoEndpoints.Add(annoEndpoint);
                
            }
            //annoEndpoints.ForEach(serviceHostBase.Description.Endpoints.Add);
            annoEndpoints.ForEach(serviceHostBase.AddServiceEndpoint);

        }

        void IServiceBehavior.Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase){ }


        private void AddPreflightOperation(OperationDescription operation, Dictionary<string, PreflightOperationBehavior> uriTemplates)
        {
            if (operation.Behaviors.Find<WebGetAttribute>() != null || operation.IsOneWay )//|| operation.DeclaringContract.Operations.Any(o => o.Behaviors.Find<PreflightOperationBehavior>() != null))
            {
                // no need to add preflight operation for GET requests, no support for 1-way messages 
                return;
            }

            string originalUriTemplate;
            WebInvokeAttribute originalWia = operation.Behaviors.Find<WebInvokeAttribute>();

            if (originalWia != null && originalWia.UriTemplate != null)
            {
                originalUriTemplate = NormalizeTemplate(originalWia.UriTemplate);
            }
            else
            {
                originalUriTemplate = operation.Name;
            }

            string originalMethod = originalWia != null && originalWia.Method != null ? originalWia.Method : "POST";

            if (uriTemplates.ContainsKey(originalUriTemplate))
            {
                // there is already an OPTIONS operation for this URI, we can reuse it
                PreflightOperationBehavior operationBehavior = uriTemplates[originalUriTemplate];
                operationBehavior.AddAllowedMethod(originalMethod);
                
            }
            else
            {
                ContractDescription contract = operation.DeclaringContract;
                OperationDescription preflightOperation;
                PreflightOperationBehavior preflightOperationBehavior;
                CreatePreflightOperation(operation, originalUriTemplate, originalMethod, contract, out preflightOperation, out preflightOperationBehavior);
                uriTemplates.Add(originalUriTemplate, preflightOperationBehavior);
                
                //contract.Operations.Add(preflightOperation);
                contract.Operations.Insert(0,preflightOperation);
            }
            
        }

        private static void CreatePreflightOperation(OperationDescription operation, string originalUriTemplateStr, string originalMethod, ContractDescription contract, out OperationDescription preflightOperation, out PreflightOperationBehavior preflightOperationBehavior)
        {
            var originalUriTemplate = new UriTemplate(originalUriTemplateStr);
            preflightOperation = new OperationDescription(operation.Name + "_preflight", contract);

            //First the input message
            MessageDescription inputMessage = new MessageDescription(operation.Messages[0].Action + "_preflight", MessageDirection.Input);
            preflightOperation.Messages.Add(inputMessage);

            //We need to mirror the input parameters in the URI template
            //First any variables in the path
            if (originalUriTemplate.PathSegmentVariableNames != null && originalUriTemplate.PathSegmentVariableNames.Count > 0)
            {
                foreach (string uriParameter in originalUriTemplate.PathSegmentVariableNames)
                {
                    inputMessage.Body.Parts.Add(new MessagePartDescription(uriParameter, "") { Type = typeof(string) });
                }
            }
            //Next any in the querystring
            if (originalUriTemplate.QueryValueVariableNames != null && originalUriTemplate.QueryValueVariableNames.Count > 0)
            {
                foreach (string uriParameter in originalUriTemplate.QueryValueVariableNames)
                {
                    inputMessage.Body.Parts.Add(new MessagePartDescription(uriParameter, "") { Type = typeof(string) });
                }
            }

            //Now the output message, we only need the CORS headers in reality
            MessageDescription outputMessage = new MessageDescription(operation.Messages[1].Action + "_preflight", MessageDirection.Output);
            //outputMessage.Body.ReturnValue = new MessagePartDescription(preflightOperation.Name + "Return", contract.Namespace) { Type = typeof(Message) };
            preflightOperation.Messages.Add(outputMessage);

            WebInvokeAttribute wia = new WebInvokeAttribute();
            wia.UriTemplate = originalUriTemplate.ToString();
            wia.Method = "OPTIONS";

            //TODO:TEST
            //wia.BodyStyle = WebMessageBodyStyle.WrappedRequest;

            preflightOperation.Behaviors.Add(wia);
            preflightOperation.Behaviors.Add(new DataContractSerializerOperationBehavior(preflightOperation));
            preflightOperationBehavior = new PreflightOperationBehavior(preflightOperation);
            preflightOperationBehavior.AddAllowedMethod(originalMethod);
            preflightOperation.Behaviors.Add(preflightOperationBehavior);
        }


        private string NormalizeTemplate(string uriTemplate)
        {
            int queryIndex = uriTemplate.IndexOf('?');
            if (queryIndex >= 0)
            {
                // no query string used for this
                uriTemplate = uriTemplate.Substring(0, queryIndex);
            }

            int paramIndex;
            while ((paramIndex = uriTemplate.IndexOf('{')) >= 0)
            {
                // Replacing all named parameters with wildcards
                int endParamIndex = uriTemplate.IndexOf('}', paramIndex);
                if (endParamIndex >= 0)
                {
                    uriTemplate = uriTemplate.Substring(0, paramIndex) + '*' + uriTemplate.Substring(endParamIndex + 1);
                }
            }

            return uriTemplate;
        }

    }


    public class CORSEnablingEndpointBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(new CORSHeaderInjectingMessageInspector());
        }

        public void Validate(ServiceEndpoint endpoint)
        {
            
        }
    } 

}