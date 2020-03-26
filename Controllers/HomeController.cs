using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Logging;
using part1webapp.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace part1webapp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private DocumentClient client;
        private Uri spinsUri;
        private string functionsKey;
        private string functionsUrl;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
            client = new DocumentClient(new Uri(ConfigurationManager.AppSettings["cosmosDBUri"]), ConfigurationManager.AppSettings["cosmosDBAuthKey"]);
            functionsUrl = ConfigurationManager.AppSettings["functionsUrl"];
            functionsKey = ConfigurationManager.AppSettings["functionsKey"];
            spinsUri = UriFactory.CreateDocumentCollectionUri("BlogPostDatabase", "FunctionAppRuns");
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // GET: Home/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Home/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(IFormCollection collection)
        {
            try
            {
                var spin = new Spin()
                {
                    Seconds = int.Parse(collection["seconds"]),
                    Name = collection["name"],
                    Status = "In Progress"
                };
                var doc = await CreateSpin(spin);
                spin.id = doc.Id;
                SpinFunction(spin);

                

                return RedirectToAction(nameof(List));
            }
            catch
            {
                return View();
            }
        }

        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                var result = await DeleteSpin(id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> List()
        {
            var spins = await GetSpinList();
            return View(spins);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private async Task<List<Spin>> GetSpinList()
        {
            IDocumentQuery<Spin> query = client.CreateDocumentQuery<Spin>(spinsUri).AsDocumentQuery();

            List<Spin> results = new List<Spin>();
            while (query.HasMoreResults)
            {
                results.AddRange(await query.ExecuteNextAsync<Spin>());
            }
            return results.OrderBy(x => x.id).ToList();
        }

        private async Task<Document> CreateSpin(Spin spin)
        {
            return await client.CreateDocumentAsync(spinsUri, spin);
        }

        private async Task<string> DeleteSpin(string id)
        {
            var deleteCmd = await client.DeleteDocumentAsync(
                UriFactory.CreateDocumentUri("BlogPostDatabase", "FunctionAppRuns", id), new RequestOptions { PartitionKey = new PartitionKey(id) });
            return deleteCmd.StatusCode.ToString();
            
        }

        private async Task SpinFunction(Spin spin)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("x-functions-key", functionsKey);

                dynamic jsonContent = new JObject();
                jsonContent.name = spin.Name;
                jsonContent.seconds = spin.Seconds;

                HttpContent content = new StringContent(jsonContent.ToString(), Encoding.UTF8, "application/json");
                var result = await client.PostAsync(functionsUrl + "SpinTime?id=" + spin.id, content);
            }
        }
    }
}
