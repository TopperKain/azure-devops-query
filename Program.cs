using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WitQuickStarts.Samples;

namespace WitQuickStarts
{
    public class Program
    {
        public static int Main(string[] args)
        {
            //if (args.Length == 0)
            //{
            //    ShowUsage();
            //    return 0;
            //}

            string connectionUrl, token, repoId = "";

            try
            {
                CheckArguments(args, out connectionUrl, out token, out repoId);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex.Message);

                ShowUsage();

                return -1;
            }

            try
            {
                Console.WriteLine("Executing quick start samples...");
                Console.WriteLine("");

                //todo: may need to adjust this to scale better for more samples.

                ////instantiate objects
                //Samples.CreateBug objBug = new Samples.CreateBug(connectionUrl, token, repoId);
                //Samples.ExecuteQuery objQuery = new Samples.ExecuteQuery(connectionUrl, token, repoId);
                var objPrsByWorkItem = new GetPullRequestsWithWorkItemInfo(connectionUrl, token, repoId);
                objPrsByWorkItem.GetPRsWithWorkItemInfo().Wait();


                ////execute the client lib code. If you want to run the direct http calls then adjust (see below)
                //objBug.CreateBugUsingClientLib();
                //objQuery.RunGetBugsQueryUsingClientLib();

                //objBug.CreateBugUsingHTTP();
                //objQuery.RunGetBugsQueryUsingHTTP();

                //objBug = null;
                //objQuery = null;

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to run the sample: " + ex.Message);
                return 1;
            }

            return 0;
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Runs the WIT Quick Start samples on a Team Services account or Team Foundation Server instance.");
            Console.WriteLine("");
            Console.WriteLine("These samples are to provide you the building blocks of using the REST API's in Work Item Tracking.");
            Console.WriteLine("Examples are written using the .NET client library and using direct HTTP calls. We recommend, that");
            Console.WriteLine("whenever possible, you use the .NET client library.");
            Console.WriteLine("");
            Console.WriteLine("!!WARNING!! Some samples are destructive. Always run on a test account or collection.");
            Console.WriteLine("");
            Console.WriteLine("Arguments:");
            Console.WriteLine("");
            Console.WriteLine("  /url:fabrikam.visualstudio.com /token:personalaccesstoken /repoId:repoGuid");
            Console.WriteLine("");

            Console.ReadKey();
        }

        private static void CheckArguments(string[] args, out string connectionUrl, out string token, out string repoId)
        {
            connectionUrl = null;
            token = null;
            repoId = null;
            
            Dictionary<string, string> argsMap = new Dictionary<string, string>();
            foreach (var arg in args)
            {
                if (arg[0] == '/' && arg.IndexOf(':') > 1)
                {
                    string key = arg.Substring(1, arg.IndexOf(':') - 1);
                    string value = arg.Substring(arg.IndexOf(':') + 1);

                    switch (key)
                    {
                        case "url":
                            connectionUrl = value;
                            break;

                        case "token":
                            token = value;                          
                            break;

                        case "repoId":
                            repoId = value;
                            break;
                        default:
                            throw new ArgumentException("Unknown argument", key);
                    }
                }
            }

            if (connectionUrl == null)
            {
                Console.WriteLine("Provide Connection URL");
                connectionUrl = Console.ReadLine();
            }

            if (token == null)
            {

                Console.WriteLine("Provide Personal Access Token (PAT)");
                token = Console.ReadLine();
            }

            if (repoId == null)
            {

                Console.WriteLine("Provide repoId)");
                repoId = Console.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(connectionUrl) || string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Missing required arguments");
            }
        }


    }
}
