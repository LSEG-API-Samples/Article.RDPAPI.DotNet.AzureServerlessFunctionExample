using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using rdp_api_lib;

namespace rdp_api_lib
{
    public class ESGError
    {
        public string InvalidName { get; set; }
        public IList<string> InvalidValues { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
    public class RdpEsgError : IRdpResponseMessage
    {
        public HttpStatusCode HttpResponseStatusCode { get; set; }
        public bool IsSuccess => HttpResponseStatusCode == HttpStatusCode.OK;
        public string HttpResponseStatusText { get; set; }

        [Newtonsoft.Json.JsonProperty("code", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Code { get; set; }
        [Newtonsoft.Json.JsonProperty("errors", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public ESGError Errors { get; set; }
        [Newtonsoft.Json.JsonProperty("id", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Id { get; set; }
        [Newtonsoft.Json.JsonProperty("message", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Message { get; set; }
        [Newtonsoft.Json.JsonProperty("status", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Status { get; set; }
    }

    public class EsgUniverseHeaderMeta
    {
        [Newtonsoft.Json.JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Name { get; set; }
        [Newtonsoft.Json.JsonProperty("title", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Title { get; set; }
        [Newtonsoft.Json.JsonProperty("type", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Type { get; set; }
        [Newtonsoft.Json.JsonProperty("description", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Description { get; set; }
    }

    public class EsgUniverseData
    {
        public string PermId { get; set; }
        public string PrimaryRic { get; set; }
        public string CommonName { get; set; }
    }
    public class RdpEsgResponse : IRdpResponseMessage
    {
        public HttpStatusCode HttpResponseStatusCode { get; set; }
        public bool IsSuccess => HttpResponseStatusCode == HttpStatusCode.OK;
        public string HttpResponseStatusText { get; set; }
        public long Count { get; set; }
        public IList<EsgUniverseHeaderMeta> UniverseHeaderMetas { get; set; }
        public IList<EsgUniverseData> UniverseData { get; set; }
        public override string ToString()
        {
            var dumpText = new StringBuilder();
            dumpText.Append($"HTTP Status Code:{this.HttpResponseStatusCode}\n");
            dumpText.Append($"HttpResponseStatusText:{this.HttpResponseStatusText}\n");
            dumpText.Append($"===============================\n");
            dumpText.Append($"Count:{this.Count}\n");

            dumpText.Append($"==============================\n");
            return dumpText.ToString();
        }
    }

    public interface IEsgService
    {
        Task<IRdpResponseMessage> GetEsgUniverse(string requestToken,string tokenType,string redirectUrl=null);
    }
    public class EsgService: IEsgService
    {
        private readonly IHttpClientFactory _clientFactory;

        public EsgService(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }
        public async Task<IRdpResponseMessage> GetEsgUniverse(string requestToken, string tokenType="Bearer", string redirectUrl = null)
        {
            var tokenUri = new UriBuilder()
            {
                Scheme = "https",
                Host = RdpEndpoints.RdpServer,
                Path = RdpEndpoints.EsgUniverse

            };
            if (!string.IsNullOrEmpty(redirectUrl))
            {
                tokenUri = new UriBuilder(redirectUrl);
            }
            var request = new HttpRequestMessage(HttpMethod.Get, tokenUri.ToString());
            // Set Request Headers
            request.Headers.Authorization = new AuthenticationHeaderValue(tokenType,requestToken);
            request.Headers.Add("AllowAutoRedirect", "False");
            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);
            var rdpEsgResponse = new RdpEsgResponse();
            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TransferEncodingChunked == true || response.Content != null)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var jsonObject=JObject.Parse(json);
                    if (jsonObject["links"]?["count"] != null)
                        rdpEsgResponse.Count = long.Parse(jsonObject["links"]["count"].ToString());

                    rdpEsgResponse.UniverseHeaderMetas = jsonObject["headers"]?.ToObject<IList<EsgUniverseHeaderMeta>>();
                    var esgData = jsonObject["data"]?.ToObject<IList<IList<string>>>();
                    if (esgData != null)
                    {
                        rdpEsgResponse.UniverseData = new List<EsgUniverseData>();
                        foreach (var esgUniverse in esgData.ToList())
                        {
                            var esg = new EsgUniverseData();
                            for (var index = 0; index < esgUniverse.Count ; index++)
                            {
                                switch (index)
                                {
                                    case 0:
                                        esg.PermId = esgUniverse[index];
                                        break;
                                    case 1:
                                        esg.PrimaryRic = esgUniverse[index];
                                        break;
                                    case 2:
                                        esg.CommonName = esgUniverse[index];
                                        break;
                                }
                            }
                            rdpEsgResponse.UniverseData.Add(esg);
                        }
                    }
                }
                rdpEsgResponse.HttpResponseStatusCode = response.StatusCode;
                rdpEsgResponse.HttpResponseStatusText = response.ReasonPhrase;
                return rdpEsgResponse;
            }
            

            var rdpEsgErrorResponse = new RdpEsgError();

            if (response.Content != null)
            {
                var json = await response.Content.ReadAsStringAsync();
                rdpEsgErrorResponse = JObject.Parse(json)["error"]?.ToObject<RdpEsgError>();
            }
            rdpEsgResponse.HttpResponseStatusCode = response.StatusCode;
            rdpEsgResponse.HttpResponseStatusText = response.ReasonPhrase;
            return rdpEsgErrorResponse;

        }
    }


    public class EsgUniverseCache 
    {

        public static IEnumerable<EsgUniverseData> GetDataByPermId(string permId, IList<EsgUniverseData> esgData)
        {
            return esgData.Where(data => (!string.IsNullOrEmpty(data.PermId) && data.PermId.Contains(permId)));
            
        }

        public static IEnumerable<EsgUniverseData> GetDataByRic(string ricName,IList<EsgUniverseData> esgData)
        {
            return  esgData.Where(data => (!string.IsNullOrEmpty(data.PrimaryRic) && data.PrimaryRic.Contains(ricName)));
       
        }
        public static IEnumerable<EsgUniverseData> GetDataByCommonName(string commonName, IList<EsgUniverseData> esgData)
        {
            return esgData.Where(data => (!string.IsNullOrEmpty(data.CommonName) && data.CommonName.Contains(commonName)));
        }
        public static IEnumerable<EsgUniverseData> GetData(string keyword, IList<EsgUniverseData> esgData)
        {
            var list = new List<EsgUniverseData>();
            list.AddRange(EsgUniverseCache.GetDataByRic(keyword,esgData));
            list.AddRange(EsgUniverseCache.GetDataByCommonName(keyword,esgData));
            list.AddRange(EsgUniverseCache.GetDataByPermId(keyword,esgData));
            return list.Distinct();
        }
    }
}
