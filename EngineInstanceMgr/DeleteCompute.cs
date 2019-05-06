using k8s;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EngineInstanceMgr
{
    public static partial class InstanceManagerFunctions
    {

        [FunctionName("DeleteCompute")]
        public static void DeleteCompute([ActivityTrigger] Compute compute, ExecutionContext context, ILogger log)
        {
            log.LogInformation($"Deleting compute {compute.Key}");

            var name = compute.Key;

            var homeDirectory = context.FunctionAppDirectory;

            var config
                    = KubernetesClientConfiguration.BuildConfigFromConfigFile(Path.Combine(homeDirectory, "kubeconfig.json"));
            var client = new Kubernetes(config);

            ///TODO: Exception condition handling
            var deleteStatus = client.DeleteNamespacedDeployment(name, "default");
            deleteStatus = client.DeleteNamespacedService(name, "default");

        }
    }
}
