using System;
using System.Data;
using System.Net.Mail;
using System.Net.NetworkInformation;

namespace EvSeparationECIntegration
{
    internal class Program
    {

        static void Main(string[] args)
        {
            RunAndNotify("atos");
            RunAndNotify("eviden");
        }

        static void RunAndNotify(string companyArea)
        {
            Guid runId = RunSeparationECScheduler(companyArea);
			NotifyStatus(companyArea, runId);
        }

        private static Guid RunSeparationECScheduler(string companyArea)
        {
            // Generate a unique RunId for each Scheduler Run
            Guid runId = Guid.NewGuid();

            // Record the start time of the entire process
            DateTime processStart = DateTime.Now;
            int totalRecords = 0, successCount = 0, failedCount = 0;

            try
            {
                // Load API configuration values from the database
                var apiConfig = DatabaseHelper.GetApiConfigDictionary(companyArea, "EVSeparationECIntegration");

                // Generate initial token
                var tokenInfo = ApiService.RefreshToken(companyArea, apiConfig, runId);

                if (tokenInfo == null) //if null value received abort further processing.
                    return runId;

                // Fetch pending employee separation records from the database
                var records = DatabaseHelper.GetPendingSeparationEC(companyArea);
                totalRecords = records.Count;

                // Process each record one by one
                foreach (var record in records)
                {
                    try
                    {
                        // Check if the token is expired and refresh if needed
                        if (DateTime.UtcNow >= tokenInfo.ExpiresAt)
                        {
                            tokenInfo = ApiService.RefreshToken(companyArea, apiConfig, runId);
                            if (tokenInfo == null) //if null value received abort further processing.
                                return runId;
                        }

                        // Retrieve userId from SuccessFactors using the username (DASID)
                        string userId = ApiService.GetUserId(companyArea, tokenInfo.Token, record.DASID, apiConfig, runId, record.ResignationId);

                        // If userId is not found, skip this record (error already logged in function ApiService.GetUserIdAsync)
                        if (string.IsNullOrWhiteSpace(userId))
                        {
                            failedCount++;
                            continue;
                        }

                        // Call the termination API
                        record.SFUserId = userId;
                        bool isSuccess = ApiService.CallEmploymentTermination(companyArea, record, apiConfig, tokenInfo.Token, runId);

                        if (isSuccess)
                        {
                            // If successful, update the status in the database
                            DatabaseHelper.UpdateSeparationECStatus(companyArea, record.ResignationId);
                            successCount++;
                        }
                        else
                        {
                            failedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        DatabaseHelper.LogSeparationECStatusFile(companyArea, $"[ERROR] DASID: {record.DASID} - {ex.Message}");
                        failedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                DatabaseHelper.LogSeparationECStatusFile(companyArea, $"[FATAL ERROR] {ex}");
            }
            finally
            {
                DateTime processEnd = DateTime.Now;
                LogSummary(companyArea, totalRecords, successCount, failedCount, processStart, processEnd, runId);
            }
            return runId; // return to main for email notification
        }

        private static void NotifyStatus(string companyArea, Guid runId)
        {
            DataSet logRecords = DatabaseHelper.GetSeparationECAPILog(companyArea, runId);

            if (logRecords != null)
            {
                var apiConfig = DatabaseHelper.GetApiConfigDictionary(companyArea, "EVSeparationECIntegrationEmail");
                if (logRecords.Tables[0].Rows.Count > 0)
                    EmailService.SendNotificationIT(companyArea, logRecords.Tables[0], runId, apiConfig);

                if (logRecords.Tables[1].Rows.Count > 0)
                    EmailService.SendNotificationHR(companyArea, logRecords.Tables[1], runId, apiConfig);
            }
        }

        public static void LogSummary(string companyArea, int total, int successCount, int failedCount, DateTime processStart, DateTime processEnd, Guid runId)
        {
            var duration = processEnd - processStart;
            DatabaseHelper.LogSeparationECStatusFile(companyArea,
                $"=== Process Summary ===\n" +
                $"Run ID         : {runId}\n" +
                $"Start Time     : {processStart:yyyy-MM-dd HH:mm:ss}\n" +
                $"End Time       : {processEnd:yyyy-MM-dd HH:mm:ss}\n" +
                $"Duration       : {duration.TotalSeconds} seconds\n" +
                $"Total Records  : {total}\n" +
                $"Success Count  : {successCount}\n" +
                $"Failed Count   : {failedCount}\n" +
                $"========================"
            );
        }


		/*
         //NotifyStatus("Atos", new Guid("489C8430-E3D3-43C7-9673-CC833E080210"));
		  // TESTING DATE
		static void Main(string[] args)
		{
            var records = DatabaseHelper.GetPendingSeparationEC("Atos");
            DateTime utcDate = new DateTime(records[0].LWD.Year, records[0].LWD.Month, records[0].LWD.Day, 0, 0, 0, DateTimeKind.Utc);
            long unixMilliseconds = new DateTimeOffset(utcDate).ToUnixTimeMilliseconds();
            DateTime inputDate = records[0].LWD;
			string print = "Input date: " + inputDate.ToString("dd-MMM-yyyy");
            print += ", utcDate: " + utcDate.ToString();
            print += ", unixMilliseconds: " + unixMilliseconds.ToString();
            DatabaseHelper.LogSeparationECStatusFile("Atos", print);
		}
		
		 
		 //TESTING Multiple EmailID's
		static void Main(string[] args)
		{
			string print = "";
			try
			{
				using (var client = new SmtpClient("10.92.32.13", 25))
				{
					var mailMessage = new MailMessage
					{
						From = new MailAddress("ITAP.TECH.EXTERNAL@ATOS.NET"),
						Subject = "Test Email",
						Body = "Test Email Body",
						IsBodyHtml = true
					};
					string toEmail = "shraddha.pawar@atos.net,shardul.mahajan@atos.net";
					foreach (var email in toEmail.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
					{
						print += " " + email;
						mailMessage.To.Add(email.Trim());
					}
					string ccEmail = "shraddha.pawar@atos.net,shardul.mahajan@atos.net";
					print += " cc ";
					if (!string.IsNullOrEmpty(ccEmail))
					{
						foreach (var email in ccEmail.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
						{
							print += " " + email;
							mailMessage.CC.Add(email.Trim());
						}
					}
					client.Send(mailMessage);
				}
			}
			catch (SmtpException ex)
			{
				print += " Error: " + ex.Message;
			}
			DatabaseHelper.LogSeparationECStatusFile("Atos", print);
		}

		 

        
        */
	}
}
