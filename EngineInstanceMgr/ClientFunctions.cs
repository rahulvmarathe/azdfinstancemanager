using Microsoft.Azure.WebJobs;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Threading.Tasks;

namespace EngineInstanceMgr
{
    public static partial class InstanceManagerFunctions
    {

        [FunctionName("GetEngineInstance")]
        public static async Task<Instance> GetEngineInstance(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetEngineInstance/{caseNumber}")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            string caseNumber,
            ILogger log)
        {

            var userSession = new UserSession();
            //to be set from auth token
            userSession.userId = "";
            userSession.CaseNumber = caseNumber;

            var instance = await GetInstanceForCase(starter, caseNumber, log);
            if (instance == null)
            {
                // Function input comes from the request content.
                string instanceId = await starter.StartNewAsync("EngineInstance", userSession);

                log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

                await starter.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId);

                ///TODO:Error handling in case of failure
                instance = await GetInstanceForCase(starter, caseNumber, log);
            }

            //return starter.CreateCheckStatusResponse(req, $"{instance.State.Compute.IPAddress}:{instance.State.Compute.Port}");
            return instance;
        }



        [FunctionName("DeleteEngineInstance")]
        public static async void DeleteEngineInstance(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "DeleteEngineInstance/{caseNumber}")]HttpRequestMessage req,
    [OrchestrationClient]DurableOrchestrationClient starter,
    string caseNumber,
    ILogger log)
        {

            log.LogInformation($"Delete instance for case #{caseNumber}");

            Instance caseInstance = await GetInstanceForCase(starter, caseNumber, log);

            if (caseInstance != null)
            {
                var compute = new Compute();
                compute.Key = caseInstance.State.Compute.Key;

                var eventsOrchestratorEventId = GetEventListenerInstanceId(caseInstance.InstanceId);
                await starter.RaiseEventAsync(eventsOrchestratorEventId, "EndSessionEvent", compute);

                ///TODO: Wait untill the status of the orchestartor for a case is complete.
                ///Temp implementation, does not cover all scenarios
                bool isComplete = false;
                while (!isComplete)
                {
                    caseInstance = await GetInstanceForCase(starter, caseNumber, log);
                    if (caseInstance == null)
                    {
                        isComplete = true;
                    }
                }

                log.LogInformation($"Deleted orchestration for CaseNumber = '{caseNumber}'.");

            }
            else
            {
                //return HTTP 500*
                log.LogInformation($"No orchestrator for delete = '{caseNumber}'.");
            }

        }


    }
}
