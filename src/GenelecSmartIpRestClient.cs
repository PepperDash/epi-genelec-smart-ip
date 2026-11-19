using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Core.Logging;

namespace PepperDash.Essentials.Plugins
{
  /// <summary>
  /// Minimal REST client for the Genelec Smart IP unicast API (HTTP/1.1, Basic auth, port 9000).
  /// Requests are serialized (one in flight at a time) and use "Connection: close" because the
  /// device only supports four simultaneous connections.
  /// </summary>
  public class GenelecSmartIpRestClient : IDisposable
  {
    private const string ApiRoot = "/public/v1";

    private readonly IKeyed parent;
    private readonly HttpClient client;
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Creates the client
    /// </summary>
    public GenelecSmartIpRestClient(IKeyed parent, string host, int port, string username, string password, int timeoutMs)
    {
      this.parent = parent;

      client = new HttpClient
      {
        BaseAddress = new Uri(string.Format("http://{0}:{1}", host, port)),
        Timeout = TimeSpan.FromMilliseconds(timeoutMs)
      };
      client.DefaultRequestHeaders.ConnectionClose = true;
      client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", username, password)));
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    /// <summary>
    /// GET a path under /public/v1. Returns null on any failure.
    /// </summary>
    public async Task<JObject> GetAsync(string path)
    {
      return await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, ApiRoot + path)).ConfigureAwait(false);
    }

    /// <summary>
    /// PUT a JSON body to a path under /public/v1. Returns true on 2xx.
    /// </summary>
    public async Task<bool> PutAsync(string path, object body)
    {
      var json = JsonConvert.SerializeObject(body);
      var result = await SendAsync(() => new HttpRequestMessage(HttpMethod.Put, ApiRoot + path)
      {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
      }).ConfigureAwait(false);
      return result != null;
    }

    private async Task<JObject> SendAsync(Func<HttpRequestMessage> requestFactory)
    {
      await gate.WaitAsync().ConfigureAwait(false);
      try
      {
        using (var request = requestFactory())
        using (var response = await client.SendAsync(request).ConfigureAwait(false))
        {
          var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

          if (!response.IsSuccessStatusCode)
          {
            parent.LogWarning("{Method} {Uri} failed: {Status} {Body}", request.Method, request.RequestUri, (int)response.StatusCode, text);
            return null;
          }

          if (string.IsNullOrWhiteSpace(text)) return new JObject();

          try { return JObject.Parse(text); }
          catch (JsonException) { return new JObject(); }
        }
      }
      catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException)
      {
        parent.LogDebug("Request failed: {Message}", ex.Message);
        return null;
      }
      finally
      {
        gate.Release();
      }
    }

    /// <inheritdoc />
    public void Dispose()
    {
      client.Dispose();
      gate.Dispose();
    }
  }
}
