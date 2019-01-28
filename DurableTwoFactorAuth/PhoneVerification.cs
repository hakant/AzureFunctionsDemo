using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace VSSample
{
    public static class PhoneVerification
    {
        [FunctionName("SmsPhoneVerification")]
        public static async Task<bool> Run(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
        {
            string phoneNumber = context.GetInput<string>();
            if (string.IsNullOrEmpty(phoneNumber))
            {
                throw new ArgumentNullException(
                    nameof(phoneNumber),
                    "A phone number input is required.");
            }

            int challengeCode = await context.CallActivityAsync<int>(
                "SendSmsChallenge",
                phoneNumber);

            using (var timeoutCts = new CancellationTokenSource())
            {
                // The user has 90 seconds to respond with the code they received in the SMS message.
                DateTime expiration = context.CurrentUtcDateTime.AddSeconds(90);
                Task timeoutTask = context.CreateTimer(expiration, timeoutCts.Token);

                bool authorized = false;
                for (int retryCount = 0; retryCount <= 3; retryCount++)
                {
                    Task<int> challengeResponseTask =
                        context.WaitForExternalEvent<int>("SmsChallengeResponse");

                    Task winner = await Task.WhenAny(challengeResponseTask, timeoutTask);
                    if (winner == challengeResponseTask)
                    {
                        // We got back a response! Compare it to the challenge code.
                        if (challengeResponseTask.Result == challengeCode)
                        {
                            log.LogWarning($"Correct verification code is entered: {challengeResponseTask.Result}");
                            authorized = true;
                            break;
                        }

                        log.LogWarning($"Wrong verification code is entered: {challengeResponseTask.Result}");
                    }
                    else
                    {
                        // Timeout expired
                        log.LogWarning("Timeout expired!");
                        break;
                    }
                }

                if (!timeoutTask.IsCompleted)
                {
                    // All pending timers must be complete or canceled before the function exits.
                    timeoutCts.Cancel();
                }

                return authorized;
            }
        }

        [FunctionName("SendSmsChallenge")]
        public static int SendSmsChallenge(
            [ActivityTrigger] string phoneNumber,
            ILogger log)
        {
            // Get a random number generator with a random seed (not time-based)
            var rand = new Random(Guid.NewGuid().GetHashCode());
            int challengeCode = rand.Next(10000);

            log.LogWarning($"Sending verification code {challengeCode} to {phoneNumber}.");

            return challengeCode;
        }
    }
}