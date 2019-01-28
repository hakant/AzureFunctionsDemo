using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace AzureFunctionsDemo
{
    public static class DemoQueueTrigger
    {
        [FunctionName("DemoQueueTrigger")]
        public static void Run([QueueTrigger("myqueue-items")]string myQueueItem, ILogger log)
        {
            log.LogWarning($"C# Queue trigger function processed: {myQueueItem}");
        }
    }
}
