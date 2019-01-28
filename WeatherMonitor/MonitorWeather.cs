using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

/* This sample demonstrates the Monitor workflow. In this pattern, the orchestrator function is
 * used to periodically check something's status and take action as appropriate. While a
 * Timer-triggered function can perform similar polling action, the Monitor has additional
 * capabilities:
 *
 *   - manual termination (via request to the orchestrator termination endpoint)
 *   - termination when some condition is met
 *   - monitoring of multiple arbitrary subjects
 *
 */
namespace VSSample
{
    public static class Monitor
    {

        [FunctionName("MonitorWeather_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
                    [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
                    [OrchestrationClient]DurableOrchestrationClient starter,
                    ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("E3_Monitor", new MonitorRequest
            {
                Phone = "0643787755",
                Location = new Location {
                    City = "Amsterdam",
                    State = "NoordHolland"
                }
            });

            log.LogWarning($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("E3_Monitor")]
        public static async Task Run([OrchestrationTrigger] DurableOrchestrationContext monitorContext, ILogger log)
        {
            MonitorRequest input = monitorContext.GetInput<MonitorRequest>();
            if (!monitorContext.IsReplaying)
            {
                log.LogWarning(
                    $"Received monitor request. Location: {input?.Location}. Phone: {input?.Phone}."
                );
            }

            VerifyRequest(input);

            DateTime endTime = monitorContext.CurrentUtcDateTime.AddHours(1);
            if (!monitorContext.IsReplaying)
            {
                log.LogWarning($"Instantiating monitor for {input.Location}. Expires: {endTime}.");
            }

            while (monitorContext.CurrentUtcDateTime < endTime)
            {
                // Check the weather
                if (!monitorContext.IsReplaying)
                {
                    log.LogWarning(
                        $"Checking current weather conditions for {input.Location} at {monitorContext.CurrentUtcDateTime}."
                    );
                }

                bool isClear = await monitorContext.CallActivityAsync<bool>("E3_GetIsClear", input.Location);

                if (isClear)
                {
                    // It's not raining! Or snowing. Or misting. Tell our user to take advantage of it.
                    if (!monitorContext.IsReplaying)
                    {
                        log.LogWarning($"Detected clear weather for {input.Location}. Notifying {input.Phone}.");
                    }

                    await monitorContext.CallActivityAsync("E3_SendGoodWeatherAlert", input.Phone);
                    break;
                }
                else
                {
                    // Wait for the next checkpoint
                    var nextCheckpoint = monitorContext.CurrentUtcDateTime.AddSeconds(10);
                    if (!monitorContext.IsReplaying)
                    {
                        log.LogWarning($"Next check for {input.Location} at {nextCheckpoint}.");
                    }

                    await monitorContext.CreateTimer(nextCheckpoint, CancellationToken.None);
                }
            }

            log.LogWarning($"Monitor ending.");
        }

        [FunctionName("E3_GetIsClear")]
        public static async Task<bool> GetIsClear([ActivityTrigger] Location location)
        {
            var currentConditions = await WeatherUnderground.GetCurrentConditionsAsync(location);
            return currentConditions.Equals(WeatherCondition.Clear);
        }

        [FunctionName("E3_SendGoodWeatherAlert")]
        public static void SendGoodWeatherAlert(
            [ActivityTrigger] string whatever,
            ILogger log)
        {
            log.LogInformation("YAY! Good weather alert!");
        }

        private static void VerifyRequest(MonitorRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request), "An input object is required.");
            }

            if (request.Location == null)
            {
                throw new ArgumentNullException(nameof(request.Location), "A location input is required.");
            }

            if (string.IsNullOrEmpty(request.Phone))
            {
                throw new ArgumentNullException(nameof(request.Phone), "A phone number input is required.");
            }
        }
    }

    public class MonitorRequest
    {
        public Location Location { get; set; }

        public string Phone { get; set; }
    }

    public class Location
    {
        public string State { get; set; }

        public string City { get; set; }

        public override string ToString() => $"{City}, {State}";
    }

    public enum WeatherCondition
    {
        Other,
        Clear,
        Precipitation,
    }

    internal class WeatherUnderground
    {
        internal static async Task<WeatherCondition> GetCurrentConditionsAsync(Location location)
        {
            var random = new Random(Guid.NewGuid().GetHashCode());
            var weatherIndex = random.Next(0, 14);
            if (weatherIndex > 11)
                return WeatherCondition.Clear;
            else
                return WeatherCondition.Precipitation;
        }
    }
}