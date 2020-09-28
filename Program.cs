using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Generic;
using System.Text;

namespace CoxAutoInterviewTest
{    
    class Program
    {
        static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var datasetId = await GetDatasetId();

            var vehicleIds = await GetVehicleIdsForDataset(datasetId);

            var vehicles = await GetInfoForVehicles(datasetId, vehicleIds);

            var dealerIds = vehicles.Select(v => v.DealerId).Distinct();
            var dealers = await GetInfoForDealers(datasetId, dealerIds);

            AssignVehiclesToDealers(vehicles, dealers);

            await PostAnswer(datasetId, dealers);

            stopwatch.Stop();
            Console.WriteLine($"Program finished. Elapsed time = {stopwatch.Elapsed}");
        }

        private static async Task<string> GetDatasetId()
        {
            var response = await client.GetAsync("http://api.coxauto-interview.com/api/datasetId");
            var content = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(content);
            var datasetId = document.RootElement.GetProperty("datasetId").GetString();
            Console.WriteLine($"Retrieved datasetId = {datasetId}");

            return datasetId;
        }

        private static async Task<IEnumerable<int>> GetVehicleIdsForDataset(string datasetId)
        {
            var response = await client.GetAsync($"http://api.coxauto-interview.com/api/{datasetId}/vehicles");
            var content = await response.Content.ReadAsStringAsync();

            // TODO: Consider handling IDisposable here with JsonDocument & ArrayEnumerator.
            var document = JsonDocument.Parse(content);
            var vehicleIds = document.RootElement
                .GetProperty("vehicleIds")
                .EnumerateArray()
                .Select(vehicleId => vehicleId.GetInt32());
            Console.WriteLine($"Retrieved vehicleIds = {string.Join(", ", vehicleIds)}");

            return vehicleIds;
        }

        private static async Task<Vehicle> GetInfoForVehicle(string datasetId, int vehicleId)
        {
            var response = await client.GetAsync($"http://api.coxauto-interview.com/api/{datasetId}/vehicles/{vehicleId}");
            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);            

            var vehicle = new Vehicle {
                DealerId = document.RootElement.GetProperty("dealerId").GetInt32(),
                VehicleId = document.RootElement.GetProperty("vehicleId").GetInt32(),
                Year = document.RootElement.GetProperty("year").GetInt32(),
                Make = document.RootElement.GetProperty("make").GetString(),
                Model = document.RootElement.GetProperty("model").GetString()
            };

            Console.WriteLine($"Got vehicle info = {JsonSerializer.Serialize(vehicle)}");

            return vehicle;
        }

        private static async Task<IEnumerable<Vehicle>> GetInfoForVehicles(string datasetId, IEnumerable<int> vehicleIds)
        {
            var tasks = new List<Task<Vehicle>>();
            foreach (var vehicleId in vehicleIds)
            {
                tasks.Add(GetInfoForVehicle(datasetId, vehicleId));
            }

            await Task.WhenAll(tasks);

            return tasks.Select(t => t.Result);
        }

        private static async Task<Dealer> GetInfoForDealer(string datasetId, int dealerId)
        {
            var response = await client.GetAsync($"http://api.coxauto-interview.com/api/{datasetId}/dealers/{dealerId}");
            var content = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(content);            

            var dealer = new Dealer {
                DealerId = document.RootElement.GetProperty("dealerId").GetInt32(),
                Name = document.RootElement.GetProperty("name").GetString()
            };

            Console.WriteLine($"Got dealer info = {JsonSerializer.Serialize(dealer)}");

            return dealer;
        }

        private static async Task<IEnumerable<Dealer>> GetInfoForDealers(string datasetId, IEnumerable<int> dealerIds)
        {
            var tasks = new List<Task<Dealer>>();
            foreach (var dealerId in dealerIds)
            {
                tasks.Add(GetInfoForDealer(datasetId, dealerId));
            }

            await Task.WhenAll(tasks);

            return tasks.Select(t => t.Result);
        }

        private static void AssignVehiclesToDealers(IEnumerable<Vehicle> vehicles, IEnumerable<Dealer> dealers)
        {
            foreach (var dealer in dealers)
            {
                dealer.Vehicles.AddRange(
                    vehicles.Where(v => v.DealerId == dealer.DealerId)
                );
            }

            Console.WriteLine($"Assigned vehicles to dealers");
        }

        private static async Task PostAnswer(string datasetId, IEnumerable<Dealer> dealers)
        {
            var answer = new Answer { Dealers = dealers };

            var options = new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize<Answer>(answer, options);
            Console.WriteLine($"Posting answer JSON = {json}");

            var contentToPost = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(
                $"http://api.coxauto-interview.com/api/{datasetId}/answer",
                contentToPost
            );

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Posted answer. Response = {responseContent}");
        }
    }
}
