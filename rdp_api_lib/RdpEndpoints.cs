namespace rdp_api_lib
{
    /// <summary>
    /// Class to hold RDP service endpoint required by the main application
    /// </summary>
    public class RdpEndpoints
    {
        public static readonly string RdpServer=$"api.refinitiv.com";
        public static readonly string AuthTokenService = $"auth/oauth2/v1/token";
        public static readonly string EsgUniverse = $"data/environmental-social-governance/v1/universe";
    }

}
