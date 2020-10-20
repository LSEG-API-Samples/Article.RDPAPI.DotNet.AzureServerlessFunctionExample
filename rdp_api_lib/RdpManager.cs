using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace rdp_api_lib
{
    public interface IRdpResponseMessage
    {
        HttpStatusCode HttpResponseStatusCode { get; set; }
        bool IsSuccess { get; }
        string HttpResponseStatusText { get; set; }

    }
    public class RdpAuthenticationError: IRdpResponseMessage
    {
        [Newtonsoft.Json.JsonProperty("error", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Error { get; set; }

        [Newtonsoft.Json.JsonProperty("error_description", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string ErrorDescription { get; set; }

        [Newtonsoft.Json.JsonProperty("error_uri", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string ErrorUri { get; set; }
        public HttpStatusCode HttpResponseStatusCode { get; set; }
        public bool IsSuccess => HttpResponseStatusCode == HttpStatusCode.OK;
        public string HttpResponseStatusText { get; set; }
        public override string ToString()
        {
            var dumpText = new StringBuilder();
            dumpText.Append($"HTTP Status Code:{this.HttpResponseStatusCode}\n");
            dumpText.Append($"HttpResponseStatusText:{this.HttpResponseStatusText}\n");
            dumpText.Append($"===============================\n");
            dumpText.Append($"Error:{this.Error}\n");
            dumpText.Append($"Error Description:{this.ErrorDescription}\n");
            dumpText.Append($"Error Uri:{this.ErrorUri}\n");
            dumpText.Append($"==============================\n");
            return dumpText.ToString();
        }
    }
    public class RdpTokenResponse: IRdpResponseMessage
    {
        [Newtonsoft.Json.JsonProperty("access_token", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string AccessToken { get; set; }

        [Newtonsoft.Json.JsonProperty("expires_in", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public long ExpiresIn { get; set; }

        [Newtonsoft.Json.JsonProperty("refresh_token", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string RefreshToken { get; set; }

        [Newtonsoft.Json.JsonProperty("scope", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string Scope { get; set; }

        [Newtonsoft.Json.JsonProperty("token_type", DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
        public string TokenType { get; set; }

        public HttpStatusCode HttpResponseStatusCode { get; set; }

        public string HttpResponseStatusText { get; set; }

        public bool IsSuccess => HttpResponseStatusCode == HttpStatusCode.OK;
        public override string ToString()
        {
            var dumpText=new StringBuilder();
            dumpText.Append($"HTTP Status Code:{this.HttpResponseStatusCode}\n");
            dumpText.Append($"HttpResponseStatusText:{this.HttpResponseStatusText}\n");
            dumpText.Append($"===============================\n");
            dumpText.Append($"AccessToken:{this.AccessToken}\n");
            dumpText.Append($"ExpiresIn:{this.ExpiresIn} second\n");
            dumpText.Append($"RefreshToken:{this.RefreshToken}\n");
            dumpText.Append($"Scope:{this.Scope}\n");
            dumpText.Append($"TokenType:{this.TokenType}\n");
            dumpText.Append($"==============================\n");
            return dumpText.ToString();
        }
    }

    

    public interface IRdpAuthorizeService
    {
        Task<IRdpResponseMessage> GetToken(string username, string password, string client_id, string scope = "trapi", string refreshToken = "",
            bool useRefreshToken = false,string redirectUrl="");
    }

    public class RdpAuthorizeService : IRdpAuthorizeService
    {
        private readonly IHttpClientFactory _clientFactory;

        public RdpAuthorizeService(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task<IRdpResponseMessage> GetToken(string username, string password, string client_id,string scope="trapi",string refreshToken = null,
            bool useRefreshToken = false, string redirectUrl= null)
        {
            var tokenUri = new UriBuilder()
            {
                Scheme = "https",
                Host = RdpEndpoints.RdpServer,
                Path = RdpEndpoints.AuthTokenService

            };
            if (!string.IsNullOrEmpty(redirectUrl))
            {
                tokenUri=new UriBuilder(redirectUrl);
            }
            var request = new HttpRequestMessage(HttpMethod.Post, tokenUri.ToString());


            var queryStringKV = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("client_id", client_id)
            };

            if (useRefreshToken)
            {
                queryStringKV.Add(new KeyValuePair<string, string>("grant_type","refresh_token"));
                queryStringKV.Add(new KeyValuePair<string, string>("refresh_token", refreshToken));
            }
            else
            {
                queryStringKV.Add(new KeyValuePair<string, string>("takeExclusiveSignOnControl","True"));
                queryStringKV.Add(new KeyValuePair<string, string>("scope", scope));
                queryStringKV.Add(new KeyValuePair<string, string>("grant_type", "password"));
                queryStringKV.Add(new KeyValuePair<string, string>("password",password));
            }
            // Set Content 
            request.Content = new FormUrlEncodedContent(queryStringKV);

            // Set Request Headers
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            request.Headers.Add("AllowAutoRedirect","False");
            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);

            IRdpResponseMessage rdpTokenResult = new RdpTokenResponse();
            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TransferEncodingChunked==true || response.Content != null)
                {
                    var json= await response.Content.ReadAsStringAsync();
                    rdpTokenResult = JsonConvert.DeserializeObject<RdpTokenResponse>(json);
                }
                rdpTokenResult.HttpResponseStatusCode = response.StatusCode;
                rdpTokenResult.HttpResponseStatusText = response.ReasonPhrase;
                return rdpTokenResult;
            }

           
          
            switch (response.StatusCode)
            {
                case HttpStatusCode.Moved: // 301
                case HttpStatusCode.Redirect: // 302
                case HttpStatusCode.TemporaryRedirect: // 307
                case (HttpStatusCode) 308: // 308 Permanent Redirect
                {
                    // Perform URL redirect
                    var newLocation = response.Headers.Location.ToString();
                    if (!string.IsNullOrEmpty(newLocation))
                        return await GetToken(username, password, client_id, scope, refreshToken, useRefreshToken,
                            newLocation);
                }
                    break;
            }
            
            rdpTokenResult =new RdpAuthenticationError();

            if (response.Content != null)
            {
                var json = await response.Content.ReadAsStringAsync();
                rdpTokenResult=JsonConvert.DeserializeObject<RdpAuthenticationError>(json);
            }
            rdpTokenResult.HttpResponseStatusCode = response.StatusCode;
            rdpTokenResult.HttpResponseStatusText = response.ReasonPhrase;
            return rdpTokenResult;
        }
    }
}
