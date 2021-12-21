using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace appsvc_fnc_disableadmin_dotnet001
{
    public static class disableAdmin
    {
        [FunctionName("disableAdmin")]
        //Run every day at 2am
        public static async Task Run([TimerTrigger("0 0 2 * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables()
           .Build();

            var adminGroup = config["adminGroup"];
            var requesterEmail = config["requesterEmail"];
            var emailSender = config["emailSender"];

            Auth auth = new Auth();
            var graphAPIAuth = auth.graphAuth(log);
            await DisableAdmin(graphAPIAuth, adminGroup, requesterEmail, emailSender, exceptionUser, log);
        }

        public static async Task<bool> DisableAdmin(GraphServiceClient graphAPIAuth, string adminGroup, string requesterEmail, string emailSender, string exceptionUser, ILogger log)
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

                            //If the count of signIn is 0, this mean the user never signIn
                            if (signInCount >= 1)
                            {
                                foreach (var userLogs in signIn)
                                {
                                    //Check if the user signIn in the last 28 days. 
                                    //If yes, stop this foreach and check the next member
                                    DateTime expiryDate = DateTime.Now - TimeSpan.FromDays(28);
                                    if (userLogs.CreatedDateTime > expiryDate)
                                    {
                                        log.LogInformation($" user {userLogs.UserDisplayName} - {userLogs.CreatedDateTime} - {expiryDate}");
                                        break;
                                    }
                                    else
                                    {
                                        //User did not signIn in the last 28 days
                                        // Disable the acount
                                        log.LogInformation($"Disable user {admin.Id}");
                                        var user = new User
                                        {
                                            AccountEnabled = false
                                        };

                                        await graphAPIAuth.Users[admin.Id]
                                            .Request()
                                            .UpdateAsync(user);

                                        var MessageDisableAdmin = new Message
                                        {
                                            Subject = "Admin account is disable",
                                            Body = new ItemBody
                                            {
                                                ContentType = BodyType.Html,
                                                Content = $"Hi,<br><br>The admin account with id <strong>{admin.Id}</strong> got disable because the user did not login in the last 28 days.<br><br>Thank you"
                                            },
                                            ToRecipients = new List<Recipient>()
                                        {
                                            new Recipient
                                            {
                                                EmailAddress = new EmailAddress
                                                {
                                                    Address = $"{requesterEmail}"
                                                }
                                            }
                                        },
                                        };
                                        await graphAPIAuth.Users[emailSender]
                                            .SendMail(MessageDisableAdmin)
                                            .Request()
                                            .PostAsync();

                                        log.LogInformation($"Send email to {requesterEmail} successfully.");
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
                            log.LogInformation($"Error - {admin.Id} - {ex}");
                        }
                    }
                } 
            catch (Exception ex)
            {
                log.LogInformation($"Error - {ex}");
            }
            return true;
        }
    }
}