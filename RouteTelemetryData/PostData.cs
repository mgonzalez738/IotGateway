// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

namespace RouteTelemetry
{
    public static class PostData
    {
        private static readonly HttpClient client = new HttpClient();

        [FunctionName("PostData")]
        public static async void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            //log.LogInformation(eventGridEvent.Data.ToString());

            // Cast de los datos recibidos del IoT Hub a JObject
            JObject jOb = (JObject)eventGridEvent.Data;

            // Recupera las propiedades de los datos
            JObject properties = (JObject)jOb["properties"];
            JObject systemProperties = (JObject)jOb["systemProperties"];

            string device = systemProperties["iothub-connection-device-id"].ToString();
            string tag = properties["DeviceTag"].ToString();

            // Transmite solo si tiene tag
            if (tag != "")
            {
                string myJson = JsonConvert.SerializeObject(eventGridEvent.Data, Formatting.Indented);
                var response = await client.PostAsync("http://mgonzalez738.ddns.net:3000", new StringContent(myJson, Encoding.UTF8, "application/json"));
                var responseString = await response.Content.ReadAsStringAsync();
                //log.LogInformation(responseString);
            }
            else
            {
                log.LogInformation($"Evento no enviado al servidor. Recibido de dispositivo {device} sin Tag.");
            }       
        }
    }
}
