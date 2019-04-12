using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace SubOrchestration
{
    public static class Functions
    {
        [FunctionName("Main")]
        public static async Task<List<string>> RunOrchestratorMain(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("Functions_Hello", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("Functions_Hello", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("Functions_Hello", "London"));

            Task provisionTask = context.CallSubOrchestratorAsync("Sub", "SameContext");

            


            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("Sub")]
        public static async Task<List<string>> RunOrchestratorSub(
                [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("Functions_Hello", "sub Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("Functions_Hello", "sub Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("Functions_Hello", "sub London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }


        [FunctionName("Functions_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("Functions_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("Main", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}