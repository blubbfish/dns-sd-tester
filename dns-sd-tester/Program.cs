using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Makaretu.Dns;

namespace dns_sd_tester {
  class Program {

    private Dictionary<DnsType, ResourceRecord> DiscoverMulticastService(String address) {
      Console.WriteLine($"Make Request for '{address}'");
      Message query = new Message();
      query.Questions.Add(new Question { Name = address, Type = DnsType.ANY });
      MulticastService mdns = new MulticastService();
      mdns.Start();
      Task<Message> answer = mdns.ResolveAsync(query);
      List<ResourceRecord> data = new List<ResourceRecord>();
      data.AddRange(answer.Result.Answers);
      data.AddRange(answer.Result.AdditionalRecords);

      Dictionary<DnsType, ResourceRecord> ret = new Dictionary<DnsType, ResourceRecord>();

      Console.WriteLine("Get answer for the request");
      if(answer.Status == TaskStatus.RanToCompletion) {
        foreach(ResourceRecord item in data) {
          Console.WriteLine($"{item.Type}: " + item);
          if(!ret.ContainsKey(item.Type)) {
            ret.Add(item.Type, item);
          }
        }
        Console.WriteLine();
      }
      return ret;
    }

    private String ParseDNSAnswer(Dictionary<DnsType, ResourceRecord> answer) {
      Console.WriteLine("Parse the DNS Answer");
      if((answer.ContainsKey(DnsType.A) || answer.ContainsKey(DnsType.AAAA)) && answer.ContainsKey(DnsType.SRV) && answer.ContainsKey(DnsType.TXT)) {
        TXTRecord txt_item = answer[DnsType.TXT] as TXTRecord;
        Dictionary<String, String> text_dic = new Dictionary<String, String>();
        foreach(String item in txt_item.Strings) {
          String[] item_split = item.Split("=");
          text_dic.Add(item_split[0], item_split[1]);
        }
        if(text_dic.ContainsKey("td")) {
          Console.WriteLine($"{txt_item.Type}: " + $"'{txt_item.Name}' {String.Join(" ", txt_item.Strings.ToArray())}");

          Boolean using_a = false;
          if(answer.ContainsKey(DnsType.A)) {
            using_a = true;
          }

          AddressRecord aa_item;
          if(using_a) {
            aa_item = answer[DnsType.A] as AddressRecord;
            Console.WriteLine($"{aa_item.Type}: " + $"host '{aa_item.Name}' at '{aa_item.Address}'");
          } else {
            aa_item = answer[DnsType.AAAA] as AddressRecord;
            Console.WriteLine($"{aa_item.Type}: " + $"host '{aa_item.Name}' at '{aa_item.Address}'");
          }

          SRVRecord srv_item = answer[DnsType.SRV] as SRVRecord;
          Console.WriteLine($"{srv_item.Type}: " + $"service '{srv_item.Name}' on '{srv_item.Target}' at '{srv_item.Port}'");
          
          String ret = $"http://{aa_item.Address}:{srv_item.Port}{text_dic["td"]}";
          Console.WriteLine($"Combine Answer to {ret}\n");
          return ret;
        }
      }
      return "";
    }

    private String GetTDJson(String guid = "") {
      Dictionary<String, Object> jsonobject = new Dictionary<String, Object>() {
        { "@context", "https://www.w3.org/2019/wot/td/v1" },
        { "title", "DNS-SD Test Entry" },
        { "securityDefinitions", new Dictionary<String, Object>() { { "nosec_sc", new Dictionary<String, Object>() { { "scheme", "nosec" } } } } },
        { "security", new List<String> () { "nosec_sc" } },
        { "events", new Dictionary<String, Object>() {
          { "sensordata", new Dictionary<String, Object>() {
            { "data", new Dictionary<String, String> () {
              { "type", "object" }
            } },
            { "forms", new List<Object>() {
              new Dictionary<String, String> () {
                { "href", "mqtt://mqtt.example.org:1883/esp8266/sensor/" },
                { "contentType", "text/plain" },
                { "op", "subscribeevent" }
              }
            } }
          } }
        } }
      };
      if(guid != "") {
        jsonobject.Add("id", guid);
      }
      return JsonSerializer.Serialize(jsonobject);
    }


    public Program() {
      String conn = this.ParseDNSAnswer(this.DiscoverMulticastService("_wot._tcp.local"));
      String guid = $"urn:dev:ops:{Environment.MachineName}_dns-sd-tester";

      /*Console.WriteLine("Read Registerd devices:");
      Console.WriteLine(WebRequestor.GetInstance(conn).Interact("").Result);
      */

      Console.WriteLine("Read registerd devices:");
      String jsonresult = WebRequestor.GetInstance(conn).Interact("").Result;
      Console.WriteLine(jsonresult);

      Console.WriteLine("\nDelete every registerd devices:");
      Dictionary<String, JsonElement> queryresult = (Dictionary<String, JsonElement>)JsonSerializer.Deserialize(jsonresult, typeof(Dictionary<String, JsonElement>));
      foreach(JsonElement item in queryresult["items"].EnumerateArray()) {
        foreach(JsonProperty props in item.EnumerateObject()) {
          if(props.Name.ToLower() == "id") {
            Console.WriteLine($"Delete Device {props.Value.GetString()}: " + WebRequestor.GetInstance(conn).Interact($"/{props.Value.GetString()}", "", true, WebRequestor.RequestMethod.DELETE, false).Result);
          }
        }
      }

      Console.WriteLine("\nCreates new Thing Description with system-generated ID");
      Console.WriteLine(WebRequestor.GetInstance(conn).Interact("/", this.GetTDJson(), true, WebRequestor.RequestMethod.POST).Result);

      Console.WriteLine($"Creates new Thing Description with {guid} ID");
      Console.WriteLine(WebRequestor.GetInstance(conn).Interact($"/{guid}", this.GetTDJson(guid), true, WebRequestor.RequestMethod.PUT).Result);

      Console.WriteLine("\nQuery all id's from Elements with Test in title.");
      String jsonidlist = WebRequestor.GetInstance(conn).Interact("?jsonpath=$[?(@.title =~ /Test/i)].id").Result;
      Console.WriteLine(jsonidlist);

      JsonElement idlist = (JsonElement)JsonSerializer.Deserialize(jsonidlist, typeof(JsonElement));

    }

    public static void Main() => new Program();
  }
}
