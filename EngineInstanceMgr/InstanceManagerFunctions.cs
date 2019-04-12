using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EngineInstanceMgr
{
    public static class InstanceManagerFunctions
    {
        [FunctionName("EngineInstance")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context
            ,ILogger log)
        {
            UserSession session = context.GetInput<UserSession>();

            // Create compute
            var compute = await context.CallActivityAsync<Compute>("CreateCompute", session.CaseNumber);

            var startState = new CustomState();
            startState.CaseNumber = session.CaseNumber;
            startState.userId = session.userId;
            startState.Compute = compute;
            context.SetCustomStatus(startState);

            var caseToEnd = await context.WaitForExternalEvent<Compute>("EndSession");
            // Delete compute
            await context.CallActivityAsync<Compute>("DeleteCompute", caseToEnd);
            var endState = new CustomState();
            endState.CaseNumber = string.Empty;
            endState.userId = string.Empty;
            endState.Compute = null;
            context.SetCustomStatus(endState);

            return context.InstanceId;

        }

        [FunctionName("CreateCompute")]
        public static Compute CreateCompute([ActivityTrigger] string caseNumber, ILogger log)
        {
            log.LogInformation($"Creating compute for {caseNumber}.");
            var compute = new Compute();
            compute.Key = Guid.NewGuid().ToString();

            return compute;
        }

        [FunctionName("DeleteCompute")]
        public static void DeleteCompute([ActivityTrigger] Compute compute, ILogger log)
        {
            log.LogInformation($"Deleting compute {compute.Key}");

            
        }



        [FunctionName("GetEngineInstance")]
        public static async Task<HttpResponseMessage> GetEngineInstance(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",Route = "GetEngineInstance/{caseNumber}")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            string caseNumber,
            ILogger log)
        {

            var userSession = new UserSession();
            //to be set from auth token
            userSession.userId = "";
            userSession.CaseNumber = caseNumber;

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("EngineInstance", userSession);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }


        [FunctionName("DeleteEngineInstance")]
        public static async void DeleteEngineInstance(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "DeleteEngineInstance/{caseNumber}")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            string caseNumber,
            ILogger log)
        {

            log.LogInformation($"Delete instance for case #{caseNumber}");

            var runnigInstances = await GetInstances(starter, OrchestrationRuntimeStatus.Running, log);
            var caseInstance = runnigInstances.Last<Instance>(
                instance => String.Compare(instance.State.CaseNumber, caseNumber, true) == 0);

            var compute = new Compute();
            compute.Key = caseInstance.State.Compute.Key;

            await starter.RaiseEventAsync(caseInstance.InstanceId, "EndSession", compute);
            

            //log.LogInformation($"Completed orchestration with ID = '{instanceId}'.");
        }

        private static async Task<IEnumerable<Instance>> GetInstances(
            DurableOrchestrationClient client, OrchestrationRuntimeStatus runtimeStatus
            ,ILogger log)
        {
            var currentDate = DateTime.UtcNow;
            IEnumerable<OrchestrationRuntimeStatus> runningInstanceStatus = new List<OrchestrationRuntimeStatus> {
                                                                        OrchestrationRuntimeStatus.Running
                                                                    };
            IEnumerable<DurableOrchestrationStatus> instances =
                await client.GetStatusAsync(currentDate.AddHours(-4),currentDate, runningInstanceStatus);

            List<Instance> engineInstances = new List<Instance>();
            foreach (var instance in instances)
            {
                engineInstances.Add(new Instance() { InstanceId = instance.InstanceId
                                                     ,State = 
                                                        JsonConvert.DeserializeObject<CustomState>(instance.CustomStatus.ToString())
                                                    });
            };

            return engineInstances;
        }
    }
}