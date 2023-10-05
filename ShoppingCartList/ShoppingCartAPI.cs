using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ShoppingCartList.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Cosmos;
using System.Net;
using System.Reflection.Metadata;

namespace ShoppingCartList
{
    public class ShoppingCartAPI
    {
        private Container documentContainer;
        private readonly CosmosClient cosmosClient;

        public ShoppingCartAPI(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
            documentContainer = cosmosClient.GetContainer("ShoppingCartItems", "Items");
        }

        [FunctionName("GetShoppingCartItems")]
        public async Task<IActionResult> GetShoppingCartItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shoppingcartitem")] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Getting all shopping cart items.");
           
            var items = documentContainer.GetItemQueryIterator<ShoppingCartItem>();
            return new OkObjectResult((await items.ReadNextAsync()).ToList());
        }

        [FunctionName("GetShoppingCartItem")]
        public async Task<IActionResult> GetShoppingCartItemById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "shoppingcartitem/{id}/{category}")] 
            HttpRequest req, ILogger log, string id, string category)
        {
            log.LogInformation($"Getting shopping cart item with id ${id}.");
            try
            {
                var item = await documentContainer.ReadItemAsync<ShoppingCartItem>(id, new PartitionKey(category));
                if (item.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new NotFoundResult();
                }
                return new OkObjectResult(item.Resource);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        [FunctionName("CreateShoppingCartItems")]
        public async Task<IActionResult> CreateShoppingCartItems(
                [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shoppingcartitem")] HttpRequest req,
                ILogger log)
        {
            log.LogInformation("Creating Shopping Cart Item.");
            string requestData = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<CreateShoppingCartItem>(requestData);

            var item = new ShoppingCartItem
            {
                ItemName = data.ItemName,
                Category = data.Category
            };

            await documentContainer.CreateItemAsync(item, new PartitionKey(item.Category));

            return new OkObjectResult(item);
        }

        [FunctionName("UpdateShoppingCartItem")]
        public async Task<IActionResult> UpdateShoppingCartItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", 
            Route = "shoppingcartitem/{id}/{category}")] HttpRequest req,
            ILogger log, string id, string category)
        {
            log.LogInformation($"Updating shopping cart item with id {id}.");

            string requestData = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<UpdateShoppingCartItem>(requestData);

            var item = await documentContainer.ReadItemAsync<ShoppingCartItem>(id, new PartitionKey(category));
            
            if (item.StatusCode == HttpStatusCode.NotFound)
            {
                return new NotFoundResult();
            }

            item.Resource.Collected = data.Collected;
            await documentContainer.UpsertItemAsync(item.Resource);

            return new OkObjectResult(item.Resource);
        }

        [FunctionName("DeleteShoppingCartItem")]
        public async Task<IActionResult> DeleteShoppingCartItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "shoppingcartitem/{id}/{category}")] HttpRequest req,
            ILogger log, string id, string category)
        {
            log.LogInformation($"Deleting shopping cart item with id {id}.");

            await documentContainer.DeleteItemAsync<ShoppingCartItem>(id, new PartitionKey(category));
            return new OkResult();
        }
    }
}
