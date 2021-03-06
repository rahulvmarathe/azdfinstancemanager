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
    public static partial class InstanceManagerFunctions
    {
        [FunctionName("EngineInstance")]
        public static async Task<string> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context
            ,ExecutionContext contex
            , ILogger log)
        {

            var session = context.GetInput<UserSession>();

            var compute = await context.CallActivityAsync<Compute>("CreateCompute", session.CaseNumber);

            var startState = new CustomState();
            startState.CaseNumber = session.CaseNumber;
            startState.userId = session.userId;
            startState.Compute = compute;
            startState.InstanceId = context.InstanceId;
            context.SetCustomStatus(startState);

            log.LogInformation("Saved Compute State");
            var eventsOrchestratorEventId = GetEventListenerInstanceId(context.InstanceId);
            await context.CallSubOrchestratorAsync<string>("EngineInstanceEvents", eventsOrchestratorEventId, null);

            log.LogInformation("Completed eventListnerOrchestrator");

            return context.InstanceId;

        }

        private static string GetEventListenerInstanceId(string instanceId)
        {
            return $"{instanceId}-eventListnerOrchestrator";
        }

        [FunctionName("EngineInstanceEvents")]
        public static async Task<string> RunEngineEventsOrchestrator(
    [OrchestrationTrigger] DurableOrchestrationContext context
    , ILogger log)
        {

            var endSessionEvent = context.WaitForExternalEvent<Compute>("EndSessionEvent");
            var addCollaborateEvent = context.WaitForExternalEvent<Collaborator>("AddCollaboratorEvent");
            ///todo:implement timeout logic, options: DurableTimer/Event based
            ///

            var triggeredEvent = await Task.WhenAny(endSessionEvent, addCollaborateEvent);

            if (triggeredEvent == endSessionEvent)
            {
                var computeToDelete = endSessionEvent.Result;

                // Delete compute
                await context.CallActivityAsync<Compute>("DeleteCompute", computeToDelete);

                log.LogInformation($"Delete instance {context.InstanceId} ");
                //end session
            }

            if (triggeredEvent == addCollaborateEvent)
            {
                //call addCollaborator activity


                //continue as new
                context.ContinueAsNew(null);
            }

            return context.InstanceId;
        }







        private static async Task<Instance> GetInstanceForCase(DurableOrchestrationClient starter, string caseNumber, ILogger log)
        {
            var runnigInstances = await GetInstances(starter, OrchestrationRuntimeStatus.Running, log);
            var caseInstance = runnigInstances.LastOrDefault<Instance>(
                instance => String.Compare(instance?.State?.CaseNumber, caseNumber, true) == 0);
            return caseInstance;
        }

        [FunctionName("AddCollaborator")]
        public static async void AddCollaborator(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "AddCollaborator/{caseNumber}")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            string caseNumber,
            //additonal parameters need to add collaborator
            ILogger log)
        {

            log.LogInformation($"Add collaborator for case #{caseNumber}");

            Instance caseInstance = await GetInstanceForCase(starter, caseNumber, log);

            var collaborator = new Collaborator();
            collaborator.CollaboratorUserId = string.Empty;

            var eventsOrchestratorEventId = GetEventListenerInstanceId(caseInstance.InstanceId);
            await starter.RaiseEventAsync(eventsOrchestratorEventId, "AddCollaboratorEvent", collaborator);

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