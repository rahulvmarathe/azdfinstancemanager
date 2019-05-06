using k8s;
using k8s.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace EngineInstanceMgr
{
    public static partial class InstanceManagerFunctions
    {
        [FunctionName("CreateCompute")]
        public static Compute CreateCompute([ActivityTrigger] string caseNumber, ExecutionContext context, ILogger log)
        {
            log.LogInformation($"Creating compute for {caseNumber}.");

            var homeDirectory = context.FunctionAppDirectory;

            var compute = new Compute();

            var config
                    = KubernetesClientConfiguration.BuildConfigFromConfigFile(Path.Combine(homeDirectory, "kubeconfig.json"));
            config.ClientCertificateKeyStoreFlags = X509KeyStorageFlags.MachineKeySet
                                                        | X509KeyStorageFlags.PersistKeySet
                                                        | X509KeyStorageFlags.Exportable;
            var client = new Kubernetes(config);

            try
            {
                var name = $"dsirona-engine-ag-{caseNumber}";

                var v1service = Yaml.LoadFromFileAsync<V1Service>(Path.Combine(homeDirectory, "engineService.yaml")).Result;
                v1service.Metadata.Name = name;
                v1service.Spec.Selector.Clear();
                v1service.Spec.Selector.Add("app", name);
                var service = client.CreateNamespacedService(v1service, "default");

                var v1Deployment = Yaml.LoadFromFileAsync<V1Deployment>(Path.Combine(homeDirectory, "engineDeployment.yaml")).Result;
                var v1PodTemplate = v1Deployment.Spec.Template;
                v1PodTemplate.Spec.Containers[0].Name = name;
                v1Deployment.Metadata.Name = name;
                v1Deployment.Spec.Selector.MatchLabels.Clear();
                v1Deployment.Spec.Selector.MatchLabels.Add("app", name);
                v1Deployment.Spec.Template.Metadata.Labels.Clear();
                v1Deployment.Spec.Template.Metadata.Labels.Add("app", name);


                var deployment = client.CreateNamespacedDeployment(v1Deployment, "default");

                HttpOperationResponse<V1PodList> pods;
                V1Pod pod = null;
                //TODO: Keep trying till the status of the pod is Running, implement timeout
                for (int retry = 0; retry < 60; retry++)
                {
                    pods = client.ListNamespacedPodWithHttpMessagesAsync("default", labelSelector: $"app={name}").Result;
                    pod = pods.Body.Items[0];
                    if (pod.Status.Phase == "Running")
                    {
                        break;
                    }

                    Task.Delay(1000);
                }

                //Console.WriteLine($"Host IP : { pod.Status.HostIP}, ports: {service.Spec.Ports[0].NodePort},{service.Spec.Ports[1].NodePort},{service.Spec.Ports[2].NodePort}");

                compute.Key = name;
                compute.IPAddress = pod.Status?.HostIP;
                compute.Port = service.Spec.Ports[0].NodePort.ToString();

            }
            catch (Exception serviceException)
            {

                ///TODO: Implement rollback
                compute.Status = "Error";
                compute.LastErrorMessage = serviceException.Message;
                Console.Write(serviceException.Message);
            }

            compute.Status = "Healthy";

            return compute;
        }

    }
}
