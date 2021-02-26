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

        private async Task ReplaceItemAsync(string ListingID, int Price)
        {
            ItemResponse<Property> LocalQueryResponse = await this.container.ReadItemAsync<Property>(ListingID, new PartitionKey(ListingID));
            var itemBody = LocalQueryResponse.Resource; 
            itemBody.Price = Price;
            LocalQueryResponse = await this.container.ReplaceItemAsync<Property>(itemBody, itemBody.id, new PartitionKey(itemBody.id));
            Console.WriteLine("Updated {0} to {1}\n", ListingID, Price);
        }
        private async Task AddItemsToContainerAsync()
        {
            Console.WriteLine("get or read");
            string GetRead = Console.ReadLine();
            dynamic responseJSON = JsonConvert.DeserializeObject(GetInfo(GetRead));
            double RTUs = 0;
            List<string> ActivePropIDs = new List<string>();
            foreach (var property in responseJSON["properties"])
            {
                double lotSize = ConvertSize((property["lot_size"]["units"]).ToString(), (property["lot_size"]["size"]).ToObject<double>());
                string AddressLine = GetAddressLine((property["address"]["line"]).ToString());
                //Console.WriteLine(property);
                Property CurrentProperty = new Property
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
                    ItemResponse<Property> ExistingProperty = await this.container.ReadItemAsync<Property>(CurrentProperty.ListingID, new PartitionKey(CurrentProperty.ListingID));
                    if (ExistingProperty.Resource.Price == CurrentProperty.Price)
                    {
                        Console.BackgroundColor = ConsoleColor.Black;
                    }
                    else if (CurrentProperty.Price > ExistingProperty.Resource.Price)
                    {
                        Console.BackgroundColor = ConsoleColor.Red;
                        await ReplaceItemAsync(CurrentProperty.ListingID, CurrentProperty.Price);
                    }
                    else
                    {
                        Console.BackgroundColor = ConsoleColor.Green;
                        Console.WriteLine("{0},{1}\r", CurrentProperty.ListingID, CurrentProperty.Price);
                        await ReplaceItemAsync(CurrentProperty.ListingID, CurrentProperty.Price);
                    }
                    Console.WriteLine("Old item~{0},{1},{2},{3},{4},{5},{6}\r",
                        ExistingProperty.Resource.ListingID,
                        ExistingProperty.Resource.PropertyID,
                        ExistingProperty.Resource.AddressLine,
                        ExistingProperty.Resource.LotSize,
                        ExistingProperty.Resource.PropType,
                        ExistingProperty.Resource.Price,
                        ExistingProperty.Resource.RDC_web_url);
                    ActivePropIDs.Add(CurrentProperty.id);
                    RTUs = RTUs + ExistingProperty.RequestCharge;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Create an item in the container representing the property record. Note we provide the value of the partition key for this item, which is "ListingID
                    ItemResponse<Property> NewProperty = await this.container.CreateItemAsync<Property>(CurrentProperty, new PartitionKey(CurrentProperty.ListingID));

                    // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
                    Console.BackgroundColor = ConsoleColor.Black; 
                    Console.WriteLine("New item~{0},{1},{2},{3},{4},{5},{6}\r",
                        NewProperty.Resource.ListingID,
                        NewProperty.Resource.PropertyID,
                        NewProperty.Resource.AddressLine,
                        NewProperty.Resource.LotSize,
                        NewProperty.Resource.PropType,
                        NewProperty.Resource.Price,
                        NewProperty.Resource.RDC_web_url);
                    ActivePropIDs.Add(NewProperty.Resource.id);
                    RTUs = RTUs + NewProperty.RequestCharge;
                }
            }
            Console.WriteLine("Total RTUs: {0}\n\r", RTUs);
            Console.ReadLine();

            List<Property> AllLines = new List<Property>();
            AllLines.AddRange(await RunQuery("SELECT p.ListingID, p.AddressLine FROM Properties p"));
            foreach (Property line in AllLines)
            {
                if(!ActivePropIDs.Contains(line.ListingID)) 
                {
                    var partitionKeyValue = line.ListingID;
                    ItemResponse<Property> DeleteProperty = await this.container.DeleteItemAsync<Property>(line.ListingID, new PartitionKey(partitionKeyValue));
                    Console.WriteLine("Removed: {0},{1}\r",
                      line.ListingID,
                      line.AddressLine);
                }
            }
            Console.WriteLine();
        }
        private async Task<List<Property>> RunQuery(string sqlQueryText)
        {

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<Property> queryResultSetIterator = this.container.GetItemQueryIterator<Property>(queryDefinition);

            List<Property> Lines = new List<Property>();

            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<Property> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (Property line in currentResultSet)
                {

                    Lines.Add(line);
                    //Console.WriteLine("{2}\t{1}\t{0}", line.AddressLine, line.LotSize, line.Price);
                }
            }
            return (Lines);
        }
        private async Task CreateReportsAsync()
        {
            
            List<Property> AllLines = new List<Property>();

            AllLines.AddRange(await RunQuery("SELECT p.ListingID, p.AddressLine, p.Price, p.LotSize, p.RDC_web_url " +
                "FROM Properties p " +
                "WHERE p.AddressLine LIKE '%Spencer%' OR p.AddressLine LIKE '%Tayl%'"));
            Console.WriteLine("Properties on Spencer or Tayloes Neck:");
            foreach (Property line in AllLines)
            {
                Console.WriteLine("{0},{1},{2},{3},{4}\r",
                          line.ListingID,
                          line.AddressLine,
                          line.LotSize,
                          line.Price,
                          line.RDC_web_url);
            }
            Console.WriteLine();

            List<Property> AllLines1 = new List<Property>();
            AllLines1.AddRange(await RunQuery("SELECT p.ListingID, p.AddressLine, p.Price, p.LotSize, p.RDC_web_url " +
                "FROM Properties p " +  
                "WHERE p.LotSize >= 5 ORDER BY p.LotSize ASC"));
            Console.WriteLine("Properties 5+ Acres:");
            foreach (Property line in AllLines1)
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
            await this.CreateReportsAsync();
            //await this.ReplaceFamilyItemAsync();
            //await this.DeleteItemAsync();
            Console.Read();
        }

        public static async Task Main(string[] args)
        {
            Program p = new Program();
            await p.ProcessInfo();
        }
    }
}
