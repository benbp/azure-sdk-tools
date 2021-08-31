using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using k8s;

namespace chaos_watcher
{
    public class GenericChaosClient
    {
        // Plural names for listing resource instances via Kubernetes API
        public enum ChaosResourcePlurals
        {
            networkchaos,
            stresschaos,
            httpchaos,
            iochaos,
            kernelchaos,
            timechaos,
            jvmchaos
        };

        public string Group = "chaos-mesh.org";
        public string Version = "v1alpha1";

        private List<GenericClient> Clients = new List<GenericClient>();

        public GenericChaosClient(KubernetesClientConfiguration config)
        {
            foreach (var plural in Enum.GetValues(typeof(ChaosResourcePlurals)))
            {
                Clients.Add(new GenericClient(config, Group, Version, plural.ToString()));
            }
        }

        public async Task<CustomResourceList<GenericChaosResource>> ListNamespacedAsync(string _namespace)
        {
            var tasks = new List<Task<CustomResourceList<GenericChaosResource>>>();
            foreach (var client in Clients)
            {
                tasks.Add(Task.Run(async () =>
                {
                    return await client.ListNamespacedAsync<CustomResourceList<GenericChaosResource>>(_namespace);
                }));
            }

            await Task.WhenAll(tasks);

            var results = new CustomResourceList<GenericChaosResource>();
            results.Items = new List<GenericChaosResource>();

            foreach (var task in tasks)
            {
                results.Items.AddRange(task.Result.Items);
            }

            return results;
        }
    }
}
