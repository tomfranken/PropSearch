using System;
using unirest_net.http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;
//using PropSearchConsole;

namespace PropSearch
{
    class Program
    { 
        private static readonly string EndpointUri = "https://tomtestsosmoetc.documents.azure.com:443/";
        // The primary key for the Azure Cosmos account.
        private static readonly string PrimaryKey = "3KyGXoLt0HmtGMSYXuKXrfIxGY7cCgmLk9nyou4s4gI7XYmdcJwjn0GXxy2gXlcXRNTXOYCach5BEbPELhV9hA==";

        // The Cosmos client instance
        private CosmosClient cosmosClient;

        // The database we will create
        private Database database;

        // The container we will create.
        private Container container;

        // The name of the database and container we will create
        private string databaseId = "Props";
        private string containerId = "Properties";

        private async Task CreateDatabaseAsync()
        {
            // Create a new database
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            Console.WriteLine("Created Database: {0}\n", this.database.Id);
        }

        private async Task CreateContainerAsync()
        {
            // Create a new container
            this.container = await this.database.CreateContainerIfNotExistsAsync(containerId, "/ListingID");
            Console.WriteLine("Created Container: {0}\n", this.container.Id);
        }

        private async Task AddItemsToContainerAsync()
        {
            dynamic responseJSON = JsonConvert.DeserializeObject(GetInfo("get" + ""));
            double RTUs = 0;
            foreach (var property in responseJSON["properties"])
            {
                double lotSize = ConvertSize((property["lot_size"]["units"]).ToString(), (property["lot_size"]["size"]).ToObject<double>());
                string AddressLine = GetAddressLine((property["address"]["line"]).ToString());
                //Console.WriteLine(property);
                PropLine NewPropLine = new PropLine
                {
                    id = property["listing_id"],
                    ListingID = property["listing_id"],
                    PropertyID = property["property_id"],
                    AddressLine = AddressLine,
                    PropType = property["prop_type"],
                    Price = (property["price"].ToObject<int>()),
                    LotSize = lotSize,
                    RDC_web_url = property["rdc_web_url"]
                };
                try
                {
                    // Read the item to see if it exists.  
                    ItemResponse<PropLine> PropLineResponse = await this.container.ReadItemAsync<PropLine>(NewPropLine.ListingID, new PartitionKey(NewPropLine.ListingID));
                    if (PropLineResponse.Resource.Price == NewPropLine.Price)
                    {
                        Console.BackgroundColor = ConsoleColor.Black;
                    }
                    else if (NewPropLine.Price > PropLineResponse.Resource.Price)
                    {
                        Console.BackgroundColor = ConsoleColor.Red;
                    }
                    else
                    {
                        Console.BackgroundColor = ConsoleColor.Green;
                    }
                    Console.WriteLine("Old item: {0},{1},{2},{3},{4},{5},{6}\r",
                        PropLineResponse.Resource.ListingID,
                        PropLineResponse.Resource.PropertyID,
                        PropLineResponse.Resource.AddressLine,
                        PropLineResponse.Resource.LotSize,
                        PropLineResponse.Resource.PropType,
                        PropLineResponse.Resource.Price,
                        PropLineResponse.Resource.RDC_web_url);

                    RTUs = RTUs + PropLineResponse.RequestCharge;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Create an item in the container representing the property record. Note we provide the value of the partition key for this item, which is "ListingID
                    ItemResponse<PropLine> PropLineResponse = await this.container.CreateItemAsync<PropLine>(NewPropLine, new PartitionKey(NewPropLine.ListingID));

                    // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                    Console.BackgroundColor = ConsoleColor.Black; 
                    Console.WriteLine("New item: {0},{1},{2},{3},{4},{5},{6}\r",
                        PropLineResponse.Resource.ListingID,
                        PropLineResponse.Resource.PropertyID,
                        PropLineResponse.Resource.AddressLine,
                        PropLineResponse.Resource.LotSize,
                        PropLineResponse.Resource.PropType,
                        PropLineResponse.Resource.Price,
                        PropLineResponse.Resource.RDC_web_url);
                    RTUs = RTUs + PropLineResponse.RequestCharge;
                }
            }
            Console.WriteLine("Total RTUs: {0}\n\r", RTUs);
            Console.ReadLine();
        }

        private async Task<List<ReportLine>> RunQuery(string sqlQueryText)
        {

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<ReportLine> queryResultSetIterator = this.container.GetItemQueryIterator<ReportLine>(queryDefinition);

            List<ReportLine> Lines = new List<ReportLine>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<ReportLine> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (ReportLine line in currentResultSet)
                {

                    Lines.Add(line);
                    //Console.WriteLine("{2}\t{1}\t{0}", line.AddressLine, line.LotSize, line.Price);
                }
            }
            return (Lines);
        }
        private async Task QueryItemsAsync()
        {
            
            List<ReportLine> AllLines = new List<ReportLine>();

            AllLines.AddRange(await RunQuery("SELECT p.ListingID, p.AddressLine, p.Price, p.LotSize, p.RDC_web_url " +
                "FROM Properties p " +
                "WHERE p.AddressLine LIKE '%Spencer%' OR p.AddressLine LIKE '%Tayl%'"));
            foreach (ReportLine line in AllLines)
            {
                Console.WriteLine("{0},{1},{2},{3},{4}\r",
                          line.ListingID,
                          line.AddressLine,
                          line.LotSize,
                          line.Price,
                          line.RDC_web_url);
            }
            Console.WriteLine();
            Console.WriteLine("Press the any key to continue.");

            List<ReportLine> AllLines1 = new List<ReportLine>();
            AllLines1.AddRange(await RunQuery("SELECT p.ListingID, p.AddressLine, p.Price, p.LotSize, p.RDC_web_url " +
                "FROM Properties p " +  
                "WHERE p.LotSize >= 5 ORDER BY p.LotSize ASC"));
            foreach (ReportLine line in AllLines1)
            {
                Console.WriteLine("{0},{1},{2},{3},{4}\r",
                          line.ListingID,
                          line.AddressLine,
                          line.LotSize,
                          line.Price,
                          line.RDC_web_url);
            }
        }

        public static string GetInfo (string getread)
        {
            string response = "";
            if (getread == "get")
            {
                HttpResponse<string> info = Unirest.get("https://realtor.p.rapidapi.com/properties/v2/list-for-sale?city=Nanjemoy&limit=200&offset=0&state_code=MD")
                .header("X-RapidAPI-Host", "realtor.p.rapidapi.com")
                .header("X-RapidAPI-Key", "37e55441e9msh666c920aa1b90a1p1ea11cjsn7020db0de8f5")
                .header("Accept", "application/json")
                .asJson<string>();
                response = info.Body;
                System.IO.File.WriteAllText(@"E:\DataStore\PropSearchConsole\responseText.txt", response);
            }
            else if (getread == "read")
            {
                response = System.IO.File.ReadAllText(@"E:\DataStore\PropSearchConsole\responseText.txt");
            }
            return response;
        }

        public static double ConvertSize(string units, double size)
        {
            //Console.WriteLine(units + size);
            double lotSize = 0;
            if (units == "sqft")
            {
                lotSize = Math.Round((size / 43560), 2);
            }
            else
            {
                lotSize = size;
            }
            return lotSize;
        }

        public static string GetAddressLine(string AddressLine)
        {
            if (AddressLine != null)
            {
                return AddressLine;
            }
            else
            {
                return "No Address";
            }
        }

        public async Task ProcessInfo()
        {
            this.cosmosClient = new CosmosClient(EndpointUri, PrimaryKey);
            this.container = this.cosmosClient.GetContainer(databaseId, containerId);
            //await this.CreateDatabaseAsync();
            //await this.CreateContainerAsync();
            await this.AddItemsToContainerAsync();
            await this.QueryItemsAsync();
            //await this.ReplaceFamilyItemAsync();
            //await this.DeleteFamilyItemAsync();
            Console.Read();
        }

        public static async Task Main(string[] args)
        {
            Program p = new Program();
            await p.ProcessInfo();
        }
    }
}
