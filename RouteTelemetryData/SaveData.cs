// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json.Linq;

namespace RouteTelemetry
{
    public static class SaveData
    {
        [FunctionName("SaveData")]
        public static void Run([EventGridTrigger]EventGridEvent eventGridEvent, ILogger log)
        {
            //log.LogInformation(eventGridEvent.Data.ToString());
            
            // Cast de los datos recibidos del IoT Hub a JObject
            JObject jOb = (JObject)eventGridEvent.Data;

            // Recupera los tres objetos principales de los datos
            JObject systemProperties = (JObject)jOb["systemProperties"];
            JObject properties = (JObject)jOb["properties"];
            JObject body = (JObject)jOb["body"];

            // Prepara los nombres del contenedor y del archivo
            string device = systemProperties["iothub-connection-device-id"].ToString().ToLower();
            string type = properties["MessageType"].ToString().ToLower();
            DateTime dt = DateTime.Parse(body["UtcTime"].ToString());
            string file = $"{dt.Year}/{dt.Month:D2}/{dt.Day:D2}/{device}_{type}_{dt.Year}_{dt.Month:D2}_{dt.Day:D2}.json";

            //log.LogInformation(file);

            if (CloudStorageAccount.TryParse("DefaultEndpointsProtocol=https;AccountName=monitoringstoragemog;AccountKey=nqILYPqqSbP2TxQ2Y6qbE4Z1QTeRE/IXa0hXEpb1NJpAovDyNxaPTAiz2LW9Dq7ywjAwJiyrDk+/kt7FgM+87Q==;EndpointSuffix=core.windows.net", out CloudStorageAccount cloudStorageAccount))
            {
                // Crea el cliente al almacenamiento
                var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();

                // Obtiene referencia o crea contenedor
                var cloudBlobContainer = cloudBlobClient.GetContainerReference(device);
                if(!cloudBlobContainer.Exists())
                {
                    cloudBlobContainer.Create();
                }

                // Obtiene referencia o crea blob
                CloudAppendBlob appendBlob = cloudBlobContainer.GetAppendBlobReference(file);
                if (!appendBlob.Exists())
                {
                    appendBlob.CreateOrReplace();
                }

                // Agrega los datos
                appendBlob.AppendText(body.ToString());

                //log.LogInformation("Oks");
            }
            else
            {
                log.LogInformation("Error");
            }

        }
    }
}
