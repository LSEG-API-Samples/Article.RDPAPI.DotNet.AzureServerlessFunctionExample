using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using rdp_api_lib;
using StackExchange.Redis;

namespace RdpAzureFunctions
{
    public class RdpHttpTrigger
    {
        private readonly IHttpClientFactory _client;
        private readonly IRdpAuthorizeService _authService;
        private readonly IEsgService _esgService;
        private const string RedisConnString="<Redis Connection String>";
        public RdpHttpTrigger(IHttpClientFactory httpClient)
        {
            this._client = httpClient;
            _authService = new RdpAuthorizeService(_client);
            _esgService = new EsgService(_client);
        }
        /// <summary>
        /// Serverless Function SearchUnvierse. It was created to perform ESG universe search by using PermId, Ric name, Common Name or any fields.
        /// I will read the ESG universe data from Redis Cache conifured in RedisConnString. User has to run GetESGUniverse first to add data to Redis Cache.
        /// </summary>
        /// <param name="req">HTTP Request. Client has too pass parameter username,query and type with the HTTP GET</param>
        /// <param name="log"></param>
        /// <returns>List of PermId,Ric and Common Name that match with the query</returns>
        [FunctionName("SearchUniverse")]
        public async Task<IActionResult> SearchEsgUniverse(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            ILogger log)
        {
            var cache = (await ConnectionMultiplexer.ConnectAsync(RedisConnString)
                ).GetDatabase();
            var username = req.Query["username"];
            var query = req.Query["query"];
            var searchtype = (string)req.Query["type"];
            if (string.IsNullOrEmpty(searchtype))
                searchtype = "any";
            var esgUniverse = cache.StringGet($"{username}");
            if (!string.IsNullOrEmpty(esgUniverse)&& !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(query))
            {
                var esgJsonObj = JObject.Parse(esgUniverse.ToString());
                var esgData = esgJsonObj["EsgUniverse"].ToObject<List<EsgUniverseData>>();


                var searchResult = new List<EsgUniverseData>();
                if(searchtype.ToLower().Contains("permid"))
                    searchResult = EsgUniverseCache.GetDataByPermId(query, esgData)?.ToList();
                else
                if (searchtype.ToLower().Contains("ric"))
                        searchResult = EsgUniverseCache.GetDataByRic(query, esgData)?.ToList();
                else
                if (searchtype.ToLower().Contains("name"))
                    searchResult = EsgUniverseCache.GetDataByCommonName(query, esgData)?.ToList();
                else
                if (searchtype.ToLower().Contains("any"))
                    searchResult = EsgUniverseCache.GetData(query, esgData)?.ToList();

                return new JsonResult(JsonConvert.SerializeObject(searchResult));
            }
            return new OkObjectResult("{[]}");
        }

        
        /// <summary>
        /// Serverless Function GetESGUniverse. Support HTTP GET/POSt.
        /// It was designed to Get ESG universe data from Refinitv Data Platfrom. 
        /// </summary>
        /// <param name="req">HTTP Request client can pass following parameters with HTTP request
        /// token: RDP Access Tokey.
        /// tokentype: RDP Token Type, default is "Bearer".
        /// username: RDP username. 
        /// updatecache: true or false . If true it will write the Esg Universe data to Redis Cache based on the username provided in HTTP request</param>.
        ///              If username is duplicated, it will overwrite data on Redis Cache.
        /// <param name="log"></param>
        /// <returns>ESG Content from RDP ESG services in JSON format. It will be a JSON message serialize from RdpEsgResponse class</returns>
        [FunctionName("GetESGUniverse")]
        public async Task<IActionResult> GetESGUniverse(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
            HttpRequest req,
            ILogger log)
        {
                var token = string.Empty;
                var tokenType = string.Empty;
                var updatecache = string.Empty;
                var username = string.Empty;
                var returnContent = string.Empty;
                if (req.Method.ToLower() == "get")
                {
                    token = req.Query["token"];
                    tokenType = req.Query["tokentype"];
                    updatecache = req.Query["updatecache"];
                    username = req.Query["username"];
                    returnContent = req.Query["showuniverse"];
                }
                else if (req.Method.ToLower() == "post")
                {
                    var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    dynamic data = JObject.Parse(requestBody);
                    token = data.token;
                    tokenType = data.tokentype;
                    updatecache = data.updatecache;
                    username = data.username;
                    returnContent = data.showuniverse;
                }
                tokenType ??= "Bearer";
                updatecache ??= "false";
                returnContent ??= "true";

                var response = await _esgService.GetEsgUniverse(token, tokenType);
                if (response.IsSuccess)
                {

                    var esgCache = response as RdpEsgResponse;

                    if (!string.IsNullOrEmpty(updatecache) && !string.IsNullOrEmpty(username) && updatecache.Contains("true"))
                    {
                        var cache = (await ConnectionMultiplexer.ConnectAsync(RedisConnString)
                            ).GetDatabase();
                        var jsonObj = new
                        {
                            EsgUniverseCount = esgCache.Count,
                            EsgUniverseHeader = esgCache?.UniverseHeaderMetas,
                            EsgUniverse = esgCache?.UniverseData,
                        };

                        cache.StringSet(username, JsonConvert.SerializeObject(jsonObj));
                        

                      
                    }

                    if (returnContent.Contains("false"))
                    {
                        esgCache.UniverseData = new List<EsgUniverseData>();
                        esgCache.UniverseHeaderMetas = new List<EsgUniverseHeaderMeta>();
                    }

                    return new JsonResult(JsonConvert.SerializeObject(esgCache));
                }

            var errorData = response as RdpEsgError; 
            return new JsonResult(JsonConvert.SerializeObject(errorData));

        }
        /// <summary>
        /// Function to get new RDP Token from the RDP server.
        ///
        /// </summary>
        /// <param name="req">HTTP request with the following parameters.
        /// username: RDP username or Machine Id
        /// password: RDP password
        /// appkey: RDP client id or appkey</param>
        /// refreshtoken: Refresh Token to get a new Access Token
        /// userefreshtoken: true/false. You need to pass it with the query parameters to tell the function to use refreshtoken to get a new access token instead.
        /// <param name="log"></param>
        /// <returns>Response Message from RDP Token service with addtional info from HTTP response message in JSON format</returns>
        [FunctionName("GetNewToken")]
        public async Task<IActionResult> GetNewToken(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
            HttpRequest req,
            ILogger log)
        {
            var username = string.Empty;
            var password = string.Empty;
            var appId = string.Empty;
            var useRefreshToken = "false";
            var refreshToken = string.Empty;

            if (req.Method.ToLower() == "get")
            {
                username = req.Query["username"];
                password = req.Query["password"];
                appId = req.Query["appid"];
                useRefreshToken = req.Query["userefreshtoken"];
                refreshToken = req.Query["refreshtoken"];
            }
            else if (req.Method.ToLower() == "post")
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JObject.Parse(requestBody);
                username = data.username;
                password = data.password;
                appId = data.appid;
                useRefreshToken = data.userefreshtoken;
                refreshToken = data.refreshtoken;
      
            }

            if (string.IsNullOrEmpty(useRefreshToken))
                useRefreshToken = "false";
            


            if (string.IsNullOrEmpty(refreshToken))
                refreshToken = string.Empty;
            
            var response = await _authService.GetToken(username, password, appId,"trapi",refreshToken,Convert.ToBoolean(useRefreshToken));
            if (response.IsSuccess)
            {
                var tokenData = response as RdpTokenResponse;

                return new JsonResult(JsonConvert.SerializeObject(tokenData));
            }

            var errorData = response as RdpAuthenticationError;
            return new JsonResult(JsonConvert.SerializeObject(errorData));
        }


    }
}