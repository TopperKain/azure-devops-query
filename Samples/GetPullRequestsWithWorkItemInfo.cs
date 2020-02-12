using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.CircuitBreaker;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Location;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WitQuickStarts.Samples
{
    class GetPullRequestsWithWorkItemInfo
    {
        readonly string _uri;
        readonly string _personalAccessToken;
        readonly string repoId;

        public GetPullRequestsWithWorkItemInfo(string url, string pat, string project)
        {
            _uri = url;
            _personalAccessToken = pat;
            repoId = project;
        }


        public async Task GetPRsWithWorkItemInfo()
        {
            try
            {
                Uri uri = new Uri(_uri);
                string personalAccessToken = _personalAccessToken;
                Guid repo = new Guid(repoId);

                VssBasicCredential credentials = new VssBasicCredential("", _personalAccessToken);

                //create instance of work item tracking http client
                using (WorkItemTrackingHttpClient workItemTrackingHttpClient = new WorkItemTrackingHttpClient(uri, credentials))
                using (GitHttpClient gitClient = new GitHttpClient(uri, credentials))
                {
                    List<GitPullRequest> prs = await gitClient.GetPullRequestsAsync(repo, new GitPullRequestSearchCriteria() { Status = PullRequestStatus.Completed });
                    var recentPrs = prs.Where(pr => pr.ClosedDate > DateTime.Now.AddDays(-90));

                    int prCount = recentPrs.Count();
                    int currentPr = 0;
                    List<PullRequestFractional> pullRequests = new List<PullRequestFractional>();
                    foreach (var pr in recentPrs)
                    {
                        currentPr++;
                        Console.WriteLine($"Working n {currentPr} of {prCount} pull requests");
                        var workItemRefs = await gitClient.GetPullRequestWorkItemRefsAsync(repo, pr.PullRequestId);
                        var workItemIds = workItemRefs
                            .Where(wiRef => int.TryParse(wiRef.Id, out int throwaway))
                            .Select(wiRef => int.Parse(wiRef.Id));
                        var createdBy = pr.CreatedBy.DisplayName;
                        var name = pr.Title.Replace(',', ' ');
                        var id = pr.PullRequestId;
                        PullRequestFractional pullRequest = new PullRequestFractional()
                        {
                            name = name,
                            id = id,
                            createdBy = createdBy,
                            workItems = new List<WorkItemFractional>(),
                            totalBugs = 0,
                            totalStoryPoints = 0,
                            totalUserStories = 0,
                            totalWorkItems = 0
                        };

                        pullRequests.Add(pullRequest);

                        foreach (var workItemRef in workItemIds)
                        {
                            var workItem = await workItemTrackingHttpClient.GetWorkItemAsync(workItemRef);
                            string storyPoints = string.Empty;
                            string workItemType = string.Empty;
                            string workItemTitle = string.Empty;
                            if (workItem.Fields.ContainsKey("Microsoft.VSTS.Scheduling.StoryPoints")){
                                storyPoints = workItem.Fields["Microsoft.VSTS.Scheduling.StoryPoints"]?.ToString();
                            }
                            if (workItem.Fields.ContainsKey("System.WorkItemType"))
                            {
                                workItemType = workItem.Fields["System.WorkItemType"]?.ToString();
                            }
                            if (workItem.Fields.ContainsKey("System.Title"))
                            {
                                workItemTitle = workItem.Fields["System.Title"]?.ToString();
                            }

                            var workItemFrac = new WorkItemFractional()
                            {
                                id = workItemRef,
                                workItemType = workItemType,
                                workItemTitle = workItemTitle,
                                storyPoints = string.IsNullOrEmpty(storyPoints) ? 0 : double.Parse(storyPoints)

                            };

                            switch (workItemFrac.workItemType.ToUpperInvariant())
                            {
                                case "USER STORY":
                                    pullRequest.totalWorkItems++;
                                    pullRequest.totalUserStories++;
                                    break;
                                case "BUG":
                                    pullRequest.totalWorkItems++;
                                    pullRequest.totalBugs++;
                                    break;
                            }

                            pullRequest.totalStoryPoints += workItemFrac.storyPoints;
                            pullRequest.dividedStoryPoints = 0;
                            pullRequest.workItems.Add(workItemFrac);
                        }
                    }

                    // Add up the divided story points (when the  work item is associated with multiple PRs, don't double count the points- divide them equally between PRs)
                    var distinctWorkItems = pullRequests
                        .SelectMany(pr => pr.workItems)
                        .Where(workItem => workItem.storyPoints > 0)
                        .Select(workItem => new { workItem.id, workItem.storyPoints })
                        .Distinct();

                    foreach (var workItem in distinctWorkItems)
                    {
                        var prsAssociatedWithWorkItem = pullRequests.Where(pr => pr.workItems.Any(wi => wi.id == workItem.id));
                        double associatedPrCount = (double)prsAssociatedWithWorkItem.Count();
                        double storyPoints = workItem.storyPoints;
                        double dividedPoints = storyPoints / associatedPrCount;

                        foreach (var assoicatedPr in prsAssociatedWithWorkItem)
                        {
                            assoicatedPr.dividedStoryPoints += dividedPoints;
                        }
                    }

                    Console.WriteLine("finishing");

                    var memStream = new MemoryStream();
                    
                    using (var consoleWriter = new StreamWriter(memStream))
                    {
                        var csv = new CsvWriter(consoleWriter, new CsvConfiguration(CultureInfo.InvariantCulture));
                        csv.WriteRecords(pullRequests);
                        consoleWriter.Flush();
                    }

                    memStream.Flush();
                    byte[] byteArray = memStream.ToArray();
                    string output1 = Encoding.ASCII.GetString(byteArray, 0, byteArray.Length);
                    Console.WriteLine(output1);
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public class WorkItemFractional : IEquatable<WorkItemFractional>, IComparable<WorkItemFractional>
        {
            public int id { get; set; }
            public string workItemType { get; set; }
            public double storyPoints { get; set; }
            public string workItemTitle { get; set; }

            public int CompareTo(WorkItemFractional other)
            {
                return this.id.CompareTo(other.id);
            }

            public bool Equals(WorkItemFractional other)
            {
                return id == other.id;
            }
        }

        public class PullRequestFractional
        {
            public int id { get; set; }
            public string name { get; set; }
            public string createdBy { get; set; }
            public double totalStoryPoints { get; set; }
            public int totalWorkItems { get; set; } 
            public int totalBugs { get; set; }
            public int totalUserStories { get; set; }
            public double dividedStoryPoints { get; set; }

            public List<WorkItemFractional> workItems { get; set; }
        }
    }
}
