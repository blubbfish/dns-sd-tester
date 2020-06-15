using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace dns_sd_tester {
  public class WebRequestor {
    private readonly HttpClient client = new HttpClient();
    private static readonly Dictionary<String, WebRequestor> instances = new Dictionary<String, WebRequestor>();

    private String Server {
      get;
    }

    public enum RequestMethod {
      GET,
      POST,
      PUT,
      DELETE
    }

    public static WebRequestor GetInstance(String server) {
      if(!instances.ContainsKey(server)) {
        instances.Add(server, new WebRequestor(server));
      }
      return instances[server];
    }

    private WebRequestor(String server) => this.Server = server;

    public async Task<String> Interact(String address, String json = "", Boolean withoutput = true, RequestMethod method = RequestMethod.GET, Boolean exeptionOnNotSuccess = true) {
      String ret = null;
      try {
        HttpResponseMessage response = null;
        if(method == RequestMethod.POST || method == RequestMethod.PUT) {
          HttpContent content = new StringContent(json);
          content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
          if(method == RequestMethod.POST) {
            response = await this.client.PostAsync(this.Server + address, content);
          } else if(method == RequestMethod.PUT) {
            response = await this.client.PutAsync(this.Server + address, content);
          }
          content.Dispose();
        } else if(method == RequestMethod.GET) {
          response = await this.client.GetAsync(this.Server + address);
        } else if(method == RequestMethod.DELETE) {
          response = await this.client.DeleteAsync(this.Server + address);
        }
        if(!response.IsSuccessStatusCode && exeptionOnNotSuccess) {
          throw new Exception(response.StatusCode + ": " + response.ReasonPhrase);
        }
        if(withoutput && response != null) {
          ret = await response.Content.ReadAsStringAsync();
        }
      } catch(Exception e) {
        throw new WebException($"Error while accessing resource '{address}' on Server '{this.Server}' with method '{method}' and data '{json}'. Error: \n'{e.Message}'");
      }
      return ret;
    }
  }
}
