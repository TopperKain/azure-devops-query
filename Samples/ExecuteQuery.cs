using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi;
using System.Net.Http.Headers;
using System.Net.Http;
using Newtonsoft.Json;

namespace WitQuickStarts.Samples
{
    
    public class ExecuteQuery
    {
        readonly string _uri;
        readonly string _personalAccessToken;
        readonly string _project;

        /// <summary>
        /// Constructor. Manaully set values to match your account.
        /// </summary>
        public ExecuteQuery()
        {
            _uri = "https://accountname.visualstudio.com";
            _personalAccessToken = "personal access token";
            _project = "project name";
        }

        public ExecuteQuery(string url, string pat, string project)
        {
            _uri = url;
            _personalAccessToken = pat;
            _project = project;
        }
        /// <summary>
        /// Execute a WIQL query to reutnr a list of bugs using the .NET client library
        /// </summary>
        /// <returns>List of Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem</returns>
        public List<WorkItem> RunGetBugsQueryUsingClientLib()
        {
            Uri uri = new Uri(_uri);
            string personalAccessToken = _personalAccessToken;
            string project = _project;

            VssBasicCredential credentials = new VssBasicCredential("", _personalAccessToken);

            //create a wiql object and build our query
            Wiql wiql = new Wiql()
            {
                Query = "Select [State], [Title] " +
                        "From WorkItems " +
                        "Where [Work Item Type] = 'Bug' " +
                        "And [System.TeamProject] = '" + project + "' " +
                        "And [System.State] <> 'Closed' " +
                        "Order By [State] Asc, [Changed Date] Desc"
            };

            //create instance of work item tracking http client
            using (WorkItemTrackingHttpClient workItemTrackingHttpClient = new WorkItemTrackingHttpClient(uri, credentials))
            {
                //execute the query to get the list of work items in teh results
                WorkItemQueryResult workItemQueryResult = workItemTrackingHttpClient.QueryByWiqlAsync(wiql).Result;

                //some error handling                
                if (workItemQueryResult.WorkItems.Count() != 0)
                {
                    //need to get the list of our work item id's and put them into an array
                    List<int> list = new List<int>();
                    foreach (var item in workItemQueryResult.WorkItems)
                    {
                        list.Add(item.Id);
                    }
                    int[] arr = list.ToArray();

                    //build a list of the fields we want to see
                    string[] fields = new string[3];
                    fields[0] = "System.Id";
                    fields[1] = "System.Title";
                    fields[2] = "System.State";

                    //get work items for the id's found in query
                    var workItems = workItemTrackingHttpClient.GetWorkItemsAsync(arr, fields, workItemQueryResult.AsOf).Result;

                    Console.WriteLine("Query Results: {0} items found", workItems.Count);

                    //loop though work items and write to console
                    foreach (var workItem in workItems)
                    {
                        Console.WriteLine("{0}          {1}                     {2}", workItem.Id, workItem.Fields["System.Title"], workItem.Fields["System.State"]);
                    }

                    return workItems;
                }

                return null;
            }
        }

        /// <summary>
        /// Execute a WIQL query to return a list of bugs using HTTP call
        /// </summary>
        /// <returns>HttpWorkItems</returns>
        public HttpWorkItems RunGetBugsQueryUsingHTTP()
        {
            string uri = _uri;
            string personalAccessToken = _personalAccessToken;
            string project = _project;
            string credentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", personalAccessToken)));

            //create wiql object
            var wiql = new
            {
                query = "Select [State], [Title] " +
                        "From WorkItems " +
                        "Where [Work Item Type] = 'Bug' " +
                        "And [System.TeamProject] = '" + project + "' " +
                        "And [System.State] <> 'Closed' " +
                        "Order By [State] Asc, [Changed Date] Desc"
            };

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(uri);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                //serialize the wiql object into a json string   
                var postValue = new StringContent(JsonConvert.SerializeObject(wiql), Encoding.UTF8, "application/json"); //mediaType needs to be application/json for a post call

                //send qeury to REST endpoint to return list of id's from query
                var method = new HttpMethod("POST");
                var httpRequestMessage = new HttpRequestMessage(method, uri + "/_apis/wit/wiql?api-version=2.2") { Content = postValue };
                var httpResponseMessage = client.SendAsync(httpRequestMessage).Result;

                if (httpResponseMessage.IsSuccessStatusCode)
                {

                    //get results and bind to WorkItemsQueryResult object (from .net lib)
                    WorkItemQueryResult workItemQueryResult = httpResponseMessage.Content.ReadAsAsync<WorkItemQueryResult>().Result;

                    //now that we have a bunch of work items, build a list of id's so we can get details
                    var builder = new System.Text.StringBuilder();
                    foreach (var item in workItemQueryResult.WorkItems)
                    {
                        builder.Append(item.Id.ToString()).Append(",");
                    }

                    //clean up string of id's
                    string ids = builder.ToString().TrimEnd(new char[] { ',' });

                    //get work item details for id's returned in query
                    HttpResponseMessage getWorkItemsHttpResponse = client.GetAsync("_apis/wit/workitems?ids=" + ids + "&fields=System.Id,System.Title,System.State&asOf=" + workItemQueryResult.AsOf + "&api-version=2.2").Result;

                    if (getWorkItemsHttpResponse.IsSuccessStatusCode)
                    {
                        var workItems = getWorkItemsHttpResponse.Content.ReadAsAsync<HttpWorkItems>().Result;

                        Console.WriteLine("Query Results: {0} items found", workItems.count);

                        //loop through results and write to console
                        foreach (var workItem in workItems.value)
                        {
                            Console.WriteLine("{0}          {1}                     {2}", workItem.id, workItem.fields.SystemTitle, workItem.fields.SystemState);
                        }

                        return workItems;
                    }
                    else
                    {
                        Console.WriteLine("Error getting list of work items: " + getWorkItemsHttpResponse.ReasonPhrase);
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("Error executing query to get bugs: " + httpResponseMessage.ReasonPhrase);
                    return null;
                }
            }
        }

    }

    //workitems classes for http endpoint
    public class HttpWorkItems
    {
        public int count { get; set; }
        public WorkItem[] value { get; set; }

        public class WorkItem
        {
            public int id { get; set; }
            public int rev { get; set; }
            public Fields fields { get; set; }
            public string url { get; set; }
        }

        //simplified version of Fields. Add other work item fields as needed
        public class Fields
        {
            public string SystemWorkItemType { get; set; }
            [JsonProperty(PropertyName = "System.State")]
            public string SystemState { get; set; }
            [JsonProperty(PropertyName = "System.Title")]
            public string SystemTitle { get; set; }

        }
    }

}