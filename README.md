# appsvc-fnc-disableadmin-dotnet001

This script is use to disable an admin account the the user did not login in the last 28 days.
The 28 days is use because the signin logs are keeps for only 30 days. After that the logs are delete and we can know if the user did not login in the pass 30 or never login.

This function app is a script that run on a timer. Everyday at 2am.

It connect to a group and get all the member id of this group.
For each member (on exception from the exception array in the settings) it gets the signlogs.
If the signinlogs is older than 28 days, the script will disable the user and its send an email to a mailbox.

## App settings
adminGroup: Id of the group where all user (admin) are <br>
clientId: App registration with aufitlogs.read.all, groupMember.read.all, mail.send, user.readwrite.all <br>
clientSecret: secret of the app reg <br>
tenantId: id of the tenant <br>
requesterEmail: mailbox where the email is send when a user is disable <br>
emailSender: mailbox that send the email <br>
exceptionUser: list of user that is exclude (EX: 1234, 4321, 5678)
