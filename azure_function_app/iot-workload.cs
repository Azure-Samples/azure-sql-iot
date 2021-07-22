using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventHubs;
using System.Text;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;


namespace Microsoft.SqlDb.Workloads
{
    public static class iot_workload
    {
        private const string EnvSQLDBConnectionString = "SQLDBConnectionString";

        private static readonly string SQLDBConnectionString =
                        Environment
                        .GetEnvironmentVariable(
                            EnvSQLDBConnectionString,
                            EnvironmentVariableTarget.Process);

        static iot_workload()
        {
            if (string.IsNullOrEmpty(SQLDBConnectionString))
            {
                throw new ArgumentException("Azure SQL Database connection string is not valid.", nameof(SQLDBConnectionString));
            }
        }

        private static HttpClient client = new HttpClient();

        [FunctionName("iot_workload")]
        public static async Task Run([IoTHubTrigger("warmpath", Connection = "EventHubConnectionAppSetting")]EventData[] messages, Microsoft.Azure.WebJobs.ExecutionContext context, ILogger log)
        {

            DateTimeOffset ticksUTCNow = DateTimeOffset.UtcNow;

            CustomTelemetry.TrackMetric(
                              context,
                              "IoTHubMessagesReceived",
                              messages.Length);

            // Track whether messages are arriving at the function late.
            DateTime? firstMsgEnqueuedTicksUtc = messages[0]?.SystemProperties.EnqueuedTimeUtc;

            if (firstMsgEnqueuedTicksUtc.HasValue)
            {
                CustomTelemetry.TrackMetric(
                                  context,
                                  "IoTHubMessagesReceivedFreshnessMsec",
                                  (ticksUTCNow - firstMsgEnqueuedTicksUtc.Value)
                                  .TotalMilliseconds);
            }

            var length = (double)messages.Length;

            log.LogInformation($"Starting load | Docs to bulk load {messages.Length} | Docs to bulk load per task {length}");

            // Bulk load events
            long sqlDbTotalMilliseconds = await BulkLoadEvents(messages, log);

            CustomTelemetry.TrackMetric(
                               context,
                               "IoTHubMessagesDropped",
                               messages.Length);
            CustomTelemetry.TrackMetric(
                               context,
                               "SqlDbDocumentsCreated",
                               messages.Length);
            var latency = messages.Length > 0 
                                ? sqlDbTotalMilliseconds / messages.Length 
                                : 0;
            CustomTelemetry.TrackMetric(
                               context,
                               "SqlDbLatencyMsec",
                               latency);

            foreach (var message in messages)
                {
                    log.LogInformation($"C# function triggered to process a message: {Encoding.UTF8.GetString(message.Body)}");
                    log.LogInformation($"EnqueuedTimeUtc={message.SystemProperties.EnqueuedTimeUtc}");
                }
        }
        private static async Task<long> BulkLoadEvents(
                            IEnumerable<EventData> docsToUpsert,
                            ILogger log)
        {

            // Define data structure that will load events into database
            DataTable dt = new DataTable();
            dt.Columns.Add("deviceid",typeof(string));
            dt.Columns.Add("timestamp",typeof(DateTime));
            dt.Columns.Add("json",typeof(string));

            var sqlDbLatency = new Stopwatch();
            // for each message read from IoTHub
            foreach (var message in docsToUpsert)
            {
                var text = Encoding.UTF8.GetString(message.Body);
                // Create a new row
                DataRow dr = dt.NewRow();
                // Parse telemetry message
                dynamic telemetry = JObject.Parse(text);

                dr["deviceid"]=telemetry.deviceId;
                dr["timestamp"]=message.SystemProperties.EnqueuedTimeUtc;
                dr["json"]=text;

                dt.Rows.Add(dr);                  
            }    

            try
            {
                sqlDbLatency.Start();

                using(SqlConnection cnn = new SqlConnection(SQLDBConnectionString))
                {
                    cnn.Open();
                    SqlBulkCopy bc = new SqlBulkCopy(cnn);
                    bc.BatchSize=10000;
                    bc.DestinationTableName="events";
                    await bc.WriteToServerAsync(dt);
                }

                sqlDbLatency.Stop();
            }
            catch (SqlException sqlEx)
            {
                log.LogInformation($"Error processing message with err number {sqlEx.Number}. Exception was {sqlEx.ToString()}");
            }
            catch(Exception e)
            {
                log.LogInformation($"Error processing message. Exception was {e.ToString()}");
            }

            return (long)sqlDbLatency
                .Elapsed
                .TotalMilliseconds;
        }

    }
}