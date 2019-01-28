// using System;
// using System.IO;
// using System.Linq;
// using System.Net.Http;
// using System.Threading.Tasks;
// using Microsoft.Azure.WebJobs;
// using Microsoft.Azure.WebJobs.Extensions.Http;
// using Microsoft.Extensions.Logging;
// using Microsoft.WindowsAzure.Storage.Blob;

// namespace VSSample
// {
//     public static class BackupContentFaultTolerant
//     {
//         [FunctionName("DurableFunctionsOrchestrationCSharp_HttpStart")]
//         public static async Task<HttpResponseMessage> HttpStart(
//             [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
//             [OrchestrationClient]DurableOrchestrationClient starter,
//             ILogger log)
//         {
//             // Function input comes from the request content.
//             string instanceId = await starter.StartNewAsync("E2_BackupSiteContent", null);

//             log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

//             return starter.CreateCheckStatusResponse(req, instanceId);
//         }

//         [FunctionName("E2_BackupSiteContent")]
//         public static async Task<long> Run(
//             [OrchestrationTrigger] DurableOrchestrationContext backupContext)
//         {
//             string rootDirectory = backupContext.GetInput<string>()?.Trim();
//             if (string.IsNullOrEmpty(rootDirectory))
//             {
//                 rootDirectory = Directory.GetParent(typeof(BackupContentFaultTolerant).Assembly.Location).FullName;
//             }

//             string[] files = await backupContext.CallActivityAsync<string[]>(
//                 "E2_GetFileList",
//                 rootDirectory);

//             var retryOptions = new RetryOptions(
//                 firstRetryInterval: TimeSpan.FromSeconds(5),
//                 maxNumberOfAttempts: 3
//             );

//             var tasks = new Task<long>[files.Length];
//             for (int i = 0; i < files.Length; i++)
//             {
//                 tasks[i] = backupContext.CallActivityWithRetryAsync<long>(
//                     "E2_CopyFileToBlob",
//                     retryOptions,
//                     files[i]
//                 );
//             }

//             await Task.WhenAll(tasks);

//             long totalBytes = tasks.Sum(t => t.Result);
//             return totalBytes;
//         }

//         [FunctionName("E2_GetFileList")]
//         public static string[] GetFileList(
//             [ActivityTrigger] string rootDirectory,
//             ILogger log)
//         {
//             return new[] { "/Users/hakantuncer/Projects/Playground/AzureFunctionsDemo/FanOutFanIn/bin/output/bin/FanOutFanIn.dll" };
//         }

//         [FunctionName("E2_CopyFileToBlob")]
//         public static async Task<long> CopyFileToBlob(
//             [ActivityTrigger] string filePath,
//             Binder binder,
//             ILogger log)
//         {
//             long byteCount = new FileInfo(filePath).Length;

//             var rnd = new Random(Guid.NewGuid().GetHashCode());
//             if (rnd.Next() % 2 == 0)
//             {
//                 throw new Exception("I have failed randomly!");
//             }

//             // strip the drive letter prefix and convert to forward slashes
//             string blobPath = filePath
//                 .Substring(Path.GetPathRoot(filePath).Length)
//                 .Replace('\\', '/');
//             string outputLocation = $"backups/{blobPath}";

//             log.LogInformation($"Copying '{filePath}' to '{outputLocation}'. Total bytes = {byteCount}.");

//             // copy the file contents into a blob
//             using (Stream source = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
//             using (Stream destination = await binder.BindAsync<CloudBlobStream>(
//                 new BlobAttribute(outputLocation, FileAccess.Write)))
//             {
//                 await source.CopyToAsync(destination);
//             }

//             return byteCount;
//         }
//     }
// }