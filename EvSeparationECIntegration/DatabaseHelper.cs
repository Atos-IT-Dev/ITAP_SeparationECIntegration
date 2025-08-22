using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Configuration;
using System;
using System.IO;

namespace EvSeparationECIntegration
{
	internal class DatabaseHelper
	{
		//private static string ConnectionString => ConfigurationManager.ConnectionStrings["SQLDB"].ConnectionString;

		private static string GetConnectionString(string companyArea)
		{
			// You can customize these keys as per your configuration file
			if (companyArea.Equals("atos", StringComparison.OrdinalIgnoreCase))
				return ConfigurationManager.ConnectionStrings["SQLDB_ATOS"].ConnectionString;
			else if (companyArea.Equals("eviden", StringComparison.OrdinalIgnoreCase))
				return ConfigurationManager.ConnectionStrings["SQLDB_EVIDEN"].ConnectionString;
			else
				throw new Exception($"No connection string defined for company: {companyArea}");
		}

		public static Dictionary<string, string> GetApiConfigDictionary(string companyArea, string apiName)
		{
			var config = new Dictionary<string, string>();
			string spName = "USP_GetApiConfig";
			if (companyArea.Equals("eviden"))
				spName = "ESP.USP_GetApiConfig";

			string ConnectionString = GetConnectionString(companyArea);
			using (SqlConnection conn = new SqlConnection(ConnectionString))
			{
				using (SqlCommand cmd = new SqlCommand(spName, conn))
				{
					cmd.CommandType = CommandType.StoredProcedure;
					cmd.Parameters.AddWithValue("@Api", apiName);

					conn.Open();

					using (SqlDataReader reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							string key = reader["ConfigKey"].ToString();
							string value = reader["ConfigValue"].ToString();
							config[key] = value;
						}
					}
				}
			}

			return config;
		}

		public static List<TerminationData> GetPendingSeparationEC(string companyArea)
		{
			var records = new List<TerminationData>();
			string spName = "USP_GetPendingSeparationEC";
			if (companyArea.Equals("eviden"))
				spName = "ESP.USP_GetPendingSeparationEC";

			string ConnectionString = GetConnectionString(companyArea);
			using (SqlConnection conn = new SqlConnection(ConnectionString))
			{
				SqlCommand cmd = new SqlCommand(spName, conn);
				cmd.CommandType = CommandType.StoredProcedure;

				conn.Open();
				SqlDataReader reader = cmd.ExecuteReader();

				while (reader.Read())
				{
					records.Add(new TerminationData
					{
						DASID = reader["DASID"].ToString(),
						ResignationId = Convert.ToInt32(reader["Resignation_Id"].ToString()),
						LWD = Convert.ToDateTime(reader["LWD"].ToString()),
						EventReason = reader["EventReason"].ToString()
					});
				}
			}
			return records;
		}

		public static void UpdateSeparationECStatus(string companyArea, int ResignationId)
		{
			string ConnectionString = GetConnectionString(companyArea);
			string spName = "USP_UpdateSeparationECStatus";
			if (companyArea.Equals("eviden"))
				spName = "ESP.USP_UpdateSeparationECStatus";
			using (SqlConnection conn = new SqlConnection(ConnectionString))
			{
				SqlCommand cmd = new SqlCommand(spName, conn);
				cmd.CommandType = CommandType.StoredProcedure;

				cmd.Parameters.AddWithValue("@Resignation_Id", ResignationId);

				conn.Open();
				cmd.ExecuteNonQuery();
			}
		}

		public static void LogSeparationECStatusDB(SeparationECStatusLogData obj)
		{
			try
			{
				string ConnectionString = GetConnectionString(obj.CompanyArea);
				string spName = "USP_LogSeparationECApiCall";
				if (obj.CompanyArea.Equals("eviden"))
					spName = "ESP.USP_LogSeparationECApiCall";
				using (SqlConnection conn = new SqlConnection(ConnectionString))
				{
					SqlCommand cmd = new SqlCommand(spName, conn); // Stored proc you must create
					cmd.CommandType = CommandType.StoredProcedure;

					cmd.Parameters.AddWithValue("@DASId", obj.DasId);
					cmd.Parameters.AddWithValue("@IsSuccess", obj.IsSuccess);
					cmd.Parameters.AddWithValue("@RequestContent", (object)obj.RequestContent ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ResponseContent", (object)obj.ResponseContent ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ErrorMessage", (object)obj.ErrorMessage ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@StartTime", obj.StartTime);
					cmd.Parameters.AddWithValue("@EndTime", obj.EndTime);
					cmd.Parameters.AddWithValue("@DurationSec", (obj.EndTime - obj.StartTime).TotalSeconds);
					cmd.Parameters.AddWithValue("@RunId", obj.RunId);
					cmd.Parameters.AddWithValue("@ErrorDetails", (object)obj.ErrorDetails ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ErrorSendTo", obj.ErrorSendTo);

                    conn.Open();
					cmd.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				string ErrorDetails = ex.Message;
				ErrorDetails += " | " + ex.InnerException.ToString();
				ErrorDetails += " | " + (ex.StackTrace ?? "null");

				LogSeparationECStatusFile(obj.CompanyArea, $"[DB Log Failed] {DateTime.Now} - {ErrorDetails}");
			}
		}

		public static void LogSeparationECStatusFile(string companyArea, string message)
		{
			string filename = "AtosApiLog.txt";
			if (companyArea.Equals("eviden"))
				filename = "EvidenApiLog.txt";
			string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
			using (StreamWriter writer = new StreamWriter(logPath, true))
			{
				writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
			}
		}

		public static DataSet GetSeparationECAPILog(string companyArea, Guid runId)
		{
			string ConnectionString = GetConnectionString(companyArea);
			string spName = "USP_GetSeparationECAPILog";
			if (companyArea.Equals("eviden"))
				spName = "ESP.USP_GetSeparationECAPILog";

			using (var conn = new SqlConnection(ConnectionString))
			using (var cmd = new SqlCommand(spName, conn))
			{
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.Parameters.AddWithValue("@RunId", runId);

				var dt = new DataSet();
				var adapter = new SqlDataAdapter(cmd);
				adapter.Fill(dt);
				return dt;
			}
		}

		public static void AddAPIEmailLog(string companyArea, string Sender, string Recipient, string CC, string Subject, string Body, bool IsSent, string FailureReason, string ErrorSendTo)
		{
			string ConnectionString = GetConnectionString(companyArea);
			string spName = "USP_AddAPIEmailLog";
			if (companyArea.Equals("eviden"))
				spName = "ESP.USP_AddAPIEmailLog";
			using (SqlConnection conn = new SqlConnection(ConnectionString))
			{
				SqlCommand cmd = new SqlCommand(spName, conn);
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.Parameters.AddWithValue("@Sender", Sender);
				cmd.Parameters.AddWithValue("@Recipient", Recipient);
				cmd.Parameters.AddWithValue("@CC", CC);
				cmd.Parameters.AddWithValue("@Subject", Subject);
				cmd.Parameters.AddWithValue("@Body", Body);
				cmd.Parameters.AddWithValue("@IsSent", IsSent);
				cmd.Parameters.AddWithValue("@FailureReason", FailureReason);
                cmd.Parameters.AddWithValue("@ErrorSendTo", ErrorSendTo);
                conn.Open();
				cmd.ExecuteNonQuery();
			}
		}

	}
}
