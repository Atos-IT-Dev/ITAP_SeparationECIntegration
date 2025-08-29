using System;
using System.Data;
using System.Net.Mail;
using System.Text;
using System.Collections.Generic;
using System.Configuration;
using EvSeparationECIntegration;

internal static class EmailService
{
	public static bool SendNotificationIT(string companyArea, DataTable logRecords, Guid runId, Dictionary<string, string> config)
	{
		string toEmail = "", ccEmail = "", fromEmail = "", smtpServer = "", testEmail = "", subject = "", body = "", emailTemplate = "";
		int smtpPort;

		try
		{
			toEmail = config["NotificationToIT"];
			ccEmail = config["NotificationCcIT"];
			fromEmail = config["EmailFrom"];
			smtpServer = config["SmtpHost"];
			smtpPort = int.Parse(config["SmtpPort"]);
			testEmail = ConfigurationManager.AppSettings["testEmail"].ToString();
			subject = config["EmailSubjectTemplate"]; //$"[EC Termination] Failures Detected - Run ID: {runId}";
			string runDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");
			subject = subject.Replace("{{RunDate}}", runDate);
			emailTemplate = config["EmailTemplate"];
			body = GenerateEmailBody(logRecords, emailTemplate, runDate, runId);

			using (var client = new SmtpClient(smtpServer, smtpPort))
			{
				var mailMessage = new MailMessage
				{
					From = new MailAddress(fromEmail),
					Subject = subject,
					Body = body,
					IsBodyHtml = true
				};


                //mailMessage.To.Add(toEmail.Trim()); For single email ID for multiple use below
                foreach (var email in toEmail.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    mailMessage.To.Add(email.Trim());
                }

                if (!string.IsNullOrEmpty(ccEmail))
				{
                    foreach (var email in ccEmail.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        mailMessage.CC.Add(email.Trim());
                    }
                }
					

				client.Send(mailMessage);

				DatabaseHelper.AddAPIEmailLog(companyArea,fromEmail, toEmail, ccEmail, subject, body, true, "","IT");
				return true;
			}
		}
		catch (SmtpException ex)
		{
			DatabaseHelper.AddAPIEmailLog(companyArea,fromEmail, toEmail, ccEmail, subject, body, false, "Smtp Exception: " + ex.Message, "IT");
		}
		catch (Exception ex)
		{
			DatabaseHelper.AddAPIEmailLog(companyArea, fromEmail, toEmail, ccEmail, subject, body, false, "Unexpected Error: " + ex.Message, "IT");
		}

		return false;
	}

    public static bool SendNotificationHR(string companyArea, DataTable logRecords, Guid runId, Dictionary<string, string> config)
    {
        string toEmail = "", ccEmail = "", fromEmail = "", smtpServer = "", testEmail = "", subject = "", body = "", emailTemplate = "";
        int smtpPort;

        try
        {
            toEmail = config["NotificationToHR"];
            ccEmail = config["NotificationCcHR"];
            fromEmail = config["EmailFrom"];
            smtpServer = config["SmtpHost"];
            smtpPort = int.Parse(config["SmtpPort"]);
            testEmail = ConfigurationManager.AppSettings["testEmail"].ToString();
            subject = config["EmailSubjectTemplate"];
            string runDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");
            subject = subject.Replace("{{RunDate}}", runDate);
            emailTemplate = config["EmailTemplate"];
            body = GenerateEmailBody(logRecords, emailTemplate, runDate, runId);

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };


				//mailMessage.To.Add(toEmail);For single email ID for multiple use below
				foreach (var email in toEmail.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					mailMessage.To.Add(email.Trim());
				}
				if (!string.IsNullOrEmpty(ccEmail))
				{
					foreach (var email in ccEmail.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
					{
						mailMessage.CC.Add(email.Trim());
					}
				}

				client.Send(mailMessage);

                DatabaseHelper.AddAPIEmailLog(companyArea, fromEmail, toEmail, ccEmail, subject, body, true, "","HR");
                return true;
            }
        }
        catch (SmtpException ex)
        {
            DatabaseHelper.AddAPIEmailLog(companyArea, fromEmail, toEmail, ccEmail, subject, body, false, "Smtp Exception: " + ex.Message, "HR");
        }
        catch (Exception ex)
        {
            DatabaseHelper.AddAPIEmailLog(companyArea, fromEmail, toEmail, ccEmail, subject, body, false, "Unexpected Error: " + ex.Message, "HR");
        }

        return false;
    }

    private static string GenerateEmailBody(DataTable dt, string template, string runDate, Guid runId)
	{
		// Prepare the dynamic values
		string htmlTable = ConvertDataTableToHtml(dt);

		// Replace placeholders in the template
		string emailBody = template
			.Replace("{{RunDate}}", runDate)
			.Replace("{{RunID}}", runId.ToString())
			.Replace("{{Table}}", htmlTable);

		return emailBody;
	}

	private static string ConvertDataTableToHtml(DataTable dt)
	{
		var sb = new StringBuilder();
		sb.Append("<table border='1' cellpadding='5' cellspacing='0'>");

		// Table header
		sb.Append("<tr>");
		foreach (DataColumn column in dt.Columns)
		{
			sb.AppendFormat("<th>{0}</th>", column.ColumnName);
		}
		sb.Append("</tr>");

		// Table rows
		foreach (DataRow row in dt.Rows)
		{
			if (row["Status"].ToString().ToLower()== "failure")
				sb.Append("<tr style='color:red;'>");
			else
				sb.Append("<tr>");
			foreach (var item in row.ItemArray)
			{
				sb.AppendFormat("<td>{0}</td>", item?.ToString());
			}
			sb.Append("</tr>");
		}

		sb.Append("</table>");
		return sb.ToString();
	}

}
