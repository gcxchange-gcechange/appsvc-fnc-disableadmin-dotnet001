using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace appsvc_fnc_disableadmin_dotnet001
{
    public static class disableAdmin
    {
        [FunctionName("disableAdmin")]
        // public static async Task<IActionResult> Run([TimerTrigger("0 30 9 * * Sun")]TimerInfo myTimer, ILogger log)
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables()
           .Build();

            var adminGroup = config["adminGroup"];

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            var result = await CallMSFunction(graphAPIAuth, adminGroup, log);

            string responseMessage = result
               ? "Work as it should"
               : $"Something went wrong. Check the logs";

            return new OkObjectResult(responseMessage);
        }

        public static async Task<bool> CallMSFunction(Microsoft.Graph.GraphServiceClient graphAPIAuth, string adminGroup, ILogger log)
        {
            try
            {
                //Get all member of the admin group
                var AdminMembers = await graphAPIAuth.Groups[adminGroup].Members
                               .Request()
                               .GetAsync();

                foreach (DirectoryObject admin in AdminMembers)
                {
                    //Filter the sign logs on each member
                    try
                    {
                        log.LogInformation($"Get signin of {admin.Id}");
                        var signIn = await graphAPIAuth.AuditLogs.SignIns
                               .Request()
                               .Filter($"userId eq '{admin.Id}'")
                               .GetAsync();
                        var signInCount = signIn.Count;
                        log.LogInformation($"signIn number {signInCount}");

                        //If the count of signIn is 0, this mean the user never signIn in the last 30days
                        if (signInCount >= 1)
                        {
                            foreach (var userLogs in signIn)
                            {
                                //Check if the user signIn in the last 30 days. 
                                //If yes, stop this foreach and check the next member
                                DateTime expiryDate = DateTime.Now - TimeSpan.FromDays(30);
                                if (userLogs.CreatedDateTime > expiryDate)
                                {
                                    log.LogInformation($" user {userLogs.UserDisplayName} - {userLogs.CreatedDateTime} - {expiryDate}");
                                    break;
                                }
                                else
                                {
                                    //User did not signIn in the last 30 days
                                    log.LogInformation($"{admin.Id}");
                                }
                            }
                        }
                        else
                        {
                            //No signIn info for this user
                            log.LogInformation($"No sign In info for {admin.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogInformation($"no logs or error - {admin.Id} - {ex}");
                    }
                } 
            }
            catch (Exception ex)
            {
                log.LogInformation($"{ex}");
            }

        
            return true;
        }
    }
}
