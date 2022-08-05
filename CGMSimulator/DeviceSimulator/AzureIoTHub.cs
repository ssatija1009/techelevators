using Azure.Messaging.EventHubs.Consumer;
using CsvHelper;
using DeviceSimulator.DataObjects;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceSimulator
{
    public static class AzureIoTHub
    {
        private static int taskDelay = 10 * 1500;
        //#####################################################################
        //Replace Hub Name, hub Shared Access Key, and then the device shared access keys
        //  if you have different names for your devices, update those as well
        //#####################################################################
        private static string hubName = "techadtiothub";
        private static string hubSharedAccessKey = "mr+zMLzzuzPMBAhy9PRIufjIEGQ+7MECHyr+RlVaE+s=";

        private static string device1Name = "CGMInfoOne";
        private static string device1SharedAccessKey = "dT2MXO+R62Ol7x7jYuLLIeKYHc1cCpj3IMjiCv/osNc=";

        //these are composed from the above values
        private static string iotHubConnectionString = @$"HostName={hubName}.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey={hubSharedAccessKey}";

        private static string deviceConnectionString1 = $"HostName={hubName}.azure-devices.net;DeviceId={device1Name};SharedAccessKey={device1SharedAccessKey}";

        //#####################################################################
        //Replace these for the correct device simulation
        //#####################################################################
        private static string deviceConnectionString = deviceConnectionString1;
        private static string deviceId = device1Name;

        public static async Task<string> CreateDeviceIdentityAsync(string deviceName)
        {
            var registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            var device = new Device(deviceName);
            try
            {
                device = await registryManager.AddDeviceAsync(device);
            }
            catch (DeviceAlreadyExistsException)
            {
                device = await registryManager.GetDeviceAsync(deviceName);
            }

            return device.Authentication.SymmetricKey.PrimaryKey;
        }

        public static async Task SendDeviceToCloudMessageAsync(CancellationToken cancelToken)
        {
            try
            {
                var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

                // Read CSV Data
                var csvRecords = new List<CGMFileInfo>();
                var filepath = "examplecsv/adult#001.csv";
                var envpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var path = Path.Combine(envpath, filepath);
                using (var reader = new StreamReader(path))
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csvRecords = csv.GetRecords<CGMFileInfo>().ToList();
                }

                while (!cancelToken.IsCancellationRequested)
                {
                    if (csvRecords.Count > 0)
                    {
                        var record = csvRecords.FirstOrDefault();
                        csvRecords.RemoveAt(0);

                        var telemetryDataPoint = new CGMTelemetry
                        {
                            glucoseLevel = Convert.ToDouble(record.BG),
                            timestamp = record.Time
                        };
                        var messageString = JsonSerializer.Serialize(telemetryDataPoint);

                        var message = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(messageString))
                        {
                            ContentType = "application/json",
                            ContentEncoding = "utf-8"
                        };
                        await deviceClient.SendEventAsync(message);
                        Console.WriteLine($"{DateTime.Now} > Sending message: {messageString}");
                    }

                    //Keep this value above 1000 to keep a safe buffer above the ADT service limits
                    //See https://aka.ms/adt-limits for more info
                    await Task.Delay(taskDelay);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static async Task<string> ReceiveCloudToDeviceMessageAsync()
        {
            var oneSecond = TimeSpan.FromSeconds(1);
            var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

            while (true)
            {
                var receivedMessage = await deviceClient.ReceiveAsync();
                if (receivedMessage == null)
                {
                    await Task.Delay(oneSecond);
                    continue;
                }

                var messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                await deviceClient.CompleteAsync(receivedMessage);
                return messageData;
            }
        }

        public static async Task ReceiveMessagesFromDeviceAsync(CancellationToken cancelToken)
        {
            try
            {
                string eventHubConnectionString = await IotHubConnection.GetEventHubsConnectionStringAsync(iotHubConnectionString);
                await using var consumerClient = new EventHubConsumerClient(
                    EventHubConsumerClient.DefaultConsumerGroupName,
                    eventHubConnectionString);

                await foreach (PartitionEvent partitionEvent in consumerClient.ReadEventsAsync(cancelToken))
                {
                    if (partitionEvent.Data == null) continue;

                    string data = Encoding.UTF8.GetString(partitionEvent.Data.Body.ToArray());
                    Console.WriteLine($"Message received. Partition: {partitionEvent.Partition.PartitionId} Data: '{data}'");
                }
            }
            catch (TaskCanceledException) { } // do nothing
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading event: {ex}");
            }
        }
    }
}
