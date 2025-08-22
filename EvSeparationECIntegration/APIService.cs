using System; //Provides basic types like DateTime, Guid, Exception, etc.
using System.Collections.Generic; //Used for generic collections like Dictionary, List, etc.
using System.Net.Http; //For making HTTP requests (GET, POST, etc.)
using System.Net.Http.Headers; //For manipulating HTTP headers (like Authorization, Accept).
using System.Text; //Required for string encoding (like Encoding.UTF8).
using System.Linq; //For querying collections (FirstOrDefault, Select, etc.)
using System.Xml.Linq; //For parsing and querying XML responses using LINQ to XML.
using Newtonsoft.Json.Linq; //For parsing JSON using JObject, JToken, etc.

namespace EvSeparationECIntegration
{
    //Api Service class to manage all API interactions for EC (Employee Central) separation module
    internal class ApiService
    {
        // Shared Static HttpClient instance for efficient reuse across API calls
        private static readonly HttpClient httpClient = new HttpClient();

        // Generates SAML assertion required to get access token
        public static string GenerateSAML(string companyArea, Dictionary<string, string> config, Guid runId)
        {
            DateTime start = DateTime.Now;
            try
            {
                string url = config["SAML_Endpoint"];
                var formData = new Dictionary<string, string>
                {
                    { "client_id", config["ClientId"] },
                    { "token_url", config["Token_Endpoint"] },
                    { "private_key", config["SAML_PrivateKey"] },
                    { "user_id", config["SAML_UserId"] }
                };
                var content = new FormUrlEncodedContent(formData);

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                request.Content = new FormUrlEncodedContent(formData);
                var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                string errorDetails = "InnerException: " + (ex.InnerException?.ToString() ?? "null");
                errorDetails += "| StackTrace: " + (ex.StackTrace ?? "null");

                SeparationECStatusLogData objError = new SeparationECStatusLogData();
                objError.CompanyArea = companyArea;
                objError.DasId = "";
                objError.IsSuccess = false;
                objError.RequestContent = "";
                objError.ResponseContent = "";
                objError.ErrorMessage = "GenerateSAMLAsync Error: " + ex.Message;
                objError.StartTime = start;
                objError.EndTime = DateTime.Now;
                objError.RunId = runId;
                objError.ErrorDetails = errorDetails;
                errorDetails = objError.ErrorMessage + " | " + errorDetails;
                objError.ErrorSendTo = "IT";
                DatabaseHelper.LogSeparationECStatusDB(objError);
                DatabaseHelper.LogSeparationECStatusFile(companyArea, errorDetails);
                return null;
            }
        }

        // Generate Access Token using SAML Assertion
        public static (string, int) GenerateAccessTokenWithExpiry(string companyArea, string saml, Dictionary<string, string> config, Guid runId)
        {
            DateTime start = DateTime.Now;
            try
            {
                string url = config["Token_Endpoint"];

                var payload = new Dictionary<string, string>
                {
                    { "company_id", config["CompanyId"] },
                    { "client_id", config["ClientId"] },
                    { "grant_type", config["Token_GrantType"] },
                    { "assertion", saml }
                };

                var content = new FormUrlEncodedContent(payload);
                var response = httpClient.PostAsync(url, content).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var obj = JObject.Parse(json);

                string token = obj[config["token_key_name"]]?.ToString();
                int expiresIn = obj["expires_in"]?.ToObject<int>() ?? 0;

                return (token, expiresIn);
            }
            catch (Exception ex)
            {
                string errorDetails = "| InnerException: " + (ex.InnerException?.ToString() ?? "null");
                errorDetails += "| StackTrace: " + (ex.StackTrace ?? "null");

                SeparationECStatusLogData objError = new SeparationECStatusLogData();
                objError.CompanyArea = companyArea;
                objError.DasId = "";
                objError.IsSuccess = false;
                objError.RequestContent = "";
                objError.ResponseContent = "";
                objError.ErrorMessage = "GenerateAccessTokenWithExpiryAsync Error: " + ex.Message;
                objError.StartTime = start;
                objError.EndTime = DateTime.Now;
                objError.RunId = runId;
                objError.ErrorDetails = errorDetails;
                errorDetails = objError.ErrorMessage + " | " + errorDetails;
                objError.ErrorSendTo = "IT";
                DatabaseHelper.LogSeparationECStatusDB(objError);
                DatabaseHelper.LogSeparationECStatusFile(companyArea, errorDetails);
                return (null, 0);
            }
        }

        // Refreshes the access token using SAML assertion and returns token info
        public static TokenInfo RefreshToken(string companyArea, Dictionary<string, string> config, Guid runId)
        {

            // Generate SAML
            string saml = ApiService.GenerateSAML(companyArea, config, runId);

            // If SAML generation failed, return null to halt processing
            if (string.IsNullOrWhiteSpace(saml))
                return null;

            // Generate access token and expiry
            var (token, expiresIn) = ApiService.GenerateAccessTokenWithExpiry(companyArea, saml, config, runId);

            // If token generation failed, return null
            if (string.IsNullOrWhiteSpace(token))
                return null;

            // Return token object with a 5-minute buffer for expiry safety
            return new TokenInfo
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 300)
            };
        }

        // Calls SuccessFactors user enity lookup API to get userId based on DAS ID
        public static string GetUserId(string companyArea, string accessToken, string username, Dictionary<string, string> config, Guid runId, int ResignationId)
        {
            string baseUrl = config["User_EndpointBase"];
            string url = baseUrl.Replace("{dasid}", "'" + username + "'");
            string errorDetails = "ResignationID:[" + ResignationId.ToString() + "]";
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                var response = httpClient.SendAsync(request).GetAwaiter().GetResult();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var tokenInfo = RefreshToken(companyArea, config, runId);
                    var requestRetry = new HttpRequestMessage(HttpMethod.Get, url);
                    requestRetry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenInfo.Token);
                    requestRetry.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                    response = httpClient.SendAsync(requestRetry).GetAwaiter().GetResult();
                }
                response.EnsureSuccessStatusCode();

                string xml = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                var doc = XDocument.Parse(xml);
                XNamespace nsMeta = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
                XNamespace nsData = "http://schemas.microsoft.com/ado/2007/08/dataservices";

                var userIdElement = doc
                    .Descendants(nsMeta + "properties")
                    .Elements(nsData + "userId")
                    .FirstOrDefault();

                if (userIdElement == null || string.IsNullOrWhiteSpace(userIdElement.Value))
                {
                    SeparationECStatusLogData objError = new SeparationECStatusLogData();
                    objError.CompanyArea = companyArea;
                    objError.DasId = username;
                    objError.IsSuccess = false;
                    objError.RequestContent = url;
                    objError.ResponseContent = xml;
                    objError.ErrorMessage = "UserId element not found or empty in SuccessFactors response.";
                    objError.StartTime = DateTime.Now;
                    objError.EndTime = DateTime.Now;
                    objError.RunId = runId;
                    objError.ErrorDetails = errorDetails;
                    objError.ErrorSendTo = "HR";
                    DatabaseHelper.LogSeparationECStatusDB(objError);
                    return null;
                }

                return userIdElement.Value;
            }
            catch (Exception ex)
            {
                errorDetails = errorDetails + " | InnerException: " + (ex.InnerException?.ToString() ?? "null");
                errorDetails += "| StackTrace: " + (ex.StackTrace ?? "null");

                SeparationECStatusLogData objError = new SeparationECStatusLogData();
                objError.CompanyArea = companyArea;
                objError.DasId = username;
                objError.IsSuccess = false;
                objError.RequestContent = url;
                objError.ResponseContent = "";
                objError.ErrorMessage = "Error retrieving userId: " + ex.Message;
                objError.StartTime = DateTime.Now;
                objError.EndTime = DateTime.Now;
                objError.RunId = runId;
                objError.ErrorDetails = errorDetails;
                objError.ErrorSendTo = "IT";
                DatabaseHelper.LogSeparationECStatusDB(objError);
                errorDetails = objError.ErrorMessage + " | " + errorDetails;
                DatabaseHelper.LogSeparationECStatusFile(companyArea, $"[GET USER] DASID: {username} | Error: {errorDetails}");
                return null;
            }
        }

        // Makes a termination call to the EC API for a specific employee
        public static bool CallEmploymentTermination(string companyArea, TerminationData data, Dictionary<string, string> config, string accessToken, Guid runId)
        {
            DateTime start = DateTime.Now;
            string responseBody = null;
            string error = null, errorDetails = "ResignationID:[" + data.ResignationId.ToString() + "]";
            string jsonPayload = null;
            bool isSuccess = false;

            try
            {
                // Create the JSON payload for termination
                var payload = TerminationPayload.Create(data.DASID, data.SFUserId, data.LWD, data.EventReason, config["EmpTermination_uri"]);
                jsonPayload = payload.ToJson();
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Get endpoint and make the first API call
                string apiUrl = config["EmpTermination_Endpoint"];

                // Prepare headers
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = httpClient.SendAsync(request).GetAwaiter().GetResult();

                responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                // Retry with refreshed token if first call was unauthorized
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var tokenInfo = RefreshToken(companyArea, config, runId);
                    var retryRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenInfo.Token);
                    retryRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    retryRequest.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    response = httpClient.SendAsync(retryRequest).GetAwaiter().GetResult();
                }

                // Check success from API response JSON
                isSuccess = IsApiResponseSuccessful(responseBody, out string apiErrorMessage);
                if (!isSuccess)
                    error = apiErrorMessage;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                errorDetails += " | Inner Exception: " + (ex.InnerException?.ToString() ?? "null") + " | StackTrace: " + (ex.StackTrace ?? "null");
            }
            finally
            {
                SeparationECStatusLogData objLog = new SeparationECStatusLogData();
                objLog.CompanyArea = companyArea;
                objLog.DasId = data.DASID;
                objLog.IsSuccess = isSuccess;
                objLog.RequestContent = jsonPayload;
                objLog.ResponseContent = responseBody;
                objLog.ErrorMessage = error;
                objLog.StartTime = start;
                objLog.EndTime = DateTime.Now;
                objLog.RunId = runId;
                objLog.ErrorDetails = errorDetails;
                objLog.ErrorSendTo = "HR";
                DatabaseHelper.LogSeparationECStatusDB(objLog);
            }
            return isSuccess;
        }

		// Validates if API response is successful by checking 'status' field
		/*public static bool IsApiResponseSuccessful(string jsonResponse, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                var json = JObject.Parse(jsonResponse);
                var status = json["d"]?[0]?["status"]?.ToString().ToUpper();
                var message = json["d"]?[0]?["message"]?.ToString();

                if (status == "OK" && string.IsNullOrEmpty(message)) return true;
                errorMessage = message ?? "Unknown error";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Invalid response format: " + ex.Message;
                return false;
            }
        }
        Shraddha: 22-AUG-2025:
        Updated function IsApiResponseSuccessful to handle both JObject and JArray. 
        Earlier it was only handling JObject, which threw error.
        Error: Invalid response format: Error reading JObject from JsonReader. 
        Current JsonReader item is not an object: StartArray. Path '', line 1, position 1.
		*/

		public static bool IsApiResponseSuccessful(string jsonResponse, out string errorMessage)
		{
			errorMessage = null;
			try
			{
				JToken token = JToken.Parse(jsonResponse);
				JToken firstItem;

				if (token is JObject obj)
				{
					// Normal case: { "d": [ { ... } ] }
					firstItem = obj["d"]?[0];
				}
				else if (token is JArray arr)
				{
					// Special case: [ { ... } ]
					firstItem = arr[0];
				}
				else
				{
					errorMessage = "Unexpected JSON format.";
					return false;
				}

				string status = firstItem?["status"]?.ToString().ToUpper();
				string message = firstItem?["message"]?.ToString();

				if (status == "OK" && string.IsNullOrEmpty(message))
					return true;

				errorMessage = message ?? "Unknown error";
				return false;
			}
			catch (Exception ex)
			{
				errorMessage = "Invalid response format: " + ex.Message;
				return false;
			}
		}


	}
}
