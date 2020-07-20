using SpotifyAPI.Web;
using System;
using System.Threading.Tasks;

namespace Toastify
{
  internal static partial class SpotifyApiClient
  {
    // override these in SpotifyApiClient.private.cs.
    // If you don't have such a file, create one using the following template:
    // Note: missing " on purpose so that it doesn't build if you just dump it in :)
    //
    /* 
    namespace Toastify
    {
      internal static partial class SpotifyApiClient
      {
        static SpotifyApiClient()
        {
          _CLIENT_ID = INSERT_REAL_VALUE;
          _CLIENT_SECRET = INSERT_REAL_VALUE;
        }
      }
    }
    */
    private static readonly string _CLIENT_ID = "CHANGEME";
    private static readonly string _CLIENT_SECRET = "CHANGEME";

    private static readonly SpotifyClientConfig _spotifyClientConfig = SpotifyClientConfig.CreateDefault();
    private static ClientCredentialsTokenResponse _tokenResponse;

    private static SpotifyClient _spotifyClient;

    public static async Task<SpotifyClient> GetAsync()
    {

      if (_spotifyClient == null || _tokenResponse == null || _tokenResponse.IsExpired)
      {
        if (_CLIENT_ID == "CHANGEME" || _CLIENT_SECRET == "CHANGEME")
        {
          throw new Exception("You need to override _CLIENT_ID and _CLIENT_SECRET with the values from your Spotify dev account");
        }

        var request = new ClientCredentialsRequest(_CLIENT_ID, _CLIENT_SECRET);
        _tokenResponse = await new OAuthClient(_spotifyClientConfig).RequestToken(request);

        _spotifyClient = new SpotifyClient(_spotifyClientConfig.WithToken(_tokenResponse.AccessToken));
      }


      return _spotifyClient;
    }

  }
}
