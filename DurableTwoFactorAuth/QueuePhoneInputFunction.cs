using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DurableTwoFactorAuth
{
    public static class QueuePhoneInputFunction
    {
        [FunctionName("QueuePhoneInputFunction")]
        public static async Task Run(
            [QueueTrigger("phoneverification-input-queue")]string verificationInput,
            [OrchestrationClient]DurableOrchestrationClient client,
            ILogger log
            )
        {
            var input = JsonConvert.DeserializeObject<VerificationInput>(verificationInput);
            log.LogInformation($"Processing Queue trigger for verification code: {input.VerificationCode}");

            await client.RaiseEventAsync(input.InstanceId, "SmsChallengeResponse", input.VerificationCode);
        }
    }

    public class VerificationInput
    {
        public string InstanceId { get; set; }
        public string VerificationCode { get; set; }
    }
}
