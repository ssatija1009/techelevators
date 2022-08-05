// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using System.Net.Http;
using Azure.Core.Pipeline;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Azure;
using cgmtwiningestfunction.DataObjects;
using System.Text;

namespace cgmtwiningestfunction
{
    public static class IOTHubTwinsFunction
    {
        public static readonly string cgmInfoDeviceId = "CGMInfoOne";
        public static readonly string healthAppDeviceId = "HealthAppInfoOne";
        public static readonly string surveyInfoDeviceId = "SurveyInfoOne";

        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient singletonHttpClientInstance = new HttpClient();

        [FunctionName("IOTHubtoTwins")]
        public async static void Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            if (adtInstanceUrl == null) log.LogError("Application setting \"ADT_SERVICE_URL\" not set");
            try
            {
                var cred = new ManagedIdentityCredential("https://digitaltwins.azure.net");

                var client = new DigitalTwinsClient(
                new Uri(adtInstanceUrl),
                cred,
                new DigitalTwinsClientOptions
                {
                    Transport = new HttpClientTransport(singletonHttpClientInstance)
                });

                log.LogInformation($"ADT service client connection created.");

                if (eventGridEvent != null && eventGridEvent.Data != null)
                {
                    log.LogInformation(eventGridEvent.Data.ToString());

                    // convert the message into a json object
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());

                    // Get CGM data from the object
                    string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];

                    var updateTwinData = new JsonPatchDocument();

                    if (deviceId == cgmInfoDeviceId)
                    {
                        var glucoseLevel = deviceMessage["body"]["glucoseLevel"];
                        var timestamp = deviceMessage["body"]["timestamp"];
                        //log the glocoselevel and timestamp
                        log.LogInformation($"Device:{deviceId}; Glucose Level is:{glucoseLevel} and Timestamp is: {timestamp}");

                        updateTwinData.AppendReplace("/glucoseLevel", glucoseLevel.Value<double>());
                        updateTwinData.AppendReplace("/timestamp", timestamp.Value<DateTime>());
                    }
                    else if (deviceId == healthAppDeviceId)
                    {
                        // Convert base64 string
                        string deviceBodyMessage = Convert.ToString(deviceMessage["body"]);

                        log.LogInformation($"Device:{deviceId}; Device Body value:{deviceBodyMessage}");

                        byte[] data = Convert.FromBase64String(deviceBodyMessage);
                        string jsonBack = Encoding.UTF8.GetString(data);
                        var healthAppInfo = JsonConvert.DeserializeObject<HealthAppInfo>(jsonBack);

                        log.LogInformation($"Device:{deviceId}; HealthAppInfo:{JsonConvert.SerializeObject(healthAppInfo)}");

                        updateTwinData.AppendReplace("/systolicBP", healthAppInfo.SystolicBloodPressure);
                        updateTwinData.AppendReplace("/diastolicBP", healthAppInfo.DiastolicBloodPressure);
                        updateTwinData.AppendReplace("/bloodOxygen", healthAppInfo.BloodOxygen);
                        updateTwinData.AppendReplace("/bmi", healthAppInfo.BloodOxygen);
                        updateTwinData.AppendReplace("/steps", healthAppInfo.Steps);
                        updateTwinData.AppendReplace("/weight", healthAppInfo.Weight);
                        updateTwinData.AppendReplace("/height", healthAppInfo.Height);
                    }
                    else if (deviceId == surveyInfoDeviceId)
                    {
                        // Convert base64 string
                        string deviceBodyMessage = Convert.ToString(deviceMessage["body"]);
                        log.LogInformation($"Device:{deviceId}; Device Body value:{deviceBodyMessage}");

                        byte[] data = Convert.FromBase64String(deviceBodyMessage);

                        string jsonBack = Encoding.UTF8.GetString(data);
                        var surveyInfo = JsonConvert.DeserializeObject<SurveyInfo>(jsonBack);

                        log.LogInformation($"Device:{deviceId}; SurveyInfo:{JsonConvert.SerializeObject(surveyInfo)}");

                        updateTwinData.AppendReplace("/visionStatus", surveyInfo.VisionStatus);
                        updateTwinData.AppendReplace("/fatigueStatus", surveyInfo.FatigueStatus);
                        updateTwinData.AppendReplace("/sleepHours", surveyInfo.SleepHours);
                        updateTwinData.AppendReplace("/waterConsumptionStatus", surveyInfo.WaterConsumptionStatus);
                        updateTwinData.AppendReplace("/urinationStatus", surveyInfo.UrinationStatus);
                        updateTwinData.AppendReplace("/cutsHealingStatus", surveyInfo.CutsHealingStatus);
                        updateTwinData.AppendReplace("/tinglingSensationStatus", surveyInfo.TinglingSensationStatus);
                        updateTwinData.AppendReplace("/weightStatus", surveyInfo.WeightStatus);
                    }

                    await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);
                }
            }

            catch (Exception ex)
            {
                log.LogError($"Error in ingest function: {ex.Message}");
            }

        }
    }
}
