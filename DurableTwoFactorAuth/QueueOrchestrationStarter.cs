using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DurableTwoFactorAuth
{
    public static class QueueOrchestrationStarter
    {
        [FunctionName("QueueOrchestrationStarter")]
        public static async Task Run(
            [QueueTrigger("phoneverification-starter-queue")]string phoneNumber, 
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log
            )
        {
            log.LogInformation($"Processing Queue trigger for phone number: {phoneNumber}");

            // Function input comes from the request content.
            string instanceId = await client.StartNewAsync("SmsPhoneVerification", phoneNumber);

            log.LogWarning($"Started orchestration with ID = '{instanceId}'.");
        }
    }
}
